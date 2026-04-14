using System;
using System.IO;
using System.Windows.Forms;

namespace DismToolGui
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                if (!Settings.Default.LicenseAccepted)
                {
                    using var licenseForm = new LicenseForm();
                    var result = licenseForm.ShowDialog();

                    if (!licenseForm.Accepted || result != DialogResult.OK)
                    {
                        MessageBox.Show(
                            "You must accept the license to use this tool.",
                            "License Required",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return;
                    }

                    if (licenseForm.RememberAcceptance)
                    {
                        Settings.Default.LicenseAccepted = true;
                        Settings.Default.Save();
                    }
                }

                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                try
                {
                    string logDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "DismToolGui",
                        "Logs");

                    Directory.CreateDirectory(logDirectory);

                    string logPath = Path.Combine(logDirectory, "startup-error.log");

                    File.WriteAllText(
                        logPath,
                        $"[ERROR - {DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{ex}");

                    MessageBox.Show(
                        $"A startup error occurred.{Environment.NewLine}Log saved to:{Environment.NewLine}{logPath}",
                        "Fatal Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch
                {
                    MessageBox.Show(
                        $"A startup error occurred:{Environment.NewLine}{ex.Message}",
                        "Fatal Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }
    }
}
