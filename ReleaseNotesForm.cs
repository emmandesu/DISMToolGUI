using System.Drawing;
using System.Windows.Forms;

namespace DismToolGui
{
    public class ReleaseNotesForm : Form
    {
        private const string Notes =
            "Version 1.8.6-stable\r\n\r\n" +
            "• Verified the application Authenticode signature against the embedded official signing certificate before startup\r\n" +
            "• Blocked unsigned, modified, and differently signed application copies with a link to the official GitHub Releases page\r\n" +
            "• Preserved embedded image-tool state and synchronized busy controls when applying themes\r\n" +
            "• Improved light-theme menu, button, radio-button, and checkbox readability\r\n" +
            "• Tracked registry hives that finish loading during cancellation and warned when theme preferences cannot be saved\r\n" +
            "• Parsed SFC console progress sequences and showed percentage updates on the running button without adding progress noise to the log\r\n" +
            "• Moved MSU/CAB expansion from the command selector to Tools > Package Tools\r\n" +
            "• Replaced the separate PowerShell window with an integrated themed expander using the shared log and cancellation controls\r\n" +
            "• Fixed blank expand.exe exit-code failures by using the tracked process runner and displaying real diagnostic output\r\n" +
            "• Added direct MSU/CAB input and bounded recursive nested-CAB extraction into CAB_Extracted\r\n\r\n" +
            "Version 1.8.5-stable\r\n\r\n" +
            "• Replaced the separate Light and Dark buttons with a categorized Themes menu\r\n" +
            "• Added 50 curated palettes spanning accessibility, Fluent, developer, terminal, and color styles\r\n" +
            "• Persisted the selected theme and applied changes immediately across the main UI and integrated Tools\r\n" +
            "• Added contrast-validated colors for controls, menus, grids, dialogs, and color-coded logs\r\n" +
            "• Kept running and disabled control text readable across every included palette\r\n\r\n" +
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
            : this(ThemeCatalog.Default)
        {
        }

        internal ReleaseNotesForm(bool darkTheme)
            : this(darkTheme ? ThemeCatalog.Default : ThemeCatalog.DefaultLight)
        {
        }

        internal ReleaseNotesForm(AppTheme theme)
        {
            theme = theme ?? ThemeCatalog.Default;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Release Notes";
            ClientSize = new Size(640, 480);
            MinimumSize = new Size(480, 360);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);
            ShowIcon = false;
            MinimizeBox = false;

            BackColor = theme.Background;
            ForeColor = theme.Foreground;

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12),
                BackColor = theme.PanelBackground,
                ForeColor = theme.Foreground
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var notesBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = theme.OutputBackground,
                ForeColor = theme.OutputForeground,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false,
                Text = Notes
            };

            var closeButton = new ThemedButton
            {
                Text = "Close",
                AutoSize = true,
                MinimumSize = new Size(100, 32),
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 10, 0, 0),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            ThemeStyler.ApplyButton(closeButton, theme);
            closeButton.Click += (s, e) => Close();

            rootLayout.Controls.Add(notesBox, 0, 0);
            rootLayout.Controls.Add(closeButton, 0, 1);
            Controls.Add(rootLayout);

            AcceptButton = closeButton;
            CancelButton = closeButton;
        }
    }
}
