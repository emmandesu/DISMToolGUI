using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace DismToolGui
{
    internal sealed class DismOutputLine
    {
        public DismOutputLine(string text, bool isError)
        {
            Text = text;
            IsError = isError;
        }

        public string Text { get; }
        public bool IsError { get; }
    }

    internal sealed class DismCommandResult
    {
        public DismCommandResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
        public bool Succeeded => ExitCode == 0;
    }

    internal static class DismCommandRunner
    {
        internal static readonly string ExecutablePath =
            System.IO.Path.Combine(Environment.SystemDirectory, "dism.exe");

        public static async Task<DismCommandResult> RunAsync(
            string arguments,
            IProgress<DismOutputLine> progress = null)
        {
            var standardOutput = new StringBuilder();
            var standardError = new StringBuilder();
            var outputLock = new object();

            var startInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = arguments,
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

                progress?.Report(new DismOutputLine(eventArgs.Data, false));
            };

            process.ErrorDataReceived += (sender, eventArgs) =>
            {
                if (string.IsNullOrWhiteSpace(eventArgs.Data))
                    return;

                lock (outputLock)
                    standardError.AppendLine(eventArgs.Data);

                progress?.Report(new DismOutputLine(eventArgs.Data, true));
            };

            if (!process.Start())
                throw new InvalidOperationException("Unable to start DISM.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await Task.Run(() => process.WaitForExit());

            lock (outputLock)
            {
                return new DismCommandResult(
                    process.ExitCode,
                    standardOutput.ToString(),
                    standardError.ToString());
            }
        }
    }
}
