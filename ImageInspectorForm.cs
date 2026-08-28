using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace DismToolGui
{
    internal sealed class ImageInspectorForm : Form
    {
        private AppTheme currentTheme;
        private readonly TextBox imagePathBox;
        private readonly Button browseButton;
        private readonly Button inspectButton;
        private readonly Button useSelectionButton;
        private readonly Button closeButton;
        private readonly DataGridView imageGrid;
        private readonly RichTextBox outputBox;
        private readonly bool embeddedMode;
        private readonly Action<string, bool> logSink;
        private bool isBusy;

        public ImageInspectorForm(bool darkTheme, string initialImagePath)
            : this(
                darkTheme ? ThemeCatalog.Default : ThemeCatalog.DefaultLight,
                initialImagePath,
                false,
                null)
        {
        }

        internal ImageInspectorForm(
            AppTheme theme,
            string initialImagePath,
            bool embeddedMode,
            Action<string, bool> logSink)
        {
            currentTheme = theme ?? ThemeCatalog.Default;
            this.embeddedMode = embeddedMode;
            this.logSink = logSink;

            Text = "WIM / ESD Image Inspector";
            ClientSize = new Size(900, 620);
            MinimumSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            if (embeddedMode)
                MinimumSize = Size.Empty;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var pathLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                Margin = new Padding(0, 0, 0, 10)
            };
            pathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var pathLabel = new Label
            {
                Text = "Image file:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 7, 8, 0)
            };

            imagePathBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = initialImagePath ?? string.Empty,
                Margin = new Padding(0, 3, 8, 3)
            };

            browseButton = CreateButton("Browse", 86);
            browseButton.Click += BrowseButton_Click;

            inspectButton = CreateButton("Inspect", 92);
            inspectButton.Click += InspectButton_Click;

            pathLayout.Controls.Add(pathLabel, 0, 0);
            pathLayout.Controls.Add(imagePathBox, 1, 0);
            pathLayout.Controls.Add(browseButton, 2, 0);
            pathLayout.Controls.Add(inspectButton, 3, 0);

            imageGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 10)
            };
            imageGrid.Columns.Add(CreateColumn("Index", "Index", 60));
            imageGrid.Columns.Add(CreateColumn("Edition / name", "Name", 215));
            imageGrid.Columns.Add(CreateColumn("Architecture", "Architecture", 95));
            imageGrid.Columns.Add(CreateColumn("Version", "Version", 110));
            imageGrid.Columns.Add(CreateColumn("Size", "Size", 125));
            var descriptionColumn = CreateColumn("Description", "Description", 240);
            descriptionColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            imageGrid.Columns.Add(descriptionColumn);
            imageGrid.SelectionChanged += (sender, args) =>
                useSelectionButton.Enabled = !isBusy && imageGrid.SelectedRows.Count == 1;
            imageGrid.CellDoubleClick += (sender, args) =>
            {
                if (args.RowIndex >= 0 && useSelectionButton.Enabled)
                    UseSelection();
            };

            outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                WordWrap = false,
                DetectUrls = false,
                Margin = new Padding(0, 0, 0, 10)
            };

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0)
            };

            closeButton = CreateButton("Close", 90);
            if (embeddedMode)
            {
                closeButton.Text = "Back";
                closeButton.Click += (sender, args) => CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                closeButton.DialogResult = DialogResult.Cancel;
            }

            useSelectionButton = CreateButton("Use selected index", 155);
            useSelectionButton.Enabled = false;
            useSelectionButton.Click += (sender, args) => UseSelection();

            footer.Controls.Add(closeButton);
            footer.Controls.Add(useSelectionButton);

            root.Controls.Add(pathLayout, 0, 0);
            root.Controls.Add(imageGrid, 0, 1);
            root.Controls.Add(outputBox, 0, 2);
            root.Controls.Add(footer, 0, 3);
            Controls.Add(root);

            if (logSink != null)
            {
                outputBox.Visible = false;
                root.RowStyles[2] = new RowStyle(SizeType.Absolute, 0F);
            }

            if (!embeddedMode)
                CancelButton = closeButton;
            FormClosing += ImageInspectorForm_FormClosing;
            ApplyTheme(currentTheme);
        }

        public string SelectedImagePath { get; private set; }
        public int SelectedImageIndex { get; private set; }
        internal string CurrentImagePath => imagePathBox.Text;
        internal bool IsBusy => isBusy;
        internal event EventHandler SelectionAccepted;
        internal event EventHandler CloseRequested;

        private static Button CreateButton(string text, int width)
        {
            return new ThemedButton
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(width, 32),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(4, 0, 0, 0)
            };
        }

        private static DataGridViewTextBoxColumn CreateColumn(
            string header,
            string property,
            int width)
        {
            return new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                DataPropertyName = property,
                Width = width,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Windows image files (*.wim;*.esd)|*.wim;*.esd|All files (*.*)|*.*",
                CheckFileExists = true,
                Title = "Select a Windows image"
            };

            if (File.Exists(imagePathBox.Text))
                dialog.FileName = imagePathBox.Text;

            if (dialog.ShowDialog(this) == DialogResult.OK)
                imagePathBox.Text = dialog.FileName;
        }

        private async void InspectButton_Click(object sender, EventArgs e)
        {
            string imagePath = imagePathBox.Text.Trim();
            if (!File.Exists(imagePath))
            {
                MessageBox.Show(this, "Select an existing WIM or ESD file.", "Image not found",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string extension = Path.GetExtension(imagePath);
            if (!extension.Equals(".wim", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".esd", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "The selected file must use the .wim or .esd extension.",
                    "Unsupported image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            imageGrid.DataSource = null;
            outputBox.Clear();
            AppendOutput($"Inspecting {imagePath}", false);

            try
            {
                var progress = new Progress<DismOutputLine>(line =>
                    AppendOutput(line.Text, line.IsError));
                var result = await DismCommandRunner.RunAsync(
                    $"/English /Get-ImageInfo /ImageFile:\"{imagePath}\"",
                    progress);

                if (!result.Succeeded)
                {
                    MessageBox.Show(this, $"DISM failed with exit code {result.ExitCode}.",
                        "Inspection failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                List<WimImageInfo> images = ParseImageInfo(result.StandardOutput);
                if (images.Count == 0)
                {
                    MessageBox.Show(this, "DISM completed, but no image indexes were found.",
                        "No images found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                imageGrid.DataSource = images;
                imageGrid.ClearSelection();
                AppendOutput($"Found {images.Count} image index(es).", false);
            }
            catch (Exception ex)
            {
                AppendOutput($"ERROR: {ex.Message}", true);
                MessageBox.Show(this, ex.Message, "Inspection failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static List<WimImageInfo> ParseImageInfo(string output)
        {
            var images = new List<WimImageInfo>();
            WimImageInfo current = null;

            foreach (string rawLine in (output ?? string.Empty).Split(
                new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                int separator = rawLine.IndexOf(':');
                if (separator < 0)
                    continue;

                string key = rawLine.Substring(0, separator).Trim();
                string value = rawLine.Substring(separator + 1).Trim();

                if (key.Equals("Index", StringComparison.OrdinalIgnoreCase))
                {
                    if (current != null)
                        images.Add(current);

                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                    {
                        current = null;
                        continue;
                    }

                    current = new WimImageInfo { Index = index };
                    continue;
                }

                if (current == null)
                    continue;

                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    current.Name = value;
                else if (key.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    current.Description = value;
                else if (key.Equals("Architecture", StringComparison.OrdinalIgnoreCase))
                    current.Architecture = value;
                else if (key.Equals("Version", StringComparison.OrdinalIgnoreCase))
                    current.Version = value;
                else if (key.Equals("Size", StringComparison.OrdinalIgnoreCase))
                    current.Size = value;
            }

            if (current != null)
                images.Add(current);

            return images;
        }

        private void UseSelection()
        {
            if (imageGrid.SelectedRows.Count != 1 ||
                !(imageGrid.SelectedRows[0].DataBoundItem is WimImageInfo selected))
                return;

            SelectedImagePath = imagePathBox.Text.Trim();
            SelectedImageIndex = selected.Index;
            if (embeddedMode)
            {
                SelectionAccepted?.Invoke(this, EventArgs.Empty);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void SetBusy(bool busy)
        {
            isBusy = busy;
            imagePathBox.Enabled = !busy;
            browseButton.Enabled = !busy;
            inspectButton.Enabled = !busy;
            closeButton.Enabled = !busy;
            imageGrid.Enabled = !busy;
            useSelectionButton.Enabled = !busy && imageGrid.SelectedRows.Count == 1;
        }

        private void AppendOutput(string message, bool isError)
        {
            if (logSink != null)
            {
                logSink(message, isError);
                return;
            }

            outputBox.SelectionStart = outputBox.TextLength;
            outputBox.SelectionLength = 0;
            outputBox.SelectionColor = isError
                ? currentTheme.LogError
                : currentTheme.LogInfo;
            outputBox.AppendText(message + Environment.NewLine);
            outputBox.ScrollToCaret();
        }

        private void ImageInspectorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isBusy)
                return;

            e.Cancel = true;
            MessageBox.Show(this, "Wait for DISM inspection to finish before closing this window.",
                "Inspection in progress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        internal void ApplyTheme(AppTheme theme)
        {
            currentTheme = theme ?? ThemeCatalog.Default;
            BackColor = currentTheme.Background;
            ForeColor = currentTheme.Foreground;
            ThemeStyler.ApplyControlTree(this, currentTheme);

            imagePathBox.BackColor = currentTheme.InputBackground;
            imagePathBox.ForeColor = currentTheme.InputForeground;
            outputBox.BackColor = currentTheme.OutputBackground;
            outputBox.ForeColor = currentTheme.OutputForeground;

            foreach (Button button in new[] { browseButton, inspectButton, useSelectionButton })
                ThemeStyler.ApplyButton(button, currentTheme);

            ThemeStyler.ApplyGrid(imageGrid, currentTheme);
        }

        private sealed class WimImageInfo
        {
            public int Index { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Architecture { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public string Size { get; set; } = string.Empty;
        }
    }
}
