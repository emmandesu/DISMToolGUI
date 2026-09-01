using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DismToolGui
{
    internal sealed class IntegrityErrorForm : Form
    {
        internal const string OfficialReleasesUrl =
            "https://github.com/emmandesu/DISMToolGUI/releases";

        public IntegrityErrorForm(string failureReason)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Invalid application signature";
            ClientSize = new Size(610, 245);
            MinimumSize = new Size(520, 225);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            BackColor = Color.White;
            ForeColor = Color.FromArgb(25, 25, 25);
            Font = new Font("Segoe UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(18),
                BackColor = BackColor
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var icon = new PictureBox
            {
                Image = SystemIcons.Error.ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Margin = new Padding(0, 3, 16, 0)
            };
            root.Controls.Add(icon, 0, 0);
            root.SetRowSpan(icon, 3);

            var heading = new Label
            {
                Text = "DISM Tool GUI has an invalid signature and cannot be opened.",
                AutoSize = true,
                MaximumSize = new Size(500, 0),
                Font = new Font("Segoe UI Semibold", 11F),
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(heading, 1, 0);

            var explanation = new Label
            {
                Text = "This copy is unsigned, has been modified, or was signed with an unexpected certificate. " +
                       "Kindly download a verified copy from the official source:",
                AutoSize = true,
                MaximumSize = new Size(500, 0),
                Margin = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(explanation, 1, 1);

            var linkPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0)
            };
            var releaseLink = new LinkLabel
            {
                Text = "Official GitHub Releases page",
                AutoSize = true,
                LinkColor = Color.FromArgb(0, 102, 204),
                ActiveLinkColor = Color.FromArgb(0, 75, 150),
                Margin = new Padding(0, 0, 0, 8)
            };
            releaseLink.LinkClicked += (sender, args) => OpenOfficialSource();
            linkPanel.Controls.Add(releaseLink);

            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                linkPanel.Controls.Add(new Label
                {
                    Text = "Verification details: " + failureReason,
                    AutoSize = true,
                    MaximumSize = new Size(500, 0),
                    ForeColor = Color.DimGray,
                    Font = new Font("Segoe UI", 8F),
                    Margin = new Padding(0)
                });
            }
            root.Controls.Add(linkPanel, 1, 2);

            var closeButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                MinimumSize = new Size(90, 32),
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 12, 0, 0)
            };
            root.Controls.Add(closeButton, 1, 3);

            Controls.Add(root);
            AcceptButton = closeButton;
            CancelButton = closeButton;
        }

        private void OpenOfficialSource()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = OfficialReleasesUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Unable to open the browser. Visit:" + Environment.NewLine +
                    OfficialReleasesUrl + Environment.NewLine + Environment.NewLine + ex.Message,
                    "Official download",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
