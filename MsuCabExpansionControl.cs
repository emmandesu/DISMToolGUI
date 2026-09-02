using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DismToolGui
{
    internal sealed class MsuCabExpansionControl : ToolkitPageBase
    {
        private const int MaximumNestedCabFiles = 512;
        private static readonly string ExpandExecutable =
            Path.Combine(Environment.SystemDirectory, "expand.exe");

        private readonly TextBox sourceFileBox;
        private readonly TextBox destinationBox;
        private readonly TextBox commandPreviewBox;
        private readonly Button browseSourceButton;
        private readonly Button browseDestinationButton;
        private readonly Button expandButton;
        private readonly Button cancelButton;
        private readonly Button openDestinationButton;
        private readonly CheckBox deepExpandCheckBox;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;

        public MsuCabExpansionControl(Action<ToolkitLogLevel, string> logger)
            : base(logger)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var notice = new Label
            {
                Text = "Extract MSU or CAB contents with the Windows expansion utility. " +
                       "Optional deep expansion places nested CAB payloads in isolated subfolders without renaming source files.",
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
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 6)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            sourceFileBox = CreateTextBox();
            destinationBox = CreateTextBox();
            browseSourceButton = CreateButton("Browse", 88);
            browseDestinationButton = CreateButton("Browse", 88);

            browseSourceButton.Click += (sender, args) =>
            {
                string selected = BrowseForFile(
                    "Select an MSU or CAB package",
                    "Windows packages (*.msu;*.cab)|*.msu;*.cab|MSU packages (*.msu)|*.msu|CAB packages (*.cab)|*.cab|All files (*.*)|*.*",
                    NormalizePathInput(sourceFileBox.Text));
                if (selected != null)
                {
                    sourceFileBox.Text = selected;
                    if (string.IsNullOrWhiteSpace(destinationBox.Text))
                    {
                        string parent = Path.GetDirectoryName(selected);
                        string name = Path.GetFileNameWithoutExtension(selected);
                        if (!string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(name))
                            destinationBox.Text = Path.Combine(parent, name + "_Expanded");
                    }
                }
            };
            browseDestinationButton.Click += (sender, args) =>
            {
                string selected = BrowseForFolder(
                    "Select the extraction destination",
                    NormalizePathInput(destinationBox.Text));
                if (selected != null)
                    destinationBox.Text = selected;
            };

            fields.Controls.Add(CreateLabel("MSU / CAB file:"), 0, 0);
            fields.Controls.Add(sourceFileBox, 1, 0);
            fields.Controls.Add(browseSourceButton, 2, 0);
            fields.Controls.Add(CreateLabel("Destination:"), 0, 1);
            fields.Controls.Add(destinationBox, 1, 1);
            fields.Controls.Add(browseDestinationButton, 2, 1);

            deepExpandCheckBox = new CheckBox
            {
                Text = "Recursively expand nested CAB payloads into CAB_Extracted",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 8)
            };

            var previewGroup = new GroupBox
            {
                Text = "Command Preview",
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 58,
                MinimumSize = new System.Drawing.Size(0, 58),
                MaximumSize = new System.Drawing.Size(0, 58),
                Padding = new Padding(8),
                Margin = new Padding(0, 0, 0, 8)
            };
            commandPreviewBox = new TextBox
            {
                Dock = DockStyle.Top,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new System.Drawing.Font("Consolas", 9F),
                MinimumSize = new System.Drawing.Size(0, 23)
            };
            previewGroup.Controls.Add(commandPreviewBox);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            expandButton = CreateButton("Expand package", 125);
            cancelButton = CreateButton("Cancel", 90);
            openDestinationButton = CreateButton("Open destination", 135);
            cancelButton.Enabled = false;
            openDestinationButton.Enabled = false;
            expandButton.Click += async (sender, args) => await ExpandPackageAsync();
            cancelButton.Click += (sender, args) =>
            {
                cancelButton.Enabled = false;
                statusLabel.Text = "Cancelling expansion...";
                CancelOperation();
            };
            openDestinationButton.Click += (sender, args) => OpenDestination();
            toolbar.Controls.AddRange(new Control[]
            {
                expandButton,
                cancelButton,
                openDestinationButton
            });

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Margin = new Padding(0, 0, 0, 6)
            };
            statusLabel = new Label
            {
                Text = "Select a package and destination.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0)
            };

            sourceFileBox.TextChanged += (sender, args) => UpdatePreviewAndButtons();
            destinationBox.TextChanged += (sender, args) => UpdatePreviewAndButtons();

            root.Controls.Add(notice, 0, 0);
            root.Controls.Add(fields, 0, 1);
            root.Controls.Add(deepExpandCheckBox, 0, 2);
            root.Controls.Add(previewGroup, 0, 3);
            root.Controls.Add(toolbar, 0, 4);

            var progressPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            progressPanel.Controls.Add(progressBar, 0, 0);
            progressPanel.Controls.Add(statusLabel, 0, 1);
            root.Controls.Add(progressPanel, 0, 5);

            Controls.Add(root);
            UpdatePreviewAndButtons();
        }

        private async Task ExpandPackageAsync()
        {
            string sourceFile;
            string destination;
            try
            {
                sourceFile = ValidateSourcePath(sourceFileBox.Text);
                destination = ValidateDestinationPath(destinationBox.Text);
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is IOException ||
                ex is InvalidOperationException ||
                ex is NotSupportedException ||
                ex is PathTooLongException ||
                ex is UnauthorizedAccessException)
            {
                ShowWarning(ex.Message);
                return;
            }

            bool destinationHasContent;
            try
            {
                destinationHasContent = Directory.Exists(destination) &&
                                        Directory.EnumerateFileSystemEntries(destination).Any();
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                ShowWarning($"The destination cannot be inspected: {ex.Message}");
                return;
            }

            string existingContentWarning = destinationHasContent
                ? Environment.NewLine + Environment.NewLine +
                  "The destination is not empty. Files with matching names may be overwritten." +
                  (deepExpandCheckBox.Checked
                      ? " Deep expansion will process every CAB already present in the destination."
                      : string.Empty)
                : string.Empty;
            if (MessageBox.Show(
                    this,
                    $"Expand this package?{Environment.NewLine}{Environment.NewLine}" +
                    $"Source: {sourceFile}{Environment.NewLine}" +
                    $"Destination: {destination}{Environment.NewLine}" +
                    $"Deep CAB expansion: {(deepExpandCheckBox.Checked ? "Yes" : "No")}" +
                    existingContentWarning,
                    "Confirm package expansion",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            CancellationToken token = BeginOperation();
            try
            {
                Directory.CreateDirectory(destination);
                string cabOutputRoot = Path.Combine(destination, "CAB_Extracted");

                progressBar.Value = 10;
                statusLabel.Text = $"Expanding {Path.GetFileName(sourceFile)}...";
                Log(ToolkitLogLevel.Process,
                    $"Expanding {sourceFile} to {destination}.");
                await ExpandFileAsync(sourceFile, destination, token);
                progressBar.Value = 40;

                int nestedCabCount = 0;
                if (deepExpandCheckBox.Checked)
                {
                    List<string> initialCabFiles = await Task.Run(
                        () => FindCabFiles(destination, cabOutputRoot, token),
                        token);
                    nestedCabCount = await ExpandNestedCabFilesAsync(
                        initialCabFiles,
                        destination,
                        cabOutputRoot,
                        token);
                }

                token.ThrowIfCancellationRequested();
                progressBar.Value = 100;
                statusLabel.Text = nestedCabCount == 0
                    ? "Package expansion completed."
                    : $"Package expansion completed; {nestedCabCount} nested CAB file(s) expanded.";
                Log(ToolkitLogLevel.Success,
                    $"Expansion completed successfully: {destination}");
                openDestinationButton.Enabled = true;
            }
            catch (OperationCanceledException)
            {
                progressBar.Value = 0;
                statusLabel.Text = "Expansion cancelled. Partial files were left in the destination for review.";
                Log(ToolkitLogLevel.Warning, statusLabel.Text);
            }
            catch (Exception ex)
            {
                progressBar.Value = 0;
                statusLabel.Text = "Expansion failed.";
                Log(ToolkitLogLevel.Error, ex.Message);
                MessageBox.Show(this, ex.Message, "Expansion failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task<int> ExpandNestedCabFilesAsync(
            IEnumerable<string> initialCabFiles,
            string destination,
            string cabOutputRoot,
            CancellationToken token)
        {
            var pending = new Queue<string>(initialCabFiles);
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int expandedCount = 0;

            if (pending.Count == 0)
            {
                Log(ToolkitLogLevel.Info, "No new nested CAB payloads were found.");
                return 0;
            }

            Directory.CreateDirectory(cabOutputRoot);
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                if (processed.Count >= MaximumNestedCabFiles)
                {
                    throw new InvalidOperationException(
                        $"Deep expansion stopped after {MaximumNestedCabFiles} CAB files to prevent an unbounded archive chain.");
                }

                string cabFile = Path.GetFullPath(pending.Dequeue());
                if (!processed.Add(cabFile) || !File.Exists(cabFile))
                    continue;

                if (Path.GetFileName(cabFile).Equals(
                        "wsusscan.cab",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Log(ToolkitLogLevel.Info, $"Skipped catalog payload: {cabFile}");
                    continue;
                }

                string relativePath = IsUnderDirectory(cabFile, cabOutputRoot)
                    ? GetRelativePath(cabOutputRoot, cabFile)
                    : GetRelativePath(destination, cabFile);
                string relativeDirectory = Path.GetDirectoryName(relativePath) ?? string.Empty;
                string outputDirectory = Path.Combine(
                    cabOutputRoot,
                    relativeDirectory,
                    Path.GetFileNameWithoutExtension(relativePath));
                Directory.CreateDirectory(outputDirectory);

                statusLabel.Text = $"Expanding nested CAB: {Path.GetFileName(cabFile)}";
                Log(ToolkitLogLevel.Process, $"Expanding nested CAB: {cabFile}");
                await ExpandFileAsync(cabFile, outputDirectory, token);
                expandedCount++;

                int totalKnown = expandedCount + pending.Count;
                progressBar.Value = Math.Min(
                    95,
                    40 + (int)(55D * expandedCount / Math.Max(1, totalKnown)));

                foreach (string childCab in Directory.GetFiles(
                             outputDirectory,
                             "*.cab",
                             SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    string fullChildPath = Path.GetFullPath(childCab);
                    if (!processed.Contains(fullChildPath))
                        pending.Enqueue(fullChildPath);
                }
            }

            return expandedCount;
        }

        private async Task ExpandFileAsync(
            string sourceFile,
            string destination,
            CancellationToken token)
        {
            string arguments = "-R " + ToolkitProcessRunner.QuoteArgument(sourceFile) +
                               " -F:* " +
                               ToolkitProcessRunner.QuoteArgument(destination);
            Log(ToolkitLogLevel.Command, $"{ExpandExecutable} {arguments}");

            var output = new Progress<ProcessOutputLine>(line =>
            {
                string text = (line.Text ?? string.Empty).Trim();
                if (text.Length == 0)
                    return;
                Log(line.IsError ? ToolkitLogLevel.Error : ToolkitLogLevel.Debug, text);
            });
            ProcessExecutionResult result = await ToolkitProcessRunner.RunAsync(
                ExpandExecutable,
                arguments,
                token,
                output);
            if (result.Succeeded)
                return;

            string detail = FirstNonEmptyLine(result.StandardError) ??
                            FirstNonEmptyLine(result.StandardOutput);
            string suffix = detail == null ? string.Empty : $" {detail}";
            throw new InvalidOperationException(
                $"expand.exe failed with exit code {result.ExitCode}.{suffix}");
        }

        private static List<string> FindCabFiles(
            string destination,
            string cabOutputRoot,
            CancellationToken token)
        {
            var result = new List<string>();
            foreach (string cabFile in Directory.GetFiles(
                         destination,
                         "*.cab",
                         SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                string fullPath = Path.GetFullPath(cabFile);
                if (IsUnderDirectory(fullPath, cabOutputRoot))
                    continue;

                result.Add(fullPath);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static string ValidateSourcePath(string value)
        {
            string normalized = NormalizePathInput(value);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("Select an MSU or CAB package file.");

            string fullPath = Path.GetFullPath(normalized);
            if (!File.Exists(fullPath))
                throw new InvalidOperationException("The selected package file does not exist.");

            string extension = Path.GetExtension(fullPath);
            if (!extension.Equals(".msu", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".cab", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The selected package must use the .msu or .cab extension.");
            }

            return fullPath;
        }

        private static string ValidateDestinationPath(string value)
        {
            string normalized = NormalizePathInput(value);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("Select an extraction destination.");

            string fullPath = Path.GetFullPath(normalized);
            if (File.Exists(fullPath))
                throw new InvalidOperationException("The destination path points to a file.");
            return fullPath;
        }

        private static string NormalizePathInput(string value)
        {
            string path = (value ?? string.Empty).Trim();
            if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"')
                path = path.Substring(1, path.Length - 2).Trim();
            return path;
        }

        private static bool IsUnderDirectory(string path, string directory)
        {
            string fullPath = Path.GetFullPath(path);
            string prefix = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRelativePath(string directory, string path)
        {
            string prefix = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("An extracted CAB resolved outside the destination.");
            return fullPath.Substring(prefix.Length);
        }

        private static string FirstNonEmptyLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return value
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0);
        }

        private void UpdatePreviewAndButtons()
        {
            string source = NormalizePathInput(sourceFileBox.Text);
            string destination = NormalizePathInput(destinationBox.Text);
            commandPreviewBox.Text = ExpandExecutable + " -R " +
                                     ToolkitProcessRunner.QuoteArgument(
                                         string.IsNullOrWhiteSpace(source)
                                             ? "<MSU or CAB file>"
                                             : source) +
                                     " -F:* " +
                                     ToolkitProcessRunner.QuoteArgument(
                                         string.IsNullOrWhiteSpace(destination)
                                             ? "<destination>"
                                             : destination);
            if (!IsBusy)
            {
                expandButton.Enabled = !string.IsNullOrWhiteSpace(source) &&
                                       !string.IsNullOrWhiteSpace(destination);
                openDestinationButton.Enabled = Directory.Exists(destination);
            }
        }

        private void OpenDestination()
        {
            string destination = NormalizePathInput(destinationBox.Text);
            if (!Directory.Exists(destination))
            {
                ShowWarning("The extraction destination does not exist.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "explorer.exe"),
                    Arguments = ToolkitProcessRunner.QuoteArgument(Path.GetFullPath(destination)),
                    UseShellExecute = true
                });
            }
            catch (Exception ex) when (
                ex is System.ComponentModel.Win32Exception ||
                ex is InvalidOperationException)
            {
                MessageBox.Show(this, ex.Message, "Unable to open destination",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnBusyChanged(bool busy)
        {
            sourceFileBox.ReadOnly = busy;
            destinationBox.ReadOnly = busy;
            sourceFileBox.TabStop = !busy;
            destinationBox.TabStop = !busy;
            browseSourceButton.Enabled = !busy;
            browseDestinationButton.Enabled = !busy;
            SetChoiceControlState(deepExpandCheckBox, !busy);
            expandButton.Enabled = !busy &&
                                   !string.IsNullOrWhiteSpace(sourceFileBox.Text) &&
                                   !string.IsNullOrWhiteSpace(destinationBox.Text);
            cancelButton.Enabled = busy;
            openDestinationButton.Enabled = !busy &&
                                            Directory.Exists(
                                                NormalizePathInput(destinationBox.Text));
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(this, message, "MSU / CAB Expander",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
