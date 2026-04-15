using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;
using HardwareDiagnostics.UI;

namespace HardwareDiagnostics
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

                Logger.Info("Application starting...");

                // 检查管理员权限
                if (!IsRunAsAdministrator())
                {
                    MessageBox.Show(
                        "本程序需要管理员权限才能正常运行。\n请右键点击程序图标，选择'以管理员身份运行'。",
                        "需要管理员权限",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // 尝试重新启动为管理员
                    RestartAsAdministrator();
                    return;
                }

                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                Logger.Fatal($"Application crashed: {ex}");
                MessageBox.Show(
                    $"程序发生严重错误:\n{ex.Message}\n\n详细信息已记录到日志文件。",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static bool IsRunAsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static void RestartAsAdministrator()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = Application.ExecutablePath,
                    Verb = "runas"
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to restart as administrator", ex);
            }
        }
    }
}
