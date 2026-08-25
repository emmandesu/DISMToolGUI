using System.Drawing;
using System.Windows.Forms;

namespace DismToolGui
{
    public class ReleaseNotesForm : Form
    {
        private const string Notes =
            "Version 1.6.1-stable\r\n\r\n" +
            "• Replaced Extract MSU/CAB with MSU Expander Tool\r\n" +
            "• Fixed command result reporting\r\n" +
            "• Improved responsive layout and theme switching\r\n" +
            "• Added safer startup logging\r\n" +
            "• Cleaned up license persistence\r\n" +
            "• Added offline handling for supported DISM commands";

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
