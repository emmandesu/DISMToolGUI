using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DismToolGui
{
    internal sealed class WinSxsSearchControl : ToolkitPageBase
    {
        private readonly TextBox fileNameBox;
        private readonly TextBox rootBox;
        private readonly Button browseButton;
        private readonly Button searchButton;
        private readonly Button cancelButton;
        private readonly Button copyComponentButton;
        private readonly Button copyPathButton;
        private readonly DataGridView resultsGrid;
        private readonly Label summaryLabel;

        public WinSxsSearchControl(Action<ToolkitLogLevel, string> logger)
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
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 8)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            fileNameBox = CreateTextBox();
            rootBox = CreateTextBox(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "WinSxS"));
            browseButton = CreateButton("Browse", 88);
            browseButton.Click += (sender, args) =>
            {
                string selected = BrowseForFolder("Select the WinSxS search root", rootBox.Text);
                if (selected != null)
                    rootBox.Text = selected;
            };

            fields.Controls.Add(CreateLabel("File name:"), 0, 0);
            fields.Controls.Add(fileNameBox, 1, 0);
            fields.SetColumnSpan(fileNameBox, 2);
            fields.Controls.Add(CreateLabel("WinSxS path:"), 0, 1);
            fields.Controls.Add(rootBox, 1, 1);
            fields.Controls.Add(browseButton, 2, 1);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            searchButton = CreateButton("Search", 100);
            cancelButton = CreateButton("Cancel", 90);
            copyComponentButton = CreateButton("Copy component name", 160);
            copyPathButton = CreateButton("Copy full path", 125);
            cancelButton.Enabled = false;
            copyComponentButton.Enabled = false;
            copyPathButton.Enabled = false;

            searchButton.Click += async (sender, args) => await SearchAsync();
            cancelButton.Click += (sender, args) => CancelOperation();
            copyComponentButton.Click += (sender, args) => CopySelected(false);
            copyPathButton.Click += (sender, args) => CopySelected(true);
            toolbar.Controls.AddRange(new Control[]
            {
                searchButton,
                cancelButton,
                copyComponentButton,
                copyPathButton
            });

            resultsGrid = CreateResultsGrid();
            resultsGrid.Columns.Add(CreateColumn("Component", "Component", 330));
            resultsGrid.Columns.Add(CreateColumn("File", "FileName", 150));
            resultsGrid.Columns.Add(CreateColumn("Full path", "FullPath", 400, true));
            resultsGrid.SelectionChanged += (sender, args) => UpdateSelectionButtons();
            resultsGrid.CellDoubleClick += (sender, args) =>
            {
                if (args.RowIndex >= 0)
                    CopySelected(true);
            };

            summaryLabel = new Label
            {
                Text = "Enter an exact file name to begin.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };

            root.Controls.Add(fields, 0, 0);
            root.Controls.Add(toolbar, 0, 1);
            root.Controls.Add(resultsGrid, 0, 2);
            root.Controls.Add(summaryLabel, 0, 3);
            Controls.Add(root);
        }

        private async Task SearchAsync()
        {
            string fileName = fileNameBox.Text.Trim();
            string searchRoot = rootBox.Text.Trim();
            CancellationToken token;

            try
            {
                token = BeginOperation();
            }
            catch (InvalidOperationException ex)
            {
                Log(ToolkitLogLevel.Warning, ex.Message);
                return;
            }

            resultsGrid.DataSource = null;
            summaryLabel.Text = "Searching...";
            Log(ToolkitLogLevel.Process, $"Searching '{searchRoot}' for the exact file name '{fileName}'.");

            try
            {
                List<ToolkitSearchResult> results = await Task.Run(
                    () => ToolkitFileOperations.SearchFiles(searchRoot, fileName, token),
                    token);

                resultsGrid.DataSource = results;
                resultsGrid.ClearSelection();
                summaryLabel.Text = $"{results.Count} matching file(s) found.";
                Log(results.Count == 0 ? ToolkitLogLevel.Warning : ToolkitLogLevel.Success,
                    summaryLabel.Text);
            }
            catch (OperationCanceledException)
            {
                summaryLabel.Text = "Search cancelled.";
                Log(ToolkitLogLevel.Warning, "WinSxS search cancelled.");
            }
            catch (Exception ex)
            {
                summaryLabel.Text = "Search failed.";
                Log(ToolkitLogLevel.Error, ex.Message);
                MessageBox.Show(this, ex.Message, "WinSxS search failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private void CopySelected(bool fullPath)
        {
            if (resultsGrid.SelectedRows.Count != 1 ||
                !(resultsGrid.SelectedRows[0].DataBoundItem is ToolkitSearchResult selected))
                return;

            string value = fullPath ? selected.FullPath : selected.Component;
            if (string.IsNullOrWhiteSpace(value))
                return;

            try
            {
                Clipboard.SetText(value);
                Log(ToolkitLogLevel.Info,
                    fullPath ? "Copied the full file path." : "Copied the component name.");
            }
            catch (ExternalException)
            {
                MessageBox.Show(this, "The clipboard is temporarily unavailable.",
                    "Copy failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void OnBusyChanged(bool busy)
        {
            fileNameBox.Enabled = !busy;
            rootBox.Enabled = !busy;
            browseButton.Enabled = !busy;
            searchButton.Enabled = !busy;
            cancelButton.Enabled = busy;
            resultsGrid.Enabled = !busy;
            UpdateSelectionButtons();
        }

        private void UpdateSelectionButtons()
        {
            bool selected = !IsBusy && resultsGrid.SelectedRows.Count == 1;
            copyComponentButton.Enabled = selected;
            copyPathButton.Enabled = selected;
        }
    }
}
