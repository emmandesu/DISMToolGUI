using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DismToolGui
{
    internal abstract class ToolkitPageBase : UserControl
    {
        private readonly Action<ToolkitLogLevel, string> logger;
        private CancellationTokenSource operationCancellation;

        protected ToolkitPageBase(Action<ToolkitLogLevel, string> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 9F);
            AutoScroll = true;
        }

        public bool IsBusy { get; private set; }

        public void CancelOperation()
        {
            operationCancellation?.Cancel();
        }

        public virtual bool CanDeactivate()
        {
            if (!IsBusy)
                return true;

            MessageBox.Show(
                this,
                "Cancel or wait for the current operation before leaving this tool.",
                "Operation in progress",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        public virtual void ApplyTheme(AppTheme theme)
        {
            CurrentTheme = theme ?? ThemeCatalog.Default;
            BackColor = CurrentTheme.PanelBackground;
            ForeColor = CurrentTheme.Foreground;
            ThemeStyler.ApplyControlTree(this, CurrentTheme);
        }

        public void ApplyTheme(bool dark)
        {
            ApplyTheme(dark ? ThemeCatalog.Default : ThemeCatalog.DefaultLight);
        }

        protected AppTheme CurrentTheme { get; private set; } = ThemeCatalog.Default;
        protected bool DarkTheme => CurrentTheme.IsDark;

        protected CancellationToken BeginOperation()
        {
            if (IsBusy)
                throw new InvalidOperationException("Another operation is already running in this tool.");

            operationCancellation?.Dispose();
            operationCancellation = new CancellationTokenSource();
            IsBusy = true;
            OnBusyChanged(true);
            return operationCancellation.Token;
        }

        protected void EndOperation()
        {
            IsBusy = false;
            operationCancellation?.Dispose();
            operationCancellation = null;
            OnBusyChanged(false);
        }

        protected virtual void OnBusyChanged(bool busy)
        {
        }

        protected void Log(ToolkitLogLevel level, string message)
        {
            logger(level, message);
        }

        protected static Button CreateButton(string text, int minimumWidth = 100)
        {
            var button = new ThemedButton
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(minimumWidth, 32),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 0, 6, 4)
            };
            button.FlatAppearance.BorderSize = 1;
            return button;
        }

        protected static TextBox CreateTextBox(string text = null)
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Text = text ?? string.Empty,
                Margin = new Padding(0, 3, 8, 3),
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        protected static Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 7, 10, 3)
            };
        }

        protected string BrowseForFolder(string description, string currentPath = null)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = description,
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
                dialog.SelectedPath = currentPath;

            return dialog.ShowDialog(this) == DialogResult.OK
                ? dialog.SelectedPath
                : null;
        }

        protected string BrowseForFile(string title, string filter, string currentPath = null)
        {
            using var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true
            };

            if (!string.IsNullOrWhiteSpace(currentPath) && File.Exists(currentPath))
                dialog.FileName = currentPath;

            return dialog.ShowDialog(this) == DialogResult.OK
                ? dialog.FileName
                : null;
        }

        protected static DataGridView CreateResultsGrid()
        {
            return new DataGridView
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
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        protected static DataGridViewTextBoxColumn CreateColumn(
            string title,
            string property,
            int width,
            bool fill = false)
        {
            return new DataGridViewTextBoxColumn
            {
                HeaderText = title,
                DataPropertyName = property,
                Width = width,
                AutoSizeMode = fill
                    ? DataGridViewAutoSizeColumnMode.Fill
                    : DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
        }

    }
}
