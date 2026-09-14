using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.System
{
    /// <summary>
    /// SFC（系统文件检查器）管理器 - DISM 修蓝图之后的“验墙”步骤。
    /// 教学定位：每个操作都展示等价手工命令，用户在本程序点一遍，
    /// 下次就能自己打开 cmd 照着敲 - 授人以渔。
    /// </summary>
    public class SfcManager
    {
        private readonly string _sfcPath;

        public SfcManager()
        {
            _sfcPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sfc.exe");
        }

        public bool IsSfcAvailable() => File.Exists(_sfcPath);

        /// <summary>一句话原理（展示给小白看的）。</summary>
        public static string GetPrinciple()
        {
            return @"【SFC 是什么？一句话讲透】
SFC（System File Checker）是 Windows 自带的“墙面质检员”。
它拿每个系统文件的“指纹”（哈希）去和蓝图仓库（WinSxS 组件存储）里的正版指纹比对，
对不上就用正版覆盖 - 只修系统文件，不碰你的个人文件。

【它和 DISM 的分工】
DISM 修蓝图仓库本身，SFC 按蓝图验墙面。
所以顺序永远是：DISM /RestoreHealth（修仓库）→ sfc /scannow（验墙）。
如果仓库先坏了，SFC 会报“找到了损坏但无法修复” - 这不是 SFC 没用，
是蓝图错了，回头先跑 DISM 再跑一次 SFC 就好。

【两个常用参数】
/verifyonly ＝ 只检查不动手（几分钟，零风险，先跑这个看有没有病）；
/scannow    ＝ 检查并自动修复（10-30 分钟，要管理员权限）。
修完的明细在 C:\Windows\Logs\CBS\CBS.log，贴日志求助时带上它。";
        }

        /// <summary>等价手工命令（“复制命令”按钮用的就是这些）。</summary>
        public static string GetManualCommands()
        {
            return @":: 管理员 cmd 里照敲即可，和本程序按钮做的事一模一样：
sfc /verifyonly
sfc /scannow";
        }

        public async Task<DismOperationResult> VerifyOnlyAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在只读检查系统文件（不修复）...");
            return await ExecuteSfcAsync("/verifyonly", progress);
        }

        public async Task<DismOperationResult> ScanNowAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在扫描并修复系统文件（10-30 分钟，卡住是正常的）...");
            return await ExecuteSfcAsync("/scannow", progress);
        }

        private async Task<DismOperationResult> ExecuteSfcAsync(string arguments, IProgress<string>? progress)
        {
            var result = new DismOperationResult();
            var sw = Stopwatch.StartNew();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _sfcPath,
                    Arguments = arguments,
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
                    result.Error = "无法启动 sfc 进程";
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
                result.Output += Environment.NewLine + InterpretResult(outSb.ToString());
                Logger.Info($"SFC executed: {arguments}, ExitCode: {result.ExitCode}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Duration = sw.Elapsed;
                result.Error = ex.Message;
                result.Success = false;
                Logger.Error("Error executing SFC", ex);
            }
            return result;
        }

        /// <summary>把 SFC 的中文结论翻译成人话，并告诉下一步。</summary>
        public static string InterpretResult(string output)
        {
            if (output.Contains("Windows 资源保护找到了损坏文件并成功修复"))
                return "【解读】坏文件已修好。建议重启后再观察，顽固问题可再跑一遍。";
            if (output.Contains("未发现完整性冲突") || output.Contains("没有发现完整性冲突"))
                return "【解读】墙面干净，没病，不用再折腾。";
            if (output.Contains("找到了损坏文件但无法修复"))
                return "【解读】蓝图仓库本身坏了，SFC 巧妇难为无米之炊。下一步：先跑 DISM /RestoreHealth，再回来跑 sfc /scannow。";
            if (output.Contains("无法执行请求的操作") || output.Contains("必须以管理员身份"))
                return "【解读】权限不够或系统正在保护模式外。请右键“以管理员身份运行”本程序再试。";
            return "【解读】输出中没有标准结论，详见 C:\\Windows\\Logs\\CBS\\CBS.log。";
        }
    }
}
