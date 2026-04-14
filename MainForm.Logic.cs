using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DismToolGui
{
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

                case "Mount and Export":
                    SetFieldVisibility("WIM File Path", "Index", "Mount Folder", "Destination Image File");
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

            isExecuting = true;
            runButton.Enabled = false;
            commandSelector.Enabled = false;
            imageTypeGroup.Enabled = false;
            unmountModeGroup.Enabled = false;

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

                            if (string.IsNullOrWhiteSpace(idx) || !int.TryParse(idx, out _))
                            {
                                WriteLog("A valid numeric index is required.", Color.Red);
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(mount) || !Directory.Exists(mount))
                            {
                                WriteLog("Mount folder not found.", Color.Red);
                                break;
                            }

                            await ExecuteCommandAsync($"/Mount-WIM /WimFile:\"{wim}\" /Index:{idx} /MountDir:\"{mount}\"");
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

                            await ExecuteCommandAsync($"{targetImage} /Remove-Package /PackageName:\"{pkg}\"");
                            break;
                        }

                    case "Mount and Export":
                        {
                            if (!File.Exists(wim))
                            {
                                WriteLog("WIM file not found.", Color.Red);
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(idx) || !int.TryParse(idx, out _))
                            {
                                WriteLog("A valid numeric index is required.", Color.Red);
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(mount) || !Directory.Exists(mount))
                            {
                                WriteLog("Mount folder not found.", Color.Red);
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(destinationImage))
                            {
                                WriteLog("Destination image file path is required.", Color.Red);
                                break;
                            }

                            int mountExit = await ExecuteCommandAsync(
                                $"/Mount-WIM /WimFile:\"{wim}\" /Index:{idx} /MountDir:\"{mount}\"");

                            if (mountExit != 0)
                                break;

                            int exportExit = await ExecuteCommandAsync(
                                $"/Export-Image /SourceImageFile:\"{wim}\" /SourceIndex:{idx} /DestinationImageFile:\"{destinationImage}\"");

                            await ExecuteCommandAsync($"/Unmount-WIM /MountDir:\"{mount}\" /Discard");

                            if (exportExit == 0)
                                WriteLog("Mount and export operation finished.", Color.Green);

                            break;
                        }

                    case "MSU Expander Tool":
                        {
                            LaunchMsuExpanderTool();
                            WriteLog("MSU Expander Tool launched.", Color.Green);
                            break;
                        }

                    case "SFC - Scannow":
                        await ExecuteCommandAsync("sfc", "/scannow");
                        break;

                    case "SFC - VerifyOnly":
                        await ExecuteCommandAsync("sfc", "/verifyonly");
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
                isExecuting = false;
                runButton.Enabled = true;
                commandSelector.Enabled = true;
                imageTypeGroup.Enabled = true;
                unmountModeGroup.Enabled = true;
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

        private string GetSelectedUnmountOption()
        {
            if (radioUnmountDiscard.Checked) return "/Discard";
            if (radioUnmountCommit.Checked) return "/Commit";
            if (radioUnmountAppend.Checked) return "/Commit /Append";
            return "/Discard";
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

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit());

            if (process.ExitCode == 0)
                WriteLog("✅ Command completed successfully.", Color.Green);
            else
                WriteLog($"❌ Command failed with exit code {process.ExitCode}.", Color.Red);

            return process.ExitCode;
        }

        private void LaunchMsuExpanderTool()
        {
            string tempScriptPath = Path.Combine(
                Path.GetTempPath(),
                "DismToolGui_MsuExpanderTool.ps1");

            File.WriteAllText(tempScriptPath, GetMsuExpanderScript(), Encoding.UTF8);

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -STA -File \"{tempScriptPath}\"",
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }

        private string GetMsuExpanderScript()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "Add-Type -AssemblyName System.Windows.Forms",
                "Add-Type -AssemblyName System.Drawing",
                "",
                "# --- Form ---",
                "$form = New-Object System.Windows.Forms.Form",
                "$form.Text = \"MSU Expander Tool v2.2\"",
                "$form.Size = New-Object System.Drawing.Size(620,460)",
                "$form.StartPosition = \"CenterScreen\"",
                "",
                "# Disable DPI scaling issues",
                "$form.AutoScaleMode = [System.Windows.Forms.AutoScaleMode]::None",
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
                "$btnExpand.Location = New-Object System.Drawing.Point(230,120)",
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
                "}",
                "",
                "# --- Expand CAB ---",
                "function Expand-CAB {",
                "    param($cabPath, $outputFolder)",
                "",
                "    $expandExe = \"$env:SystemRoot\\System32\\expand.exe\"",
                "",
                "    if (!(Test-Path $outputFolder)) {",
                "        New-Item -ItemType Directory -Path $outputFolder | Out-Null",
                "    }",
                "",
                "    Write-Log \"Expanding CAB: $cabPath\"",
                "",
                "    Start-Process -FilePath $expandExe -ArgumentList ('\"{0}\" -F:* \"{1}\"' -f $cabPath, $outputFolder) -NoNewWindow -Wait",
                "}",
                "",
                "# --- Expand Logic ---",
                "$btnExpand.Add_Click({",
                "    $msu = $txtMSU.Text",
                "    $dest = $txtDest.Text",
                "    $deep = $chkDeep.Checked",
                "",
                "    if (!(Test-Path $msu)) {",
                "        [System.Windows.Forms.MessageBox]::Show(\"Invalid MSU file\")",
                "        return",
                "    }",
                "",
                "    if (!(Test-Path $dest)) {",
                "        New-Item -ItemType Directory -Path $dest | Out-Null",
                "        Write-Log \"Created destination folder\"",
                "    }",
                "",
                "    Write-Log \"Starting MSU expansion...\"",
                "    $progress.Value = 10",
                "",
                "    try {",
                "        Start-Process \"$env:SystemRoot\\System32\\expand.exe\" -ArgumentList ('\"{0}\" -F:* \"{1}\"' -f $msu, $dest) -Wait -NoNewWindow",
                "",
                "        Write-Log \"MSU expanded\"",
                "        $progress.Value = 40",
                "",
                "        if ($deep) {",
                "            $cabFiles = Get-ChildItem $dest -Filter *.cab -Recurse",
                "            $total = $cabFiles.Count",
                "            $count = 0",
                "",
                "            foreach ($cab in $cabFiles) {",
                "                $count++",
                "                $sub = Join-Path $dest \"CAB_Extracted\\$($cab.BaseName)\"",
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
                "    }",
                "})",
                "",
                "# --- Run ---",
                "$form.Topmost = $true",
                "$form.Add_Shown({ $form.Activate() })",
                "[void]$form.ShowDialog()"
            });
        }

        private void WriteLog(string message, Color color)
        {
            if (outputBox.InvokeRequired)
            {
                outputBox.Invoke(new Action(() => WriteLog(message, color)));
                return;
            }

            string line = $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}";

            outputBox.SelectionStart = outputBox.TextLength;
            outputBox.SelectionLength = 0;
            outputBox.SelectionColor = color;
            outputBox.AppendText(line);
            outputBox.SelectionColor = outputBox.ForeColor;
            outputBox.ScrollToCaret();

            logContent += line;
        }
    }
}
