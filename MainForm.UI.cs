using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DismToolGui
{
    public partial class MainForm : Form
    {
        private const string Version = "1.8.1-stable";
        private readonly string dismPath = Path.Combine(Environment.SystemDirectory, "dism.exe");
        private readonly string sfcPath = Path.Combine(Environment.SystemDirectory, "sfc.exe");
        private readonly string powershellPath = Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");

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
        private Button themeToggleButton;
        private Button openCbsLogButton;

        private MenuStrip menuStrip;
        private ToolStripMenuItem helpMenuItem;
        private ToolStripMenuItem exportLogMenuItem;
        private ToolStripMenuItem releaseNotesMenuItem;
        private ToolStripMenuItem toolsMenuItem;
        private ToolStripMenuItem imageServicingMenuItem;
        private ToolStripMenuItem componentToolkitMenuItem;
        private ToolStripMenuItem logsMenuItem;
        private ToolStripMenuItem advancedToolsMenuItem;
        private ToolStripMenuItem imageInspectorMenuItem;
        private ToolStripMenuItem mountedImagesMenuItem;
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
        private bool isExecuting = false;
        private bool isDark = true;
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
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(10, 10, 10, 10)
            };

            topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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
                "Add Package (CAB)",
                "Get Installed Packages",
                "Remove Package",
                "Export WIM",
                "MSU Expander Tool",
                "SFC - Scannow",
                "SFC - VerifyOnly"
            });
            commandSelector.SelectedIndexChanged += CommandSelector_SelectedIndexChanged;

            runButton = new Button
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
            runButton.MouseEnter += (s, e) => runButton.BackColor = isDark ? Color.DarkCyan : Color.Silver;
            runButton.MouseLeave += (s, e) => runButton.BackColor = isDark ? Color.Teal : Color.LightGray;

            themeToggleButton = new Button
            {
                Text = "Light mode",
                AutoSize = true,
                MinimumSize = new Size(120, 36),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 0, 10, 0),
                Anchor = AnchorStyles.None
            };
            themeToggleButton.FlatAppearance.BorderSize = 0;
            themeToggleButton.Click += (s, e) =>
            {
                isDark = !isDark;
                ApplyTheme(isDark);
                themeToggleButton.Text = isDark ? "Light mode" : "Dark mode";
            };

            openCbsLogButton = new Button
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
            topBarLayout.Controls.Add(themeToggleButton, 2, 0);
            topBarLayout.Controls.Add(openCbsLogButton, 3, 0);

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
                { "CAB File Path", AddLabeledField("CAB File Path:") },
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
                isDark,
                () => GetFieldText("WIM File Path"),
                ApplyImageSelection);
            toolWorkspace.Visible = false;
            toolWorkspace.BackRequested += (sender, args) => HideToolWorkspace();
            toolWorkspace.ThemeToggleRequested += (sender, args) =>
            {
                isDark = !isDark;
                ApplyTheme(isDark);
                themeToggleButton.Text = isDark ? "Light mode" : "Dark mode";
            };
            rootLayout.Controls.Add(toolWorkspace, 0, 1);
            rootLayout.SetRowSpan(toolWorkspace, 3);

            versionLabel = new Label
            {
                Text = $"Version {Version}",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 5, 10, 5),
                AutoSize = true
            };
            rootLayout.Controls.Add(versionLabel, 0, 4);

            MainMenuStrip = menuStrip;
            AcceptButton = runButton;
            commandSelector.SelectedIndex = 0;
            ApplyTheme(isDark);
            UpdateCommandPreview();
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
            toolsMenuItem.DropDownItems.Add(componentToolkitMenuItem);
            toolsMenuItem.DropDownItems.Add(logsMenuItem);
            toolsMenuItem.DropDownItems.Add(advancedToolsMenuItem);

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
                using var releaseNotesForm = new ReleaseNotesForm(isDark);
                releaseNotesForm.ShowDialog(this);
            });

            menuStrip.Items.Add(toolsMenuItem);
            menuStrip.Items.Add(exportLogMenuItem);
            menuStrip.Items.Add(releaseNotesMenuItem);
            menuStrip.Items.Add(helpMenuItem);
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

            copyCommandButton = new Button
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

        private void SetFieldVisibility(params string[] fieldsToShow)
        {
            foreach (var pair in inputFields)
            {
                bool visible = Array.Exists(fieldsToShow, field => field == pair.Key);
                pair.Value.Label.Visible = visible;
                pair.Value.TextBox.Visible = visible;
            }
        }

        private void ApplyTheme(bool dark)
        {
            Color background = dark ? Color.FromArgb(28, 28, 30) : Color.White;
            Color panelBackground = dark ? Color.FromArgb(28, 28, 30) : Color.WhiteSmoke;
            Color menuBackground = dark ? Color.FromArgb(35, 35, 35) : Color.Gainsboro;
            Color foreground = dark ? Color.White : Color.Black;
            Color textboxBg = dark ? Color.FromArgb(45, 45, 45) : Color.White;
            Color textboxFg = dark ? Color.Cyan : Color.Black;
            Color outputBg = dark ? Color.FromArgb(20, 20, 20) : Color.White;
            Color outputFg = dark ? Color.LightGreen : Color.Black;

            BackColor = background;
            rootLayout.BackColor = background;
            topBarLayout.BackColor = panelBackground;
            inputPanel.BackColor = panelBackground;
            outputPanel.BackColor = outputBg;

            menuStrip.BackColor = menuBackground;
            menuStrip.ForeColor = foreground;
            menuStrip.Renderer = new ToolStripProfessionalRenderer(
                new ThemedMenuColorTable(dark));
            foreach (ToolStripItem item in menuStrip.Items)
                ApplyMenuItemTheme(item, menuBackground, foreground);

            Color busyForeground = dark
                ? Color.FromArgb(155, 155, 155)
                : Color.DimGray;
            Color controlForeground = isExecuting ? busyForeground : foreground;
            Color inputForeground = isExecuting ? busyForeground : textboxFg;

            commandSelector.BackColor = textboxBg;
            commandSelector.ForeColor = inputForeground;

            commandPreviewGroup.BackColor = panelBackground;
            commandPreviewGroup.ForeColor = controlForeground;
            commandPreviewBox.BackColor = textboxBg;
            commandPreviewBox.ForeColor = inputForeground;
            copyCommandButton.BackColor = dark ? Color.FromArgb(64, 64, 64) : Color.Gainsboro;
            copyCommandButton.ForeColor = controlForeground;
            confirmCommandCheckBox.BackColor = panelBackground;
            confirmCommandCheckBox.ForeColor = controlForeground;
            mountReadOnlyCheckBox.BackColor = panelBackground;
            mountReadOnlyCheckBox.ForeColor = controlForeground;

            runButton.BackColor = dark ? Color.Teal : Color.LightGray;
            runButton.ForeColor = controlForeground;

            themeToggleButton.BackColor = dark ? Color.FromArgb(64, 64, 64) : Color.Gainsboro;
            themeToggleButton.ForeColor = foreground;

            openCbsLogButton.BackColor = dark ? Color.FromArgb(70, 70, 70) : Color.Gainsboro;
            openCbsLogButton.ForeColor = foreground;

            outputBox.BackColor = outputBg;
            outputBox.ForeColor = outputFg;
            versionLabel.ForeColor = Color.Gray;
            RecolorLog(dark);

            imageTypeGroup.BackColor = panelBackground;
            imageTypeGroup.ForeColor = controlForeground;
            radioOnline.BackColor = panelBackground;
            radioOnline.ForeColor = inputForeground;
            radioOffline.BackColor = panelBackground;
            radioOffline.ForeColor = inputForeground;

            unmountModeGroup.BackColor = panelBackground;
            unmountModeGroup.ForeColor = controlForeground;
            radioUnmountDiscard.BackColor = panelBackground;
            radioUnmountDiscard.ForeColor = inputForeground;
            radioUnmountCommit.BackColor = panelBackground;
            radioUnmountCommit.ForeColor = inputForeground;
            radioUnmountAppend.BackColor = panelBackground;
            radioUnmountAppend.ForeColor = inputForeground;

            foreach (var field in inputFields.Values)
            {
                field.Label.BackColor = panelBackground;
                field.Label.ForeColor = controlForeground;
                field.TextBox.BackColor = textboxBg;
                field.TextBox.ForeColor = inputForeground;
            }

            toolWorkspace?.ApplyTheme(dark);
        }

        private Color ResolveLogColor(Color requestedColor, bool dark)
        {
            if (requestedColor == Color.White)
                return dark ? Color.Gainsboro : Color.Black;
            if (requestedColor == Color.Yellow)
                return dark ? Color.Gold : Color.DarkGoldenrod;
            if (requestedColor == Color.LightBlue)
                return dark ? Color.DeepSkyBlue : Color.RoyalBlue;
            if (requestedColor == Color.Green)
                return dark ? Color.LightGreen : Color.DarkGreen;
            if (requestedColor == Color.Red)
                return dark ? Color.IndianRed : Color.Firebrick;
            if (requestedColor == Color.Orange)
                return dark ? Color.Orange : Color.DarkOrange;

            return requestedColor;
        }

        private static void ApplyMenuItemTheme(
            ToolStripItem item,
            Color background,
            Color foreground)
        {
            item.BackColor = background;
            item.ForeColor = foreground;

            if (!(item is ToolStripMenuItem menuItem))
                return;

            menuItem.DropDown.BackColor = background;
            menuItem.DropDown.ForeColor = foreground;
            if (menuItem.DropDown is ToolStripDropDownMenu dropDownMenu)
            {
                dropDownMenu.ShowImageMargin = false;
                dropDownMenu.ShowCheckMargin = false;
            }

            foreach (ToolStripItem child in menuItem.DropDownItems)
                ApplyMenuItemTheme(child, background, foreground);
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

            runButton.Enabled = inputsEnabled;
            commandSelector.Enabled = inputsEnabled;
            SetChoiceControlState(radioOnline, inputsEnabled);
            SetChoiceControlState(radioOffline, inputsEnabled);
            SetChoiceControlState(radioUnmountDiscard, inputsEnabled);
            SetChoiceControlState(radioUnmountCommit, inputsEnabled);
            SetChoiceControlState(radioUnmountAppend, inputsEnabled);
            SetChoiceControlState(mountReadOnlyCheckBox, inputsEnabled);
            SetChoiceControlState(confirmCommandCheckBox, inputsEnabled);
            toolsMenuItem.Enabled = inputsEnabled;

            foreach (var field in inputFields.Values)
            {
                field.TextBox.ReadOnly = executing;
                field.TextBox.TabStop = inputsEnabled;
            }

            runButton.Text = executing ? "Running..." : "Execute";
            UpdateCommandPreview();
            ApplyTheme(isDark);
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

        private sealed class ThemedMenuColorTable : ProfessionalColorTable
        {
            private readonly Color background;
            private readonly Color selectedBackground;
            private readonly Color border;
            private readonly Color separator;

            public ThemedMenuColorTable(bool dark)
            {
                UseSystemColors = false;
                background = dark ? Color.FromArgb(35, 35, 35) : Color.Gainsboro;
                selectedBackground = dark ? Color.FromArgb(64, 64, 64) : Color.Silver;
                border = dark ? Color.FromArgb(82, 82, 82) : Color.DarkGray;
                separator = dark ? Color.FromArgb(95, 95, 95) : Color.DarkGray;
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

        private void RecolorLog(bool dark)
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
                outputBox.SelectionColor = ResolveLogColor(entry.RequestedColor, dark);
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
                selectedCommand == "Add Package (CAB)";
        }
    }
}
