using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DismToolGui
{
    internal sealed class RegistryHiveControl : ToolkitPageBase
    {
        private static readonly Regex ValidMountName =
            new Regex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant);

        private readonly TextBox hiveFileBox;
        private readonly ComboBox rootBox;
        private readonly TextBox mountNameBox;
        private readonly Button browseButton;
        private readonly Button loadButton;
        private readonly Button unloadButton;
        private readonly Button openRegeditButton;
        private readonly Button refreshButton;
        private readonly Button cancelButton;
        private readonly DataGridView hivesGrid;
        private readonly Label statusLabel;
        private readonly List<MountedHiveEntry> mountedByApplication =
            new List<MountedHiveEntry>();

        public RegistryHiveControl(Action<ToolkitLogLevel, string> logger)
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
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var notice = new Label
            {
                Text = "Advanced: loading a hive exposes an offline registry file under HKLM or HKU. " +
                       "This manager only unloads hives loaded successfully by this application session.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                MaximumSize = new System.Drawing.Size(900, 0),
                Margin = new Padding(0, 0, 0, 10)
            };

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 3,
                Margin = new Padding(0, 0, 0, 8)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            hiveFileBox = CreateTextBox();
            rootBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Left,
                Width = 100,
                Margin = new Padding(0, 3, 8, 3)
            };
            rootBox.Items.AddRange(new object[] { "HKLM", "HKU" });
            rootBox.SelectedIndex = 0;
            mountNameBox = CreateTextBox("DISMToolGUI_OFFLINE");
            browseButton = CreateButton("Browse", 88);
            browseButton.Click += (sender, args) =>
            {
                string selected = BrowseForFile(
                    "Select an offline registry hive",
                    "Registry hive files|SYSTEM;SOFTWARE;SAM;SECURITY;DEFAULT;NTUSER.DAT;*.hiv|All files (*.*)|*.*",
                    hiveFileBox.Text);
                if (selected != null)
                {
                    hiveFileBox.Text = selected;
                    string suggested = SuggestMountName(Path.GetFileName(selected));
                    if (!string.IsNullOrWhiteSpace(suggested))
                        mountNameBox.Text = suggested;
                }
            };

            fields.Controls.Add(CreateLabel("Hive file:"), 0, 0);
            fields.Controls.Add(hiveFileBox, 1, 0);
            fields.Controls.Add(browseButton, 2, 0);
            fields.Controls.Add(CreateLabel("Registry root:"), 0, 1);
            fields.Controls.Add(rootBox, 1, 1);
            fields.SetColumnSpan(rootBox, 2);
            fields.Controls.Add(CreateLabel("Mount name:"), 0, 2);
            fields.Controls.Add(mountNameBox, 1, 2);
            fields.SetColumnSpan(mountNameBox, 2);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            loadButton = CreateButton("Load hive", 105);
            unloadButton = CreateButton("Unload selected", 130);
            openRegeditButton = CreateButton("Open in Registry Editor", 165);
            refreshButton = CreateButton("Refresh", 90);
            cancelButton = CreateButton("Cancel", 90);
            unloadButton.Enabled = false;
            openRegeditButton.Enabled = false;
            cancelButton.Enabled = false;

            loadButton.Click += async (sender, args) => await LoadHiveAsync();
            unloadButton.Click += async (sender, args) => await UnloadSelectedAsync();
            openRegeditButton.Click += (sender, args) => OpenSelectedInRegistryEditor();
            refreshButton.Click += (sender, args) => RefreshMountedHives();
            cancelButton.Click += (sender, args) => CancelOperation();
            toolbar.Controls.AddRange(new Control[]
            {
                loadButton,
                unloadButton,
                openRegeditButton,
                refreshButton,
                cancelButton
            });

            hivesGrid = CreateResultsGrid();
            hivesGrid.Columns.Add(CreateColumn("Registry path", "RegistryPath", 260));
            hivesGrid.Columns.Add(CreateColumn("Hive file", "HiveFile", 420, true));
            hivesGrid.Columns.Add(CreateColumn("Status", "Status", 110));
            hivesGrid.SelectionChanged += (sender, args) => UpdateSelectionButtons();

            statusLabel = new Label
            {
                Text = "No hives have been mounted by this application session.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };

            root.Controls.Add(notice, 0, 0);
            root.Controls.Add(fields, 0, 1);
            root.Controls.Add(toolbar, 0, 2);
            root.Controls.Add(hivesGrid, 0, 3);
            root.Controls.Add(statusLabel, 0, 4);
            Controls.Add(root);
        }

        public bool HasMountedHives =>
            mountedByApplication.Any(entry => IsHiveLoaded(entry.Root, entry.MountName));

        public bool CanApplicationClose()
        {
            RefreshMountedHives();
            if (!HasMountedHives)
                return true;

            MessageBox.Show(
                this,
                "One or more registry hives loaded by DISM Tool GUI are still mounted. " +
                "Return to Tools > Advanced > Registry Hive Manager and unload them before closing.",
                "Mounted registry hives",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        private async Task LoadHiveAsync()
        {
            string hiveFile = hiveFileBox.Text.Trim();
            string root = rootBox.SelectedItem?.ToString() ?? "HKLM";
            string mountName = mountNameBox.Text.Trim();
            if (!File.Exists(hiveFile))
            {
                ShowWarning("Select an existing registry hive file.");
                return;
            }
            if (!ValidMountName.IsMatch(mountName))
            {
                ShowWarning("Mount name may contain only letters, numbers, underscores, and hyphens.");
                return;
            }

            string registryPath = $"{root}\\{mountName}";
            if (IsHiveLoaded(root, mountName))
            {
                ShowWarning($"{registryPath} already exists. Choose a unique mount name.");
                return;
            }

            if (MessageBox.Show(
                    this,
                    $"Load this offline hive?{Environment.NewLine}{Environment.NewLine}" +
                    $"File: {hiveFile}{Environment.NewLine}" +
                    $"Mount point: {registryPath}{Environment.NewLine}{Environment.NewLine}" +
                    "Do not edit values unless you understand the target image configuration.",
                    "Confirm hive load",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            CancellationToken token = BeginOperation();
            try
            {
                string arguments = "load " +
                                   ToolkitProcessRunner.QuoteArgument(registryPath) + " " +
                                   ToolkitProcessRunner.QuoteArgument(Path.GetFullPath(hiveFile));
                ProcessExecutionResult result = await RunRegAsync(arguments, token);
                if (!result.Succeeded || !IsHiveLoaded(root, mountName))
                    throw new InvalidOperationException(
                        $"reg.exe could not load the hive (exit code {result.ExitCode}).");

                EnsureLoadedHiveTracked(root, mountName, hiveFile);
                RefreshMountedHives();
                Log(ToolkitLogLevel.Success, $"Loaded {registryPath}.");
            }
            catch (OperationCanceledException)
            {
                if (EnsureLoadedHiveTracked(root, mountName, hiveFile))
                {
                    RefreshMountedHives();
                    Log(
                        ToolkitLogLevel.Warning,
                        $"{registryPath} finished loading before cancellation and remains tracked. " +
                        "Unload it before closing the application.");
                }
                else
                {
                    Log(ToolkitLogLevel.Warning, "Hive load cancelled.");
                }
            }
            catch (Exception ex)
            {
                Log(ToolkitLogLevel.Error, ex.Message);
                bool hiveRemainsLoaded = EnsureLoadedHiveTracked(root, mountName, hiveFile);
                if (hiveRemainsLoaded)
                {
                    RefreshMountedHives();
                    Log(
                        ToolkitLogLevel.Warning,
                        $"{registryPath} remains loaded and is tracked for safe unloading.");
                }

                string message = hiveRemainsLoaded
                    ? ex.Message + Environment.NewLine + Environment.NewLine +
                      $"{registryPath} is still loaded and has been added to the mounted-hive list. " +
                      "Unload it before closing the application."
                    : ex.Message;
                MessageBox.Show(this, message, "Hive load failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task UnloadSelectedAsync()
        {
            MountedHiveEntry selected = GetSelectedHive();
            if (selected == null || !mountedByApplication.Contains(selected))
            {
                ShowWarning("Select a hive loaded by this application session.");
                return;
            }

            if (MessageBox.Show(
                    this,
                    $"Unload {selected.RegistryPath}?{Environment.NewLine}{Environment.NewLine}" +
                    "Close Registry Editor and any other process using this hive before continuing.",
                    "Confirm hive unload",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            CancellationToken token = BeginOperation();
            try
            {
                string arguments = "unload " +
                                   ToolkitProcessRunner.QuoteArgument(selected.RegistryPath);
                ProcessExecutionResult result = await RunRegAsync(arguments, token);
                if (!result.Succeeded || IsHiveLoaded(selected.Root, selected.MountName))
                    throw new InvalidOperationException(
                        $"reg.exe could not unload the hive (exit code {result.ExitCode}). " +
                        "Close handles to the hive and try again.");

                mountedByApplication.Remove(selected);
                RefreshMountedHives();
                Log(ToolkitLogLevel.Success, $"Unloaded {selected.RegistryPath}.");
            }
            catch (OperationCanceledException)
            {
                Log(ToolkitLogLevel.Warning, "Hive unload cancelled.");
            }
            catch (Exception ex)
            {
                Log(ToolkitLogLevel.Error, ex.Message);
                MessageBox.Show(this, ex.Message, "Hive unload failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task<ProcessExecutionResult> RunRegAsync(
            string arguments,
            CancellationToken token)
        {
            string regExe = Path.Combine(Environment.SystemDirectory, "reg.exe");
            Log(ToolkitLogLevel.Command, $"{regExe} {arguments}");
            var progress = new Progress<ProcessOutputLine>(line =>
                Log(line.IsError ? ToolkitLogLevel.Error : ToolkitLogLevel.Info, line.Text));
            return await ToolkitProcessRunner.RunAsync(regExe, arguments, token, progress);
        }

        private void RefreshMountedHives()
        {
            foreach (MountedHiveEntry entry in mountedByApplication)
                entry.Status = IsHiveLoaded(entry.Root, entry.MountName) ? "Loaded" : "Not loaded";

            hivesGrid.DataSource = null;
            hivesGrid.DataSource = mountedByApplication.ToList();
            hivesGrid.ClearSelection();
            int loaded = mountedByApplication.Count(entry => entry.Status == "Loaded");
            statusLabel.Text = loaded == 0
                ? "No hives mounted by this application session."
                : $"{loaded} hive(s) mounted by this application session.";
            UpdateSelectionButtons();
        }

        private bool EnsureLoadedHiveTracked(string root, string mountName, string hiveFile)
        {
            if (!IsHiveLoaded(root, mountName))
                return false;

            MountedHiveEntry tracked = mountedByApplication.FirstOrDefault(entry =>
                string.Equals(entry.Root, root, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.MountName, mountName, StringComparison.OrdinalIgnoreCase));
            if (tracked == null)
            {
                tracked = new MountedHiveEntry();
                mountedByApplication.Add(tracked);
            }

            tracked.Root = root;
            tracked.MountName = mountName;
            tracked.HiveFile = Path.GetFullPath(hiveFile);
            tracked.Status = "Loaded";
            return true;
        }

        private void OpenSelectedInRegistryEditor()
        {
            MountedHiveEntry selected = GetSelectedHive();
            if (selected == null || !IsHiveLoaded(selected.Root, selected.MountName))
            {
                ShowWarning("Select a currently loaded hive.");
                return;
            }

            try
            {
                string fullRoot = selected.Root == "HKLM"
                    ? "HKEY_LOCAL_MACHINE"
                    : "HKEY_USERS";
                string lastKey = $"Computer\\{fullRoot}\\{selected.MountName}";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                           @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit"))
                {
                    key?.SetValue("LastKey", lastKey, RegistryValueKind.String);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.Windows), "regedit.exe"),
                    UseShellExecute = true
                });
                Log(ToolkitLogLevel.Info, $"Opened Registry Editor at {selected.RegistryPath}.");
            }
            catch (Exception ex) when (
                ex is Win32Exception ||
                ex is UnauthorizedAccessException ||
                ex is System.Security.SecurityException)
            {
                Log(ToolkitLogLevel.Error, ex.Message);
                MessageBox.Show(this, ex.Message, "Registry Editor",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private MountedHiveEntry GetSelectedHive()
        {
            return hivesGrid.SelectedRows.Count == 1
                ? hivesGrid.SelectedRows[0].DataBoundItem as MountedHiveEntry
                : null;
        }

        private void UpdateSelectionButtons()
        {
            MountedHiveEntry selected = GetSelectedHive();
            bool hasSelection = !IsBusy && selected != null;
            unloadButton.Enabled = hasSelection && mountedByApplication.Contains(selected);
            openRegeditButton.Enabled = hasSelection &&
                                       IsHiveLoaded(selected.Root, selected.MountName);
        }

        protected override void OnBusyChanged(bool busy)
        {
            hiveFileBox.Enabled = !busy;
            rootBox.Enabled = !busy;
            mountNameBox.Enabled = !busy;
            browseButton.Enabled = !busy;
            loadButton.Enabled = !busy;
            refreshButton.Enabled = !busy;
            cancelButton.Enabled = busy;
            hivesGrid.Enabled = !busy;
            UpdateSelectionButtons();
        }

        private static bool IsHiveLoaded(string root, string mountName)
        {
            RegistryKey registryRoot = root == "HKU"
                ? Registry.Users
                : Registry.LocalMachine;
            try
            {
                using RegistryKey key = registryRoot.OpenSubKey(mountName, false);
                return key != null;
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException ||
                ex is System.Security.SecurityException ||
                ex is IOException)
            {
                return false;
            }
        }

        private static string SuggestMountName(string fileName)
        {
            string normalized = Regex.Replace(
                Path.GetFileNameWithoutExtension(fileName ?? string.Empty).ToUpperInvariant(),
                "[^A-Z0-9_-]",
                "_");
            return normalized.Length == 0
                ? "DISMToolGUI_OFFLINE"
                : "DISMToolGUI_" + normalized;
        }

        private void ShowWarning(string message)
        {
            Log(ToolkitLogLevel.Warning, message);
            MessageBox.Show(this, message, "Registry Hive Manager",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private sealed class MountedHiveEntry
        {
            public string Root { get; set; } = string.Empty;
            public string MountName { get; set; } = string.Empty;
            public string HiveFile { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string RegistryPath => $"{Root}\\{MountName}";
        }
    }
}
