//MainForm.Logic.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
                WriteLog("Wait for current execution to finish.", Color.Orange);
                return;
            }

            string cmd = commandSelector.SelectedItem?.ToString();

            imageTypeGroup.Visible = cmd == "Add Package (CAB)";
            unmountModeGroup.Visible = cmd == "Unmount WIM";

            switch (cmd)
            {
                case "Run RestoreHealth":
                    SetFieldVisibility("Source Path", radioOffline.Checked ? "Mount Folder" : null);
                    break;

                case "Add Package (CAB)":
                    SetFieldVisibility("CAB File Path", radioOffline.Checked ? "Mount Folder" : null);
                    break;

                case "Remove Package":
                    SetFieldVisibility("Package Name to Remove", radioOffline.Checked ? "Mount Folder" : null);
                    break;

                case "Mount WIM":
                    SetFieldVisibility("WIM File Path", "Index", "Mount Folder");
                    break;

                case "Unmount WIM":
                    SetFieldVisibility("Mount Folder");
                    break;

                case "Mount and Export":
                    SetFieldVisibility("WIM File Path", "Index", "Mount Folder", "Component Name");
                    break;

                case "SFC - Scannow":
                case "SFC - VerifyOnly":
                default:
                    SetFieldVisibility(); // Hide all input fields
                    break;
            }

            ToggleCbsLogButtonVisibility(cmd);
        }

        private void ToggleCbsLogButton(string selectedCommand)
        {
            openCbsLogButton.Visible = selectedCommand.Contains("RestoreHealth")
                || selectedCommand.Contains("Add Package")
                || selectedCommand.Contains("Remove Package");
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
            string comp = GetFieldText("Component Name");

            isExecuting = true;
            runButton.Enabled = false;
            commandSelector.Enabled = false;

            try
            {
                switch (cmd)
                {
                    case "Run RestoreHealth":
                        await ExecuteCommandAsync(string.IsNullOrWhiteSpace(src)
                            ? "/Online /Cleanup-Image /RestoreHealth"
                            : $"/Online /Cleanup-Image /RestoreHealth /Source:\"{src}\" /LimitAccess");
                        break;

                    case "Mount WIM":
                        if (!PathsValid(wim, mount)) break;
                        await ExecuteCommandAsync($"/Mount-WIM /WimFile:\"{wim}\" /Index:{idx} /MountDir:\"{mount}\"");
                        break;

                    case "Unmount WIM":
                        if (!Directory.Exists(mount))
                        {
                            WriteLog("Mount folder doesn't exist.", Color.Red);
                            break;
                        }
                        string unmountMode = GetSelectedUnmountOption();
                        await ExecuteCommandAsync($"/Unmount-WIM /MountDir:\"{mount}\" {unmountMode}");
                        break;

                    case "Add Package (CAB)":
                        if (!PathsValid(cab, null)) break;

                        string targetImage = radioOnline.Checked ? "/Online" : $"/Image:\"{GetFieldText("Mount Folder")}\"";

                        if (!radioOnline.Checked && string.IsNullOrWhiteSpace(GetFieldText("Mount Folder")))
                        {
                            WriteLog("Offline image selected. Mount Folder is required.", Color.Red);
                            break;
                        }

                        await ExecuteCommandAsync($"{targetImage} /Add-Package /PackagePath:\"{cab}\"");
                        break;

                    case "Get Installed Packages":
                        await ExecuteCommandAsync("/Online /Get-Packages");
                        break;

                    case "Remove Package":
                        if (string.IsNullOrWhiteSpace(pkg))
                        {
                            WriteLog("Package name required.", Color.Red);
                            break;
                        }
                        await ExecuteCommandAsync($"/Online /Remove-Package /PackageName:\"{pkg}\"");
                        break;

                    case "Mount and Export":
                        if (!PathsValid(wim, mount) || string.IsNullOrWhiteSpace(comp))
                        {
                            WriteLog("Fields missing.", Color.Red);
                            break;
                        }
                        await ExecuteCommandAsync($"/Mount-WIM /WimFile:\"{wim}\" /Index:{idx} /MountDir:\"{mount}\"");
                        await ExecuteCommandAsync($"/Export-Image /SourceImageFile:\"{wim}\" /SourceIndex:{idx} /DestinationImageFile:\"{comp}\"");
                        await ExecuteCommandAsync($"/Unmount-WIM /MountDir:\"{mount}\" /Discard");
                        break;

                    case "Extract MSU/CAB":
                        using (var ofd = new OpenFileDialog { Filter = "MSU/CAB|*.msu;*.cab" })
                        using (var fbd = new FolderBrowserDialog())
                        {
                            if (ofd.ShowDialog() != DialogResult.OK || fbd.ShowDialog() != DialogResult.OK) break;

                            string sourceFile = ofd.FileName;
                            string destination = fbd.SelectedPath;

                            await ExecuteCommandAsync("expand.exe", $"-F:* \"{sourceFile}\" \"{destination}\"");

                            WriteLog($"✅ Extracted to {destination}", Color.Green);

                            var files = Directory.GetFiles(destination, "*", SearchOption.AllDirectories);
                            foreach (var f in files)
                                WriteLog($"✔ {Path.GetFileName(f)}", Color.LightGreen);
                        }
                        break;

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
            finally
            {
                isExecuting = false;
                runButton.Enabled = true;
                commandSelector.Enabled = true;
            }
        }

        private string GetSelectedUnmountOption()
        {
            if (radioUnmountDiscard.Checked) return "/Discard";
            if (radioUnmountCommit.Checked) return "/Commit";
            if (radioUnmountAppend.Checked) return "/Commit /Append";
            return "/Discard"; // fallback
        }

        private async Task ExecuteCommandAsync(string arguments)
        {
            await ExecuteCommandAsync(dismPath, arguments);
        }

        private async Task ExecuteCommandAsync(string exePath, string arguments)
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

            using (var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
            {
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        WriteLog(e.Data, Color.White);
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        WriteLog(e.Data, Color.Red);
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                WriteLog("✅ Command completed.", Color.Green);
            }
        }


        private bool PathsValid(string file, string folder)
        {
            if (!string.IsNullOrWhiteSpace(file) && !File.Exists(file))
            {
                WriteLog($"{file} not found.", Color.Red);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            {
                WriteLog($"{folder} not found.", Color.Red);
                return false;
            }

            return true;
        }

        private void WriteLog(string message, Color color)
        {
            if (outputBox.InvokeRequired)
            {
                outputBox.Invoke(new Action(() => WriteLog(message, color)));
            }
            else
            {
                outputBox.SelectionStart = outputBox.TextLength;
                outputBox.SelectionLength = 0;
                outputBox.SelectionColor = color;
                outputBox.AppendText($"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}");
                outputBox.SelectionColor = outputBox.ForeColor;

                logContent += $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}";
                outputBox.ScrollToCaret();
            }
        }
    }
}
