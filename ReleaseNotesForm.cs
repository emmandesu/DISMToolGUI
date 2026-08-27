using System.Drawing;
using System.Windows.Forms;

namespace DismToolGui
{
    public class ReleaseNotesForm : Form
    {
        private const string Notes =
            "Version 1.8.3-stable\r\n\r\n" +
            "• Required Windows Authenticode trust and an exact match to the official Sysnative signer certificate\r\n" +
            "• Blocked unsigned or unexpected SFCFix executables with no run-anyway path\r\n" +
            "• Verified downloads before replacing an existing SFCFix copy and protected the verified file through launch\r\n" +
            "• Corrected WinTrust native interop for reliable signature validation\r\n\r\n" +
            "Version 1.8.2-stable\r\n\r\n" +
            "• Expanded Add Package with CAB/MSU browsing and manual path entry\r\n" +
            "• Added package file validation and support for paths pasted with surrounding quotes\r\n" +
            "• Added browser-compatible SFCFix downloads with guidance when Cloudflare requires interactive verification\r\n" +
            "• Preserved temporary-file, executable, hash, and signature verification safeguards\r\n\r\n" +
            "Version 1.8.1-stable\r\n\r\n" +
            "• Fixed the application icon on the main window and taskbar\r\n" +
            "• Removed the blank image gutter from the Tools dropdown\r\n" +
            "• Improved menu hover, pressed, border, and separator colors in light and dark modes\r\n" +
            "• Fixed native disabled-state rendering that made radio-button and checkbox text unreadable in dark mode\r\n" +
            "• Kept command choices locked during execution and restored normal interaction afterward\r\n\r\n" +
            "Version 1.8.0-stable\r\n\r\n" +
            "• Added a single-window Tools workspace for image, component, driver, SFCFix, and registry operations\r\n" +
            "• Added Component Export, WinSxS search, and Driver File Collector\r\n" +
            "• Added guarded SFCFix download, executable validation, SHA-256 and signature reporting, package generation, and launch confirmation\r\n" +
            "• Added session-owned offline registry hive management and Windows log shortcuts\r\n" +
            "• Added read-only WIM mounting, cancellation for file operations, isolated timestamped exports, and shared categorized logs\r\n" +
            "• Improved responsive light/dark layouts across every integrated tool\r\n\r\n" +
            "Version 1.7.0-stable\r\n\r\n" +
            "• Added WIM / ESD Image Inspector with selectable indexes\r\n" +
            "• Added Mounted Image Manager with remount, commit, discard, and cleanup actions\r\n" +
            "• Added live command preview and copy support\r\n" +
            "• Added confirmation prompts for servicing changes\r\n" +
            "• Preserved responsive layouts and readable light/dark themes across the image tools\r\n\r\n" +
            "Version 1.6.1-stable\r\n\r\n" +
            "• Replaced Extract MSU/CAB with MSU Expander Tool\r\n" +
            "• Fixed command result reporting and offline servicing behavior";

        public ReleaseNotesForm()
            : this(true)
        {
        }

        internal ReleaseNotesForm(bool darkTheme)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Release Notes";
            ClientSize = new Size(640, 480);
            MinimumSize = new Size(480, 360);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);
            ShowIcon = false;
            MinimizeBox = false;

            Color background = darkTheme ? Color.FromArgb(28, 28, 30) : Color.WhiteSmoke;
            Color textBackground = darkTheme ? Color.FromArgb(20, 20, 20) : Color.White;
            Color foreground = darkTheme ? Color.Gainsboro : Color.Black;

            BackColor = background;

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12)
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var notesBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = textBackground,
                ForeColor = foreground,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false,
                Text = Notes
            };

            var closeButton = new Button
            {
                Text = "Close",
                AutoSize = true,
                MinimumSize = new Size(100, 32),
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 10, 0, 0),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = darkTheme ? Color.FromArgb(64, 64, 64) : Color.Gainsboro,
                ForeColor = foreground
            };
            closeButton.FlatAppearance.BorderColor = darkTheme ? Color.Gray : Color.DarkGray;
            closeButton.Click += (s, e) => Close();

            rootLayout.Controls.Add(notesBox, 0, 0);
            rootLayout.Controls.Add(closeButton, 0, 1);
            Controls.Add(rootLayout);

            AcceptButton = closeButton;
            CancelButton = closeButton;
        }
    }
}
