using System;
using System.IO;
using System.Windows.Forms;

namespace DismToolGui
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // ✅ Load config file (this happens via static constructor in SettingsManager)

                // 🔐 Show license form if not accepted
                if (!SettingsManager.GetBool("licenseAccepted"))
                {
                    using var licenseForm = new LicenseForm();
                    var result = licenseForm.ShowDialog();

                    // Only allow launch if license was accepted
                    if (!licenseForm.Accepted || result != DialogResult.OK)
                    {
                        MessageBox.Show("You must accept the license to use this tool.",
                            "License Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }



                // ✅ Run the main form
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "startup-error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));

                File.WriteAllText(logPath,
                    $"[ERROR - {DateTime.Now}]\n{ex.Message}\n\n{ex.StackTrace}");

                MessageBox.Show($"A startup error occurred.\nLog saved to:\n{logPath}",
                                "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
