using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HardwareDiagnostics.Core.Models;
using HardwareDiagnostics.Core.Utils;
using Microsoft.Win32;

namespace HardwareDiagnostics.Monitoring
{
    public class BSODDetector : IDisposable
    {
        private readonly string _minidumpPath;
        private readonly string _memoryDumpPath;
        private FileSystemWatcher? _dumpWatcher;
        private Timer? _checkTimer;
        private bool _isMonitoring;

        public event EventHandler<BSODInfo>? BSODDetected;
        public event EventHandler<List<BSODInfo>>? BSODHistoryUpdated;

        public bool IsMonitoring => _isMonitoring;

        public BSODDetector()
        {
            _minidumpPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump");
            _memoryDumpPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "MEMORY.DMP");
        }

        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            try
            {
                // 启用蓝屏报告
                EnableBSODReporting();

                // 创建文件监视器
                if (Directory.Exists(_minidumpPath))
                {
                    _dumpWatcher = new FileSystemWatcher(_minidumpPath, "*.dmp")
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                        EnableRaisingEvents = true
                    };
                    _dumpWatcher.Created += OnDumpFileCreated;
                }

                // 定期检查
                _checkTimer = new Timer(CheckForBSODs, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));

                _isMonitoring = true;
                Logger.Info("BSOD monitoring started");
            }
            catch (Exception ex)
            {
                Logger.Error("Error starting BSOD monitoring", ex);
            }
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            try
            {
                _dumpWatcher?.Dispose();
                _dumpWatcher = null;

                _checkTimer?.Dispose();
                _checkTimer = null;

                _isMonitoring = false;
                Logger.Info("BSOD monitoring stopped");
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping BSOD monitoring", ex);
            }
        }

        public void EnableBSODReporting()
        {
            try
            {
                // 启用小型内存转储
                using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\CrashControl");
                if (key != null)
                {
                    key.SetValue("CrashDumpEnabled", 3, RegistryValueKind.DWord); // 3 = Small memory dump
                    key.SetValue("MinidumpDir", _minidumpPath, RegistryValueKind.String);
                    key.SetValue("LogEvent", 1, RegistryValueKind.DWord);
                    key.SetValue("SendAlert", 1, RegistryValueKind.DWord);
                    key.SetValue("AutoReboot", 1, RegistryValueKind.DWord);

                    // 确保Minidump目录存在
                    Directory.CreateDirectory(_minidumpPath);

                    Logger.Info("BSOD reporting enabled");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error enabling BSOD reporting", ex);
            }
        }

        public List<BSODInfo> GetBSODHistory()
        {
            var bsods = new List<BSODInfo>();

            try
            {
                // 从事件日志获取蓝屏历史
                using var eventLog = new EventLog("System");
                var entries = eventLog.Entries.Cast<EventLogEntry>()
                    .Where(e => e.Source == "Microsoft-Windows-Kernel-Power" ||
                                e.Source == "Microsoft-Windows-Kernel-General" ||
                                e.Source == "EventLog" ||
                                (e.Message != null && e.Message.Contains("bug check")) ||
                                (e.Message != null && e.Message.Contains("0x000000")))
                    .Where(e => e.EntryType == EventLogEntryType.Error)
                    .OrderByDescending(e => e.TimeGenerated)
                    .Take(50);

                foreach (var entry in entries)
                {
                    if (entry.Message?.Contains("0x000000") == true)
                    {
                        var bsod = ParseEventLogEntry(entry);
                        if (bsod != null)
                        {
                            bsods.Add(bsod);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting BSOD history from event log", ex);
            }

            // 从转储文件获取
            try
            {
                if (Directory.Exists(_minidumpPath))
                {
                    var dumpFiles = Directory.GetFiles(_minidumpPath, "*.dmp")
                        .OrderByDescending(f => File.GetCreationTime(f));

                    foreach (var dumpFile in dumpFiles)
                    {
                        var bsod = ParseDumpFile(dumpFile);
                        if (bsod != null && !bsods.Any(b => b.FilePath == dumpFile))
                        {
                            bsods.Add(bsod);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting BSOD history from dump files", ex);
            }

            return bsods.OrderByDescending(b => b.CrashTime).ToList();
        }

        public BSODInfo? AnalyzeDumpFile(string dumpFilePath)
        {
            if (!File.Exists(dumpFilePath))
            {
                return null;
            }

            return ParseDumpFile(dumpFilePath);
        }

        private void OnDumpFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                // 等待文件写入完成
                Thread.Sleep(2000);

                var bsod = ParseDumpFile(e.FullPath);
                if (bsod != null)
                {
                    BSODDetected?.Invoke(this, bsod);
                    Logger.LogBSOD(bsod.BugCheckCode, bsod.CausedByDriver);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error processing new dump file", ex);
            }
        }

        private void CheckForBSODs(object? state)
        {
            try
            {
                var bsods = GetBSODHistory();
                BSODHistoryUpdated?.Invoke(this, bsods);
            }
            catch (Exception ex)
            {
                Logger.Error("Error checking for BSODs", ex);
            }
        }

        private BSODInfo? ParseEventLogEntry(EventLogEntry entry)
        {
            try
            {
                var bsod = new BSODInfo
                {
                    CrashTime = entry.TimeGenerated,
                    FilePath = "Event Log"
                };

                // 提取Bug Check代码
                var bugCheckMatch = Regex.Match(entry.Message ?? "", @"0x[0-9A-Fa-f]{8}");
                if (bugCheckMatch.Success)
                {
                    bsod.BugCheckCode = bugCheckMatch.Value;
                    bsod.BugCheckString = GetBugCheckString(bsod.BugCheckCode);
                }

                // 提取参数
                var paramMatches = Regex.Matches(entry.Message ?? "", @"0x[0-9A-Fa-f]{16}");
                if (paramMatches.Count >= 4)
                {
                    bsod.Parameter1 = paramMatches[0].Value;
                    bsod.Parameter2 = paramMatches[1].Value;
                    bsod.Parameter3 = paramMatches[2].Value;
                    bsod.Parameter4 = paramMatches[3].Value;
                }

                // 生成用户友好解释
                bsod.UserFriendlyExplanation = GenerateUserFriendlyExplanation(bsod);
                bsod.PossibleCauses = GetPossibleCauses(bsod.BugCheckCode);
                bsod.Solutions = GetSolutions(bsod.BugCheckCode);

                return bsod;
            }
            catch (Exception ex)
            {
                Logger.Error("Error parsing event log entry", ex);
                return null;
            }
        }

        private BSODInfo? ParseDumpFile(string dumpFilePath)
        {
            try
            {
                var bsod = new BSODInfo
                {
                    CrashTime = File.GetCreationTime(dumpFilePath),
                    FilePath = dumpFilePath,
                    DumpFileSize = new FileInfo(dumpFilePath).Length.ToString()
                };

                // 尝试从文件名提取信息
                string fileName = Path.GetFileNameWithoutExtension(dumpFilePath);
                if (fileName.StartsWith("Minidump"))
                {
                    // 标准minidump文件名格式
                    var parts = fileName.Split('_');
                    if (parts.Length >= 3)
                    {
                        // 尝试解析日期
                    }
                }

                // 尝试使用cdb或windbg分析（如果可用）
                TryAnalyzeWithDebugger(bsod, dumpFilePath);

                // 如果无法使用调试器，尝试从事件日志获取更多信息
                if (string.IsNullOrEmpty(bsod.BugCheckCode))
                {
                    EnrichFromEventLog(bsod);
                }

                // 生成用户友好解释
                bsod.UserFriendlyExplanation = GenerateUserFriendlyExplanation(bsod);
                bsod.PossibleCauses = GetPossibleCauses(bsod.BugCheckCode);
                bsod.Solutions = GetSolutions(bsod.BugCheckCode);

                return bsod;
            }
            catch (Exception ex)
            {
                Logger.Error("Error parsing dump file", ex);
                return null;
            }
        }

        private void TryAnalyzeWithDebugger(BSODInfo bsod, string dumpFilePath)
        {
            try
            {
                // 检查是否有cdb.exe (Debugging Tools for Windows)
                string[] possiblePaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Kits", "10", "Debuggers", "x64", "cdb.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits", "10", "Debuggers", "x86", "cdb.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Debugging Tools for Windows (x64)", "cdb.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Debugging Tools for Windows (x86)", "cdb.exe")
                };

                string? cdbPath = possiblePaths.FirstOrDefault(File.Exists);
                if (cdbPath == null)
                {
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = cdbPath,
                    Arguments = $"-z \"{dumpFilePath}\" -c \"!analyze -v;q\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(30000); // 最多等待30秒

                    // 解析输出
                    ParseDebuggerOutput(bsod, output);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Could not analyze dump with debugger: {ex.Message}");
            }
        }

        private void ParseDebuggerOutput(BSODInfo bsod, string output)
        {
            try
            {
                // 提取Bug Check代码
                var bugCheckMatch = Regex.Match(output, @"BUGCHECK_CODE:\s*(\w+)");
                if (bugCheckMatch.Success)
                {
                    bsod.BugCheckCode = bugCheckMatch.Groups[1].Value;
                    bsod.BugCheckString = GetBugCheckString(bsod.BugCheckCode);
                }

                // 提取Bug Check参数
                var paramMatches = Regex.Matches(output, @"BUGCHECK_P\d:\s*(\w+)");
                if (paramMatches.Count >= 4)
                {
                    bsod.Parameter1 = paramMatches[0].Groups[1].Value;
                    bsod.Parameter2 = paramMatches[1].Groups[1].Value;
                    bsod.Parameter3 = paramMatches[2].Groups[1].Value;
                    bsod.Parameter4 = paramMatches[3].Groups[1].Value;
                }

                // 提取导致崩溃的驱动
                var driverMatch = Regex.Match(output, @"IMAGE_NAME:\s*(\S+)");
                if (driverMatch.Success)
                {
                    bsod.CausedByDriver = driverMatch.Groups[1].Value;
                }

                // 提取模块信息
                var moduleMatch = Regex.Match(output, @"MODULE_NAME:\s*(\S+)");
                if (moduleMatch.Success)
                {
                    bsod.CausedByDriver = moduleMatch.Groups[1].Value;
                }

                // 提取故障地址
                var addressMatch = Regex.Match(output, @"FAULTING_IP:\s*(\S+)");
                if (addressMatch.Success)
                {
                    bsod.CausedByAddress = addressMatch.Groups[1].Value;
                }
            }
            catch { }
        }

        private void EnrichFromEventLog(BSODInfo bsod)
        {
            try
            {
                using var eventLog = new EventLog("System");
                var entries = eventLog.Entries.Cast<EventLogEntry>()
                    .Where(e => e.TimeGenerated >= bsod.CrashTime.AddMinutes(-5) &&
                                e.TimeGenerated <= bsod.CrashTime.AddMinutes(5) &&
                                e.EntryType == EventLogEntryType.Error)
                    .OrderBy(e => Math.Abs((e.TimeGenerated - bsod.CrashTime).TotalMinutes));

                foreach (var entry in entries)
                {
                    if (entry.Message?.Contains("0x000000") == true)
                    {
                        var bugCheckMatch = Regex.Match(entry.Message, @"0x[0-9A-Fa-f]{8}");
                        if (bugCheckMatch.Success && string.IsNullOrEmpty(bsod.BugCheckCode))
                        {
                            bsod.BugCheckCode = bugCheckMatch.Value;
                            bsod.BugCheckString = GetBugCheckString(bsod.BugCheckCode);
                        }
                    }
                }
            }
            catch { }
        }

        private string GetBugCheckString(string bugCheckCode)
        {
            return bugCheckCode.ToUpper() switch
            {
                "0X0000001A" => "MEMORY_MANAGEMENT",
                "0X0000001E" => "KMODE_EXCEPTION_NOT_HANDLED",
                "0X0000003B" => "SYSTEM_SERVICE_EXCEPTION",
                "0X00000050" => "PAGE_FAULT_IN_NONPAGED_AREA",
                "0X0000007A" => "KERNEL_DATA_INPAGE_ERROR",
                "0X0000007B" => "INACCESSIBLE_BOOT_DEVICE",
                "0X0000007E" => "SYSTEM_THREAD_EXCEPTION_NOT_HANDLED",
                "0X0000007F" => "UNEXPECTED_KERNEL_MODE_TRAP",
                "0X0000009F" => "DRIVER_POWER_STATE_FAILURE",
                "0X000000A5" => "ACPI_BIOS_ERROR",
                "0X000000D1" => "DRIVER_IRQL_NOT_LESS_OR_EQUAL",
                "0X000000EA" => "THREAD_STUCK_IN_DEVICE_DRIVER",
                "0X000000EF" => "CRITICAL_PROCESS_DIED",
                "0X00000109" => "CRITICAL_STRUCTURE_CORRUPTION",
                "0X00000116" => "VIDEO_TDR_FAILURE",
                "0X00000124" => "WHEA_UNCORRECTABLE_ERROR",
                "0X00000133" => "DPC_WATCHDOG_VIOLATION",
                "0X00000139" => "KERNEL_SECURITY_CHECK_FAILURE",
                "0X00000144" => "BUGCODE_USB3_DRIVER",
                "0XC000021A" => "STATUS_SYSTEM_PROCESS_TERMINATED",
                _ => "UNKNOWN_ERROR"
            };
        }

        private string GenerateUserFriendlyExplanation(BSODInfo bsod)
        {
            if (string.IsNullOrEmpty(bsod.BugCheckCode))
            {
                return "无法确定蓝屏的具体原因。建议查看系统事件日志获取更多信息。";
            }

            var explanations = new Dictionary<string, string>
            {
                { "0X0000001A", "内存管理错误 - 系统检测到内存管理方面的问题，可能是物理内存故障或驱动程序错误。" },
                { "0X0000001E", "内核模式异常未处理 - 内核模式程序产生了异常，但错误处理程序没有捕获到它。通常是驱动程序问题。" },
                { "0X0000003B", "系统服务异常 - 系统服务在执行时产生了异常。通常是驱动程序或系统服务错误。" },
                { "0X00000050", "非分页区域页面错误 - 系统引用了无效的内存地址。通常是内存故障或驱动程序错误。" },
                { "0X0000007A", "内核数据页错误 - 系统无法将内核数据分页到内存中。通常是磁盘错误或内存故障。" },
                { "0X0000007B", "无法访问启动设备 - 系统在启动时无法访问启动磁盘。通常是存储控制器驱动问题。" },
                { "0X0000007E", "系统线程异常未处理 - 系统线程产生了异常但未被处理。通常是驱动程序错误。" },
                { "0X0000007F", "意外的内核模式陷阱 - CPU产生了内核无法捕获的陷阱。通常是硬件故障。" },
                { "0X0000009F", "驱动程序电源状态失败 - 驱动程序在电源状态转换时出现问题。" },
                { "0X000000A5", "ACPI BIOS错误 - BIOS的ACPI实现与系统不兼容。" },
                { "0X000000D1", "驱动程序IRQL不小于或等于 - 驱动程序访问了无效的内存地址。" },
                { "0X000000EA", "线程卡在设备驱动程序中 - 通常是显卡驱动程序问题。" },
                { "0X000000EF", "关键进程死亡 - 系统关键进程意外终止。" },
                { "0X00000109", "关键结构损坏 - 驱动程序修改了关键内核结构。" },
                { "0X00000116", "视频TDR失败 - 显卡驱动程序超时未响应。" },
                { "0X00000124", "WHEA不可纠正错误 - 硬件错误，通常是CPU、内存或主板问题。" },
                { "0X00000133", "DPC看门狗违规 - DPC例程执行时间过长。" },
                { "0X00000139", "内核安全检查失败 - 驱动程序或系统文件损坏。" },
                { "0X00000144", "USB3驱动程序错误 - USB 3.0控制器驱动程序问题。" },
                { "0XC000021A", "系统进程终止 - 关键系统进程崩溃。" }
            };

            return explanations.TryGetValue(bsod.BugCheckCode.ToUpper(), out var explanation)
                ? explanation
                : $"未知错误代码 {bsod.BugCheckCode}。建议搜索微软官方文档了解详细信息。";
        }

        private List<string> GetPossibleCauses(string bugCheckCode)
        {
            var causes = new List<string>();

            if (string.IsNullOrEmpty(bugCheckCode))
            {
                causes.Add("未知原因");
                return causes;
            }

            string code = bugCheckCode.ToUpper();

            // 内存相关问题
            if (code is "0X0000001A" or "0X00000050" or "0X0000007A" or "0X00000124")
            {
                causes.Add("内存条故障或接触不良");
                causes.Add("内存超频不稳定");
                causes.Add("内存插槽损坏");
            }

            // 驱动程序相关问题
            if (code is "0X0000001E" or "0X0000003B" or "0X0000007E" or "0X000000D1" or "0X00000116" or "0X00000133")
            {
                causes.Add("过时或损坏的设备驱动程序");
                causes.Add("新安装的驱动程序与系统不兼容");
                causes.Add("驱动程序冲突");
            }

            // 磁盘相关问题
            if (code is "0X0000007A" or "0X0000007B")
            {
                causes.Add("硬盘故障或坏道");
                causes.Add("SATA/IDE数据线松动");
                causes.Add("存储控制器驱动程序问题");
            }

            // 显卡相关问题
            if (code is "0X000000EA" or "0X00000116")
            {
                causes.Add("显卡过热");
                causes.Add("显卡驱动程序问题");
                causes.Add("显卡硬件故障");
                causes.Add("显卡超频不稳定");
            }

            // 系统文件问题
            if (code is "0X00000109" or "0X00000139" or "0XC000021A")
            {
                causes.Add("系统文件损坏");
                causes.Add("恶意软件感染");
                causes.Add("Windows更新问题");
            }

            // 硬件故障
            if (code is "0X0000007F" or "0X00000124")
            {
                causes.Add("CPU故障或过热");
                causes.Add("主板故障");
                causes.Add("电源供电不稳定");
            }

            // 通用原因
            if (causes.Count == 0)
            {
                causes.Add("硬件故障");
                causes.Add("驱动程序问题");
                causes.Add("系统文件损坏");
                causes.Add("软件冲突");
            }

            return causes;
        }

        private List<string> GetSolutions(string bugCheckCode)
        {
            var solutions = new List<string>();

            if (string.IsNullOrEmpty(bugCheckCode))
            {
                solutions.Add("查看系统事件日志获取更多信息");
                solutions.Add("运行系统文件检查器 (sfc /scannow)");
                return solutions;
            }

            string code = bugCheckCode.ToUpper();

            // 内存相关解决方案
            if (code is "0X0000001A" or "0X00000050" or "0X0000007A" or "0X00000124")
            {
                solutions.Add("运行Windows内存诊断工具检查内存");
                solutions.Add("重新插拔内存条，确保接触良好");
                solutions.Add("如果有多根内存条，逐一测试排除故障条");
                solutions.Add("恢复内存默认频率，取消超频");
            }

            // 驱动程序相关解决方案
            if (code is "0X0000001E" or "0X0000003B" or "0X0000007E" or "0X000000D1" or "0X00000116" or "0X00000133")
            {
                solutions.Add("更新所有设备驱动程序到最新版本");
                solutions.Add("如果最近更新了驱动，尝试回滚到之前的版本");
                solutions.Add("卸载最近安装的可疑软件");
                solutions.Add("在安全模式下启动系统，排查问题");
            }

            // 磁盘相关解决方案
            if (code is "0X0000007A" or "0X0000007B")
            {
                solutions.Add("检查硬盘数据线和电源线连接");
                solutions.Add("运行磁盘检查工具 (chkdsk /f /r)");
                solutions.Add("检查硬盘SMART状态");
                solutions.Add("更新主板芯片组驱动程序");
            }

            // 显卡相关解决方案
            if (code is "0X000000EA" or "0X00000116")
            {
                solutions.Add("更新显卡驱动程序");
                solutions.Add("降低显卡超频设置");
                solutions.Add("检查显卡散热，清理灰尘");
                solutions.Add("测试显卡温度，必要时更换散热器");
            }

            // 系统文件问题解决方案
            if (code is "0X00000109" or "0X00000139" or "0XC000021A")
            {
                solutions.Add("运行系统文件检查器 (sfc /scannow)");
                solutions.Add("运行DISM工具修复系统映像");
                solutions.Add("执行病毒和恶意软件扫描");
                solutions.Add("考虑系统还原或重置");
            }

            // 通用解决方案
            solutions.Add("检查系统温度，确保散热良好");
            solutions.Add("更新BIOS到最新版本");
            solutions.Add("检查电源供应是否稳定");
            solutions.Add("如果问题持续，考虑硬件更换");

            return solutions;
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
