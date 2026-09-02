using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DismToolGui
{
    public partial class MainForm : Form
    {
        private static readonly string DisplayVersion = GetDisplayVersion();
        private readonly string dismPath = Path.Combine(Environment.SystemDirectory, "dism.exe");
        private readonly string sfcPath = Path.Combine(Environment.SystemDirectory, "sfc.exe");

        private ComboBox commandSelector;
        private RichTextBox outputBox;
        private Label versionLabel;
        private TableLayoutPanel rootLayout;
        private TableLayoutPanel topBarLayout;
        private TableLayoutPanel inputPanel;
        private Panel outputPanel;
        private GroupBox commandPreviewGroup;
        private TextBox commandPreviewBox;
        private Button copyCommandButton;
        private CheckBox confirmCommandCheckBox;
        private CheckBox mountReadOnlyCheckBox;
        private ToolWorkspaceControl toolWorkspace;

        private Dictionary<string, (Label Label, TextBox TextBox)> inputFields;

        private Button runButton;
        private Button openCbsLogButton;
        private Button addPackageBrowseButton;

        private MenuStrip menuStrip;
        private ToolStripMenuItem helpMenuItem;
        private ToolStripMenuItem exportLogMenuItem;
        private ToolStripMenuItem releaseNotesMenuItem;
        private ToolStripMenuItem toolsMenuItem;
        private ToolStripMenuItem themesMenuItem;
        private ToolStripMenuItem imageServicingMenuItem;
        private ToolStripMenuItem packageToolsMenuItem;
        private ToolStripMenuItem componentToolkitMenuItem;
        private ToolStripMenuItem logsMenuItem;
        private ToolStripMenuItem advancedToolsMenuItem;
        private ToolStripMenuItem imageInspectorMenuItem;
        private ToolStripMenuItem mountedImagesMenuItem;
        private ToolStripMenuItem msuCabExpanderMenuItem;
        private ToolStripMenuItem componentExportMenuItem;
        private ToolStripMenuItem winSxsSearchMenuItem;
        private ToolStripMenuItem driverCollectorMenuItem;
        private ToolStripMenuItem sfcFixMenuItem;
        private ToolStripMenuItem registryHiveMenuItem;

        private GroupBox imageTypeGroup;
        private RadioButton radioOnline;
        private RadioButton radioOffline;

        private GroupBox unmountModeGroup;
        private RadioButton radioUnmountDiscard;
        private RadioButton radioUnmountCommit;
        private RadioButton radioUnmountAppend;

        private string logContent = string.Empty;
        private readonly List<(int Start, int Length, Color RequestedColor)> logEntries =
            new List<(int Start, int Length, Color RequestedColor)>();
        private readonly List<ToolStripMenuItem> themeChoiceMenuItems =
            new List<ToolStripMenuItem>();
        private bool isExecuting = false;
        private AppTheme currentTheme = ThemeCatalog.GetById(
            SettingsManager.Get("ThemeId", ThemeCatalog.Default.Id));
        private Process activeProcess;

        private void InitializeComponent()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "DISM Tool GUI";
            ApplyApplicationIcon();
            ClientSize = new Size(900, 720);
            MinimumSize = new Size(800, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);
            FormClosing += MainForm_FormClosing;

            InitializeMenu();

            rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Controls.Add(rootLayout);
            rootLayout.Controls.Add(menuStrip, 0, 0);

            topBarLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(10, 10, 10, 10)
            };

            topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            commandSelector = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10),
                IntegralHeight = false,
                DropDownHeight = 280,
                Margin = new Padding(0, 0, 10, 0)
            };
            commandSelector.Items.AddRange(new object[]
            {
                "Run RestoreHealth",
                "Mount WIM",
                "Unmount WIM",
                "Add Package (CAB / MSU)",
                "Get Installed Packages",
                "Remove Package",
                "Export WIM",
                "SFC - Scannow",
                "SFC - VerifyOnly"
            });
            commandSelector.SelectedIndexChanged += CommandSelector_SelectedIndexChanged;

            runButton = new ThemedButton
            {
                Text = "Execute",
                AutoSize = true,
                MinimumSize = new Size(110, 36),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10),
                Margin = new Padding(0, 0, 10, 0),
                Anchor = AnchorStyles.None
            };
            runButton.FlatAppearance.BorderSize = 0;
            runButton.Click += RunButton_Click;
            runButton.MouseEnter += (s, e) =>
            {
                if (!isExecuting)
                    runButton.BackColor = currentTheme.AccentHover;
            };
            runButton.MouseLeave += (s, e) =>
                runButton.BackColor = currentTheme.Accent;

            openCbsLogButton = new ThemedButton
            {
                Text = "Open CBS.log",
                AutoSize = true,
                MinimumSize = new Size(125, 36),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Visible = false,
                Margin = new Padding(0),
                Anchor = AnchorStyles.None
            };
            openCbsLogButton.FlatAppearance.BorderSize = 0;
            openCbsLogButton.Click += (s, e) =>
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Logs",
                    "CBS",
                    "CBS.log");
                if (File.Exists(path))
                    Process.Start(Path.Combine(Environment.SystemDirectory, "notepad.exe"), path);
                else
                    MessageBox.Show("CBS.log not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };

            topBarLayout.Controls.Add(commandSelector, 0, 0);
            topBarLayout.Controls.Add(runButton, 1, 0);
            topBarLayout.Controls.Add(openCbsLogButton, 2, 0);

            rootLayout.Controls.Add(topBarLayout, 0, 1);

            inputPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(20, 10, 20, 10)
            };
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));

            imageTypeGroup = new GroupBox
            {
                Text = "Image Type",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Padding = new Padding(10),
                AutoSize = true,
                Dock = DockStyle.Top,
                Visible = false
            };

            radioOnline = new RadioButton
            {
                Text = "Online (default)",
                Checked = true,
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 20)
            };

            radioOffline = new RadioButton
            {
                Text = "Offline (use Mount Folder)",
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 45)
            };

            radioOnline.CheckedChanged += RadioImageType_CheckedChanged;
            radioOffline.CheckedChanged += RadioImageType_CheckedChanged;

            imageTypeGroup.Controls.Add(radioOnline);
            imageTypeGroup.Controls.Add(radioOffline);
            inputPanel.Controls.Add(imageTypeGroup);
            inputPanel.SetColumnSpan(imageTypeGroup, 2);

            unmountModeGroup = new GroupBox
            {
                Text = "Unmount Mode",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Padding = new Padding(10),
                AutoSize = true,
                Dock = DockStyle.Top,
                Visible = false
            };

            radioUnmountDiscard = new RadioButton
            {
                Text = "Discard changes (default)",
                Checked = true,
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 20)
            };

            radioUnmountCommit = new RadioButton
            {
                Text = "Commit changes",
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 45)
            };

            radioUnmountAppend = new RadioButton
            {
                Text = "Append changes",
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 70)
            };

            radioUnmountDiscard.CheckedChanged += (sender, args) => UpdateCommandPreview();
            radioUnmountCommit.CheckedChanged += (sender, args) => UpdateCommandPreview();
            radioUnmountAppend.CheckedChanged += (sender, args) => UpdateCommandPreview();

            unmountModeGroup.Controls.Add(radioUnmountDiscard);
            unmountModeGroup.Controls.Add(radioUnmountCommit);
            unmountModeGroup.Controls.Add(radioUnmountAppend);
            inputPanel.Controls.Add(unmountModeGroup);
            inputPanel.SetColumnSpan(unmountModeGroup, 2);

            inputFields = new Dictionary<string, (Label, TextBox)>
            {
                { "WIM File Path", AddLabeledField("WIM File Path:") },
                { "Index", AddLabeledField("Index:") },
                { "Mount Folder", AddLabeledField("Mount Folder:") },
                { "Source Path", AddLabeledField("Source Path:") },
                { "Package File Path", AddPackageFileField() },
                { "Package Name to Remove", AddLabeledField("Package Name to Remove:") },
                { "Destination Image File", AddLabeledField("Destination Image File:") }
            };

            mountReadOnlyCheckBox = new CheckBox
            {
                Text = "Mount image read-only",
                Checked = true,
                AutoSize = true,
                Visible = false,
                Margin = new Padding(0, 6, 0, 4)
            };
            mountReadOnlyCheckBox.CheckedChanged += (sender, args) => UpdateCommandPreview();
            inputPanel.Controls.Add(mountReadOnlyCheckBox);
            inputPanel.SetColumnSpan(mountReadOnlyCheckBox, 2);

            InitializeCommandPreview();

            rootLayout.Controls.Add(inputPanel, 0, 2);

            outputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoScroll = false
            };

            outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9),
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = false,
                DetectUrls = false
            };

            outputPanel.Controls.Add(outputBox);
            rootLayout.Controls.Add(outputPanel, 0, 3);

            toolWorkspace = new ToolWorkspaceControl(
                currentTheme,
                () => GetFieldText("WIM File Path"),
                ApplyImageSelection);
            toolWorkspace.Visible = false;
            toolWorkspace.BackRequested += (sender, args) => HideToolWorkspace();
            rootLayout.Controls.Add(toolWorkspace, 0, 1);
            rootLayout.SetRowSpan(toolWorkspace, 3);

            versionLabel = new Label
            {
                Text = $"Version {DisplayVersion}",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 5, 10, 5),
                AutoSize = true
            };
            rootLayout.Controls.Add(versionLabel, 0, 4);

            MainMenuStrip = menuStrip;
            AcceptButton = runButton;
            commandSelector.SelectedIndex = 0;
            ApplyTheme(currentTheme);
            UpdateCommandPreview();
        }

        private static string GetDisplayVersion()
        {
            var attribute = typeof(MainForm).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string version = attribute?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(version))
                version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "unknown";

            return version.IndexOf("-", StringComparison.Ordinal) >= 0
                ? version
                : $"{version}-stable";
        }

        private void InitializeMenu()
        {
            menuStrip = new MenuStrip
            {
                Dock = DockStyle.Top
            };

            helpMenuItem = new ToolStripMenuItem("About", null, (s, e) =>
            {
                MessageBox.Show(
                    "DISM Tool GUI\nBuilt for convenience and speed\n© 2025 - Emmanuel Flores",
                    "About",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            });

            imageInspectorMenuItem = new ToolStripMenuItem(
                "WIM / ESD Image Inspector",
                null,
                (s, e) => ShowToolWorkspace(ToolWorkspacePage.ImageInspector));

            mountedImagesMenuItem = new ToolStripMenuItem(
                "Mounted Image Manager",
                null,
                (s, e) => ShowToolWorkspace(ToolWorkspacePage.MountedImages));

            msuCabExpanderMenuItem = new ToolStripMenuItem(
                "MSU / CAB Expander",
                null,
                (s, e) => ShowToolWorkspace(ToolWorkspacePage.MsuCabExpander));

            componentExportMenuItem = new ToolStripMenuItem(
                "Component Export",
                null,
                (s, e) => ShowToolWorkspace(ToolWorkspacePage.ComponentExport));

            winSxsSearchMenuItem = new ToolStripMenuItem(
                "WinSxS File Search",
                null,
                (s, e) => ShowToolWorkspace(ToolWorkspacePage.WinSxsSearch));

            driverCollectorMenuItem = new ToolStripMenuItem(
                "Driver File Collector",
                null,
                (s, e) => ShowToolWorkspace(ToolWorkspacePage.DriverCollector));

            sfcFixMenuItem = new ToolStripMenuItem(
                "SFCFix Package && Run",
                null,
                (s, e) => ShowToolWorkspace(ToolWorkspacePage.SfcFix));

            registryHiveMenuItem = new ToolStripMenuItem(
                "Registry Hive Manager",
                null,
                (s, e) => ShowToolWorkspace(ToolWorkspacePage.RegistryHives));

            imageServicingMenuItem = new ToolStripMenuItem("Image Servicing");
            imageServicingMenuItem.DropDownItems.Add(imageInspectorMenuItem);
            imageServicingMenuItem.DropDownItems.Add(mountedImagesMenuItem);

            packageToolsMenuItem = new ToolStripMenuItem("Package Tools");
            packageToolsMenuItem.DropDownItems.Add(msuCabExpanderMenuItem);

            componentToolkitMenuItem = new ToolStripMenuItem("Component Toolkit");
            componentToolkitMenuItem.DropDownItems.Add(componentExportMenuItem);
            componentToolkitMenuItem.DropDownItems.Add(winSxsSearchMenuItem);
            componentToolkitMenuItem.DropDownItems.Add(driverCollectorMenuItem);
            componentToolkitMenuItem.DropDownItems.Add(new ToolStripSeparator());
            componentToolkitMenuItem.DropDownItems.Add(sfcFixMenuItem);

            logsMenuItem = new ToolStripMenuItem("Logs");
            logsMenuItem.DropDownItems.Add(new ToolStripMenuItem(
                "Open CBS.log", null, (s, e) => OpenSystemLog(SystemLogKind.Cbs)));
            logsMenuItem.DropDownItems.Add(new ToolStripMenuItem(
                "Open DISM.log", null, (s, e) => OpenSystemLog(SystemLogKind.Dism)));
            logsMenuItem.DropDownItems.Add(new ToolStripMenuItem(
                "Open SetupAPI.dev.log", null, (s, e) => OpenSystemLog(SystemLogKind.SetupApi)));

            advancedToolsMenuItem = new ToolStripMenuItem("Advanced");
            advancedToolsMenuItem.DropDownItems.Add(registryHiveMenuItem);

            toolsMenuItem = new ToolStripMenuItem("Tools");
            toolsMenuItem.DropDownItems.Add(imageServicingMenuItem);
            toolsMenuItem.DropDownItems.Add(packageToolsMenuItem);
            toolsMenuItem.DropDownItems.Add(componentToolkitMenuItem);
            toolsMenuItem.DropDownItems.Add(logsMenuItem);
            toolsMenuItem.DropDownItems.Add(advancedToolsMenuItem);

            themesMenuItem = new ToolStripMenuItem("Themes");
            string lastCategory = null;
            ToolStripMenuItem categoryMenu = null;
            foreach (AppTheme theme in ThemeCatalog.All)
            {
                if (!string.Equals(lastCategory, theme.Category, StringComparison.Ordinal))
                {
                    lastCategory = theme.Category;
                    categoryMenu = new ToolStripMenuItem(theme.Category);
                    themesMenuItem.DropDownItems.Add(categoryMenu);
                }

                AppTheme selectedTheme = theme;
                var themeItem = new ToolStripMenuItem(theme.Name)
                {
                    Tag = theme
                };
                themeItem.Click += (sender, args) => SelectTheme(selectedTheme);
                categoryMenu.DropDownItems.Add(themeItem);
                themeChoiceMenuItems.Add(themeItem);
            }
            themesMenuItem.DropDownItems.Add(new ToolStripSeparator());
            themesMenuItem.DropDownItems.Add(new ToolStripMenuItem(
                "Reset to DISM Dark",
                null,
                (sender, args) => SelectTheme(ThemeCatalog.Default)));

            exportLogMenuItem = new ToolStripMenuItem("Export Log", null, (s, e) =>
            {
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "Text Files|*.txt",
                    FileName = "DismLog.txt"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                    File.WriteAllText(saveDialog.FileName, logContent ?? string.Empty);
            })
            {
                Enabled = false
            };

            releaseNotesMenuItem = new ToolStripMenuItem("Release Notes", null, (s, e) =>
            {
                using var releaseNotesForm = new ReleaseNotesForm(currentTheme);
                releaseNotesForm.ShowDialog(this);
            });

            menuStrip.Items.Add(toolsMenuItem);
            menuStrip.Items.Add(themesMenuItem);
            menuStrip.Items.Add(exportLogMenuItem);
            menuStrip.Items.Add(releaseNotesMenuItem);
            menuStrip.Items.Add(helpMenuItem);
            UpdateThemeChecks();
        }

        private void InitializeCommandPreview()
        {
            commandPreviewGroup = new GroupBox
            {
                Text = "Command Preview",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(10),
                Margin = new Padding(0, 8, 0, 0)
            };

            var previewLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0)
            };
            previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            commandPreviewBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 8, 6)
            };

            copyCommandButton = new ThemedButton
            {
                Text = "Copy",
                AutoSize = true,
                MinimumSize = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 6)
            };
            copyCommandButton.Click += (sender, args) => CopyCommandPreview();

            confirmCommandCheckBox = new CheckBox
            {
                Text = "Confirm servicing changes before execution",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0)
            };

            previewLayout.Controls.Add(commandPreviewBox, 0, 0);
            previewLayout.Controls.Add(copyCommandButton, 1, 0);
            previewLayout.Controls.Add(confirmCommandCheckBox, 0, 1);
            previewLayout.SetColumnSpan(confirmCommandCheckBox, 2);
            commandPreviewGroup.Controls.Add(previewLayout);

            inputPanel.Controls.Add(commandPreviewGroup);
            inputPanel.SetColumnSpan(commandPreviewGroup, 2);
        }

        private (Label, TextBox) AddLabeledField(string labelText)
        {
            var label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 0, 5),
                Visible = false
            };

            var textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            textBox.TextChanged += (sender, args) => UpdateCommandPreview();

            inputPanel.Controls.Add(label);
            inputPanel.Controls.Add(textBox);

            return (label, textBox);
        }

        private (Label, TextBox) AddPackageFileField()
        {
            var label = new Label
            {
                Text = "Package File Path:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 0, 5),
                Visible = false
            };

            var textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 8, 0)
            };
            textBox.TextChanged += (sender, args) => UpdateCommandPreview();

            addPackageBrowseButton = new ThemedButton
            {
                Text = "Browse...",
                AutoSize = true,
                MinimumSize = new Size(90, 28),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0)
            };
            addPackageBrowseButton.Click += (sender, args) =>
            {
                if (!isExecuting)
                    BrowseForPackageFile();
            };

            var fieldLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Visible = false
            };
            fieldLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fieldLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fieldLayout.Controls.Add(textBox, 0, 0);
            fieldLayout.Controls.Add(addPackageBrowseButton, 1, 0);

            inputPanel.Controls.Add(label);
            inputPanel.Controls.Add(fieldLayout);
            return (label, textBox);
        }

        private void BrowseForPackageFile()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select a Windows package",
                Filter = "Windows packages (*.cab;*.msu)|*.cab;*.msu|CAB packages (*.cab)|*.cab|MSU packages (*.msu)|*.msu|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            string currentPath = NormalizeFilePathInput(
                GetFieldText("Package File Path"));
            if (File.Exists(currentPath))
                dialog.FileName = currentPath;

            if (dialog.ShowDialog(this) == DialogResult.OK)
                inputFields["Package File Path"].TextBox.Text = dialog.FileName;
        }

        private void SetFieldVisibility(params string[] fieldsToShow)
        {
            foreach (var pair in inputFields)
            {
                bool visible = Array.Exists(fieldsToShow, field => field == pair.Key);
                pair.Value.Label.Visible = visible;
                pair.Value.TextBox.Visible = visible;
                if (pair.Value.TextBox.Parent != inputPanel)
                    pair.Value.TextBox.Parent.Visible = visible;
            }
        }

        private void SelectTheme(AppTheme theme, bool persist = true)
        {
            currentTheme = theme ?? ThemeCatalog.Default;
            string persistenceError = null;
            if (persist)
            {
                try
                {
                    SettingsManager.Set("ThemeId", currentTheme.Id);
                }
                catch (Exception ex) when (
                    ex is IOException ||
                    ex is UnauthorizedAccessException ||
                    ex is System.Security.SecurityException)
                {
                    persistenceError = ex.Message;
                }
            }

            UpdateThemeChecks();
            ApplyTheme(currentTheme);
            if (persistenceError != null)
            {
                MessageBox.Show(
                    this,
                    "The theme was applied for this session, but it could not be saved for the next launch." +
                    Environment.NewLine + Environment.NewLine + persistenceError,
                    "Theme not saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void UpdateThemeChecks()
        {
            foreach (ToolStripMenuItem item in themeChoiceMenuItems)
            {
                var itemTheme = item.Tag as AppTheme;
                item.Checked = itemTheme != null &&
                    string.Equals(itemTheme.Id, currentTheme.Id, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void ApplyTheme(AppTheme theme)
        {
            currentTheme = theme ?? ThemeCatalog.Default;
            Color controlForeground = isExecuting
                ? currentTheme.DisabledForeground
                : currentTheme.Foreground;
            Color inputForeground = isExecuting
                ? currentTheme.DisabledForeground
                : currentTheme.InputForeground;

            BackColor = currentTheme.Background;
            ForeColor = currentTheme.Foreground;
            rootLayout.BackColor = currentTheme.Background;
            topBarLayout.BackColor = currentTheme.PanelBackground;
            inputPanel.BackColor = currentTheme.PanelBackground;
            outputPanel.BackColor = currentTheme.OutputBackground;

            menuStrip.BackColor = currentTheme.MenuBackground;
            menuStrip.ForeColor = currentTheme.Foreground;
            menuStrip.Renderer = new AccessibleMenuRenderer(currentTheme);
            foreach (ToolStripItem item in menuStrip.Items)
                ApplyMenuItemTheme(item, currentTheme);

            commandSelector.BackColor = currentTheme.InputBackground;
            commandSelector.ForeColor = inputForeground;

            commandPreviewGroup.BackColor = currentTheme.PanelBackground;
            commandPreviewGroup.ForeColor = controlForeground;
            commandPreviewBox.BackColor = currentTheme.InputBackground;
            commandPreviewBox.ForeColor = inputForeground;
            ThemeStyler.ApplyButton(copyCommandButton, currentTheme);
            copyCommandButton.ForeColor = isExecuting
                ? currentTheme.DisabledForeground
                : currentTheme.ButtonForeground;
            confirmCommandCheckBox.BackColor = currentTheme.PanelBackground;
            confirmCommandCheckBox.ForeColor = controlForeground;
            mountReadOnlyCheckBox.BackColor = currentTheme.PanelBackground;
            mountReadOnlyCheckBox.ForeColor = controlForeground;

            ThemeStyler.ApplyButton(runButton, currentTheme, true);
            runButton.ForeColor = isExecuting
                ? currentTheme.AccentDisabledForeground
                : currentTheme.AccentForeground;
            ThemeStyler.ApplyButton(openCbsLogButton, currentTheme);
            ThemeStyler.ApplyButton(addPackageBrowseButton, currentTheme);
            addPackageBrowseButton.ForeColor = isExecuting
                ? currentTheme.DisabledForeground
                : currentTheme.ButtonForeground;

            outputBox.BackColor = currentTheme.OutputBackground;
            outputBox.ForeColor = currentTheme.OutputForeground;
            versionLabel.ForeColor = currentTheme.Footer;
            RecolorLog();

            imageTypeGroup.BackColor = currentTheme.PanelBackground;
            imageTypeGroup.ForeColor = controlForeground;
            radioOnline.BackColor = currentTheme.PanelBackground;
            radioOnline.ForeColor = inputForeground;
            radioOffline.BackColor = currentTheme.PanelBackground;
            radioOffline.ForeColor = inputForeground;

            unmountModeGroup.BackColor = currentTheme.PanelBackground;
            unmountModeGroup.ForeColor = controlForeground;
            radioUnmountDiscard.BackColor = currentTheme.PanelBackground;
            radioUnmountDiscard.ForeColor = inputForeground;
            radioUnmountCommit.BackColor = currentTheme.PanelBackground;
            radioUnmountCommit.ForeColor = inputForeground;
            radioUnmountAppend.BackColor = currentTheme.PanelBackground;
            radioUnmountAppend.ForeColor = inputForeground;

            foreach (var field in inputFields.Values)
            {
                field.Label.BackColor = currentTheme.PanelBackground;
                field.Label.ForeColor = controlForeground;
                field.TextBox.BackColor = currentTheme.InputBackground;
                field.TextBox.ForeColor = inputForeground;
            }

            toolWorkspace?.ApplyTheme(currentTheme);
            UpdateThemeChecks();
        }

        private Color ResolveLogColor(Color requestedColor, AppTheme theme)
        {
            if (requestedColor == Color.White)
                return theme.LogInfo;
            if (requestedColor == Color.Yellow)
                return theme.LogWarning;
            if (requestedColor == Color.LightBlue)
                return theme.LogProcess;
            if (requestedColor == Color.Green)
                return theme.LogSuccess;
            if (requestedColor == Color.Red)
                return theme.LogError;
            if (requestedColor == Color.Orange)
                return theme.LogCommand;

            return ThemeContrast.Ensure(theme.OutputBackground, requestedColor, 4.5);
        }

        private static void ApplyMenuItemTheme(ToolStripItem item, AppTheme theme)
        {
            item.BackColor = theme.MenuBackground;
            item.ForeColor = item.Enabled ? theme.Foreground : theme.DisabledForeground;

            if (!(item is ToolStripMenuItem menuItem))
                return;

            menuItem.DropDown.BackColor = theme.MenuBackground;
            menuItem.DropDown.ForeColor = theme.Foreground;
            if (menuItem.DropDown is ToolStripDropDownMenu dropDownMenu)
            {
                dropDownMenu.ShowImageMargin = false;
                dropDownMenu.ShowCheckMargin = ContainsThemeChoices(menuItem);
            }

            foreach (ToolStripItem child in menuItem.DropDownItems)
                ApplyMenuItemTheme(child, theme);
        }

        private static bool ContainsThemeChoices(ToolStripMenuItem menuItem)
        {
            foreach (ToolStripItem child in menuItem.DropDownItems)
            {
                if (child.Tag is AppTheme)
                    return true;
            }
            return false;
        }

        private void ApplyApplicationIcon()
        {
            try
            {
                using (Icon executableIcon = Icon.ExtractAssociatedIcon(
                    typeof(MainForm).Assembly.Location))
                {
                    if (executableIcon != null)
                        Icon = (Icon)executableIcon.Clone();
                }
            }
            catch (ArgumentException)
            {
                // Keep the platform default if the executable icon cannot be read.
            }
        }

        private void SetExecutionUiState(bool executing)
        {
            isExecuting = executing;
            bool inputsEnabled = !executing;

            // Keep the owner-drawn button enabled so its busy text uses the
            // theme's readable disabled color. The click handler rejects a
            // second execution while a command is running.
            runButton.Enabled = true;
            runButton.TabStop = inputsEnabled;
            commandSelector.Enabled = inputsEnabled;
            SetChoiceControlState(radioOnline, inputsEnabled);
            SetChoiceControlState(radioOffline, inputsEnabled);
            SetChoiceControlState(radioUnmountDiscard, inputsEnabled);
            SetChoiceControlState(radioUnmountCommit, inputsEnabled);
            SetChoiceControlState(radioUnmountAppend, inputsEnabled);
            SetChoiceControlState(mountReadOnlyCheckBox, inputsEnabled);
            SetChoiceControlState(confirmCommandCheckBox, inputsEnabled);
            toolsMenuItem.Enabled = inputsEnabled;
            addPackageBrowseButton.Enabled = true;
            addPackageBrowseButton.TabStop = inputsEnabled;

            foreach (var field in inputFields.Values)
            {
                field.TextBox.ReadOnly = executing;
                field.TextBox.TabStop = inputsEnabled;
            }

            runButton.Text = executing ? "Running..." : "Execute";
            UpdateCommandPreview();
            ApplyTheme(currentTheme);
        }

        private static void SetChoiceControlState(
            CheckBox choiceControl,
            bool interactive)
        {
            // Native disabled rendering ignores ForeColor and draws almost-black
            // text on the dark theme. AutoCheck locks the value while allowing
            // the themed gray foreground to remain readable.
            choiceControl.Enabled = true;
            choiceControl.AutoCheck = interactive;
            choiceControl.TabStop = interactive;
        }

        private static void SetChoiceControlState(
            RadioButton choiceControl,
            bool interactive)
        {
            choiceControl.Enabled = true;
            choiceControl.AutoCheck = interactive;
            choiceControl.TabStop = interactive;
        }

        private sealed class AccessibleMenuRenderer : ToolStripProfessionalRenderer
        {
            private readonly AppTheme theme;

            public AccessibleMenuRenderer(AppTheme theme)
                : base(new ThemedMenuColorTable(theme))
            {
                this.theme = theme;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = !e.Item.Enabled
                    ? theme.DisabledForeground
                    : e.Item.Selected
                        ? theme.ButtonForeground
                        : theme.Foreground;
                base.OnRenderItemText(e);
            }
        }

        private sealed class ThemedMenuColorTable : ProfessionalColorTable
        {
            private readonly Color background;
            private readonly Color selectedBackground;
            private readonly Color border;
            private readonly Color separator;

            public ThemedMenuColorTable(AppTheme theme)
            {
                UseSystemColors = false;
                background = theme.MenuBackground;
                selectedBackground = theme.ButtonBackground;
                border = theme.Border;
                separator = theme.Border;
            }

            public override Color ToolStripDropDownBackground => background;
            public override Color ImageMarginGradientBegin => background;
            public override Color ImageMarginGradientMiddle => background;
            public override Color ImageMarginGradientEnd => background;
            public override Color MenuItemSelected => selectedBackground;
            public override Color MenuItemSelectedGradientBegin => selectedBackground;
            public override Color MenuItemSelectedGradientEnd => selectedBackground;
            public override Color MenuItemPressedGradientBegin => selectedBackground;
            public override Color MenuItemPressedGradientMiddle => selectedBackground;
            public override Color MenuItemPressedGradientEnd => selectedBackground;
            public override Color MenuItemBorder => border;
            public override Color MenuBorder => border;
            public override Color SeparatorDark => separator;
            public override Color SeparatorLight => background;
        }

        private void RecolorLog()
        {
            if (outputBox == null || outputBox.TextLength == 0)
                return;

            int selectionStart = outputBox.SelectionStart;
            int selectionLength = outputBox.SelectionLength;

            foreach (var entry in logEntries)
            {
                if (entry.Start + entry.Length > outputBox.TextLength)
                    continue;

                outputBox.Select(entry.Start, entry.Length);
                outputBox.SelectionColor = ResolveLogColor(entry.RequestedColor, currentTheme);
            }

            outputBox.Select(selectionStart, selectionLength);
        }

        private void RadioImageType_CheckedChanged(object sender, EventArgs e)
        {
            if (commandSelector?.SelectedItem != null)
                CommandSelector_SelectedIndexChanged(commandSelector, EventArgs.Empty);
        }

        private string GetFieldText(string key)
        {
            return inputFields.TryGetValue(key, out var field)
                ? field.TextBox.Text.Trim()
                : string.Empty;
        }

        private void ToggleCbsLogButtonVisibility(string selectedCommand)
        {
            openCbsLogButton.Visible =
                selectedCommand == "Run RestoreHealth" ||
                selectedCommand == "Remove Package" ||
                selectedCommand == "Add Package (CAB / MSU)";
        }
    }
}
