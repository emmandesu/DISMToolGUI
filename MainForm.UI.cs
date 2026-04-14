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
        private const string Version = "1.6.1-stable";
        private readonly string dismPath = Path.Combine(Environment.SystemDirectory, "dism.exe");

        private ComboBox commandSelector;
        private RichTextBox outputBox;
        private Label versionLabel;
        private TableLayoutPanel rootLayout;
        private TableLayoutPanel topBarLayout;
        private TableLayoutPanel inputPanel;
        private Panel outputPanel;

        private Dictionary<string, (Label Label, TextBox TextBox)> inputFields;

        private Button runButton;
        private Button themeToggleButton;
        private Button openCbsLogButton;

        private MenuStrip menuStrip;
        private ToolStripMenuItem helpMenuItem;
        private ToolStripMenuItem exportLogMenuItem;
        private ToolStripMenuItem releaseNotesMenuItem;

        private GroupBox imageTypeGroup;
        private RadioButton radioOnline;
        private RadioButton radioOffline;

        private GroupBox unmountModeGroup;
        private RadioButton radioUnmountDiscard;
        private RadioButton radioUnmountCommit;
        private RadioButton radioUnmountAppend;

        private string logContent = string.Empty;
        private bool isExecuting = false;
        private bool isDark = true;

        private void InitializeComponent()
        {
            Text = "DISM Tool GUI";
            Size = new Size(820, 640);
            MinimumSize = new Size(820, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);

            InitializeMenu();

            rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1
            };

            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));

            Controls.Add(rootLayout);
            rootLayout.Controls.Add(menuStrip, 0, 0);

            topBarLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
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
                "Mount and Export",
                "MSU Expander Tool",
                "SFC - Scannow",
                "SFC - VerifyOnly"
            });
            commandSelector.SelectedIndexChanged += CommandSelector_SelectedIndexChanged;

            runButton = new Button
            {
                Text = "▶ Execute",
                Width = 120,
                Height = 36,
                AutoSize = false,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10),
                Margin = new Padding(0, 0, 10, 0)
            };
            runButton.FlatAppearance.BorderSize = 0;
            runButton.Click += RunButton_Click;
            runButton.MouseEnter += (s, e) => runButton.BackColor = isDark ? Color.DarkCyan : Color.Silver;
            runButton.MouseLeave += (s, e) => runButton.BackColor = isDark ? Color.Teal : Color.LightGray;

            themeToggleButton = new Button
            {
                Text = "🌙 Light Mode",
                Width = 130,
                Height = 36,
                AutoSize = false,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 0, 10, 0)
            };
            themeToggleButton.FlatAppearance.BorderSize = 0;
            themeToggleButton.Click += (s, e) =>
            {
                isDark = !isDark;
                ApplyTheme(isDark);
                themeToggleButton.Text = isDark ? "🌙 Light Mode" : "🌑 Dark Mode";
            };

            openCbsLogButton = new Button
            {
                Text = "☐ Open CBS.log",
                Width = 135,
                Height = 36,
                AutoSize = false,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Visible = false,
                Margin = new Padding(0)
            };
            openCbsLogButton.FlatAppearance.BorderSize = 0;
            openCbsLogButton.Click += (s, e) =>
            {
                const string path = @"C:\Windows\Logs\CBS\CBS.log";
                if (File.Exists(path))
                    Process.Start("notepad.exe", path);
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
                Location = new Point(10, 20)
            };

            radioOffline = new RadioButton
            {
                Text = "Offline (use Mount Folder)",
                AutoSize = true,
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
                Location = new Point(10, 20)
            };

            radioUnmountCommit = new RadioButton
            {
                Text = "Commit changes",
                AutoSize = true,
                Location = new Point(10, 45)
            };

            radioUnmountAppend = new RadioButton
            {
                Text = "Append changes",
                AutoSize = true,
                Location = new Point(10, 70)
            };

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

            rootLayout.Controls.Add(inputPanel, 0, 2);

            outputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoScroll = true
            };

            outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9),
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = false
            };

            outputPanel.Controls.Add(outputBox);
            rootLayout.Controls.Add(outputPanel, 0, 3);

            versionLabel = new Label
            {
                Text = $"Version {Version}",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 5, 10, 5)
            };
            rootLayout.Controls.Add(versionLabel, 0, 4);

            ApplyTheme(isDark);
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

            exportLogMenuItem = new ToolStripMenuItem("Export Log", null, (s, e) =>
            {
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "Text Files|*.txt",
                    FileName = "DismLog.txt"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                    File.WriteAllText(saveDialog.FileName, logContent ?? string.Empty);
            });

            releaseNotesMenuItem = new ToolStripMenuItem("Release Notes", null, (s, e) =>
            {
                MessageBox.Show(
                    "Release Notes\n\n" +
                    "• Replaced Extract MSU/CAB with MSU Expander Tool\n" +
                    "• Fixed command result reporting\n" +
                    "• Improved top-bar layout and theme switching\n" +
                    "• Added safer startup logging\n" +
                    "• Cleaned up license persistence\n" +
                    "• Added offline handling for supported DISM commands",
                    "Release Notes",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            });

            menuStrip.Items.Add(helpMenuItem);
            menuStrip.Items.Add(exportLogMenuItem);
            menuStrip.Items.Add(releaseNotesMenuItem);
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

            commandSelector.BackColor = textboxBg;
            commandSelector.ForeColor = textboxFg;

            runButton.BackColor = dark ? Color.Teal : Color.LightGray;
            runButton.ForeColor = foreground;

            themeToggleButton.BackColor = dark ? Color.FromArgb(64, 64, 64) : Color.Gainsboro;
            themeToggleButton.ForeColor = foreground;

            openCbsLogButton.BackColor = dark ? Color.FromArgb(70, 70, 70) : Color.Gainsboro;
            openCbsLogButton.ForeColor = foreground;

            outputBox.BackColor = outputBg;
            outputBox.ForeColor = outputFg;
            versionLabel.ForeColor = Color.Gray;

            imageTypeGroup.BackColor = panelBackground;
            imageTypeGroup.ForeColor = foreground;
            radioOnline.BackColor = panelBackground;
            radioOnline.ForeColor = textboxFg;
            radioOffline.BackColor = panelBackground;
            radioOffline.ForeColor = textboxFg;

            unmountModeGroup.BackColor = panelBackground;
            unmountModeGroup.ForeColor = foreground;
            radioUnmountDiscard.BackColor = panelBackground;
            radioUnmountDiscard.ForeColor = textboxFg;
            radioUnmountCommit.BackColor = panelBackground;
            radioUnmountCommit.ForeColor = textboxFg;
            radioUnmountAppend.BackColor = panelBackground;
            radioUnmountAppend.ForeColor = textboxFg;

            foreach (var field in inputFields.Values)
            {
                field.Label.BackColor = panelBackground;
                field.Label.ForeColor = foreground;
                field.TextBox.BackColor = textboxBg;
                field.TextBox.ForeColor = textboxFg;
            }
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
