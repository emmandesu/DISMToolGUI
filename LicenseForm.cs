using System;
using System.Drawing;
using System.Windows.Forms;

namespace DismToolGui
{
    public class LicenseForm : Form
    {
        public bool Accepted { get; private set; }
        public bool RememberAcceptance => dontShowAgainCheckBox.Checked;

        private readonly CheckBox dontShowAgainCheckBox;
        private readonly Button acceptButton;
        private readonly Button declineButton;

        public LicenseForm()
        {
            SuspendLayout();

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "MIT License Agreement";
            ClientSize = new Size(720, 520);
            MinimumSize = new Size(540, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9);
            ShowIcon = false;

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12)
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var licenseTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.White,
                ForeColor = Color.Black,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true,
                Text = GetLicenseText()
            };

            var bottomLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0, 10, 0, 0)
            };
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            dontShowAgainCheckBox = new CheckBox
            {
                Text = "Don't show again",
                AutoSize = true,
                ForeColor = Color.Black,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0)
            };

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            declineButton = new Button
            {
                Text = "Decline",
                Size = new Size(100, 32),
                BackColor = Color.LightCoral,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 0, 10, 0)
            };
            declineButton.FlatAppearance.BorderColor = Color.Firebrick;

            acceptButton = new Button
            {
                Text = "Accept",
                Size = new Size(100, 32),
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0)
            };
            acceptButton.FlatAppearance.BorderColor = Color.SeaGreen;

            acceptButton.Click += (s, e) =>
            {
                Accepted = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            declineButton.Click += (s, e) =>
            {
                Accepted = false;
                DialogResult = DialogResult.Cancel;
                Close();
            };

            buttonPanel.Controls.Add(declineButton);
            buttonPanel.Controls.Add(acceptButton);

            bottomLayout.Controls.Add(dontShowAgainCheckBox, 0, 0);
            bottomLayout.Controls.Add(buttonPanel, 1, 0);
            rootLayout.Controls.Add(licenseTextBox, 0, 0);
            rootLayout.Controls.Add(bottomLayout, 0, 1);
            Controls.Add(rootLayout);

            AcceptButton = acceptButton;
            CancelButton = declineButton;

            ResumeLayout(false);
        }

        private string GetLicenseText()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "MIT License",
                "",
                "Copyright (c) 2025 Emmanuel Flores",
                "",
                "Permission is hereby granted, free of charge, to any person obtaining a copy",
                "of this software and associated documentation files (the \"Software\"), to deal",
                "in the Software without restriction, including without limitation the rights",
                "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell",
                "copies of the Software, and to permit persons to whom the Software is",
                "furnished to do so, subject to the following conditions:",
                "",
                "The above copyright notice and this permission notice shall be included in",
                "all copies or substantial portions of the Software.",
                "",
                "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR",
                "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,",
                "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE",
                "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER",
                "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,",
                "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN",
                "THE SOFTWARE."
            });
        }
    }
}
