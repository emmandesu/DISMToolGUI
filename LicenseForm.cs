using System;
using System.Drawing;
using System.Windows.Forms;

namespace DismToolGui
{
    public class LicenseForm : Form
    {
        public bool Accepted { get; private set; } = false;

        private CheckBox dontShowAgainCheckBox;
        private Button acceptButton;
        private Button declineButton;

        public LicenseForm()
        {
            Text = "MIT License Agreement";
            Size = new Size(720, 520);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            var licenseTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Top,
                Height = 370,
                Font = new Font("Segoe UI", 9),
                Text = GetLicenseText(),
                BackColor = Color.White,
                ForeColor = Color.Black
            };

            dontShowAgainCheckBox = new CheckBox
            {
                Text = "Don't show again",
                Location = new Point(20, 390),
                AutoSize = true,
                ForeColor = Color.Black
            };

            acceptButton = new Button
            {
                Text = "Accept",
                DialogResult = DialogResult.OK,
                Width = 100,
                Height = 30,
                Location = new Point(580, 420),
                BackColor = Color.LightGreen
            };

            declineButton = new Button
            {
                Text = "Decline",
                DialogResult = DialogResult.Cancel,
                Width = 100,
                Height = 30,
                Location = new Point(470, 420),
                BackColor = Color.LightCoral
            };

            acceptButton.Click += (s, e) =>
            {
                // ✅ Only save if "Don't show again" is checked
                if (dontShowAgainCheckBox.Checked)
                {
                    SettingsManager.SetBool("licenseAccepted", true);
                }
                Accepted = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            declineButton.Click += (s, e) =>
            {
                Accepted = false;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            Controls.Add(licenseTextBox);
            Controls.Add(dontShowAgainCheckBox);
            Controls.Add(acceptButton);
            Controls.Add(declineButton);
        }

        public static bool IsLicenseAccepted()
        {
            return SettingsManager.GetBool("licenseAccepted");
        }

        private string GetLicenseText()
        {
            return @"MIT License

Copyright (c) 2025 Emmanuel Flores

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.";
        }
    }
}
