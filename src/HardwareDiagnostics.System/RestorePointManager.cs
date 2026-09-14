using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.System
{
    /// <summary>
    /// 系统还原点管理器 - 用 PowerShell 原生命令实现（零新依赖），
    /// 且界面上原样展示这些命令：用户点一遍按钮，就学会自己开 PowerShell 敲。
    /// 原理一句话：还原点是系统盘的“后悔药”，用卷影复制（VSS）记录系统文件+注册表的快照，
    /// 还原只动系统，不动你的文档照片；还原点本身占磁盘，太老的会被系统自动删。
    /// </summary>
    public class RestorePointManager
    {
        public static string GetPrinciple()
        {
            return @"【还原点是什么？一句话讲透】
还原点是给“系统”拍的快照：系统文件 + 注册表 + 已装程序的状态。
电脑折腾坏了（驱动翻车、更新蓝屏、注册表改崩），一键回到拍照那一刻。
但注意两点：① 它不备份你的个人文件，文档该备份还得备份；
② 还原点存在系统盘里占地方，磁盘太满或太老的点会被 Windows 自动删掉。

【三个动作】
启用保护 ＝ 先给 C 盘打开“拍照功能”开关（很多电脑默认是关的）；
创建还原点 ＝ 手动按一次快门，动手术（删驱动/ResetBase/装功能）前按一次；
系统还原 ＝ 真出事了点它，重启进向导选个点回去（rstrui.exe）。

【占多少地方】
一般占系统盘几个百分点，可在“系统属性→系统保护→配置”里调上限。";
        }

        public static string GetManualCommands()
        {
            return @"# 管理员 PowerShell 里照敲，和本程序按钮等价：
Enable-ComputerRestore -Drive 'C:\'
Checkpoint-Computer -Description 'CPCH_手动还原点' -RestorePointType 'MODIFY_SETTINGS'
Get-ComputerRestorePoint | Select-Object SequenceNumber,Description,CreationTime
rstrui.exe";
        }

        public async Task<DismOperationResult> EnableProtectionAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在为 C 盘启用系统保护...");
            return await RunPowerShellAsync("Enable-ComputerRestore -Drive 'C:\\'", progress);
        }

        public async Task<DismOperationResult> CreateRestorePointAsync(string description, IProgress<string>? progress = null)
        {
            progress?.Report($"正在创建还原点：{description}（可能需要 1-5 分钟）...");
            string safe = description.Replace("'", "");
            return await RunPowerShellAsync($"Checkpoint-Computer -Description '{safe}' -RestorePointType 'MODIFY_SETTINGS'", progress);
        }

        public async Task<DismOperationResult> ListRestorePointsAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在列出已有还原点...");
            return await RunPowerShellAsync("Get-ComputerRestorePoint | Select-Object SequenceNumber,Description,CreationTime,RestorePointType | Format-Table -AutoSize | Out-String -Width 200", progress);
        }

        public void OpenSystemRestore()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rstrui.exe",
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"打开系统还原失败：{ex.Message}");
                throw;
            }
        }

        private static async Task<DismOperationResult> RunPowerShellAsync(string command, IProgress<string>? progress)
        {
            var result = new DismOperationResult();
            var sw = Stopwatch.StartNew();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + command + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using var process = Process.Start(psi);
                if (process == null)
                {
                    result.Error = "无法启动 PowerShell";
                    return result;
                }
                var outSb = new StringBuilder();
                var errSb = new StringBuilder();
                process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) { outSb.AppendLine(e.Data); progress?.Report(e.Data); } };
                process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) { errSb.AppendLine(e.Data); progress?.Report("错误: " + e.Data); } };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
                sw.Stop();
                result.Duration = sw.Elapsed;
                result.ExitCode = process.ExitCode;
                result.Output = outSb.ToString();
                result.Error = errSb.ToString();
                result.Success = process.ExitCode == 0;
                Logger.Info($"RestorePoint PS executed, ExitCode: {result.ExitCode}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Duration = sw.Elapsed;
                result.Error = ex.Message;
                result.Success = false;
                Logger.Error("Error running restore-point powershell", ex);
            }
            return result;
        }
    }
}
