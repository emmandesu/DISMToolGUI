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
                cmd == "Add Package (CAB)" ||
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

                case "Add Package (CAB)":
                    if (radioOffline.Checked)
                        SetFieldVisibility("CAB File Path", "Mount Folder");
                    else
                        SetFieldVisibility("CAB File Path");
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

                case "MSU Expander Tool":
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
            string cab = GetFieldText("CAB File Path");
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

                    case "Add Package (CAB)":
                        {
                            if (string.IsNullOrWhiteSpace(cab) || !File.Exists(cab))
                            {
                                WriteLog("CAB file not found.", Color.Red);
                                break;
                            }

                            if (!TryGetImageTarget(out string targetImage))
                                break;

                            if (!ConfirmCommandExecution(cmd))
                                break;

                            await ExecuteCommandAsync($"{targetImage} /Add-Package /PackagePath:\"{cab}\"");
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

                    case "MSU Expander Tool":
                        {
                            LaunchMsuExpanderTool();
                            WriteLog("MSU Expander Tool launched.", Color.Green);
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
                !string.IsNullOrWhiteSpace(commandPreviewBox.Text) &&
                selectedCommand != "MSU Expander Tool";
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
            string cab = PreviewValue(GetFieldText("CAB File Path"), "<CAB file>");
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
                case "Add Package (CAB)":
                    return $"{dismPath} {imageTarget} /Add-Package /PackagePath:\"{cab}\"";
                case "Get Installed Packages":
                    return $"{dismPath} /Online /Get-Packages";
                case "Remove Package":
                    return $"{dismPath} {imageTarget} /Remove-Package /PackageName:\"{package}\"";
                case "Export WIM":
                    return $"{dismPath} /Export-Image /SourceImageFile:\"{wim}\" /SourceIndex:{index} /DestinationImageFile:\"{destination}\"";
                case "MSU Expander Tool":
                    return "Built-in MSU Expander Tool (streamed securely to Windows PowerShell)";
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
                   command == "Add Package (CAB)" ||
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
                if (!string.IsNullOrWhiteSpace(e.Data))
                    WriteLog(e.Data, Color.White);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    WriteLog(e.Data, Color.Red);
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

        private void LaunchMsuExpanderTool()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = powershellPath,
                Arguments = "-NoProfile -STA -Command -",
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.Exited += (sender, args) => process.Dispose();

            try
            {
                if (!process.Start())
                    throw new InvalidOperationException("Unable to start the MSU Expander Tool.");

                process.StandardInput.Write(GetMsuExpanderScript());
                process.StandardInput.Close();
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        private string GetMsuExpanderScript()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "Add-Type -AssemblyName System.Windows.Forms",
                "Add-Type -AssemblyName System.Drawing",
                "$ErrorActionPreference = \"Stop\"",
                "",
                "# --- Form ---",
                "$form = New-Object System.Windows.Forms.Form",
                "$form.Text = \"MSU Expander Tool v2.2\"",
                "$form.ClientSize = New-Object System.Drawing.Size(620,460)",
                "$form.MinimumSize = New-Object System.Drawing.Size(560,420)",
                "$form.StartPosition = \"CenterScreen\"",
                "",
                "# Scale the window and controls with the display DPI",
                "$form.AutoScaleDimensions = New-Object System.Drawing.SizeF(96,96)",
                "$form.AutoScaleMode = [System.Windows.Forms.AutoScaleMode]::Dpi",
                "",
                "# Consistent font",
                "$uiFont = New-Object System.Drawing.Font(\"Segoe UI\",9)",
                "$form.Font = $uiFont",
                "",
                "# --- MSU Label ---",
                "$lblMSU = New-Object System.Windows.Forms.Label",
                "$lblMSU.Text = \"MSU File:\"",
                "$lblMSU.Size = New-Object System.Drawing.Size(80,23)",
                "$lblMSU.Location = New-Object System.Drawing.Point(10,20)",
                "$lblMSU.TextAlign = \"MiddleLeft\"",
                "$form.Controls.Add($lblMSU)",
                "",
                "# --- MSU Textbox ---",
                "$txtMSU = New-Object System.Windows.Forms.TextBox",
                "$txtMSU.Location = New-Object System.Drawing.Point(90,20)",
                "$txtMSU.Size = New-Object System.Drawing.Size(400,23)",
                "$txtMSU.Anchor = \"Top,Left,Right\"",
                "$txtMSU.AutoCompleteMode = \"SuggestAppend\"",
                "$txtMSU.AutoCompleteSource = \"FileSystem\"",
                "$form.Controls.Add($txtMSU)",
                "",
                "# --- Browse MSU ---",
                "$btnBrowseMSU = New-Object System.Windows.Forms.Button",
                "$btnBrowseMSU.Text = \"Browse\"",
                "$btnBrowseMSU.Size = New-Object System.Drawing.Size(75,23)",
                "$btnBrowseMSU.Location = New-Object System.Drawing.Point(500,20)",
                "$btnBrowseMSU.Anchor = \"Top,Right\"",
                "$btnBrowseMSU.Add_Click({",
                "    $dialog = New-Object System.Windows.Forms.OpenFileDialog",
                "    $dialog.Filter = \"MSU Files (*.msu)|*.msu\"",
                "    if ($dialog.ShowDialog() -eq \"OK\") {",
                "        $txtMSU.Text = $dialog.FileName",
                "    }",
                "})",
                "$form.Controls.Add($btnBrowseMSU)",
                "",
                "# --- Destination Label ---",
                "$lblDest = New-Object System.Windows.Forms.Label",
                "$lblDest.Text = \"Destination:\"",
                "$lblDest.Size = New-Object System.Drawing.Size(80,23)",
                "$lblDest.Location = New-Object System.Drawing.Point(10,60)",
                "$lblDest.TextAlign = \"MiddleLeft\"",
                "$form.Controls.Add($lblDest)",
                "",
                "# --- Destination Textbox ---",
                "$txtDest = New-Object System.Windows.Forms.TextBox",
                "$txtDest.Location = New-Object System.Drawing.Point(90,60)",
                "$txtDest.Size = New-Object System.Drawing.Size(400,23)",
                "$txtDest.Anchor = \"Top,Left,Right\"",
                "$txtDest.AutoCompleteMode = \"SuggestAppend\"",
                "$txtDest.AutoCompleteSource = \"FileSystemDirectories\"",
                "$form.Controls.Add($txtDest)",
                "",
                "# --- Browse Destination ---",
                "$btnBrowseDest = New-Object System.Windows.Forms.Button",
                "$btnBrowseDest.Text = \"Browse\"",
                "$btnBrowseDest.Size = New-Object System.Drawing.Size(75,23)",
                "$btnBrowseDest.Location = New-Object System.Drawing.Point(500,60)",
                "$btnBrowseDest.Anchor = \"Top,Right\"",
                "$btnBrowseDest.Add_Click({",
                "    $folder = New-Object System.Windows.Forms.FolderBrowserDialog",
                "    if ($folder.ShowDialog() -eq \"OK\") {",
                "        $txtDest.Text = $folder.SelectedPath",
                "    }",
                "})",
                "$form.Controls.Add($btnBrowseDest)",
                "",
                "# --- Checkbox ---",
                "$chkDeep = New-Object System.Windows.Forms.CheckBox",
                "$chkDeep.Text = \"Deep Expand CAB Payloads\"",
                "$chkDeep.Location = New-Object System.Drawing.Point(90,95)",
                "$chkDeep.AutoSize = $true",
                "$form.Controls.Add($chkDeep)",
                "",
                "# --- Expand Button ---",
                "$btnExpand = New-Object System.Windows.Forms.Button",
                "$btnExpand.Text = \"Expand MSU\"",
                "$btnExpand.Size = New-Object System.Drawing.Size(130,30)",
                "$btnExpand.Location = New-Object System.Drawing.Point(245,120)",
                "$btnExpand.Anchor = \"Top\"",
                "$form.Controls.Add($btnExpand)",
                "",
                "# --- Progress Bar ---",
                "$progress = New-Object System.Windows.Forms.ProgressBar",
                "$progress.Location = New-Object System.Drawing.Point(10,160)",
                "$progress.Size = New-Object System.Drawing.Size(580,20)",
                "$progress.Anchor = \"Top,Left,Right\"",
                "$form.Controls.Add($progress)",
                "",
                "# --- Log Box ---",
                "$txtLog = New-Object System.Windows.Forms.TextBox",
                "$txtLog.Multiline = $true",
                "$txtLog.ReadOnly = $true",
                "$txtLog.WordWrap = $false",
                "$txtLog.ScrollBars = \"Vertical\"",
                "$txtLog.Location = New-Object System.Drawing.Point(10,190)",
                "$txtLog.Size = New-Object System.Drawing.Size(580,220)",
                "$txtLog.Anchor = \"Top,Bottom,Left,Right\"",
                "$form.Controls.Add($txtLog)",
                "",
                "# --- Logging ---",
                "function Write-Log {",
                "    param([string]$msg)",
                "    $timestamp = (Get-Date).ToString(\"yyyy-MM-dd HH:mm:ss\")",
                "    $txtLog.AppendText(\"[$timestamp] $msg`r`n\")",
                "    $txtLog.SelectionStart = $txtLog.TextLength",
                "    $txtLog.ScrollToCaret()",
                "    [System.Windows.Forms.Application]::DoEvents()",
                "}",
                "",
                "# --- Responsive process runner ---",
                "function Invoke-ExpandProcess {",
                "    param($sourcePath, $outputFolder)",
                "",
                "    $expandExe = [System.IO.Path]::Combine([Environment]::SystemDirectory, \"expand.exe\")",
                "    $process = Start-Process -FilePath $expandExe -ArgumentList ('\"{0}\" -F:* \"{1}\"' -f $sourcePath, $outputFolder) -NoNewWindow -PassThru",
                "",
                "    while (!$process.HasExited) {",
                "        [System.Windows.Forms.Application]::DoEvents()",
                "        Start-Sleep -Milliseconds 100",
                "    }",
                "",
                "    $exitCode = $process.ExitCode",
                "    $process.Dispose()",
                "    if ($exitCode -ne 0) { throw \"expand.exe failed with exit code $exitCode\" }",
                "}",
                "",
                "# --- Expand CAB ---",
                "function Expand-CAB {",
                "    param($cabPath, $outputFolder)",
                "",
                "    if (!(Test-Path -LiteralPath $outputFolder -PathType Container)) {",
                "        [System.IO.Directory]::CreateDirectory($outputFolder) | Out-Null",
                "    }",
                "",
                "    Write-Log \"Expanding CAB: $cabPath\"",
                "",
                "    Invoke-ExpandProcess $cabPath $outputFolder",
                "}",
                "",
                "# --- Expand Logic ---",
                "$btnExpand.Add_Click({",
                "    $msu = $txtMSU.Text",
                "    $dest = $txtDest.Text",
                "    $deep = $chkDeep.Checked",
                "",
                "    $btnExpand.Enabled = $false",
                "    $txtMSU.Enabled = $false",
                "    $txtDest.Enabled = $false",
                "    $btnBrowseMSU.Enabled = $false",
                "    $btnBrowseDest.Enabled = $false",
                "    $chkDeep.Enabled = $false",
                "    [System.Windows.Forms.Application]::DoEvents()",
                "",
                "    try {",
                "        $msu = $msu.Trim()",
                "        $dest = $dest.Trim()",
                "",
                "        if ([string]::IsNullOrWhiteSpace($msu) -or !(Test-Path -LiteralPath $msu -PathType Leaf)) {",
                "            throw \"Select an existing MSU file.\"",
                "        }",
                "",
                "        if ([System.IO.Path]::GetExtension($msu) -ine \".msu\") {",
                "            throw \"The selected source must have an .msu extension.\"",
                "        }",
                "",
                "        if ([string]::IsNullOrWhiteSpace($dest)) {",
                "            throw \"Select a destination folder.\"",
                "        }",
                "",
                "        if (Test-Path -LiteralPath $dest) {",
                "            if (!(Test-Path -LiteralPath $dest -PathType Container)) {",
                "                throw \"The destination path is not a folder.\"",
                "            }",
                "        } else {",
                "            [System.IO.Directory]::CreateDirectory($dest) | Out-Null",
                "            Write-Log \"Created destination folder\"",
                "        }",
                "",
                "        $dest = [System.IO.Path]::GetFullPath($dest)",
                "        $cabOutputRoot = Join-Path $dest \"CAB_Extracted\"",
                "        $cabOutputPrefix = $cabOutputRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar",
                "        $destinationPrefix = $dest.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar",
                "        $cabStateBefore = @{}",
                "",
                "        if ($deep) {",
                "            Get-ChildItem -LiteralPath $dest -Filter *.cab -File -Recurse -ErrorAction Stop |",
                "                Where-Object { !$_.FullName.StartsWith($cabOutputPrefix, [System.StringComparison]::OrdinalIgnoreCase) } |",
                "                ForEach-Object { $cabStateBefore[$_.FullName] = \"$($_.Length):$($_.LastWriteTimeUtc.Ticks)\" }",
                "        }",
                "",
                "        Write-Log \"Starting MSU expansion...\"",
                "        $progress.Value = 10",
                "        [System.Windows.Forms.Application]::DoEvents()",
                "",
                "        Invoke-ExpandProcess $msu $dest",
                "",
                "        Write-Log \"MSU expanded\"",
                "        $progress.Value = 40",
                "        [System.Windows.Forms.Application]::DoEvents()",
                "",
                "        if ($deep) {",
                "            $cabFiles = @(Get-ChildItem -LiteralPath $dest -Filter *.cab -File -Recurse -ErrorAction Stop | Where-Object {",
                "                !$_.FullName.StartsWith($cabOutputPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and",
                "                (!$cabStateBefore.ContainsKey($_.FullName) -or $cabStateBefore[$_.FullName] -ne \"$($_.Length):$($_.LastWriteTimeUtc.Ticks)\")",
                "            })",
                "            $total = $cabFiles.Count",
                "            $count = 0",
                "",
                "            foreach ($cab in $cabFiles) {",
                "                $count++",
                "                $relativeCabPath = $cab.FullName.Substring($destinationPrefix.Length)",
                "                $relativeOutputPath = [System.IO.Path]::ChangeExtension($relativeCabPath, $null)",
                "                $sub = Join-Path $cabOutputRoot $relativeOutputPath",
                "                Expand-CAB $cab.FullName $sub",
                "",
                "                if ($total -gt 0) {",
                "                    $progress.Value = 40 + [int](($count / $total) * 50)",
                "                }",
                "            }",
                "        }",
                "",
                "        $progress.Value = 100",
                "        Write-Log \"Completed successfully\"",
                "    }",
                "    catch {",
                "        Write-Log \"ERROR: $_\"",
                "        $progress.Value = 0",
                "    }",
                "    finally {",
                "        $btnExpand.Enabled = $true",
                "        $txtMSU.Enabled = $true",
                "        $txtDest.Enabled = $true",
                "        $btnBrowseMSU.Enabled = $true",
                "        $btnBrowseDest.Enabled = $true",
                "        $chkDeep.Enabled = $true",
                "    }",
                "})",
                "",
                "# --- Run ---",
                "$form.Add_FormClosing({ if (!$btnExpand.Enabled) { $_.Cancel = $true } })",
                "$form.Add_Shown({ $form.Activate() })",
                "[void]$form.ShowDialog()"
            });
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
            outputBox.SelectionColor = ResolveLogColor(color, isDark);
            outputBox.AppendText(line);
            outputBox.SelectionColor = outputBox.ForeColor;
            outputBox.ScrollToCaret();

            logEntries.Add((entryStart, line.Length, color));
            logContent += line;
            exportLogMenuItem.Enabled = true;
        }
    }
}
