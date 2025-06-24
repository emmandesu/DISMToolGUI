// ReleaseNotesForm.cs
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DismToolGui
{
    public class ReleaseNotesForm : Form
    {
        public ReleaseNotesForm()
        {
            this.Text = "Release Notes";
            this.Size = new Size(640, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 10);
            this.BackColor = Color.FromArgb(28, 28, 30);

            var notesBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 10),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReleaseNotes.txt");
            notesBox.Text = File.Exists(path) ? File.ReadAllText(path) : "Release notes file not found.";

            this.Controls.Add(notesBox);
        }
    }
}
