using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace DismToolGui
{
    internal sealed class MountedImagesForm : Form
    {
        private readonly bool darkTheme;
        private readonly DataGridView mountedGrid;
        private readonly RichTextBox outputBox;
        private readonly Button refreshButton;
        private readonly Button openFolderButton;
        private readonly Button remountButton;
        private readonly Button commitButton;
        private readonly Button discardButton;
        private readonly Button cleanupButton;
        private readonly Action<string, bool> logSink;
        private bool isBusy;

        public MountedImagesForm(bool darkTheme, bool autoRefresh = true)
            : this(darkTheme, autoRefresh, null, false)
        {
        }

        internal MountedImagesForm(
            bool darkTheme,
            bool autoRefresh,
            Action<string, bool> logSink,
            bool embeddedMode)
        {
            this.darkTheme = darkTheme;
            this.logSink = logSink;

            Text = "Mounted Image Manager";
            ClientSize = new Size(980, 650);
            MinimumSize = new Size(820, 540);
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
                RowCount = 3,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 64F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 10)
            };

            refreshButton = CreateButton("Refresh", 90);
            openFolderButton = CreateButton("Open folder", 105);
            remountButton = CreateButton("Remount", 95);
            commitButton = CreateButton("Commit && unmount", 140);
            discardButton = CreateButton("Discard && unmount", 145);
            cleanupButton = CreateButton("Clean stale mounts", 145);

            refreshButton.Click += async (sender, args) => await RefreshMountedImagesAsync();
            openFolderButton.Click += OpenFolderButton_Click;
            remountButton.Click += async (sender, args) =>
                await RunSelectedActionAsync("Remount", "/Remount-Image", false);
            commitButton.Click += async (sender, args) =>
                await RunSelectedActionAsync("Commit and unmount", "/Unmount-Image", true, "/Commit");
            discardButton.Click += async (sender, args) =>
                await RunSelectedActionAsync("Discard and unmount", "/Unmount-Image", true, "/Discard");
            cleanupButton.Click += async (sender, args) => await CleanupMountPointsAsync();

            toolbar.Controls.AddRange(new Control[]
            {
                refreshButton,
                openFolderButton,
                remountButton,
                commitButton,
                discardButton,
                cleanupButton
            });

            mountedGrid = new DataGridView
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
            mountedGrid.Columns.Add(CreateColumn("Mount directory", "MountDirectory", 250));
            var imageColumn = CreateColumn("Image file", "ImageFile", 290);
            imageColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            mountedGrid.Columns.Add(imageColumn);
            mountedGrid.Columns.Add(CreateColumn("Index", "ImageIndex", 60));
            mountedGrid.Columns.Add(CreateColumn("Read/write", "ReadWrite", 90));
            mountedGrid.Columns.Add(CreateColumn("Status", "Status", 100));
            mountedGrid.SelectionChanged += (sender, args) => UpdateActionButtons();
            mountedGrid.CellDoubleClick += (sender, args) =>
            {
                if (args.RowIndex >= 0)
                    OpenSelectedFolder();
            };

            outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                WordWrap = false,
                DetectUrls = false
            };

            root.Controls.Add(toolbar, 0, 0);
            root.Controls.Add(mountedGrid, 0, 1);
            root.Controls.Add(outputBox, 0, 2);
            Controls.Add(root);

            if (logSink != null)
            {
                outputBox.Visible = false;
                root.RowStyles[2] = new RowStyle(SizeType.Absolute, 0F);
            }

            FormClosing += MountedImagesForm_FormClosing;
            if (autoRefresh)
                Shown += async (sender, args) => await RefreshMountedImagesAsync();
            ApplyTheme(root, toolbar);
            UpdateActionButtons();
        }

        internal bool IsBusy => isBusy;

        private static Button CreateButton(string text, int width)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(width, 32),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 6, 0)
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

        private async System.Threading.Tasks.Task RefreshMountedImagesAsync()
        {
            if (isBusy)
                return;

            SetBusy(true);
            outputBox.Clear();
            AppendOutput("Refreshing mounted images...", false);

            try
            {
                var progress = new Progress<DismOutputLine>(line =>
                    AppendOutput(line.Text, line.IsError));
                var result = await DismCommandRunner.RunAsync(
                    "/English /Get-MountedImageInfo",
                    progress);

                if (!result.Succeeded)
                {
                    MessageBox.Show(this, $"DISM failed with exit code {result.ExitCode}.",
                        "Refresh failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                List<MountedImageInfo> images = ParseMountedImages(result.StandardOutput);
                mountedGrid.DataSource = images;
                mountedGrid.ClearSelection();
                AppendOutput(images.Count == 0
                    ? "No mounted images were found."
                    : $"Found {images.Count} mounted image(s).", false);
            }
            catch (Exception ex)
            {
                AppendOutput($"ERROR: {ex.Message}", true);
                MessageBox.Show(this, ex.Message, "Refresh failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task RunSelectedActionAsync(
            string actionName,
            string dismAction,
            bool requiresConfirmation,
            string additionalArgument = null)
        {
            MountedImageInfo selected = GetSelectedImage();
            if (selected == null)
                return;

            if (requiresConfirmation)
            {
                string warning = additionalArgument == "/Discard"
                    ? "All uncommitted changes in this mounted image will be permanently discarded."
                    : "All changes will be written to the source image before it is unmounted.";
                if (MessageBox.Show(this,
                        $"{warning}{Environment.NewLine}{Environment.NewLine}{selected.MountDirectory}{Environment.NewLine}{Environment.NewLine}Continue?",
                        actionName,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
            }

            string arguments = $"/English {dismAction} /MountDir:\"{selected.MountDirectory}\"";
            if (!string.IsNullOrWhiteSpace(additionalArgument))
                arguments += " " + additionalArgument;

            bool succeeded = await RunActionAsync(actionName, arguments);
            if (succeeded)
                await RefreshMountedImagesAsync();
        }

        private async System.Threading.Tasks.Task CleanupMountPointsAsync()
        {
            if (MessageBox.Show(this,
                    "Clean resources belonging to corrupted, unrecoverable mount points? Recoverable and healthy mounts are not removed.",
                    "Clean stale mounts",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            bool succeeded = await RunActionAsync("Clean stale mounts", "/English /Cleanup-Mountpoints");
            if (succeeded)
                await RefreshMountedImagesAsync();
        }

        private async System.Threading.Tasks.Task<bool> RunActionAsync(string actionName, string arguments)
        {
            SetBusy(true);
            outputBox.Clear();
            AppendOutput($"{DismCommandRunner.ExecutablePath} {arguments}", false);

            try
            {
                var progress = new Progress<DismOutputLine>(line =>
                    AppendOutput(line.Text, line.IsError));
                var result = await DismCommandRunner.RunAsync(arguments, progress);

                if (!result.Succeeded)
                {
                    MessageBox.Show(this, $"DISM failed with exit code {result.ExitCode}.",
                        actionName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                AppendOutput($"{actionName} completed successfully.", false);
                return true;
            }
            catch (Exception ex)
            {
                AppendOutput($"ERROR: {ex.Message}", true);
                MessageBox.Show(this, ex.Message, actionName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static List<MountedImageInfo> ParseMountedImages(string output)
        {
            var images = new List<MountedImageInfo>();
            MountedImageInfo current = null;

            foreach (string rawLine in (output ?? string.Empty).Split(
                new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                int separator = rawLine.IndexOf(':');
                if (separator < 0)
                    continue;

                string key = rawLine.Substring(0, separator).Trim();
                string value = rawLine.Substring(separator + 1).Trim();

                if (key.Equals("Mount Dir", StringComparison.OrdinalIgnoreCase))
                {
                    if (current != null)
                        images.Add(current);
                    current = new MountedImageInfo { MountDirectory = value };
                    continue;
                }

                if (current == null)
                    continue;

                if (key.Equals("Image File", StringComparison.OrdinalIgnoreCase))
                    current.ImageFile = value;
                else if (key.Equals("Image Index", StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                    current.ImageIndex = index;
                else if (key.Equals("Mounted Read/Write", StringComparison.OrdinalIgnoreCase))
                    current.ReadWrite = value;
                else if (key.Equals("Status", StringComparison.OrdinalIgnoreCase))
                    current.Status = value;
            }

            if (current != null)
                images.Add(current);

            return images;
        }

        private MountedImageInfo GetSelectedImage()
        {
            return mountedGrid.SelectedRows.Count == 1
                ? mountedGrid.SelectedRows[0].DataBoundItem as MountedImageInfo
                : null;
        }

        private void OpenFolderButton_Click(object sender, EventArgs e)
        {
            OpenSelectedFolder();
        }

        private void OpenSelectedFolder()
        {
            MountedImageInfo selected = GetSelectedImage();
            if (selected == null || !Directory.Exists(selected.MountDirectory))
            {
                MessageBox.Show(this, "The selected mount directory is not accessible.",
                    "Folder unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "explorer.exe"),
                Arguments = $"\"{selected.MountDirectory}\"",
                UseShellExecute = true
            });
        }

        private void SetBusy(bool busy)
        {
            isBusy = busy;
            refreshButton.Enabled = !busy;
            cleanupButton.Enabled = !busy;
            mountedGrid.Enabled = !busy;
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            bool hasSelection = !isBusy && GetSelectedImage() != null;
            openFolderButton.Enabled = hasSelection;
            remountButton.Enabled = hasSelection;
            commitButton.Enabled = hasSelection;
            discardButton.Enabled = hasSelection;
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
                ? (darkTheme ? Color.IndianRed : Color.Firebrick)
                : (darkTheme ? Color.Gainsboro : Color.Black);
            outputBox.AppendText(message + Environment.NewLine);
            outputBox.ScrollToCaret();
        }

        private void MountedImagesForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isBusy)
                return;

            e.Cancel = true;
            MessageBox.Show(this, "Wait for the current DISM operation to finish before closing this window.",
                "Operation in progress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ApplyTheme(params Control[] containers)
        {
            Color background = darkTheme ? Color.FromArgb(28, 28, 30) : Color.WhiteSmoke;
            Color foreground = darkTheme ? Color.White : Color.Black;
            Color inputBackground = darkTheme ? Color.FromArgb(45, 45, 48) : Color.White;
            Color buttonBackground = darkTheme ? Color.FromArgb(64, 64, 64) : Color.Gainsboro;

            BackColor = background;
            ForeColor = foreground;
            foreach (Control control in containers)
            {
                control.BackColor = background;
                control.ForeColor = foreground;
            }

            foreach (Button button in new[]
            {
                refreshButton,
                openFolderButton,
                remountButton,
                commitButton,
                discardButton,
                cleanupButton
            })
            {
                button.BackColor = buttonBackground;
                button.ForeColor = foreground;
            }

            outputBox.BackColor = darkTheme ? Color.FromArgb(20, 20, 20) : Color.White;
            outputBox.ForeColor = foreground;

            mountedGrid.BackgroundColor = inputBackground;
            mountedGrid.GridColor = darkTheme ? Color.DimGray : Color.LightGray;
            mountedGrid.DefaultCellStyle.BackColor = inputBackground;
            mountedGrid.DefaultCellStyle.ForeColor = foreground;
            mountedGrid.DefaultCellStyle.SelectionBackColor = darkTheme ? Color.Teal : Color.SteelBlue;
            mountedGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            mountedGrid.ColumnHeadersDefaultCellStyle.BackColor = buttonBackground;
            mountedGrid.ColumnHeadersDefaultCellStyle.ForeColor = foreground;
            mountedGrid.EnableHeadersVisualStyles = false;
        }

        private sealed class MountedImageInfo
        {
            public string MountDirectory { get; set; } = string.Empty;
            public string ImageFile { get; set; } = string.Empty;
            public int ImageIndex { get; set; }
            public string ReadWrite { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }
    }
}
