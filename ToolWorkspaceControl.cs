using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DismToolGui
{
    internal enum ToolWorkspacePage
    {
        ImageInspector,
        MountedImages,
        MsuCabExpander,
        ComponentExport,
        WinSxsSearch,
        DriverCollector,
        SfcFix,
        RegistryHives
    }

    internal sealed class ToolWorkspaceControl : UserControl
    {
        private readonly Func<string> initialImagePathProvider;
        private readonly Action<string, int> applyImageSelection;
        private readonly TableLayoutPanel root;
        private readonly Panel pageHost;
        private readonly Label titleLabel;
        private readonly Button backButton;
        private readonly Button clearLogButton;
        private readonly Button exportLogButton;
        private readonly RichTextBox logBox;
        private readonly Dictionary<ToolWorkspacePage, ToolkitPageBase> pages =
            new Dictionary<ToolWorkspacePage, ToolkitPageBase>();
        private readonly List<LogEntry> logEntries = new List<LogEntry>();
        private RegistryHiveControl registryHivePage;
        private SfcFixControl sfcFixPage;
        private Control currentPage;
        private ToolWorkspacePage currentPageKind;
        private AppTheme currentTheme;

        public ToolWorkspaceControl(
            AppTheme theme,
            Func<string> initialImagePathProvider,
            Action<string, int> applyImageSelection)
        {
            currentTheme = theme ?? ThemeCatalog.Default;
            this.initialImagePathProvider = initialImagePathProvider ?? (() => string.Empty);
            this.applyImageSelection = applyImageSelection ?? ((path, index) => { });

            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 9F);

            root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 68F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 32F));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(10, 8, 10, 8),
                Margin = new Padding(0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            titleLabel = new Label
            {
                Text = "Tools",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 12F),
                UseMnemonic = false,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 8, 0)
            };
            backButton = CreateHeaderButton("Back to commands", 135);
            clearLogButton = CreateHeaderButton("Clear log", 90);
            exportLogButton = CreateHeaderButton("Export log", 95);
            clearLogButton.Enabled = false;
            exportLogButton.Enabled = false;
            backButton.Click += (sender, args) =>
            {
                if (CanLeaveCurrentPage())
                    BackRequested?.Invoke(this, EventArgs.Empty);
            };
            clearLogButton.Click += (sender, args) => ClearLog();
            exportLogButton.Click += (sender, args) => ExportLog();

            header.Controls.Add(titleLabel, 0, 0);
            header.Controls.Add(backButton, 1, 0);
            header.Controls.Add(clearLogButton, 2, 0);
            header.Controls.Add(exportLogButton, 3, 0);

            pageHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            var logGroup = new GroupBox
            {
                Text = "Tool Log",
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Margin = new Padding(10, 0, 10, 8)
            };
            logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                WordWrap = false,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };
            logGroup.Controls.Add(logBox);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(pageHost, 0, 1);
            root.Controls.Add(logGroup, 0, 2);
            Controls.Add(root);
            ApplyTheme(currentTheme);
        }

        public ToolWorkspaceControl(
            bool darkTheme,
            Func<string> initialImagePathProvider,
            Action<string, int> applyImageSelection)
            : this(
                darkTheme ? ThemeCatalog.Default : ThemeCatalog.DefaultLight,
                initialImagePathProvider,
                applyImageSelection)
        {
        }

        public event EventHandler BackRequested;

        public void ShowPage(ToolWorkspacePage page)
        {
            if (currentPage != null && page != currentPageKind && !CanLeaveCurrentPage())
                return;

            if (currentPage is Form currentForm)
            {
                currentForm.Hide();
                currentForm.Dispose();
                currentPage = null;
            }
            else if (currentPage != null)
            {
                currentPage.Visible = false;
            }

            currentPageKind = page;
            titleLabel.Text = GetPageTitle(page);
            if (page == ToolWorkspacePage.ImageInspector)
                currentPage = CreateImageInspector();
            else if (page == ToolWorkspacePage.MountedImages)
                currentPage = CreateMountedImagesManager();
            else
                currentPage = GetOrCreateToolkitPage(page);

            if (currentPage.Parent != pageHost)
                pageHost.Controls.Add(currentPage);
            currentPage.Dock = DockStyle.Fill;
            currentPage.Visible = true;
            currentPage.BringToFront();
            AppendLog(ToolkitLogLevel.Info, $"Opened {GetPageTitle(page)}.");
        }

        public bool CanCloseApplication()
        {
            if (!CanLeaveCurrentPage())
                return false;
            return registryHivePage == null || registryHivePage.CanApplicationClose();
        }

        public void ApplyTheme(AppTheme theme)
        {
            currentTheme = theme ?? ThemeCatalog.Default;

            BackColor = currentTheme.Background;
            ForeColor = currentTheme.Foreground;
            root.BackColor = currentTheme.Background;
            pageHost.BackColor = currentTheme.PanelBackground;

            foreach (Button button in new[]
            {
                backButton,
                clearLogButton,
                exportLogButton
            })
            {
                ThemeStyler.ApplyButton(button, currentTheme);
            }

            ApplySimpleTheme(root, currentTheme.PanelBackground, currentTheme.Foreground);
            logBox.BackColor = currentTheme.OutputBackground;
            logBox.ForeColor = currentTheme.OutputForeground;
            foreach (ToolkitPageBase page in pages.Values)
                page.ApplyTheme(currentTheme);
            ApplyEmbeddedPageTheme();
            RecolorLog();
        }

        public void AppendLog(ToolkitLogLevel level, string message)
        {
            if (IsDisposed || Disposing || logBox.IsDisposed)
                return;

            if (logBox.InvokeRequired)
            {
                try
                {
                    logBox.BeginInvoke(new Action(() => AppendLog(level, message)));
                }
                catch (InvalidOperationException)
                {
                }
                return;
            }

            string normalized = (message ?? string.Empty).TrimEnd('\r', '\n');
            if (normalized.Length == 0)
                return;

            string line = $"[{DateTime.Now:HH:mm:ss}] [{GetLevelName(level)}] {normalized}{Environment.NewLine}";
            int start = logBox.TextLength;
            logBox.SelectionStart = start;
            logBox.SelectionLength = 0;
            logBox.SelectionColor = currentTheme.GetLogColor(level);
            logBox.AppendText(line);
            logBox.SelectionColor = logBox.ForeColor;
            logBox.ScrollToCaret();
            logEntries.Add(new LogEntry(start, line.Length, level));
            exportLogButton.Enabled = true;
            clearLogButton.Enabled = true;
        }

        private Control CreateImageInspector(string initialPath = null)
        {
            var inspector = new ImageInspectorForm(
                currentTheme,
                initialPath ?? initialImagePathProvider(),
                true,
                AppendEmbeddedLog);
            inspector.FormBorderStyle = FormBorderStyle.None;
            inspector.TopLevel = false;
            inspector.SelectionAccepted += (sender, args) =>
            {
                applyImageSelection(inspector.SelectedImagePath, inspector.SelectedImageIndex);
                AppendLog(ToolkitLogLevel.Success,
                    $"Selected image index {inspector.SelectedImageIndex} from {inspector.SelectedImagePath}.");
                BackRequested?.Invoke(this, EventArgs.Empty);
            };
            inspector.CloseRequested += (sender, args) => BackRequested?.Invoke(this, EventArgs.Empty);
            pageHost.Controls.Add(inspector);
            inspector.Show();
            return inspector;
        }

        private Control CreateMountedImagesManager()
        {
            var manager = new MountedImagesForm(
                currentTheme,
                true,
                AppendEmbeddedLog,
                true);
            manager.FormBorderStyle = FormBorderStyle.None;
            manager.TopLevel = false;
            pageHost.Controls.Add(manager);
            manager.Show();
            return manager;
        }

        private ToolkitPageBase GetOrCreateToolkitPage(ToolWorkspacePage page)
        {
            if (pages.TryGetValue(page, out ToolkitPageBase existing))
                return existing;

            ToolkitPageBase created;
            switch (page)
            {
                case ToolWorkspacePage.ComponentExport:
                    var componentExport = new ComponentExportControl(AppendLog);
                    componentExport.PackageCreated += OnPackageCreated;
                    created = componentExport;
                    break;
                case ToolWorkspacePage.MsuCabExpander:
                    created = new MsuCabExpansionControl(AppendLog);
                    break;
                case ToolWorkspacePage.WinSxsSearch:
                    created = new WinSxsSearchControl(AppendLog);
                    break;
                case ToolWorkspacePage.DriverCollector:
                    var driverCollector = new DriverCollectorControl(AppendLog);
                    driverCollector.PackageCreated += OnPackageCreated;
                    created = driverCollector;
                    break;
                case ToolWorkspacePage.SfcFix:
                    sfcFixPage = new SfcFixControl(AppendLog);
                    created = sfcFixPage;
                    break;
                case ToolWorkspacePage.RegistryHives:
                    registryHivePage = new RegistryHiveControl(AppendLog);
                    created = registryHivePage;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(page), page, null);
            }

            pages.Add(page, created);
            created.ApplyTheme(currentTheme);
            return created;
        }

        private void OnPackageCreated(object sender, PackageCreatedEventArgs eventArgs)
        {
            if (sfcFixPage == null)
                sfcFixPage = (SfcFixControl)GetOrCreateToolkitPage(ToolWorkspacePage.SfcFix);
            sfcFixPage.SetPackagePath(eventArgs.PackagePath);
            AppendLog(ToolkitLogLevel.Info,
                "The generated package is ready in the integrated SFCFix tool.");
        }

        private void AppendEmbeddedLog(string message, bool isError)
        {
            ToolkitLogLevel level = ToolkitLogLevel.Info;
            string normalized = (message ?? string.Empty).TrimStart();
            if (isError || normalized.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                level = ToolkitLogLevel.Error;
            else if (normalized.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     normalized.StartsWith("Found ", StringComparison.OrdinalIgnoreCase))
                level = ToolkitLogLevel.Success;
            else if (normalized.StartsWith(DismCommandRunner.ExecutablePath,
                         StringComparison.OrdinalIgnoreCase))
                level = ToolkitLogLevel.Command;
            else if (normalized.StartsWith("Inspecting", StringComparison.OrdinalIgnoreCase) ||
                     normalized.StartsWith("Refreshing", StringComparison.OrdinalIgnoreCase))
                level = ToolkitLogLevel.Process;

            AppendLog(level, message);
        }

        private bool CanLeaveCurrentPage()
        {
            if (currentPage is ToolkitPageBase toolkitPage)
                return toolkitPage.CanDeactivate();
            if (currentPage is ImageInspectorForm inspector && inspector.IsBusy)
            {
                MessageBox.Show(this,
                    "Wait for image inspection to finish before leaving this tool.",
                    "Inspection in progress",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
            if (currentPage is MountedImagesForm manager && manager.IsBusy)
            {
                MessageBox.Show(this,
                    "Wait for the current mounted-image operation to finish before leaving this tool.",
                    "Operation in progress",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private void ApplyEmbeddedPageTheme()
        {
            if (currentPage is ImageInspectorForm inspector)
                inspector.ApplyTheme(currentTheme);
            else if (currentPage is MountedImagesForm manager)
                manager.ApplyTheme(currentTheme);
        }

        private void ClearLog()
        {
            logBox.Clear();
            logEntries.Clear();
            exportLogButton.Enabled = false;
            clearLogButton.Enabled = false;
        }

        private void ExportLog()
        {
            if (logBox.TextLength == 0)
                return;

            using var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"DISMToolGUI-Tools-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                File.WriteAllText(dialog.FileName, logBox.Text);
                AppendLog(ToolkitLogLevel.Success, $"Exported tool log to {dialog.FileName}");
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, ex.Message, "Log export failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RecolorLog()
        {
            if (logBox.TextLength == 0)
                return;

            int selectionStart = logBox.SelectionStart;
            int selectionLength = logBox.SelectionLength;
            foreach (LogEntry entry in logEntries)
            {
                if (entry.Start + entry.Length > logBox.TextLength)
                    continue;
                logBox.Select(entry.Start, entry.Length);
                logBox.SelectionColor = currentTheme.GetLogColor(entry.Level);
            }
            logBox.Select(selectionStart, selectionLength);
        }

        private static Button CreateHeaderButton(string text, int width)
        {
            return new ThemedButton
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(width, 32),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 0, 8, 0)
            };
        }

        private static string GetPageTitle(ToolWorkspacePage page)
        {
            switch (page)
            {
                case ToolWorkspacePage.ImageInspector:
                    return "WIM / ESD Image Inspector";
                case ToolWorkspacePage.MountedImages:
                    return "Mounted Image Manager";
                case ToolWorkspacePage.MsuCabExpander:
                    return "MSU / CAB Expander";
                case ToolWorkspacePage.ComponentExport:
                    return "Component Export";
                case ToolWorkspacePage.WinSxsSearch:
                    return "WinSxS File Search";
                case ToolWorkspacePage.DriverCollector:
                    return "Driver File Collector";
                case ToolWorkspacePage.SfcFix:
                    return "SFCFix Package & Run";
                case ToolWorkspacePage.RegistryHives:
                    return "Registry Hive Manager";
                default:
                    return "Tools";
            }
        }

        private static string GetLevelName(ToolkitLogLevel level)
        {
            return level == ToolkitLogLevel.Warning ? "WARN" : level.ToString().ToUpperInvariant();
        }

        private static void ApplySimpleTheme(Control parent, Color background, Color foreground)
        {
            foreach (Control control in parent.Controls)
            {
                if (!(control is TextBoxBase) && !(control is Button))
                {
                    control.BackColor = background;
                    control.ForeColor = foreground;
                }
                if (control.HasChildren)
                    ApplySimpleTheme(control, background, foreground);
            }
        }

        private sealed class LogEntry
        {
            public LogEntry(int start, int length, ToolkitLogLevel level)
            {
                Start = start;
                Length = length;
                Level = level;
            }

            public int Start { get; }
            public int Length { get; }
            public ToolkitLogLevel Level { get; }
        }
    }
}
