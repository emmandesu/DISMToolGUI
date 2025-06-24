// MainForm.UI.cs

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
        private const string Version = "1.5.8-stable";
        private readonly string dismPath = Path.Combine(Environment.SystemDirectory, "dism.exe");

        private ComboBox commandSelector;
        private RichTextBox outputBox;
        private Label versionLabel;
        private TableLayoutPanel inputPanel;
        private Dictionary<string, (Label Label, TextBox TextBox)> inputFields;
        private Button runButton;
        private Button themeToggleButton;
        private Button openCbsLogButton;
        private MenuStrip menuStrip;
        private ToolStripMenuItem helpMenuItem, exportLogMenuItem, releaseNotesMenuItem;

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
            this.Text = "DISM Tool GUI";
            this.Size = new Size(720, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 10);
            this.BackColor = Color.FromArgb(28, 28, 30);

            InitializeMenu();

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(28, 28, 30),
                RowCount = 5,
                ColumnCount = 1
            };

            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            this.Controls.Add(rootLayout);
            rootLayout.Controls.Add(menuStrip, 0, 0);

            var topPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(28, 28, 30),
                Padding = new Padding(10)
            };

            commandSelector = new ComboBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.Cyan,
                Width = 300,
                Location = new Point(10, 10)
            };
            commandSelector.Items.AddRange(new object[] {
                "Run RestoreHealth", "Mount WIM", "Unmount WIM", "Add Package (CAB)",
                "Get Installed Packages", "Remove Package", "Mount and Export", "Extract MSU/CAB",
                "SFC - Scannow", "SFC - VerifyOnly"
            });
            commandSelector.SelectedIndexChanged += CommandSelector_SelectedIndexChanged;
            topPanel.Controls.Add(commandSelector);

            runButton = new Button
            {
                Text = "\u25B6 Execute",
                Location = new Point(320, 10),
                Width = 120,
                Height = 36,
                BackColor = Color.Teal,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10)
            };
            runButton.FlatAppearance.BorderSize = 0;
            runButton.MouseEnter += (s, e) => runButton.BackColor = Color.DarkCyan;
            runButton.MouseLeave += (s, e) => runButton.BackColor = Color.Teal;
            runButton.Click += RunButton_Click;
            topPanel.Controls.Add(runButton);

            themeToggleButton = new Button
            {
                Text = "\ud83c\udf19 Light Mode",
                Location = new Point(460, 10),
                Width = 130,
                Height = 36,
                BackColor = Color.FromArgb(64, 64, 64),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            themeToggleButton.FlatAppearance.BorderSize = 0;
            themeToggleButton.Click += (s, e) =>
            {
                isDark = !isDark;
                ApplyTheme(isDark);
                themeToggleButton.Text = isDark ? "\ud83c\udf19 Light Mode" : "\ud83c\udf11 Dark Mode";
            };
            topPanel.Controls.Add(themeToggleButton);

            openCbsLogButton = new Button
            {
                Text = "📄 Open CBS.log",
                Location = new Point(600, 10),
                Width = 130,
                Height = 36,
                BackColor = Color.FromArgb(70, 70, 70),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Visible = false
            };
            openCbsLogButton.FlatAppearance.BorderSize = 0;
            openCbsLogButton.Click += (s, e) =>
            {
                string path = @"C:\Windows\Logs\CBS\CBS.log";
                if (File.Exists(path)) Process.Start("notepad.exe", path);
                else MessageBox.Show("CBS.log not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            topPanel.Controls.Add(openCbsLogButton);

            rootLayout.Controls.Add(topPanel, 0, 1);

            inputPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(20, 10, 20, 10),
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            rootLayout.Controls.Add(inputPanel, 0, 2);

            inputFields = new Dictionary<string, (Label, TextBox)>
            {
                { "WIM File Path", AddLabeledField("WIM File Path:") },
                { "Index", AddLabeledField("Index:") },
                { "Mount Folder", AddLabeledField("Mount Folder:") },
                { "Source Path", AddLabeledField("Source Path:") },
                { "CAB File Path", AddLabeledField("CAB File Path:") },
                { "Package Name to Remove", AddLabeledField("Package Name to Remove:") },
                { "Component Name", AddLabeledField("Component Name:") }
            };

            radioOffline = new RadioButton();
            radioOffline.CheckedChanged += (s, e) =>
            {
                if (commandSelector.SelectedItem?.ToString() == "Add Package (CAB)")
                {
                    SetFieldVisibility("CAB File Path", "Mount Folder");
                }
            };

            // Image Type GroupBox
            imageTypeGroup = new GroupBox
            {
                Text = "Image Type",
                ForeColor = Color.White,
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
                ForeColor = Color.Cyan,
                AutoSize = true,
                Location = new Point(10, 20)
            };

            radioOffline.Text = "Offline (use Mount Folder)";
            radioOffline.ForeColor = Color.Cyan;
            radioOffline.AutoSize = true;
            radioOffline.Location = new Point(10, 45);

            imageTypeGroup.Controls.Add(radioOnline);
            imageTypeGroup.Controls.Add(radioOffline);
            inputPanel.Controls.Add(imageTypeGroup);
            inputPanel.SetColumnSpan(imageTypeGroup, 2);

            // Unmount Mode GroupBox
            unmountModeGroup = new GroupBox
            {
                Text = "Unmount Mode",
                ForeColor = Color.White,
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
                ForeColor = Color.Cyan,
                AutoSize = true,
                Location = new Point(10, 20)
            };

            radioUnmountCommit = new RadioButton
            {
                Text = "Commit changes",
                ForeColor = Color.Cyan,
                AutoSize = true,
                Location = new Point(10, 45)
            };

            radioUnmountAppend = new RadioButton
            {
                Text = "Append changes",
                ForeColor = Color.Cyan,
                AutoSize = true,
                Location = new Point(10, 70)
            };

            unmountModeGroup.Controls.Add(radioUnmountDiscard);
            unmountModeGroup.Controls.Add(radioUnmountCommit);
            unmountModeGroup.Controls.Add(radioUnmountAppend);
            inputPanel.Controls.Add(unmountModeGroup);
            inputPanel.SetColumnSpan(unmountModeGroup, 2);

            var outputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(20, 20, 20)
            };

            outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.LightGreen,
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
                Padding = new Padding(0, 5, 10, 5),
                ForeColor = Color.Gray
            };
            rootLayout.Controls.Add(versionLabel, 0, 4);

            ApplyTheme(isDark);
        }

        private void InitializeMenu()
        {
            menuStrip = new MenuStrip
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White
            };

            helpMenuItem = new ToolStripMenuItem("About", null, (s, e) =>
            {
                MessageBox.Show("DISM Tool GUI\nBuilt for convenience and speed\n© 2025 - Emmanuel Flores", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

            exportLogMenuItem = new ToolStripMenuItem("Export Log", null, (s, e) =>
            {
                using var sfd = new SaveFileDialog { Filter = "Text Files|*.txt", FileName = "DismLog.txt" };
                if (sfd.ShowDialog() == DialogResult.OK)
                    File.WriteAllText(sfd.FileName, logContent);
            });

            releaseNotesMenuItem = new ToolStripMenuItem("Release Notes", null, (s, e) =>
            {
                using var notesForm = new ReleaseNotesForm();
                notesForm.ShowDialog();
            });

            menuStrip.Items.Add(helpMenuItem);
            menuStrip.Items.Add(exportLogMenuItem);
            menuStrip.Items.Add(releaseNotesMenuItem);
        }

        private (Label, TextBox) AddLabeledField(string labelText)
        {
            Label label = new Label
            {
                Text = labelText,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = Color.LightGray,
                Padding = new Padding(0, 5, 0, 5),
                Visible = false
            };
            TextBox textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.Cyan,
                BorderStyle = BorderStyle.FixedSingle
            };

            inputPanel.Controls.Add(label);
            inputPanel.Controls.Add(textBox);

            return (label, textBox);
        }

        private void SetFieldVisibility(params string[] fieldsToShow)
        {
            foreach (var pair in inputFields)
            {
                bool visible = Array.Exists(fieldsToShow, f => f == pair.Key);
                pair.Value.Label.Visible = visible;
                pair.Value.TextBox.Visible = visible;
            }
        }

        private void ApplyTheme(bool dark)
        {
            Color background = dark ? Color.FromArgb(28, 28, 30) : Color.White;
            Color foreground = dark ? Color.White : Color.Black;
            Color textboxBg = dark ? Color.FromArgb(45, 45, 45) : Color.White;
            Color textboxFg = dark ? Color.Cyan : Color.Black;

            this.BackColor = background;
            commandSelector.BackColor = textboxBg;
            commandSelector.ForeColor = textboxFg;
            runButton.BackColor = dark ? Color.Teal : Color.LightGray;
            runButton.ForeColor = foreground;
            outputBox.BackColor = dark ? Color.FromArgb(20, 20, 20) : Color.White;
            outputBox.ForeColor = dark ? Color.LightGreen : Color.Black;
            versionLabel.ForeColor = Color.Gray;

            imageTypeGroup.ForeColor = foreground;
            radioOnline.ForeColor = textboxFg;
            radioOffline.ForeColor = textboxFg;

            unmountModeGroup.ForeColor = foreground;
            radioUnmountDiscard.ForeColor = textboxFg;
            radioUnmountCommit.ForeColor = textboxFg;
            radioUnmountAppend.ForeColor = textboxFg;

            foreach (var field in inputFields)
            {
                field.Value.Label.ForeColor = foreground;
                field.Value.TextBox.BackColor = textboxBg;
                field.Value.TextBox.ForeColor = textboxFg;
            }
        }

        private string GetFieldText(string key) => inputFields[key].TextBox.Text.Trim();

        private void ToggleCbsLogButtonVisibility(string selectedCommand)
        {
            openCbsLogButton.Visible = selectedCommand is "Run RestoreHealth" or "Remove Package" or "Add Package (CAB)";
        }
    }
}
