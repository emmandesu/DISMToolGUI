using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DismToolGui
{
    internal sealed class ComponentExportControl : ToolkitPageBase
    {
        private readonly TextBox keywordBox;
        private readonly TextBox familyBox;
        private readonly TextBox versionedIndexBox;
        private readonly TextBox winSxsBox;
        private readonly TextBox destinationBox;
        private readonly Button browseWinSxsButton;
        private readonly Button browseDestinationButton;
        private readonly Button detectVersionButton;
        private readonly Button searchButton;
        private readonly Button exportButton;
        private readonly Button cancelButton;
        private readonly CheckBox registryCheckBox;
        private readonly CheckBox packageCheckBox;
        private readonly DataGridView matchesGrid;
        private readonly Label summaryLabel;
        private List<ToolkitDirectoryMatch> matches = new List<ToolkitDirectoryMatch>();

        public ComponentExportControl(Action<ToolkitLogLevel, string> logger)
            : base(logger)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 5,
                Margin = new Padding(0, 0, 0, 8)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            keywordBox = CreateTextBox();
            familyBox = CreateTextBox();
            versionedIndexBox = CreateTextBox();
            winSxsBox = CreateTextBox(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "WinSxS"));
            destinationBox = CreateTextBox();

            browseWinSxsButton = CreateButton("Browse", 88);
            browseDestinationButton = CreateButton("Browse", 88);
            detectVersionButton = CreateButton("Detect", 88);
            browseWinSxsButton.Click += (sender, args) =>
            {
                string selected = BrowseForFolder("Select the WinSxS source", winSxsBox.Text);
                if (selected != null)
                    winSxsBox.Text = selected;
            };
            browseDestinationButton.Click += (sender, args) =>
            {
                string selected = BrowseForFolder(
                    "Select the parent folder for a new component export",
                    destinationBox.Text);
                if (selected != null)
                    destinationBox.Text = selected;
            };
            detectVersionButton.Click += (sender, args) => DetectVersionedIndex(true);

            AddField(fields, 0, "Component keyword:", keywordBox, null);
            AddField(fields, 1, "Registry family name:", familyBox, null);
            AddField(fields, 2, "VersionedIndex:", versionedIndexBox, detectVersionButton);
            AddField(fields, 3, "WinSxS path:", winSxsBox, browseWinSxsButton);
            AddField(fields, 4, "Export parent:", destinationBox, browseDestinationButton);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            searchButton = CreateButton("Find components", 130);
            exportButton = CreateButton("Export selected", 130);
            cancelButton = CreateButton("Cancel", 90);
            registryCheckBox = new CheckBox
            {
                Text = "Export matching registry keys",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(8, 7, 0, 0)
            };
            packageCheckBox = new CheckBox
            {
                Text = "Create SFCFix package",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(12, 7, 0, 0)
            };
            exportButton.Enabled = false;
            cancelButton.Enabled = false;

            searchButton.Click += async (sender, args) => await SearchAsync();
            exportButton.Click += async (sender, args) => await ExportAsync();
            cancelButton.Click += (sender, args) => CancelOperation();
            keywordBox.TextChanged += (sender, args) => InvalidateSearch();

            toolbar.Controls.AddRange(new Control[]
            {
                searchButton,
                exportButton,
                cancelButton,
                registryCheckBox,
                packageCheckBox
            });

            matchesGrid = CreateResultsGrid();
            matchesGrid.Columns.Add(CreateColumn("Component folder", "Name", 560));
            matchesGrid.Columns.Add(CreateColumn("Size", "Size", 100));
            matchesGrid.Columns.Add(CreateColumn("Full path", "FullPath", 380, true));
            matchesGrid.SelectionChanged += (sender, args) => UpdateExportButton();

            summaryLabel = new Label
            {
                Text = "Search and choose the exact component version to export.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };

            root.Controls.Add(fields, 0, 0);
            root.Controls.Add(toolbar, 0, 1);
            root.Controls.Add(matchesGrid, 0, 2);
            root.Controls.Add(summaryLabel, 0, 3);
            Controls.Add(root);

            DetectVersionedIndex(false);
        }

        public event EventHandler<PackageCreatedEventArgs> PackageCreated;

        private static void AddField(
            TableLayoutPanel fields,
            int row,
            string label,
            TextBox textBox,
            Button button)
        {
            fields.Controls.Add(CreateLabel(label), 0, row);
            fields.Controls.Add(textBox, 1, row);
            if (button == null)
                fields.SetColumnSpan(textBox, 2);
            else
                fields.Controls.Add(button, 2, row);
        }

        private void DetectVersionedIndex(bool showMessage)
        {
            try
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"COMPONENTS\DerivedData\VersionedIndex",
                    false);
                string detected = key?.GetSubKeyNames()
                    .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(detected))
                    throw new InvalidOperationException("No VersionedIndex registry entries were found.");

                versionedIndexBox.Text = detected;
                Log(ToolkitLogLevel.Info, $"Detected VersionedIndex: {detected}");
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException ||
                ex is System.Security.SecurityException ||
                ex is IOException ||
                ex is InvalidOperationException)
            {
                Log(ToolkitLogLevel.Warning, $"VersionedIndex detection: {ex.Message}");
                if (showMessage)
                    MessageBox.Show(this, ex.Message, "VersionedIndex",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task SearchAsync()
        {
            string keyword = keywordBox.Text.Trim();
            string winSxs = winSxsBox.Text.Trim();
            if (keyword.Length < 2)
            {
                ShowValidationError("Enter at least two characters for the component keyword.");
                return;
            }

            CancellationToken token = BeginOperation();
            matches = new List<ToolkitDirectoryMatch>();
            matchesGrid.DataSource = null;
            summaryLabel.Text = "Searching component folders and calculating sizes...";
            Log(ToolkitLogLevel.Process, $"Searching WinSxS for components containing '{keyword}'.");

            try
            {
                matches = await Task.Run(() => ToolkitFileOperations.FindTopLevelDirectories(
                    "WinSxS",
                    winSxs,
                    keyword,
                    token), token);
                matchesGrid.DataSource = matches;
                matchesGrid.ClearSelection();
                summaryLabel.Text = $"{matches.Count} matching component folder(s) found.";
                Log(matches.Count == 0 ? ToolkitLogLevel.Warning : ToolkitLogLevel.Success,
                    summaryLabel.Text);
            }
            catch (OperationCanceledException)
            {
                summaryLabel.Text = "Component search cancelled.";
                Log(ToolkitLogLevel.Warning, summaryLabel.Text);
            }
            catch (Exception ex)
            {
                summaryLabel.Text = "Component search failed.";
                Log(ToolkitLogLevel.Error, ex.Message);
                MessageBox.Show(this, ex.Message, "Component search failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task ExportAsync()
        {
            ToolkitDirectoryMatch selected = GetSelectedMatch();
            if (selected == null)
            {
                ShowValidationError("Select one component folder to export.");
                return;
            }

            string destinationParent;
            try
            {
                if (string.IsNullOrWhiteSpace(destinationBox.Text))
                    throw new InvalidOperationException("Select an export destination.");
                destinationParent = Path.GetFullPath(destinationBox.Text.Trim());
                ToolkitFileOperations.EnsureDestinationOutsideSources(
                    destinationParent,
                    new[] { selected.FullPath });
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is NotSupportedException ||
                ex is PathTooLongException ||
                ex is InvalidOperationException)
            {
                ShowValidationError($"The export destination is invalid: {ex.Message}");
                return;
            }

            bool exportRegistry = registryCheckBox.Checked;
            string family = familyBox.Text.Trim();
            string version = versionedIndexBox.Text.Trim();
            string winSxsPath = winSxsBox.Text.Trim();
            string componentKeyword = keywordBox.Text.Trim();
            if (exportRegistry && (family.Length == 0 || version.Length == 0))
            {
                ShowValidationError(
                    "Registry family name and VersionedIndex are required when registry export is enabled.");
                return;
            }

            if (MessageBox.Show(
                    this,
                    $"Export this component to a new isolated directory?{Environment.NewLine}{Environment.NewLine}" +
                    $"{selected.Name}{Environment.NewLine}{Environment.NewLine}" +
                    $"Destination parent: {destinationParent}{Environment.NewLine}" +
                    $"Registry export: {(exportRegistry ? "Enabled" : "Disabled")}",
                    "Confirm component export",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            CancellationToken token = BeginOperation();
            string exportRoot = null;
            try
            {
                exportRoot = ToolkitFileOperations.CreateTimestampedDirectory(
                    destinationParent,
                    "ComponentExport");
                Log(ToolkitLogLevel.Info, $"Created isolated export directory: {exportRoot}");

                await Task.Run(() =>
                {
                    string target = Path.Combine(exportRoot, "WinSxS", selected.Name);
                    Log(ToolkitLogLevel.Process, $"Copying component folder {selected.FullPath}");
                    ToolkitFileOperations.CopyDirectory(selected.FullPath, target, token);
                    CopyMatchingManifests(selected.Name, winSxsPath, exportRoot, token);
                    WriteManifest(exportRoot, selected, exportRegistry);
                }, token);

                if (exportRegistry)
                    await ExportRegistryKeysAsync(exportRoot, componentKeyword, family, version, token);

                string packagePath = null;
                if (packageCheckBox.Checked)
                {
                    packagePath = await Task.Run(() => ToolkitFileOperations.CreateSfcFixPackage(
                        exportRoot,
                        new[]
                        {
                            @"{ARCHIVE}\WinSxS %SystemRoot%\WinSxS [DIR]",
                            @"{ARCHIVE}\Manifests %SystemRoot%\WinSxS\Manifests [DIR]"
                        },
                        token), token);
                    PackageCreated?.Invoke(this, new PackageCreatedEventArgs(packagePath));
                    Log(ToolkitLogLevel.Success, $"Created SFCFix package: {packagePath}");
                }

                summaryLabel.Text = $"Component export completed: {exportRoot}";
                Log(ToolkitLogLevel.Success, summaryLabel.Text);
            }
            catch (OperationCanceledException)
            {
                summaryLabel.Text = exportRoot == null
                    ? "Component export cancelled."
                    : $"Component export cancelled. Partial files remain in {exportRoot}";
                Log(ToolkitLogLevel.Warning, summaryLabel.Text);
            }
            catch (Exception ex)
            {
                summaryLabel.Text = exportRoot == null
                    ? "Component export failed."
                    : $"Component export failed. Partial files remain in {exportRoot}";
                Log(ToolkitLogLevel.Error, ex.Message);
                MessageBox.Show(this,
                    $"{ex.Message}{Environment.NewLine}{Environment.NewLine}{summaryLabel.Text}",
                    "Component export failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task ExportRegistryKeysAsync(
            string exportRoot,
            string componentKeyword,
            string family,
            string version,
            CancellationToken token)
        {
            string registryDirectory = Path.Combine(exportRoot, "Registry");
            Directory.CreateDirectory(registryDirectory);
            var candidates = new List<Tuple<string, string>>();

            AddMatchingRegistryKey(
                candidates,
                @"COMPONENTS\DerivedData\Components",
                name => name.IndexOf(componentKeyword, StringComparison.OrdinalIgnoreCase) >= 0,
                "Components");
            AddMatchingRegistryKey(
                candidates,
                $@"COMPONENTS\DerivedData\VersionedIndex\{version}\ComponentFamilies",
                name => name.StartsWith(family, StringComparison.OrdinalIgnoreCase),
                "ComponentFamilies");
            AddMatchingRegistryKey(
                candidates,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\SideBySide\Winners",
                name => name.StartsWith(family, StringComparison.OrdinalIgnoreCase),
                "Winners");

            if (candidates.Count == 0)
            {
                Log(ToolkitLogLevel.Warning, "No matching registry keys were found for export.");
                return;
            }

            string regExe = Path.Combine(Environment.SystemDirectory, "reg.exe");
            foreach (Tuple<string, string> candidate in candidates)
            {
                token.ThrowIfCancellationRequested();
                string fileName = SanitizeFileName(candidate.Item2) + ".reg";
                string destination = Path.Combine(registryDirectory, fileName);
                string arguments = "export " +
                                   ToolkitProcessRunner.QuoteArgument(candidate.Item1) + " " +
                                   ToolkitProcessRunner.QuoteArgument(destination) + " /y";
                Log(ToolkitLogLevel.Command, $"{regExe} {arguments}");
                var progress = new Progress<ProcessOutputLine>(line =>
                    Log(line.IsError ? ToolkitLogLevel.Error : ToolkitLogLevel.Info, line.Text));
                ProcessExecutionResult result = await ToolkitProcessRunner.RunAsync(
                    regExe,
                    arguments,
                    token,
                    progress);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Registry export failed for {candidate.Item1} (exit code {result.ExitCode}).");

                Log(ToolkitLogLevel.Success, $"Exported registry key: {candidate.Item1}");
            }
        }

        private void AddMatchingRegistryKey(
            ICollection<Tuple<string, string>> candidates,
            string parentPath,
            Func<string, bool> predicate,
            string filePrefix)
        {
            try
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(parentPath, false);
                string match = key?.GetSubKeyNames().FirstOrDefault(predicate);
                if (match == null)
                {
                    Log(ToolkitLogLevel.Warning,
                        $"No matching key found under HKLM\\{parentPath}.");
                    return;
                }

                candidates.Add(Tuple.Create(
                    $"HKLM\\{parentPath}\\{match}",
                    $"{filePrefix}-{match}"));
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException ||
                ex is System.Security.SecurityException ||
                ex is IOException)
            {
                Log(ToolkitLogLevel.Warning,
                    $"Unable to inspect HKLM\\{parentPath}: {ex.Message}");
            }
        }

        private void CopyMatchingManifests(
            string componentName,
            string winSxsPath,
            string exportRoot,
            CancellationToken token)
        {
            string manifestRoot = Path.Combine(winSxsPath, "Manifests");
            if (!Directory.Exists(manifestRoot))
            {
                Log(ToolkitLogLevel.Warning, $"Manifest directory not found: {manifestRoot}");
                return;
            }

            string[] manifests = Directory.GetFiles(
                manifestRoot,
                componentName + "*.manifest",
                SearchOption.TopDirectoryOnly);
            if (manifests.Length == 0)
            {
                Log(ToolkitLogLevel.Warning,
                    $"No manifest matched component '{componentName}'.");
                return;
            }

            string destination = Path.Combine(exportRoot, "Manifests");
            Directory.CreateDirectory(destination);
            foreach (string manifest in manifests)
            {
                token.ThrowIfCancellationRequested();
                File.Copy(manifest, Path.Combine(destination, Path.GetFileName(manifest)), false);
            }

            Log(ToolkitLogLevel.Success, $"Copied {manifests.Length} matching manifest(s).");
        }

        private static void WriteManifest(
            string exportRoot,
            ToolkitDirectoryMatch selected,
            bool registryRequested)
        {
            File.WriteAllLines(
                Path.Combine(exportRoot, "component-export-manifest.txt"),
                new[]
                {
                    "DISM Tool GUI - Component Export",
                    $"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Component: {selected.Name}",
                    $"Source: {selected.FullPath}",
                    $"Estimated bytes: {selected.SizeBytes}",
                    $"Registry export requested: {registryRequested}"
                },
                new UTF8Encoding(false));
        }

        private static string SanitizeFileName(string value)
        {
            return string.Concat((value ?? "Registry")
                .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        }

        private ToolkitDirectoryMatch GetSelectedMatch()
        {
            return matchesGrid.SelectedRows.Count == 1
                ? matchesGrid.SelectedRows[0].DataBoundItem as ToolkitDirectoryMatch
                : null;
        }

        private void InvalidateSearch()
        {
            matches = new List<ToolkitDirectoryMatch>();
            matchesGrid.DataSource = null;
            exportButton.Enabled = false;
            summaryLabel.Text = "Search and choose the exact component version to export.";
        }

        private void UpdateExportButton()
        {
            exportButton.Enabled = !IsBusy && GetSelectedMatch() != null;
        }

        private void ShowValidationError(string message)
        {
            Log(ToolkitLogLevel.Warning, message);
            MessageBox.Show(this, message, "Component Export",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        protected override void OnBusyChanged(bool busy)
        {
            keywordBox.Enabled = !busy;
            familyBox.Enabled = !busy;
            versionedIndexBox.Enabled = !busy;
            winSxsBox.Enabled = !busy;
            destinationBox.Enabled = !busy;
            browseWinSxsButton.Enabled = !busy;
            browseDestinationButton.Enabled = !busy;
            detectVersionButton.Enabled = !busy;
            searchButton.Enabled = !busy;
            cancelButton.Enabled = busy;
            SetChoiceControlState(registryCheckBox, !busy);
            SetChoiceControlState(packageCheckBox, !busy);
            matchesGrid.Enabled = !busy;
            UpdateExportButton();
        }
    }
}
