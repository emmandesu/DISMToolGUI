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
        private const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0";
        private const string ExpectedSignerPublisher =
            "Sysnative Forums Software Ltd";
        private const string ExpectedSignerThumbprint =
            "82BA2FCF85BB1DEB7B2459DA28591E2B7283EF9D";
        // SHA-256 of the DER-encoded leaf certificate supplied in the official
        // Sysnative PKCS#7 certificate bundle. This is the security identity pin;
        // the SHA-1 thumbprint above is retained only for diagnostics and display.
        private const string ExpectedSignerCertificateSha256 =
            "7F3176DEA2E713B9EBB94ADB96081EFDF62B9069E0B6C9A7A209A4193DDF0E34";

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
            bool downloadInstalled = false;
            Log(ToolkitLogLevel.Process, $"Downloading SFCFix from {DownloadUrl}");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using var client = new BrowserCompatibleWebClient();
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                Log(ToolkitLogLevel.Debug,
                    $"Request identity: {GetDownloadUserAgent()} using browser-compatible HTTP headers.");
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

                SignatureStatus downloadedSignature =
                    ToolkitFileOperations.GetSignatureStatus(temporaryPath);
                if (!IsExpectedSfcFixSigner(downloadedSignature))
                    throw new InvalidDataException(
                        "The downloaded executable is not signed by the pinned " +
                        $"{ExpectedSignerPublisher} certificate. " +
                        "The existing SFCFix.exe, if any, was not replaced.");

                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(temporaryPath, destination);
                downloadInstalled = true;
                executableBox.Text = destination;
                Log(ToolkitLogLevel.Success,
                    $"Downloaded and verified SFCFix from {ExpectedSignerPublisher} to {destination}");
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
            catch (WebException ex) when (IsForbiddenResponse(ex))
            {
                const string message =
                    "Sysnative requested interactive browser verification and rejected the in-app download. " +
                    "You can open the official download in your browser, then select the downloaded " +
                    "SFCFix.exe with the Browse button.";
                Log(ToolkitLogLevel.Error, message);

                if (MessageBox.Show(
                        this,
                        message + Environment.NewLine + Environment.NewLine +
                        "Open the official Sysnative download now?",
                        "Browser verification required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1) == DialogResult.Yes)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = DownloadUrl,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception launchException) when (
                        launchException is Win32Exception ||
                        launchException is InvalidOperationException)
                    {
                        Log(ToolkitLogLevel.Error,
                            $"Unable to open the browser: {launchException.Message}");
                        MessageBox.Show(
                            this,
                            launchException.Message,
                            "Unable to open browser",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
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

            if (downloadInstalled && File.Exists(destination))
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

        private static bool IsForbiddenResponse(WebException exception)
        {
            return exception.Response is HttpWebResponse response &&
                   response.StatusCode == HttpStatusCode.Forbidden;
        }

        private sealed class BrowserCompatibleWebClient : WebClient
        {
            private readonly CookieContainer cookies = new CookieContainer();

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest webRequest = base.GetWebRequest(address);
                if (!(webRequest is HttpWebRequest request))
                    return webRequest;

                request.UserAgent = BrowserUserAgent;
                request.Accept =
                    "text/html,application/xhtml+xml,application/xml;q=0.9," +
                    "image/avif,image/webp,*/*;q=0.8";
                request.Referer = "https://www.sysnative.com/";
                request.AutomaticDecompression =
                    DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.CookieContainer = cookies;
                request.KeepAlive = false;
                request.ProtocolVersion = HttpVersion.Version11;
                request.Headers[HttpRequestHeader.AcceptLanguage] = "en-US,en;q=0.9";
                request.Headers["Upgrade-Insecure-Requests"] = "1";
                request.Headers["Sec-Fetch-Dest"] = "document";
                request.Headers["Sec-Fetch-Mode"] = "navigate";
                request.Headers["Sec-Fetch-Site"] = "same-origin";
                request.Headers["Sec-Fetch-User"] = "?1";
                return request;
            }
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
                bool expectedSigner = IsExpectedSfcFixSigner(verifiedSignature);
                verificationBox.Text =
                    $"File: {verifiedPath}{Environment.NewLine}" +
                    $"SHA-256: {verifiedHash}{Environment.NewLine}" +
                    $"Trusted Authenticode signature: {(verifiedSignature.Trusted ? "Yes" : "No")}{Environment.NewLine}" +
                    $"Expected Sysnative signer: {(expectedSigner ? "Yes" : "No")}{Environment.NewLine}" +
                    $"Publisher: {verifiedSignature.Publisher}{Environment.NewLine}" +
                    $"Signer thumbprint: {FormatSignatureValue(verifiedSignature.Thumbprint)}{Environment.NewLine}" +
                    $"Signer certificate SHA-256: {FormatSignatureValue(verifiedSignature.CertificateSha256)}{Environment.NewLine}" +
                    $"Source URL: {DownloadUrl}";

                Log(expectedSigner ? ToolkitLogLevel.Success : ToolkitLogLevel.Error,
                    expectedSigner
                        ? $"SFCFix signature is trusted and matches the pinned {ExpectedSignerPublisher} certificate."
                        : GetSignatureFailureMessage(verifiedSignature));

                if (showDialog || !expectedSigner)
                    MessageBox.Show(this, verificationBox.Text, "SFCFix verification",
                        MessageBoxButtons.OK,
                        expectedSigner
                            ? MessageBoxIcon.Information
                            : MessageBoxIcon.Error);
                return expectedSigner;
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

            try
            {
                executable = Path.GetFullPath(executable);
                package = Path.GetFullPath(package);

                // Deny write/delete sharing while verifying, confirming, and launching
                // so the checked executable cannot be replaced between those steps.
                using (var launchLock = new FileStream(
                           executable,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read))
                {
                    // Recalculate identity immediately before launch so an earlier
                    // verification can never authorize a changed executable.
                    if (!await VerifyAsync(false))
                        return;

                    string prompt =
                        $"SFCFix may modify protected Windows files and may reboot the computer.{Environment.NewLine}{Environment.NewLine}" +
                        $"Executable: {executable}{Environment.NewLine}" +
                        $"Package: {package}{Environment.NewLine}" +
                        $"SHA-256: {verifiedHash}{Environment.NewLine}" +
                        $"Verified publisher: {verifiedSignature.Publisher}{Environment.NewLine}" +
                        $"Pinned signer certificate: matched{Environment.NewLine}{Environment.NewLine}" +
                        "Run SFCFix as administrator?";

                    if (MessageBox.Show(this, prompt, "Confirm SFCFix execution",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    {
                        Log(ToolkitLogLevel.Warning, "SFCFix execution cancelled by the user.");
                        return;
                    }

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
                }
                Log(ToolkitLogLevel.Success,
                    "SFCFix launched. Follow its external console prompts and save your work in case Windows restarts.");
            }
            catch (Exception ex) when (
                ex is Win32Exception ||
                ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                Log(ToolkitLogLevel.Error, $"Unable to launch SFCFix: {ex.Message}");
                MessageBox.Show(this, ex.Message, "SFCFix launch failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsExpectedSfcFixSigner(SignatureStatus signature)
        {
            return signature != null &&
                   signature.Trusted &&
                   string.Equals(
                       signature.CertificateSha256,
                       ExpectedSignerCertificateSha256,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       signature.Thumbprint,
                       ExpectedSignerThumbprint,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSignatureFailureMessage(SignatureStatus signature)
        {
            if (signature == null || !signature.Trusted)
                return "SFCFix execution is blocked because Windows did not report a trusted Authenticode signature.";

            return "SFCFix execution is blocked because its signer does not match the pinned " +
                   $"{ExpectedSignerPublisher} certificate.";
        }

        private static string FormatSignatureValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Not available" : value.ToUpperInvariant();
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
