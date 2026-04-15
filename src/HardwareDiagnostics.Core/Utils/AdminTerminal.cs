using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HardwareDiagnostics.Core.Utils
{
    /// <summary>
    /// 内置管理员终端 - 以管理员权限运行命令
    /// </summary>
    public class AdminTerminal
    {
        private Process? _terminalProcess;
        private StringBuilder _outputBuffer;
        private bool _isRunning;

        public event EventHandler<TerminalOutputEventArgs>? OutputReceived;
        public event EventHandler? TerminalExited;

        public bool IsRunning => _isRunning;

        public AdminTerminal()
        {
            _outputBuffer = new StringBuilder();
        }

        /// <summary>
        /// 以管理员权限启动 PowerShell 终端
        /// </summary>
        public async Task<bool> StartAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoExit -Command \"[Console]::Title='HardwareDiagnostics Admin Terminal'\"",
                    UseShellExecute = true,
                    Verb = "runas", // 请求管理员权限
                    CreateNoWindow = false
                };

                _terminalProcess = Process.Start(psi);
                
                if (_terminalProcess == null)
                {
                    return false;
                }

                _isRunning = true;
                _outputBuffer.Clear();

                // 异步等待进程退出（不阻塞 UI）
                _ = Task.Run(() =>
                {
                    _terminalProcess.WaitForExit();
                    _isRunning = false;
                    TerminalExited?.Invoke(this, EventArgs.Empty);
                });

                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // 用户拒绝了 UAC 提示
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("启动管理员终端失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 执行单条命令（管理员权限）
        /// </summary>
        public async Task<TerminalCommandResult> ExecuteCommandAsync(string command)
        {
            var result = new TerminalCommandResult();
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{command}\"",
                    UseShellExecute = false,
                    Verb = "runas",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return new TerminalCommandResult
                    {
                        Success = false,
                        ErrorMessage = "无法启动进程"
                    };
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                result.Output = await outputTask;
                result.Error = await errorTask;
                result.ExitCode = process.ExitCode;
                result.Success = process.ExitCode == 0;

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// 向终端发送命令
        /// </summary>
        public void SendCommand(string command)
        {
            if (_terminalProcess != null && !_terminalProcess.HasExited && _terminalProcess.StandardInput != null)
            {
                _terminalProcess.StandardInput.WriteLine(command);
            }
        }

        /// <summary>
        /// 获取所有输出历史
        /// </summary>
        public string GetAllOutput()
        {
            return _outputBuffer.ToString();
        }

        /// <summary>
        /// 清空输出缓冲区
        /// </summary>
        public void ClearOutput()
        {
            _outputBuffer.Clear();
        }

        /// <summary>
        /// 停止终端
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_terminalProcess != null && !_terminalProcess.HasExited)
                {
                    _terminalProcess.Kill();
                    _terminalProcess.Dispose();
                    _terminalProcess = null;
                    _isRunning = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("停止终端失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 检查是否具有管理员权限
        /// </summary>
        public static bool IsRunningAsAdmin()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 以管理员权限重启应用程序
        /// </summary>
        public static void RestartAsAdmin()
        {
            var psi = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(psi);
            Process.GetCurrentProcess().Kill();
        }
    }

    public class TerminalOutputEventArgs : EventArgs
    {
        public string Output { get; set; } = "";
        public bool IsError { get; set; }
    }

    public class TerminalCommandResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public int ExitCode { get; set; }
        public string ErrorMessage { get; set; } = "";
    }
}
