using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DismToolGui
{
    internal enum SystemLogKind
    {
        Cbs,
        Dism,
        SetupApi
    }

    public partial class MainForm : Form
    {
        private void CommandSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isExecuting)
            {
                WriteLog("Wait for the current execution to finish.", Color.Orange);
                return;
            }

            string cmd = commandSelector.SelectedItem?.ToString();

            imageTypeGroup.Visible =
                cmd == "Add Package (CAB / MSU)" ||
                cmd == "Run RestoreHealth" ||
                cmd == "Remove Package";

            unmountModeGroup.Visible = cmd == "Unmount WIM";
            mountReadOnlyCheckBox.Visible = cmd == "Mount WIM";

            switch (cmd)
            {
                case "Run RestoreHealth":
                    if (radioOffline.Checked)
                        SetFieldVisibility("Source Path", "Mount Folder");
                    else
                        SetFieldVisibility("Source Path");
                    break;

                case "Add Package (CAB / MSU)":
                    if (radioOffline.Checked)
                        SetFieldVisibility("Package File Path", "Mount Folder");
                    else
                        SetFieldVisibility("Package File Path");
                    break;

                case "Remove Package":
                    if (radioOffline.Checked)
                        SetFieldVisibility("Package Name to Remove", "Mount Folder");
                    else
                        SetFieldVisibility("Package Name to Remove");
                    break;

                case "Mount WIM":
                    SetFieldVisibility("WIM File Path", "Index", "Mount Folder");
                    break;

                case "Unmount WIM":
                    SetFieldVisibility("Mount Folder");
                    break;

                case "Export WIM":
                    SetFieldVisibility("WIM File Path", "Index", "Destination Image File");
                    break;

                case "Get Installed Packages":
                case "SFC - Scannow":
                case "SFC - VerifyOnly":
                default:
                    SetFieldVisibility();
                    break;
            }

            ToggleCbsLogButtonVisibility(cmd);
            UpdateCommandPreview();
        }

        private async void RunButton_Click(object sender, EventArgs e)
        {
            if (isExecuting)
            {
                WriteLog("Another task is already running.", Color.Orange);
                return;
            }

            string cmd = commandSelector.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(cmd))
            {
                WriteLog("Please select a command.", Color.Red);
                return;
            }

            string wim = GetFieldText("WIM File Path");
            string idx = GetFieldText("Index");
            string mount = GetFieldText("Mount Folder");
            string src = GetFieldText("Source Path");
            string packageFile = NormalizeFilePathInput(
                GetFieldText("Package File Path"));
            string pkg = GetFieldText("Package Name to Remove");
            string destinationImage = GetFieldText("Destination Image File");

            SetExecutionUiState(true);

            try
            {
                switch (cmd)
                {
                    case "Run RestoreHealth":
                        {
                            if (!TryGetImageTarget(out string targetImage))
                                break;

                            string arguments = string.IsNullOrWhiteSpace(src)
                                ? $"{targetImage} /Cleanup-Image /RestoreHealth"
                                : $"{targetImage} /Cleanup-Image /RestoreHealth /Source:\"{src}\" /LimitAccess";

                            if (!ConfirmCommandExecution(cmd))
                                break;

                            await ExecuteCommandAsync(arguments);
                            break;
                        }

                    case "Mount WIM":
                        {
                            if (!File.Exists(wim))
                            {
                                WriteLog("WIM file not found.", Color.Red);
                                break;
                            }

                            if (!TryGetPositiveImageIndex(idx, out int imageIndex))
                                break;

                            if (!TryValidateEmptyMountDirectory(mount))
                                break;

                            if (!ConfirmCommandExecution(cmd))
                                break;

                            string readOnlyArgument = mountReadOnlyCheckBox.Checked
                                ? " /ReadOnly"
                                : string.Empty;
                            await ExecuteCommandAsync(
                                $"/Mount-WIM /WimFile:\"{wim}\" /Index:{imageIndex} /MountDir:\"{mount}\"{readOnlyArgument}");
                            break;
                        }

                    case "Unmount WIM":
                        {
                            if (string.IsNullOrWhiteSpace(mount) || !Directory.Exists(mount))
                            {
                                WriteLog("Mount folder does not exist.", Color.Red);
                                break;
                            }

                            string unmountMode = GetSelectedUnmountOption();
                            if (!ConfirmCommandExecution(cmd))
                                break;

                            await ExecuteCommandAsync($"/Unmount-WIM /MountDir:\"{mount}\" {unmountMode}");
                            break;
                        }

                    case "Add Package (CAB / MSU)":
                        {
                            if (string.IsNullOrWhiteSpace(packageFile) || !File.Exists(packageFile))
                            {
                                WriteLog("CAB or MSU package file not found.", Color.Red);
                                break;
                            }

                            string packageExtension = Path.GetExtension(packageFile);
                            if (!packageExtension.Equals(".cab", StringComparison.OrdinalIgnoreCase) &&
                                !packageExtension.Equals(".msu", StringComparison.OrdinalIgnoreCase))
                            {
                                WriteLog("The package file must use the .cab or .msu extension.", Color.Red);
                                break;
                            }

                            if (!TryGetImageTarget(out string targetImage))
                                break;

                            if (!ConfirmCommandExecution(cmd))
                                break;

                            await ExecuteCommandAsync($"{targetImage} /Add-Package /PackagePath:\"{packageFile}\"");
                            break;
                        }

                    case "Get Installed Packages":
                        await ExecuteCommandAsync("/Online /Get-Packages");
                        break;

                    case "Remove Package":
                        {
                            if (string.IsNullOrWhiteSpace(pkg))
                            {
                                WriteLog("Package name is required.", Color.Red);
                                break;
                            }

                            if (!TryGetImageTarget(out string targetImage))
                                break;

                            if (!ConfirmCommandExecution(cmd))
                                break;

                            await ExecuteCommandAsync($"{targetImage} /Remove-Package /PackageName:\"{pkg}\"");
                            break;
                        }

                    case "Export WIM":
                        {
                            if (!File.Exists(wim))
                            {
                                WriteLog("WIM file not found.", Color.Red);
                                break;
                            }

                            if (!TryGetPositiveImageIndex(idx, out int imageIndex))
                                break;

                            if (string.IsNullOrWhiteSpace(destinationImage))
                            {
                                WriteLog("Destination image file path is required.", Color.Red);
                                break;
                            }

                            if (!ConfirmCommandExecution(cmd))
                                break;

                            await ExecuteCommandAsync(
                                $"/Export-Image /SourceImageFile:\"{wim}\" /SourceIndex:{imageIndex} /DestinationImageFile:\"{destinationImage}\"");

                            break;
                        }

                    case "SFC - Scannow":
                        if (!ConfirmCommandExecution(cmd))
                            break;
                        await ExecuteCommandAsync(sfcPath, "/scannow");
                        break;

                    case "SFC - VerifyOnly":
                        await ExecuteCommandAsync(sfcPath, "/verifyonly");
                        break;

                    default:
                        WriteLog("Unknown command.", Color.Red);
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Unexpected error: {ex.Message}", Color.Red);
            }
            finally
            {
                SetExecutionUiState(false);
            }
        }

        private bool TryGetImageTarget(out string target)
        {
            if (radioOnline.Checked)
            {
                target = "/Online";
                return true;
            }

            string mountFolder = GetFieldText("Mount Folder");
            if (string.IsNullOrWhiteSpace(mountFolder) || !Directory.Exists(mountFolder))
            {
                WriteLog("Offline image selected. A valid Mount Folder is required.", Color.Red);
                target = string.Empty;
                return false;
            }

            target = $"/Image:\"{mountFolder}\"";
            return true;
        }

        private bool TryGetPositiveImageIndex(string value, out int index)
        {
            if (!int.TryParse(value, out index) || index <= 0)
            {
                WriteLog("A positive numeric image index is required.", Color.Red);
                return false;
            }

            return true;
        }

        private bool TryValidateEmptyMountDirectory(string mountDirectory)
        {
            if (string.IsNullOrWhiteSpace(mountDirectory) || !Directory.Exists(mountDirectory))
            {
                WriteLog("Mount folder not found.", Color.Red);
                return false;
            }

            try
            {
                if (Directory.EnumerateFileSystemEntries(mountDirectory).Any())
                {
                    WriteLog("The mount folder must be empty.", Color.Red);
                    return false;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                WriteLog($"Unable to inspect the mount folder: {ex.Message}", Color.Red);
                return false;
            }

            return true;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (toolWorkspace != null && !toolWorkspace.CanCloseApplication())
            {
                e.Cancel = true;
                return;
            }

            if (!isExecuting || activeProcess == null)
                return;

            e.Cancel = true;
            MessageBox.Show(
                this,
                "A servicing command is still running. Wait for it to finish before closing DISM Tool GUI.",
                "Command in progress",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private string GetSelectedUnmountOption()
        {
            if (radioUnmountDiscard.Checked) return "/Discard";
            if (radioUnmountCommit.Checked) return "/Commit";
            if (radioUnmountAppend.Checked) return "/Commit /Append";
            return "/Discard";
        }

        private void UpdateCommandPreview()
        {
            if (commandPreviewBox == null)
                return;

            string selectedCommand = commandSelector?.SelectedItem?.ToString();
            commandPreviewBox.Text = BuildCommandPreview(selectedCommand);
            copyCommandButton.Enabled =
                !string.IsNullOrWhiteSpace(commandPreviewBox.Text);
        }

        private void CopyCommandPreview()
        {
            if (string.IsNullOrWhiteSpace(commandPreviewBox.Text))
                return;

            try
            {
                Clipboard.SetText(commandPreviewBox.Text);
                WriteLog("Command copied to the clipboard.", Color.LightBlue);
            }
            catch (ExternalException)
            {
                MessageBox.Show(
                    this,
                    "The clipboard is temporarily unavailable. Try copying the command again.",
                    "Copy command",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private string BuildCommandPreview(string command)
        {
            string wim = PreviewValue(GetFieldText("WIM File Path"), "<image file>");
            string index = PreviewValue(GetFieldText("Index"), "<index>");
            string mount = PreviewValue(GetFieldText("Mount Folder"), "<mount folder>");
            string source = GetFieldText("Source Path");
            string packageFile = PreviewValue(
                NormalizeFilePathInput(GetFieldText("Package File Path")),
                "<CAB or MSU file>");
            string package = PreviewValue(GetFieldText("Package Name to Remove"), "<package name>");
            string destination = PreviewValue(
                GetFieldText("Destination Image File"),
                "<destination image>");
            string imageTarget = radioOnline?.Checked != false
                ? "/Online"
                : $"/Image:\"{mount}\"";

            switch (command)
            {
                case "Run RestoreHealth":
                    return string.IsNullOrWhiteSpace(source)
                        ? $"{dismPath} {imageTarget} /Cleanup-Image /RestoreHealth"
                        : $"{dismPath} {imageTarget} /Cleanup-Image /RestoreHealth /Source:\"{source}\" /LimitAccess";
                case "Mount WIM":
                    return $"{dismPath} /Mount-WIM /WimFile:\"{wim}\" /Index:{index} /MountDir:\"{mount}\"" +
                           (mountReadOnlyCheckBox?.Checked == true ? " /ReadOnly" : string.Empty);
                case "Unmount WIM":
                    return $"{dismPath} /Unmount-WIM /MountDir:\"{mount}\" {GetSelectedUnmountOption()}";
                case "Add Package (CAB / MSU)":
                    return $"{dismPath} {imageTarget} /Add-Package /PackagePath:\"{packageFile}\"";
                case "Get Installed Packages":
                    return $"{dismPath} /Online /Get-Packages";
                case "Remove Package":
                    return $"{dismPath} {imageTarget} /Remove-Package /PackageName:\"{package}\"";
                case "Export WIM":
                    return $"{dismPath} /Export-Image /SourceImageFile:\"{wim}\" /SourceIndex:{index} /DestinationImageFile:\"{destination}\"";
                case "SFC - Scannow":
                    return $"{sfcPath} /scannow";
                case "SFC - VerifyOnly":
                    return $"{sfcPath} /verifyonly";
                default:
                    return string.Empty;
            }
        }

        private static string PreviewValue(string value, string placeholder)
        {
            return string.IsNullOrWhiteSpace(value) ? placeholder : value;
        }

        private static string NormalizeFilePathInput(string value)
        {
            string path = (value ?? string.Empty).Trim();
            if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"')
                path = path.Substring(1, path.Length - 2).Trim();
            return path;
        }

        private bool ConfirmCommandExecution(string command)
        {
            if (confirmCommandCheckBox?.Checked != true || !RequiresConfirmation(command))
                return true;

            string preview = BuildCommandPreview(command);
            return MessageBox.Show(
                       this,
                       $"Review the command before execution:{Environment.NewLine}{Environment.NewLine}{preview}{Environment.NewLine}{Environment.NewLine}Continue?",
                       "Confirm command",
                       MessageBoxButtons.YesNo,
                       MessageBoxIcon.Warning,
                       MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private static bool RequiresConfirmation(string command)
        {
            return command == "Run RestoreHealth" ||
                   command == "Mount WIM" ||
                   command == "Unmount WIM" ||
                   command == "Add Package (CAB / MSU)" ||
                   command == "Remove Package" ||
                   command == "Export WIM" ||
                   command == "SFC - Scannow";
        }

        private void OpenImageInspector()
        {
            ShowToolWorkspace(ToolWorkspacePage.ImageInspector);
        }

        private void OpenMountedImagesManager()
        {
            ShowToolWorkspace(ToolWorkspacePage.MountedImages);
        }

        private void ShowToolWorkspace(ToolWorkspacePage page)
        {
            if (isExecuting)
            {
                MessageBox.Show(
                    this,
                    "Wait for the current command to finish before opening another tool.",
                    "Command in progress",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            topBarLayout.Visible = false;
            inputPanel.Visible = false;
            outputPanel.Visible = false;
            toolWorkspace.Visible = true;
            toolWorkspace.BringToFront();
            AcceptButton = null;
            toolWorkspace.ShowPage(page);
        }

        private void HideToolWorkspace()
        {
            toolWorkspace.Visible = false;
            topBarLayout.Visible = true;
            inputPanel.Visible = true;
            outputPanel.Visible = true;
            AcceptButton = runButton;
            commandSelector.Focus();
        }

        private void ApplyImageSelection(string imagePath, int imageIndex)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || imageIndex <= 0)
                return;

            string selectedCommand = commandSelector.SelectedItem?.ToString();
            if (selectedCommand != "Mount WIM" && selectedCommand != "Export WIM")
            {
                commandSelector.SelectedItem = Path.GetExtension(imagePath)
                    .Equals(".esd", StringComparison.OrdinalIgnoreCase)
                    ? "Export WIM"
                    : "Mount WIM";
            }

            inputFields["WIM File Path"].TextBox.Text = imagePath;
            inputFields["Index"].TextBox.Text = imageIndex.ToString();
            WriteLog($"Selected image index {imageIndex} from {imagePath}.", Color.LightBlue);
        }

        private void OpenSystemLog(SystemLogKind logKind)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string path;
            switch (logKind)
            {
                case SystemLogKind.Cbs:
                    path = Path.Combine(windows, "Logs", "CBS", "CBS.log");
                    break;
                case SystemLogKind.Dism:
                    path = Path.Combine(windows, "Logs", "DISM", "dism.log");
                    break;
                case SystemLogKind.SetupApi:
                    path = Path.Combine(windows, "INF", "setupapi.dev.log");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logKind), logKind, null);
            }

            if (!File.Exists(path))
            {
                MessageBox.Show(this, $"Log file not found:{Environment.NewLine}{path}",
                    "Log not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "notepad.exe"),
                    Arguments = ToolkitProcessRunner.QuoteArgument(path),
                    UseShellExecute = true
                });
            }
            catch (Exception ex) when (
                ex is System.ComponentModel.Win32Exception ||
                ex is InvalidOperationException)
            {
                MessageBox.Show(this, ex.Message, "Unable to open log",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Task<int> ExecuteCommandAsync(string arguments)
        {
            return ExecuteCommandAsync(dismPath, arguments);
        }

        private async Task<int> ExecuteCommandAsync(string exePath, string arguments)
        {
            WriteLog("Please wait... command is in progress.", Color.Yellow);
            WriteLog($"Executing: {exePath} {arguments}", Color.LightBlue);
            bool isSfcCommand = string.Equals(
                Path.GetFileName(exePath),
                "sfc.exe",
                StringComparison.OrdinalIgnoreCase);

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
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

            process.OutputDataReceived += (s, e) =>
            {
                WriteProcessOutput(e.Data, Color.White, isSfcCommand);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                WriteProcessOutput(e.Data, Color.Red, isSfcCommand);
            };

            try
            {
                activeProcess = process;
                if (!process.Start())
                    throw new InvalidOperationException($"Unable to start {exePath}.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0)
                    WriteLog("✅ Command completed successfully.", Color.Green);
                else
                    WriteLog($"❌ Command failed with exit code {process.ExitCode}.", Color.Red);

                return process.ExitCode;
            }
            finally
            {
                if (ReferenceEquals(activeProcess, process))
                    activeProcess = null;
            }
        }

        private void WriteProcessOutput(string output, Color color, bool isSfcCommand)
        {
            if (!isSfcCommand)
            {
                if (!string.IsNullOrWhiteSpace(output))
                    WriteLog(output, color);
                return;
            }

            if (!SfcOutputParser.TryParse(
                    output,
                    out string message,
                    out int? progressPercentage))
            {
                return;
            }

            if (progressPercentage.HasValue)
            {
                UpdateExecutionProgress(progressPercentage.Value);
                return;
            }

            WriteLog(message, color);
        }

        private void UpdateExecutionProgress(int percentage)
        {
            if (runButton == null || runButton.IsDisposed || IsDisposed || Disposing)
                return;

            if (runButton.InvokeRequired)
            {
                try
                {
                    runButton.BeginInvoke(new Action(() => UpdateExecutionProgress(percentage)));
                }
                catch (ObjectDisposedException)
                {
                    // The form was disposed while SFC progress was arriving.
                }
                catch (InvalidOperationException)
                {
                    // The window handle was destroyed while SFC progress was arriving.
                }
                return;
            }

            if (isExecuting)
                runButton.Text = $"Running... {percentage}%";
        }

        private void WriteLog(string message, Color color)
        {
            if (outputBox == null || outputBox.IsDisposed || IsDisposed || Disposing)
                return;

            if (outputBox.InvokeRequired)
            {
                try
                {
                    outputBox.BeginInvoke(new Action(() => WriteLog(message, color)));
                }
                catch (ObjectDisposedException)
                {
                    // The form was disposed while process output was arriving.
                }
                catch (InvalidOperationException)
                {
                    // The window handle was destroyed while process output was arriving.
                }
                return;
            }

            string line = $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}";
            int entryStart = outputBox.TextLength;

            outputBox.SelectionStart = entryStart;
            outputBox.SelectionLength = 0;
            outputBox.SelectionColor = ResolveLogColor(color, currentTheme);
            outputBox.AppendText(line);
            outputBox.SelectionColor = outputBox.ForeColor;
            outputBox.ScrollToCaret();

            logEntries.Add((entryStart, line.Length, color));
            logContent += line;
            exportLogMenuItem.Enabled = true;
        }
    }
}
