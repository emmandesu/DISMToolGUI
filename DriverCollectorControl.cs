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
    internal sealed class DriverCollectorControl : ToolkitPageBase
    {
        private readonly TextBox repositoryKeywordBox;
        private readonly TextBox winSxsKeywordBox;
        private readonly TextBox destinationBox;
        private readonly Button browseButton;
        private readonly Button previewButton;
        private readonly Button collectButton;
        private readonly Button cancelButton;
        private readonly CheckBox createPackageCheckBox;
        private readonly DataGridView matchesGrid;
        private readonly Label summaryLabel;
        private List<ToolkitDirectoryMatch> matches = new List<ToolkitDirectoryMatch>();

        public DriverCollectorControl(Action<ToolkitLogLevel, string> logger)
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
                RowCount = 3,
                Margin = new Padding(0, 0, 0, 8)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            repositoryKeywordBox = CreateTextBox();
            winSxsKeywordBox = CreateTextBox();
            destinationBox = CreateTextBox();
            browseButton = CreateButton("Browse", 88);
            browseButton.Click += (sender, args) =>
            {
                string selected = BrowseForFolder(
                    "Select the parent folder for a new driver collection",
                    destinationBox.Text);
                if (selected != null)
                    destinationBox.Text = selected;
            };

            fields.Controls.Add(CreateLabel("FileRepository keyword:"), 0, 0);
            fields.Controls.Add(repositoryKeywordBox, 1, 0);
            fields.SetColumnSpan(repositoryKeywordBox, 2);
            fields.Controls.Add(CreateLabel("WinSxS keyword:"), 0, 1);
            fields.Controls.Add(winSxsKeywordBox, 1, 1);
            fields.SetColumnSpan(winSxsKeywordBox, 2);
            fields.Controls.Add(CreateLabel("Export parent:"), 0, 2);
            fields.Controls.Add(destinationBox, 1, 2);
            fields.Controls.Add(browseButton, 2, 2);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            previewButton = CreateButton("Preview matches", 135);
            collectButton = CreateButton("Collect files", 115);
            cancelButton = CreateButton("Cancel", 90);
            createPackageCheckBox = new CheckBox
            {
                Text = "Create SFCFix.txt and SFCFix.zip",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(8, 7, 0, 0)
            };
            collectButton.Enabled = false;
            cancelButton.Enabled = false;

            previewButton.Click += async (sender, args) => await PreviewAsync();
            collectButton.Click += async (sender, args) => await CollectAsync();
            cancelButton.Click += (sender, args) => CancelOperation();
            repositoryKeywordBox.TextChanged += (sender, args) => InvalidatePreview();
            winSxsKeywordBox.TextChanged += (sender, args) => InvalidatePreview();

            toolbar.Controls.AddRange(new Control[]
            {
                previewButton,
                collectButton,
                cancelButton,
                createPackageCheckBox
            });

            matchesGrid = CreateResultsGrid();
            matchesGrid.Columns.Add(CreateColumn("Source", "Source", 120));
            matchesGrid.Columns.Add(CreateColumn("Folder", "Name", 400));
            matchesGrid.Columns.Add(CreateColumn("Size", "Size", 90));
            matchesGrid.Columns.Add(CreateColumn("Full path", "FullPath", 400, true));

            summaryLabel = new Label
            {
                Text = "Preview matches before collecting files.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };

            root.Controls.Add(fields, 0, 0);
            root.Controls.Add(toolbar, 0, 1);
            root.Controls.Add(matchesGrid, 0, 2);
            root.Controls.Add(summaryLabel, 0, 3);
            Controls.Add(root);
        }

        public event EventHandler<PackageCreatedEventArgs> PackageCreated;

        private async Task PreviewAsync()
        {
            string repositoryKeyword = repositoryKeywordBox.Text.Trim();
            string winSxsKeyword = winSxsKeywordBox.Text.Trim();
            CancellationToken token;
            try
            {
                ValidateKeywords();
                token = BeginOperation();
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                ShowValidationError(ex.Message);
                return;
            }

            matches = new List<ToolkitDirectoryMatch>();
            matchesGrid.DataSource = null;
            summaryLabel.Text = "Calculating matches and sizes...";
            Log(ToolkitLogLevel.Process, "Previewing matching driver and component folders.");

            try
            {
                matches = await Task.Run(
                    () => FindMatches(repositoryKeyword, winSxsKeyword, token),
                    token);
                matchesGrid.DataSource = matches;
                matchesGrid.ClearSelection();
                long totalBytes = matches.Sum(match => match.SizeBytes);
                summaryLabel.Text =
                    $"{matches.Count} folder(s), {ToolkitFileOperations.FormatBytes(totalBytes)} total.";
                Log(matches.Count == 0 ? ToolkitLogLevel.Warning : ToolkitLogLevel.Success,
                    summaryLabel.Text);
            }
            catch (OperationCanceledException)
            {
                summaryLabel.Text = "Preview cancelled.";
                Log(ToolkitLogLevel.Warning, "Driver collection preview cancelled.");
            }
            catch (Exception ex)
            {
                summaryLabel.Text = "Preview failed.";
                Log(ToolkitLogLevel.Error, ex.Message);
                MessageBox.Show(this, ex.Message, "Preview failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task CollectAsync()
        {
            if (matches.Count == 0)
            {
                ShowValidationError("Preview at least one matching folder before collecting files.");
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
                    matches.Select(match => match.FullPath));
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

            if (MessageBox.Show(
                    this,
                    $"Create a new isolated collection under:{Environment.NewLine}{Environment.NewLine}" +
                    $"{destinationParent}{Environment.NewLine}{Environment.NewLine}" +
                    $"Folders to copy: {matches.Count}{Environment.NewLine}" +
                    $"Estimated size: {ToolkitFileOperations.FormatBytes(matches.Sum(match => match.SizeBytes))}",
                    "Confirm driver collection",
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
                    "DriverCollection");
                Log(ToolkitLogLevel.Info, $"Created isolated export directory: {exportRoot}");

                await Task.Run(() =>
                {
                    foreach (ToolkitDirectoryMatch match in matches)
                    {
                        token.ThrowIfCancellationRequested();
                        string sourceFolder = match.Source.Equals(
                            "FileRepository", StringComparison.OrdinalIgnoreCase)
                            ? "FileRepository"
                            : "WinSxS";
                        string destination = Path.Combine(exportRoot, sourceFolder, match.Name);
                        Log(ToolkitLogLevel.Process, $"Copying {match.FullPath}");
                        ToolkitFileOperations.CopyDirectory(match.FullPath, destination, token);
                        Log(ToolkitLogLevel.Success, $"Copied {match.Name}.");
                    }

                    WriteManifest(exportRoot, matches);
                }, token);

                string packagePath = null;
                if (createPackageCheckBox.Checked)
                {
                    packagePath = await Task.Run(() => ToolkitFileOperations.CreateSfcFixPackage(
                        exportRoot,
                        BuildSfcFixInstructions(matches),
                        token), token);
                    Log(ToolkitLogLevel.Success, $"Created SFCFix package: {packagePath}");
                    PackageCreated?.Invoke(this, new PackageCreatedEventArgs(packagePath));
                }

                summaryLabel.Text = $"Collection completed: {exportRoot}";
                Log(ToolkitLogLevel.Success, summaryLabel.Text);
            }
            catch (OperationCanceledException)
            {
                summaryLabel.Text = exportRoot == null
                    ? "Collection cancelled."
                    : $"Collection cancelled. Partial files remain in {exportRoot}";
                Log(ToolkitLogLevel.Warning, summaryLabel.Text);
            }
            catch (Exception ex)
            {
                summaryLabel.Text = exportRoot == null
                    ? "Collection failed."
                    : $"Collection failed. Partial files remain in {exportRoot}";
                Log(ToolkitLogLevel.Error, ex.Message);
                MessageBox.Show(this,
                    $"{ex.Message}{Environment.NewLine}{Environment.NewLine}{summaryLabel.Text}",
                    "Driver collection failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private static List<ToolkitDirectoryMatch> FindMatches(
            string repositoryKeyword,
            string winSxsKeyword,
            CancellationToken token)
        {
            var results = new List<ToolkitDirectoryMatch>();
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            if (!string.IsNullOrWhiteSpace(repositoryKeyword))
            {
                results.AddRange(ToolkitFileOperations.FindTopLevelDirectories(
                    "FileRepository",
                    Path.Combine(windows, "System32", "DriverStore", "FileRepository"),
                    repositoryKeyword,
                    token));
            }

            if (!string.IsNullOrWhiteSpace(winSxsKeyword))
            {
                results.AddRange(ToolkitFileOperations.FindTopLevelDirectories(
                    "WinSxS",
                    Path.Combine(windows, "WinSxS"),
                    winSxsKeyword,
                    token));
            }

            return results;
        }

        private void ValidateKeywords()
        {
            string repositoryKeyword = repositoryKeywordBox.Text.Trim();
            string winSxsKeyword = winSxsKeywordBox.Text.Trim();
            if (repositoryKeyword.Length == 0 && winSxsKeyword.Length == 0)
                throw new InvalidOperationException("Enter at least one search keyword.");
            if (repositoryKeyword.Length == 1 || winSxsKeyword.Length == 1)
                throw new InvalidOperationException("Each supplied keyword must contain at least two characters.");
        }

        private static IEnumerable<string> BuildSfcFixInstructions(
            IEnumerable<ToolkitDirectoryMatch> exportedMatches)
        {
            if (exportedMatches.Any(match => match.Source == "FileRepository"))
                yield return @"{ARCHIVE}\FileRepository %SystemRoot%\System32\DriverStore\FileRepository [DIR]";
            if (exportedMatches.Any(match => match.Source == "WinSxS"))
                yield return @"{ARCHIVE}\WinSxS %SystemRoot%\WinSxS [DIR]";
        }

        private static void WriteManifest(
            string exportRoot,
            IEnumerable<ToolkitDirectoryMatch> exportedMatches)
        {
            var lines = new List<string>
            {
                "DISM Tool GUI - Driver File Collection",
                $"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                string.Empty
            };
            lines.AddRange(exportedMatches.Select(match =>
                $"{match.Source}\t{match.Name}\t{match.SizeBytes}\t{match.FullPath}"));
            File.WriteAllLines(
                Path.Combine(exportRoot, "collection-manifest.txt"),
                lines,
                new UTF8Encoding(false));
        }

        private void InvalidatePreview()
        {
            matches = new List<ToolkitDirectoryMatch>();
            matchesGrid.DataSource = null;
            collectButton.Enabled = false;
            summaryLabel.Text = "Preview matches before collecting files.";
        }

        private void ShowValidationError(string message)
        {
            Log(ToolkitLogLevel.Warning, message);
            MessageBox.Show(this, message, "Driver File Collector",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        protected override void OnBusyChanged(bool busy)
        {
            repositoryKeywordBox.Enabled = !busy;
            winSxsKeywordBox.Enabled = !busy;
            destinationBox.Enabled = !busy;
            browseButton.Enabled = !busy;
            previewButton.Enabled = !busy;
            collectButton.Enabled = !busy && matches.Count > 0;
            cancelButton.Enabled = busy;
            SetChoiceControlState(createPackageCheckBox, !busy);
            matchesGrid.Enabled = !busy;
        }
    }
}
