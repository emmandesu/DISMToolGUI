using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DismToolGui
{
    internal sealed class SfcFixControl : ToolkitPageBase
    {
        private const string DownloadUrl =
            "https://www.sysnative.com/niemiro/apps/SFCFix.exe";
        private const string ProjectUrl =
            "https://github.com/emmandesu/DISMToolGUI";

        private readonly TextBox executableBox;
        private readonly TextBox packageBox;
        private readonly Button browseExecutableButton;
        private readonly Button downloadButton;
        private readonly Button browsePackageButton;
        private readonly Button verifyButton;
        private readonly Button runButton;
        private readonly Button cancelButton;
        private readonly ProgressBar progressBar;
        private readonly TextBox verificationBox;
        private string verifiedPath;
        private string verifiedHash;
        private SignatureStatus verifiedSignature;

        public SfcFixControl(Action<ToolkitLogLevel, string> logger)
            : base(logger)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var notice = new Label
            {
                Text = "SFCFix is a third-party repair utility. Downloading never runs it automatically; " +
                       "execution always requires a separate confirmation and may reboot Windows.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                MaximumSize = new System.Drawing.Size(900, 0),
                Margin = new Padding(0, 0, 0, 10)
            };

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 8)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            executableBox = CreateTextBox(GetDefaultExecutablePath());
            packageBox = CreateTextBox();
            browseExecutableButton = CreateButton("Browse", 88);
            downloadButton = CreateButton("Download", 100);
            browsePackageButton = CreateButton("Browse", 88);

            browseExecutableButton.Click += (sender, args) =>
            {
                string selected = BrowseForFile(
                    "Select SFCFix.exe",
                    "SFCFix executable (SFCFix.exe)|SFCFix.exe|Executable files (*.exe)|*.exe",
                    executableBox.Text);
                if (selected != null)
                    executableBox.Text = selected;
            };
            browsePackageButton.Click += (sender, args) =>
            {
                string selected = BrowseForFile(
                    "Select SFCFix.zip",
                    "SFCFix package (SFCFix.zip)|SFCFix.zip|ZIP archives (*.zip)|*.zip",
                    packageBox.Text);
                if (selected != null)
                    packageBox.Text = selected;
            };
            downloadButton.Click += async (sender, args) => await DownloadAsync();
            executableBox.TextChanged += (sender, args) => ResetVerification();

            fields.Controls.Add(CreateLabel("SFCFix.exe:"), 0, 0);
            fields.Controls.Add(executableBox, 1, 0);
            fields.Controls.Add(browseExecutableButton, 2, 0);
            fields.Controls.Add(downloadButton, 3, 0);
            fields.Controls.Add(CreateLabel("SFCFix.zip:"), 0, 1);
            fields.Controls.Add(packageBox, 1, 1);
            fields.Controls.Add(browsePackageButton, 2, 1);
            fields.SetColumnSpan(browsePackageButton, 2);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            verifyButton = CreateButton("Verify executable", 130);
            runButton = CreateButton("Run SFCFix", 110);
            cancelButton = CreateButton("Cancel", 90);
            cancelButton.Enabled = false;
            verifyButton.Click += async (sender, args) => await VerifyAsync(true);
            runButton.Click += async (sender, args) => await RunAsync();
            cancelButton.Click += (sender, args) => CancelOperation();
            toolbar.Controls.AddRange(new Control[] { verifyButton, runButton, cancelButton });

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 18,
                Minimum = 0,
                Maximum = 100,
                Visible = false,
                Margin = new Padding(0, 0, 0, 8)
            };

            verificationBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "No executable has been verified in this session."
            };

            root.Controls.Add(notice, 0, 0);
            root.Controls.Add(fields, 0, 1);
            root.Controls.Add(toolbar, 0, 2);
            root.Controls.Add(progressBar, 0, 3);
            root.Controls.Add(verificationBox, 0, 4);
            Controls.Add(root);
        }

        public void SetPackagePath(string packagePath)
        {
            if (!string.IsNullOrWhiteSpace(packagePath))
                packageBox.Text = packagePath;
        }

        private async Task DownloadAsync()
        {
            string destination;
            try
            {
                destination = Path.GetFullPath(executableBox.Text.Trim());
                if (!Path.GetFileName(destination).Equals(
                        "SFCFix.exe", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "The download destination must end with SFCFix.exe.");
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is NotSupportedException ||
                ex is PathTooLongException ||
                ex is InvalidOperationException)
            {
                ShowWarning(ex.Message);
                return;
            }

            string overwriteWarning = File.Exists(destination)
                ? $"{Environment.NewLine}{Environment.NewLine}The existing file will be replaced after the download completes."
                : string.Empty;
            if (MessageBox.Show(
                    this,
                    $"Download SFCFix from:{Environment.NewLine}{DownloadUrl}{Environment.NewLine}{Environment.NewLine}" +
                    $"Save to:{Environment.NewLine}{destination}{overwriteWarning}{Environment.NewLine}{Environment.NewLine}" +
                    "The downloaded file will not be executed automatically.",
                    "Download SFCFix",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            CancellationToken token = BeginOperation();
            progressBar.Value = 0;
            progressBar.Visible = true;
            string temporaryPath = destination + ".download";
            Log(ToolkitLogLevel.Process, $"Downloading SFCFix from {DownloadUrl}");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using var client = new WebClient();
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                client.Headers[HttpRequestHeader.UserAgent] = GetDownloadUserAgent();
                client.Headers[HttpRequestHeader.Accept] =
                    "application/x-msdownload, application/octet-stream;q=0.9, */*;q=0.8";
                client.DownloadProgressChanged += (sender, args) =>
                {
                    int progress = Math.Max(0, Math.Min(100, args.ProgressPercentage));
                    progressBar.Value = progress;
                };
                using (token.Register(client.CancelAsync))
                    await client.DownloadFileTaskAsync(new Uri(DownloadUrl), temporaryPath);

                token.ThrowIfCancellationRequested();
                if (!ToolkitFileOperations.IsPortableExecutable(temporaryPath))
                    throw new InvalidDataException(
                        "The downloaded response is not a valid Windows executable. " +
                        "The existing SFCFix.exe, if any, was not replaced.");

                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(temporaryPath, destination);
                executableBox.Text = destination;
                Log(ToolkitLogLevel.Success, $"Downloaded SFCFix to {destination}");
            }
            catch (WebException ex) when (
                token.IsCancellationRequested ||
                ex.Status == WebExceptionStatus.RequestCanceled)
            {
                Log(ToolkitLogLevel.Warning, "SFCFix download cancelled.");
            }
            catch (OperationCanceledException)
            {
                Log(ToolkitLogLevel.Warning, "SFCFix download cancelled.");
            }
            catch (Exception ex)
            {
                Log(ToolkitLogLevel.Error, $"SFCFix download failed: {ex.Message}");
                MessageBox.Show(this, ex.Message, "SFCFix download failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                progressBar.Visible = false;
                EndOperation();
            }

            if (File.Exists(destination))
                await VerifyAsync(false);
        }

        private static string GetDownloadUserAgent()
        {
            Version version = typeof(SfcFixControl).Assembly.GetName().Version;
            string productVersion = version == null
                ? "unknown"
                : $"{version.Major}.{version.Minor}.{version.Build}";
            return $"DISMToolGUI/{productVersion} (+{ProjectUrl})";
        }

        private async Task<bool> VerifyAsync(bool showDialog)
        {
            string executable = executableBox.Text.Trim();
            if (!File.Exists(executable))
            {
                ShowWarning("Select or download an existing SFCFix.exe first.");
                return false;
            }

            CancellationToken token;
            try
            {
                token = BeginOperation();
            }
            catch (InvalidOperationException ex)
            {
                Log(ToolkitLogLevel.Warning, ex.Message);
                return false;
            }

            Log(ToolkitLogLevel.Process, $"Calculating SHA-256 and checking the signature for {executable}");
            try
            {
                var result = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    string hash = ToolkitFileOperations.ComputeSha256(executable);
                    token.ThrowIfCancellationRequested();
                    SignatureStatus signature = ToolkitFileOperations.GetSignatureStatus(executable);
                    return Tuple.Create(hash, signature);
                }, token);

                verifiedPath = Path.GetFullPath(executable);
                verifiedHash = result.Item1;
                verifiedSignature = result.Item2;
                verificationBox.Text =
                    $"File: {verifiedPath}{Environment.NewLine}" +
                    $"SHA-256: {verifiedHash}{Environment.NewLine}" +
                    $"Trusted Authenticode signature: {(verifiedSignature.Trusted ? "Yes" : "No")}{Environment.NewLine}" +
                    $"Publisher: {verifiedSignature.Publisher}{Environment.NewLine}" +
                    $"Source URL: {DownloadUrl}";

                Log(verifiedSignature.Trusted ? ToolkitLogLevel.Success : ToolkitLogLevel.Warning,
                    verifiedSignature.Trusted
                        ? $"SFCFix signature is trusted. Publisher: {verifiedSignature.Publisher}"
                        : "SFCFix does not have a trusted Authenticode signature. Review the source and hash before running it.");

                if (showDialog)
                    MessageBox.Show(this, verificationBox.Text, "SFCFix verification",
                        MessageBoxButtons.OK,
                        verifiedSignature.Trusted
                            ? MessageBoxIcon.Information
                            : MessageBoxIcon.Warning);
                return true;
            }
            catch (OperationCanceledException)
            {
                Log(ToolkitLogLevel.Warning, "SFCFix verification cancelled.");
                return false;
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is CryptographicException)
            {
                Log(ToolkitLogLevel.Error, $"SFCFix verification failed: {ex.Message}");
                MessageBox.Show(this, ex.Message, "SFCFix verification failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task RunAsync()
        {
            string executable = executableBox.Text.Trim();
            string package = packageBox.Text.Trim();
            if (!File.Exists(executable))
            {
                ShowWarning("Select or download an existing SFCFix.exe first.");
                return;
            }
            if (!File.Exists(package) ||
                !Path.GetExtension(package).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ShowWarning("Select an existing SFCFix ZIP package first.");
                return;
            }

            // Recalculate identity immediately before launch so a file changed after an
            // earlier verification cannot reuse stale hash or signature information.
            if (!await VerifyAsync(false))
                return;

            string signatureWarning = verifiedSignature.Trusted
                ? $"Trusted publisher: {verifiedSignature.Publisher}"
                : "WARNING: Windows did not report a trusted Authenticode signature.";
            string prompt =
                $"SFCFix may modify protected Windows files and may reboot the computer.{Environment.NewLine}{Environment.NewLine}" +
                $"Executable: {executable}{Environment.NewLine}" +
                $"Package: {package}{Environment.NewLine}" +
                $"SHA-256: {verifiedHash}{Environment.NewLine}" +
                $"{signatureWarning}{Environment.NewLine}{Environment.NewLine}" +
                "Run SFCFix as administrator?";

            if (MessageBox.Show(this, prompt, "Confirm SFCFix execution",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                Log(ToolkitLogLevel.Warning, "SFCFix execution cancelled by the user.");
                return;
            }

            try
            {
                Log(ToolkitLogLevel.Command,
                    $"{executable} {ToolkitProcessRunner.QuoteArgument(package)}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = ToolkitProcessRunner.QuoteArgument(package),
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Path.GetDirectoryName(executable)
                });
                Log(ToolkitLogLevel.Success,
                    "SFCFix launched. Follow its external console prompts and save your work in case Windows restarts.");
            }
            catch (Win32Exception ex)
            {
                Log(ToolkitLogLevel.Error, $"Unable to launch SFCFix: {ex.Message}");
                MessageBox.Show(this, ex.Message, "SFCFix launch failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetVerification()
        {
            verifiedPath = null;
            verifiedHash = null;
            verifiedSignature = null;
            verificationBox.Text = "No executable has been verified in this session.";
        }

        private void ShowWarning(string message)
        {
            Log(ToolkitLogLevel.Warning, message);
            MessageBox.Show(this, message, "SFCFix",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        protected override void OnBusyChanged(bool busy)
        {
            executableBox.Enabled = !busy;
            packageBox.Enabled = !busy;
            browseExecutableButton.Enabled = !busy;
            downloadButton.Enabled = !busy;
            browsePackageButton.Enabled = !busy;
            verifyButton.Enabled = !busy;
            runButton.Enabled = !busy;
            cancelButton.Enabled = busy;
        }

        private static string GetDefaultExecutablePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DismToolGui",
                "Tools",
                "SFCFix.exe");
        }
    }
}
