using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DismToolGui
{
    internal enum ToolkitLogLevel
    {
        Info,
        Process,
        Success,
        Warning,
        Error,
        Debug,
        Command
    }

    internal sealed class ProcessOutputLine
    {
        public ProcessOutputLine(string text, bool isError)
        {
            Text = text;
            IsError = isError;
        }

        public string Text { get; }
        public bool IsError { get; }
    }

    internal sealed class ProcessExecutionResult
    {
        public ProcessExecutionResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput ?? string.Empty;
            StandardError = standardError ?? string.Empty;
        }

        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
        public bool Succeeded => ExitCode == 0;
    }

    internal static class ToolkitProcessRunner
    {
        public static async Task<ProcessExecutionResult> RunAsync(
            string executable,
            string arguments,
            CancellationToken cancellationToken,
            IProgress<ProcessOutputLine> progress = null)
        {
            if (string.IsNullOrWhiteSpace(executable))
                throw new ArgumentException("An executable path is required.", nameof(executable));

            var standardOutput = new StringBuilder();
            var standardError = new StringBuilder();
            var outputLock = new object();

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (sender, eventArgs) =>
            {
                if (string.IsNullOrWhiteSpace(eventArgs.Data))
                    return;

                lock (outputLock)
                    standardOutput.AppendLine(eventArgs.Data);
                progress?.Report(new ProcessOutputLine(eventArgs.Data, false));
            };

            process.ErrorDataReceived += (sender, eventArgs) =>
            {
                if (string.IsNullOrWhiteSpace(eventArgs.Data))
                    return;

                lock (outputLock)
                    standardError.AppendLine(eventArgs.Data);
                progress?.Report(new ProcessOutputLine(eventArgs.Data, true));
            };

            if (!process.Start())
                throw new InvalidOperationException($"Unable to start {executable}.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // The process exited between the state check and Kill.
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Cancellation is best effort when Windows denies termination.
                }
            }))
            {
                await Task.Run(() => process.WaitForExit(), CancellationToken.None);
            }

            cancellationToken.ThrowIfCancellationRequested();

            lock (outputLock)
            {
                return new ProcessExecutionResult(
                    process.ExitCode,
                    standardOutput.ToString(),
                    standardError.ToString());
            }
        }

        public static string QuoteArgument(string value)
        {
            if (value == null)
                return "\"\"";

            if (value.Length > 0 && value.All(character =>
                    !char.IsWhiteSpace(character) && character != '"'))
                return value;

            var result = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }

                result.Append('\\', backslashes);
                backslashes = 0;
                result.Append(character);
            }

            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }
    }

    internal sealed class ToolkitSearchResult
    {
        public string Component { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }

    internal sealed class ToolkitDirectoryMatch
    {
        public string Source { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }

        public string Size => ToolkitFileOperations.FormatBytes(SizeBytes);
    }

    internal sealed class PackageCreatedEventArgs : EventArgs
    {
        public PackageCreatedEventArgs(string packagePath)
        {
            PackagePath = packagePath ?? string.Empty;
        }

        public string PackagePath { get; }
    }

    internal static class ToolkitFileOperations
    {
        public static List<ToolkitSearchResult> SearchFiles(
            string root,
            string exactFileName,
            CancellationToken cancellationToken)
        {
            string normalizedRoot = RequireExistingDirectory(root, "Search root");
            if (string.IsNullOrWhiteSpace(exactFileName) ||
                exactFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidOperationException("Enter a valid exact file name to search for.");

            var results = new List<ToolkitSearchResult>();
            var pending = new Stack<string>();
            pending.Push(normalizedRoot);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string current = pending.Pop();

                try
                {
                    foreach (string file in Directory.EnumerateFiles(current))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!Path.GetFileName(file).Equals(
                                exactFileName,
                                StringComparison.OrdinalIgnoreCase))
                            continue;

                        results.Add(new ToolkitSearchResult
                        {
                            Component = GetTopLevelRelativeName(normalizedRoot, file),
                            FileName = Path.GetFileName(file),
                            FullPath = file
                        });
                    }

                    foreach (string directory in Directory.EnumerateDirectories(current))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var attributes = File.GetAttributes(directory);
                            if ((attributes & FileAttributes.ReparsePoint) == 0)
                                pending.Push(directory);
                        }
                        catch (Exception ex) when (
                            ex is IOException ||
                            ex is UnauthorizedAccessException)
                        {
                            // Inaccessible descendants are skipped; other branches remain searchable.
                        }
                    }
                }
                catch (Exception ex) when (
                    ex is IOException ||
                    ex is UnauthorizedAccessException)
                {
                    // WinSxS can contain protected paths. A skipped branch must not abort the search.
                }
            }

            return results
                .OrderBy(result => result.Component, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<ToolkitDirectoryMatch> FindTopLevelDirectories(
            string sourceName,
            string root,
            string keyword,
            CancellationToken cancellationToken)
        {
            string normalizedRoot = RequireExistingDirectory(root, sourceName);
            string normalizedKeyword = (keyword ?? string.Empty).Trim();
            if (normalizedKeyword.Length < 2)
                throw new InvalidOperationException(
                    $"Enter at least two characters for the {sourceName} keyword.");

            var matches = new List<ToolkitDirectoryMatch>();
            foreach (string directory in Directory.EnumerateDirectories(normalizedRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = Path.GetFileName(directory);
                if (name.IndexOf(normalizedKeyword, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                matches.Add(new ToolkitDirectoryMatch
                {
                    Source = sourceName,
                    Name = name,
                    FullPath = directory,
                    SizeBytes = GetDirectorySize(directory, cancellationToken)
                });
            }

            return matches
                .OrderBy(match => match.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static long GetDirectorySize(string directory, CancellationToken cancellationToken)
        {
            long size = 0;
            var pending = new Stack<string>();
            pending.Push(directory);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string current = pending.Pop();
                try
                {
                    foreach (string file in Directory.EnumerateFiles(current))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            size += new FileInfo(file).Length;
                        }
                        catch (Exception ex) when (
                            ex is IOException ||
                            ex is UnauthorizedAccessException)
                        {
                            // Continue calculating the accessible portion.
                        }
                    }

                    foreach (string child in Directory.EnumerateDirectories(current))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                                pending.Push(child);
                        }
                        catch (Exception ex) when (
                            ex is IOException ||
                            ex is UnauthorizedAccessException)
                        {
                        }
                    }
                }
                catch (Exception ex) when (
                    ex is IOException ||
                    ex is UnauthorizedAccessException)
                {
                }
            }

            return size;
        }

        public static void CopyDirectory(
            string source,
            string destination,
            CancellationToken cancellationToken)
        {
            string sourceRoot = RequireExistingDirectory(source, "Source directory");
            string destinationRoot = Path.GetFullPath(destination);
            Directory.CreateDirectory(destinationRoot);

            var pending = new Stack<Tuple<string, string>>();
            pending.Push(Tuple.Create(sourceRoot, destinationRoot));

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pair = pending.Pop();

                foreach (string file in Directory.EnumerateFiles(pair.Item1))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Copy(file, Path.Combine(pair.Item2, Path.GetFileName(file)), false);
                }

                foreach (string directory in Directory.EnumerateDirectories(pair.Item1))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                        continue;

                    string target = Path.Combine(pair.Item2, Path.GetFileName(directory));
                    Directory.CreateDirectory(target);
                    pending.Push(Tuple.Create(directory, target));
                }
            }
        }

        public static string CreateTimestampedDirectory(string parent, string prefix)
        {
            if (string.IsNullOrWhiteSpace(parent))
                throw new InvalidOperationException("Select an export destination.");

            string parentRoot = Path.GetFullPath(parent.Trim());
            Directory.CreateDirectory(parentRoot);

            string safePrefix = string.Concat((prefix ?? "Export")
                .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            string baseName = $"{safePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}";

            for (int suffix = 0; suffix < 1000; suffix++)
            {
                string name = suffix == 0 ? baseName : $"{baseName}-{suffix}";
                string candidate = Path.Combine(parentRoot, name);
                if (Directory.Exists(candidate) || File.Exists(candidate))
                    continue;

                Directory.CreateDirectory(candidate);
                return candidate;
            }

            throw new IOException("Unable to create a unique export directory.");
        }

        public static void EnsureDestinationOutsideSources(
            string destinationParent,
            IEnumerable<string> sourceDirectories)
        {
            string destination = Path.GetFullPath(destinationParent)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string sourceDirectory in sourceDirectories ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(sourceDirectory))
                    continue;

                string source = Path.GetFullPath(sourceDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (destination.Equals(source, StringComparison.OrdinalIgnoreCase) ||
                    destination.StartsWith(
                        source + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "The export destination cannot be inside a source folder being copied.");
            }
        }

        public static string CreateSfcFixPackage(
            string exportDirectory,
            IEnumerable<string> instructions,
            CancellationToken cancellationToken)
        {
            string root = RequireExistingDirectory(exportDirectory, "Export directory");
            string instructionPath = Path.Combine(root, "SFCFix.txt");
            var lines = new List<string> { "::" };
            lines.AddRange((instructions ?? Enumerable.Empty<string>())
                .Where(line => !string.IsNullOrWhiteSpace(line)));
            File.WriteAllLines(instructionPath, lines, new UTF8Encoding(false));

            string zipPath = Path.Combine(root, "SFCFix.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (string file in EnumerateFilesForArchive(root, cancellationToken))
                {
                    if (file.Equals(zipPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string entryName = GetRelativePath(root, file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }

            return zipPath;
        }

        public static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        public static bool IsPortableExecutable(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length < 64)
                return false;

            using var stream = File.OpenRead(path);
            return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
        }

        public static SignatureStatus GetSignatureStatus(string path)
        {
            bool trusted = AuthenticodeTrust.VerifyEmbeddedSignature(path);
            string publisher = "Not available";
            try
            {
                using var certificate = new X509Certificate2(
                    X509Certificate.CreateFromSignedFile(path));
                publisher = certificate.GetNameInfo(X509NameType.SimpleName, false);
            }
            catch (CryptographicException)
            {
                publisher = "Unsigned";
            }

            return new SignatureStatus(trusted, publisher);
        }

        public static string RequireExistingDirectory(string path, string description)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException($"{description} is required.");

            string normalized = Path.GetFullPath(path.Trim());
            if (!Directory.Exists(normalized))
                throw new DirectoryNotFoundException($"{description} does not exist: {normalized}");

            string pathRoot = Path.GetPathRoot(normalized);
            return normalized.Equals(pathRoot, StringComparison.OrdinalIgnoreCase)
                ? normalized
                : normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value:0.##} {units[unit]}";
        }

        public static string GetRelativePath(string root, string path)
        {
            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(path);

            var rootUri = new Uri(normalizedRoot);
            var pathUri = new Uri(normalizedPath);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static IEnumerable<string> EnumerateFilesForArchive(
            string root,
            CancellationToken cancellationToken)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string current = pending.Pop();
                foreach (string file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return file;
                }

                foreach (string directory in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
                        pending.Push(directory);
                }
            }
        }

        private static string GetTopLevelRelativeName(string root, string path)
        {
            string relative = GetRelativePath(root, path);
            int separator = relative.IndexOf(Path.DirectorySeparatorChar);
            return separator < 0 ? string.Empty : relative.Substring(0, separator);
        }
    }

    internal sealed class SignatureStatus
    {
        public SignatureStatus(bool trusted, string publisher)
        {
            Trusted = trusted;
            Publisher = publisher ?? string.Empty;
        }

        public bool Trusted { get; }
        public string Publisher { get; }
    }

    internal static class AuthenticodeTrust
    {
        private const uint WintrustActionGenericVerifyV2 = 0x00AAC56B;
        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdChoiceFile = 1;
        private const uint WtdStateActionVerify = 1;
        private const uint WtdStateActionClose = 2;
        private const uint WtdCacheOnlyUrlRetrieval = 0x1000;

        public static bool VerifyEmbeddedSignature(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            var fileInfo = new WinTrustFileInfo(filePath);
            var trustData = new WinTrustData(fileInfo);
            Guid action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            try
            {
                return WinVerifyTrust(IntPtr.Zero, action, trustData) == 0;
            }
            finally
            {
                trustData.StateAction = WtdStateActionClose;
                WinVerifyTrust(IntPtr.Zero, action, trustData);
                trustData.Dispose();
                fileInfo.Dispose();
            }
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int WinVerifyTrust(
            IntPtr windowHandle,
            [MarshalAs(UnmanagedType.LPStruct)] Guid actionId,
            WinTrustData trustData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class WinTrustFileInfo : IDisposable
        {
            private readonly IntPtr filePathPointer;

            public WinTrustFileInfo(string filePath)
            {
                StructSize = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo));
                filePathPointer = Marshal.StringToCoTaskMemUni(filePath);
                FilePath = filePathPointer;
            }

            public uint StructSize;
            public IntPtr FilePath;
            public IntPtr FileHandle = IntPtr.Zero;
            public IntPtr KnownSubject = IntPtr.Zero;

            public void Dispose()
            {
                if (filePathPointer != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(filePathPointer);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class WinTrustData : IDisposable
        {
            private readonly IntPtr fileInfoPointer;

            public WinTrustData(WinTrustFileInfo fileInfo)
            {
                StructSize = (uint)Marshal.SizeOf(typeof(WinTrustData));
                UiChoice = WtdUiNone;
                RevocationChecks = WtdRevokeNone;
                UnionChoice = WtdChoiceFile;
                StateAction = WtdStateActionVerify;
                ProviderFlags = WtdCacheOnlyUrlRetrieval;
                fileInfoPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(WinTrustFileInfo)));
                Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
                FileInfo = fileInfoPointer;
            }

            public uint StructSize;
            public IntPtr PolicyCallbackData = IntPtr.Zero;
            public IntPtr SipClientData = IntPtr.Zero;
            public uint UiChoice;
            public uint RevocationChecks;
            public uint UnionChoice;
            public IntPtr FileInfo;
            public uint StateAction;
            public IntPtr StateData = IntPtr.Zero;
            public IntPtr UrlReference = IntPtr.Zero;
            public uint ProviderFlags;
            public uint UiContext = 0;

            public void Dispose()
            {
                if (fileInfoPointer != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(fileInfoPointer);
            }
        }
    }
}
