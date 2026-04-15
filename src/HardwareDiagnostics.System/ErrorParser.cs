using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HardwareDiagnostics.Core.Models;
using HardwareDiagnostics.Core.Utils;

// 为.NET Framework 4.8兼容性添加
using System.Linq.Expressions;

namespace HardwareDiagnostics.System
{
    public class ErrorParser
    {
        private readonly Dictionary<string, string> _errorCodeDatabase;

        public ErrorParser()
        {
            _errorCodeDatabase = InitializeErrorDatabase();
        }

        private Dictionary<string, string> InitializeErrorDatabase()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Windows 系统错误代码
                { "0x80070002", "系统找不到指定的文件。可能原因：文件被删除、移动或路径错误。" },
                { "0x80070005", "访问被拒绝。可能原因：权限不足、文件被占用或安全软件阻止。" },
                { "0x80070057", "参数错误。可能原因：传递给函数的参数不正确或无效。" },
                { "0x80070490", "元素未找到。可能原因：指定的对象不存在或已被删除。" },
                { "0x80070643", "安装期间出现致命错误。可能原因：安装程序损坏、权限不足或依赖项缺失。" },
                { "0x80073712", "组件存储已损坏。可能原因：系统文件损坏，需要运行DISM修复。" },
                { "0x800F081F", "找不到源文件。可能原因：Windows更新源文件缺失或损坏。" },
                { "0x800F0922", "无法完成操作。可能原因：.NET Framework安装失败或系统更新问题。" },
                { "0x80070570", "文件或目录损坏且无法读取。可能原因：磁盘错误或文件系统损坏。" },
                { "0x80070035", "找不到网络路径。可能原因：网络连接问题或共享路径不存在。" },
                { "0x80070422", "无法启动服务。可能原因：相关服务被禁用或依赖服务未运行。" },
                { "0x80070020", "进程无法访问文件。可能原因：文件被其他程序占用。" },
                { "0x80070103", "驱动程序已安装。可能原因：尝试安装已存在的驱动程序版本。" },
                { "0x80072F8F", "安全错误。可能原因：日期时间设置不正确或SSL/TLS问题。" },
                { "0x80072EE2", "连接超时。可能原因：网络连接问题或服务器无响应。" },
                { "0x80070079", "信号灯超时时间已到。可能原因：操作超时或资源争用。" },
                { "0xC0000005", "访问冲突。可能原因：程序尝试访问未分配的内存。" },
                { "0xC00000FD", "堆栈溢出。可能原因：递归调用过深或局部变量过多。" },
                { "0xC0000135", "找不到指定的模块。可能原因：DLL文件缺失或路径错误。" },
                { "0xC0000142", "DLL初始化失败。可能原因：DLL加载失败或依赖项缺失。" },

                // BSOD Bug Check Codes
                { "0x0000001A", "MEMORY_MANAGEMENT - 内存管理错误。可能原因：内存故障、驱动程序问题或系统文件损坏。" },
                { "0x0000001E", "KMODE_EXCEPTION_NOT_HANDLED - 内核模式异常未处理。可能原因：驱动程序错误或硬件故障。" },
                { "0x0000003B", "SYSTEM_SERVICE_EXCEPTION - 系统服务异常。可能原因：驱动程序问题或系统服务错误。" },
                { "0x00000050", "PAGE_FAULT_IN_NONPAGED_AREA - 非分页区域页面错误。可能原因：内存问题或驱动程序错误。" },
                { "0x0000007A", "KERNEL_DATA_INPAGE_ERROR - 内核数据页错误。可能原因：磁盘错误或内存故障。" },
                { "0x0000007B", "INACCESSIBLE_BOOT_DEVICE - 无法访问启动设备。可能原因：存储控制器驱动问题或磁盘故障。" },
                { "0x0000007E", "SYSTEM_THREAD_EXCEPTION_NOT_HANDLED - 系统线程异常未处理。可能原因：驱动程序错误或硬件故障。" },
                { "0x0000007F", "UNEXPECTED_KERNEL_MODE_TRAP - 意外的内核模式陷阱。可能原因：硬件故障或驱动程序问题。" },
                { "0x0000009F", "DRIVER_POWER_STATE_FAILURE - 驱动程序电源状态失败。可能原因：驱动程序电源管理问题。" },
                { "0x000000A5", "ACPI_BIOS_ERROR - ACPI BIOS错误。可能原因：BIOS设置问题或ACPI不兼容。" },
                { "0x000000D1", "DRIVER_IRQL_NOT_LESS_OR_EQUAL - 驱动程序IRQL不小于或等于。可能原因：驱动程序访问无效内存地址。" },
                { "0x000000EA", "THREAD_STUCK_IN_DEVICE_DRIVER - 线程卡在设备驱动程序中。可能原因：显卡驱动程序问题。" },
                { "0x000000EF", "CRITICAL_PROCESS_DIED - 关键进程死亡。可能原因：系统关键进程意外终止。" },
                { "0x00000109", "CRITICAL_STRUCTURE_CORRUPTION - 关键结构损坏。可能原因：驱动程序修改了关键内核结构。" },
                { "0x00000116", "VIDEO_TDR_FAILURE - 视频TDR失败。可能原因：显卡驱动程序超时或显卡故障。" },
                { "0x00000124", "WHEA_UNCORRECTABLE_ERROR - WHEA不可纠正错误。可能原因：硬件故障，通常是CPU、内存或主板问题。" },
                { "0x00000133", "DPC_WATCHDOG_VIOLATION - DPC看门狗违规。可能原因：DPC例程执行时间过长，通常是驱动程序问题。" },
                { "0x00000139", "KERNEL_SECURITY_CHECK_FAILURE - 内核安全检查失败。可能原因：驱动程序或系统文件损坏。" },
                { "0x00000144", "BUGCODE_USB3_DRIVER - USB3驱动程序错误。可能原因：USB 3.0控制器驱动程序问题。" },
                { "0xC000021A", "STATUS_SYSTEM_PROCESS_TERMINATED - 系统进程终止。可能原因：关键系统进程崩溃。" },
            };
        }

        public string ParseErrorCode(string errorCode)
        {
            if (string.IsNullOrEmpty(errorCode))
                return "错误代码为空";

            // 标准化错误代码格式
            string normalizedCode = NormalizeErrorCode(errorCode);

            if (_errorCodeDatabase.TryGetValue(normalizedCode, out var description))
            {
                return description;
            }

            // 尝试匹配部分代码
            foreach (var entry in _errorCodeDatabase)
            {
                if (normalizedCode.EndsWith(entry.Key.TrimStart('0', 'x'), StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return $"未找到错误代码 {errorCode} 的详细说明。建议搜索微软官方文档或技术支持论坛。";
        }

        public ErrorAnalysisResult AnalyzeError(string errorText)
        {
            var result = new ErrorAnalysisResult
            {
                OriginalError = errorText,
                FoundErrorCodes = new List<string>(),
                Explanations = new List<string>(),
                Recommendations = new List<string>(),
                Severity = ErrorSeverity.Unknown
            };

            try
            {
                // 提取错误代码
                var errorCodes = ExtractErrorCodes(errorText);
                result.FoundErrorCodes.AddRange(errorCodes);

                // 解析每个错误代码
                foreach (var code in errorCodes)
                {
                    var explanation = ParseErrorCode(code);
                    result.Explanations.Add($"{code}: {explanation}");
                }

                // 生成建议
                result.Recommendations = GenerateRecommendations(errorText, errorCodes);

                // 确定严重程度
                result.Severity = DetermineSeverity(errorText, errorCodes);
            }
            catch (Exception ex)
            {
                Logger.Error("Error analyzing error text", ex);
                result.Explanations.Add($"分析过程中出现错误: {ex.Message}");
            }

            return result;
        }

        public async Task<ErrorAnalysisResult> AnalyzeCommandOutputAsync(string command, string arguments)
        {
            var result = new ErrorAnalysisResult
            {
                OriginalError = $"命令: {command} {arguments}",
                FoundErrorCodes = new List<string>(),
                Explanations = new List<string>(),
                Recommendations = new List<string>(),
                Severity = ErrorSeverity.Unknown
            };

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    string fullOutput = output + "\n" + error;
                    result.CommandOutput = fullOutput;
                    result.ExitCode = process.ExitCode;

                    if (process.ExitCode != 0 || !string.IsNullOrEmpty(error))
                    {
                        var analysis = AnalyzeError(fullOutput);
                        result.FoundErrorCodes = analysis.FoundErrorCodes;
                        result.Explanations = analysis.Explanations;
                        result.Recommendations = analysis.Recommendations;
                        result.Severity = analysis.Severity;
                    }
                    else
                    {
                        result.Severity = ErrorSeverity.None;
                        result.Explanations.Add("命令执行成功，未检测到错误。");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error analyzing command output", ex);
                result.Explanations.Add($"执行命令时出错: {ex.Message}");
                result.Severity = ErrorSeverity.Critical;
            }

            return result;
        }

        public List<EventLogEntry> ParseEventLogs(string logName, EventLogEntryType? entryType = null, int maxEntries = 100)
        {
            var entries = new List<EventLogEntry>();

            try
            {
                if (!EventLog.Exists(logName))
                {
                    return entries;
                }

                using var eventLog = new EventLog(logName);
                var allEntries = new List<global::System.Diagnostics.EventLogEntry>();
                foreach (global::System.Diagnostics.EventLogEntry entry in eventLog.Entries)
                {
                    if (entryType.HasValue && entry.EntryType != entryType.Value)
                        continue;
                    allEntries.Add(entry);
                }

                entries = allEntries
                    .OrderByDescending(e => e.TimeGenerated)
                    .Take(maxEntries)
                    .Select(e => new EventLogEntry
                    {
                        TimeGenerated = e.TimeGenerated,
                        EntryType = e.EntryType.ToString(),
                        Source = e.Source,
                        EventID = (long)e.EventID,
                        Message = e.Message,
                        Category = e.Category,
                        MachineName = e.MachineName
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error parsing event log: {logName}", ex);
            }

            return entries;
        }

        public List<EventLogEntry> ParseSystemErrorLogs(int maxEntries = 100)
        {
            return ParseEventLogs("System", EventLogEntryType.Error, maxEntries);
        }

        public List<EventLogEntry> ParseApplicationErrorLogs(int maxEntries = 100)
        {
            return ParseEventLogs("Application", EventLogEntryType.Error, maxEntries);
        }

        private List<string> ExtractErrorCodes(string text)
        {
            var codes = new List<string>();

            // 匹配 0x开头的16进制错误代码
            var hexPattern = new Regex(@"0x[0-9A-Fa-f]{8}");
            foreach (Match m in hexPattern.Matches(text))
            {
                codes.Add(m.Value);
            }

            // 匹配 STOP: 开头的蓝屏代码
            var stopPattern = new Regex(@"STOP:\s*(0x[0-9A-Fa-f]{8})");
            foreach (Match m in stopPattern.Matches(text))
            {
                codes.Add(m.Groups[1].Value);
            }

            // 匹配纯数字错误代码
            var numericPattern = new Regex(@"\b(\d{1,10})\b");
            var numericMatches = numericPattern.Matches(text);
            foreach (Match match in numericMatches)
            {
                if (uint.TryParse(match.Value, out uint num) && num > 10000)
                {
                    codes.Add($"0x{num:X8}");
                }
            }

            return codes.Distinct().ToList();
        }

        private string NormalizeErrorCode(string code)
        {
            code = code.Trim().ToUpper();

            // 确保以0x开头
            if (!code.StartsWith("0X"))
            {
                code = "0X" + code;
            }

            // 确保是8位16进制
            if (code.Length < 10)
            {
                code = "0X" + code.Substring(2).PadLeft(8, '0');
            }

            return code;
        }

        private List<string> GenerateRecommendations(string errorText, List<string> errorCodes)
        {
            var recommendations = new List<string>();

            // 基于错误代码的建议
            foreach (var code in errorCodes)
            {
                if (code.Contains("0x80070005"))
                {
                    recommendations.Add("以管理员身份运行程序");
                    recommendations.Add("检查文件和文件夹的权限设置");
                }
                else if (code.Contains("0x80070002"))
                {
                    recommendations.Add("检查文件路径是否正确");
                    recommendations.Add("确认文件未被删除或移动");
                }
                else if (code.Contains("0x80070570"))
                {
                    recommendations.Add("运行磁盘检查工具 (chkdsk)");
                    recommendations.Add("检查硬盘健康状况");
                }
                else if (code.Contains("0x80073712"))
                {
                    recommendations.Add("运行 DISM /Online /Cleanup-Image /RestoreHealth");
                    recommendations.Add("运行 sfc /scannow");
                }
                else if (code.StartsWith("0x000000", StringComparison.OrdinalIgnoreCase))
                {
                    recommendations.Add("更新所有设备驱动程序");
                    recommendations.Add("运行内存诊断工具");
                    recommendations.Add("检查系统温度");
                    recommendations.Add("查看微软官方文档了解详细解决方案");
                }
            }

            // 基于错误文本的建议
            if (errorText.IndexOf("memory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorText.IndexOf("内存", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                recommendations.Add("运行Windows内存诊断工具");
                recommendations.Add("检查内存条是否插好");
                recommendations.Add("尝试更换内存条测试");
            }

            if (errorText.IndexOf("disk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorText.IndexOf("磁盘", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                recommendations.Add("运行磁盘检查工具 (chkdsk /f)");
                recommendations.Add("检查硬盘SMART状态");
                recommendations.Add("备份重要数据");
            }

            if (errorText.IndexOf("driver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorText.IndexOf("驱动", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                recommendations.Add("更新相关设备驱动程序");
                recommendations.Add("回滚到之前的驱动版本");
                recommendations.Add("卸载并重新安装驱动程序");
            }

            // 通用建议
            if (recommendations.Count == 0)
            {
                recommendations.Add("重启计算机后重试");
                recommendations.Add("检查系统更新");
                recommendations.Add("查看事件查看器获取更多信息");
                recommendations.Add("搜索错误代码获取在线帮助");
            }

            return recommendations.Distinct().ToList();
        }

        private ErrorSeverity DetermineSeverity(string errorText, List<string> errorCodes)
        {
            // 检查是否为蓝屏错误
            if (errorCodes.Any(c => c.StartsWith("0x000000", StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorSeverity.Critical;
            }

            // 检查关键系统错误
            if (errorCodes.Any(c => c.Contains("0xC000") || c.Contains("0x8007")))
            {
                return ErrorSeverity.High;
            }

            // 检查错误文本中的关键词
            if (errorText.IndexOf("critical", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorText.IndexOf("fatal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorText.IndexOf("严重", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorText.IndexOf("致命", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ErrorSeverity.Critical;
            }

            if (errorText.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorText.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorText.IndexOf("错误", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ErrorSeverity.High;
            }

            if (errorText.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorText.IndexOf("警告", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ErrorSeverity.Medium;
            }

            return ErrorSeverity.Low;
        }
    }

    public class ErrorAnalysisResult
    {
        public string OriginalError { get; set; } = "";
        public string CommandOutput { get; set; } = "";
        public int ExitCode { get; set; }
        public List<string> FoundErrorCodes { get; set; } = new();
        public List<string> Explanations { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public ErrorSeverity Severity { get; set; }
    }

    public enum ErrorSeverity
    {
        None,
        Low,
        Medium,
        High,
        Critical,
        Unknown
    }

    public class EventLogEntry
    {
        public DateTime TimeGenerated { get; set; }
        public string EntryType { get; set; } = "";
        public string Source { get; set; } = "";
        public long EventID { get; set; }
        public string Message { get; set; } = "";
        public string Category { get; set; } = "";
        public string MachineName { get; set; } = "";
    }
}
