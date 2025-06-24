using System;
using System.IO;
using System.Windows.Forms;

namespace DismToolGui
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            try
            {
                InitializeComponent(); // No license logic here anymore
            }
            catch (Exception ex)
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDir);
                File.WriteAllText(Path.Combine(logDir, "startup-error.log"),
                    $"[ERROR - {DateTime.Now}]\n{ex.Message}\n\n{ex.StackTrace}");

                MessageBox.Show("An error occurred during startup. A log has been saved.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }

    }
}
