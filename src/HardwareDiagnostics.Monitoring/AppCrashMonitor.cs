using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HardwareDiagnostics.Core.Models;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.Monitoring
{
    public class AppCrashMonitor : IDisposable
    {
        private readonly Dictionary<int, MonitoredProcess> _monitoredProcesses = new();
        private readonly object _lock = new();
        private Timer? _monitorTimer;
        private bool _isRunning;

        public event EventHandler<CrashReport>? CrashDetected;
        public event EventHandler<ProcessEventArgs>? ProcessStarted;
        public event EventHandler<ProcessEventArgs>? ProcessExited;

        public bool IsRunning => _isRunning;

        public void StartMonitoring()
        {
            lock (_lock)
            {
                if (_isRunning) return;

                _isRunning = true;
                _monitorTimer = new Timer(MonitorCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
                Logger.Info("Application crash monitoring started");
            }
        }

        public void StopMonitoring()
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                _isRunning = false;
                _monitorTimer?.Dispose();
                _monitorTimer = null;
                Logger.Info("Application crash monitoring stopped");
            }
        }

        public void MonitorProcess(string executablePath, string? arguments = null)
        {
            if (!File.Exists(executablePath))
            {
                Logger.Error($"Executable not found: {executablePath}");
                throw new FileNotFoundException("Executable not found", executablePath);
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments ?? "",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(executablePath)
                };

                var process = Process.Start(psi);
                if (process != null)
                {
                    var monitoredProcess = new MonitoredProcess
                    {
                        Process = process,
                        ExecutablePath = executablePath,
                        StartTime = DateTime.Now,
                        LogBuilder = new StringBuilder()
                    };

                    lock (_lock)
                    {
                        _monitoredProcesses[process.Id] = monitoredProcess;
                    }

                    // 设置事件处理
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            monitoredProcess.LogBuilder.AppendLine($"[OUT] {e.Data}");
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            monitoredProcess.LogBuilder.AppendLine($"[ERR] {e.Data}");
                        }
                    };

                    process.Exited += (sender, e) =>
                    {
                        OnProcessExited(process.Id);
                    };

                    process.EnableRaisingEvents = true;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    ProcessStarted?.Invoke(this, new ProcessEventArgs
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        ExecutablePath = executablePath
                    });

                    Logger.Info($"Started monitoring process: {executablePath} (PID: {process.Id})");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error starting process monitoring", ex);
                throw;
            }
        }

        public void MonitorExistingProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process == null)
                {
                    throw new ArgumentException($"Process with ID {processId} not found");
                }

                var monitoredProcess = new MonitoredProcess
                {
                    Process = process,
                    ExecutablePath = process.MainModule?.FileName ?? "Unknown",
                    StartTime = process.StartTime,
                    LogBuilder = new StringBuilder()
                };

                lock (_lock)
                {
                    _monitoredProcesses[processId] = monitoredProcess;
                }

                process.Exited += (sender, e) =>
                {
                    OnProcessExited(processId);
                };

                process.EnableRaisingEvents = true;

                ProcessStarted?.Invoke(this, new ProcessEventArgs
                {
                    ProcessId = processId,
                    ProcessName = process.ProcessName,
                    ExecutablePath = monitoredProcess.ExecutablePath
                });

                Logger.Info($"Started monitoring existing process: {process.ProcessName} (PID: {processId})");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error monitoring existing process {processId}", ex);
                throw;
            }
        }

        public void StopMonitoringProcess(int processId)
        {
            lock (_lock)
            {
                if (_monitoredProcesses.TryGetValue(processId, out var monitoredProcess))
                {
                    try
                    {
                        if (!monitoredProcess.Process.HasExited)
                        {
                            monitoredProcess.Process.Kill();
                        }
                    }
                    catch { }

                    _monitoredProcesses.Remove(processId);
                    Logger.Info($"Stopped monitoring process: {processId}");
                }
            }
        }

        public List<MonitoredProcessInfo> GetMonitoredProcesses()
        {
            lock (_lock)
            {
                return _monitoredProcesses.Values.Select(p => new MonitoredProcessInfo
                {
                    ProcessId = p.Process.Id,
                    ProcessName = p.Process.ProcessName,
                    ExecutablePath = p.ExecutablePath,
                    StartTime = p.StartTime,
                    IsRunning = !p.Process.HasExited
                }).ToList();
            }
        }

        private void MonitorCallback(object? state)
        {
            lock (_lock)
            {
                var exitedProcesses = new List<int>();

                foreach (var kvp in _monitoredProcesses)
                {
                    try
                    {
                        var process = kvp.Value.Process;
                        if (process.HasExited)
                        {
                            exitedProcesses.Add(kvp.Key);
                            AnalyzeProcessExit(kvp.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error monitoring process {kvp.Key}", ex);
                        exitedProcesses.Add(kvp.Key);
                    }
                }

                // 清理已退出的进程
                foreach (var pid in exitedProcesses)
                {
                    _monitoredProcesses.Remove(pid);
                }
            }
        }

        private void OnProcessExited(int processId)
        {
            lock (_lock)
            {
                if (_monitoredProcesses.TryGetValue(processId, out var monitoredProcess))
                {
                    AnalyzeProcessExit(monitoredProcess);
                    _monitoredProcesses.Remove(processId);
                }
            }

            try
            {
                var process = Process.GetProcessById(processId);
                ProcessExited?.Invoke(this, new ProcessEventArgs
                {
                    ProcessId = processId,
                    ProcessName = process?.ProcessName ?? "Unknown",
                    ExecutablePath = process?.MainModule?.FileName ?? "Unknown"
                });
            }
            catch { }
        }

        private void AnalyzeProcessExit(MonitoredProcess monitoredProcess)
        {
            try
            {
                var process = monitoredProcess.Process;
                int exitCode = process.ExitCode;

                // 检查是否为异常退出
                if (exitCode != 0)
                {
                    var report = new CrashReport
                    {
                        CrashTime = DateTime.Now,
                        ApplicationName = process.ProcessName,
                        ApplicationPath = monitoredProcess.ExecutablePath,
                        ExceptionType = exitCode < 0 ? "Access Violation" : "Application Error",
                        ExceptionMessage = $"进程以非零退出代码终止: {exitCode}",
                        ProcessId = process.Id.ToString(),
                        AdditionalInfo = new Dictionary<string, string>
                        {
                            { "ExitCode", exitCode.ToString() },
                            { "StartTime", monitoredProcess.StartTime.ToString() },
                            { "Duration", (DateTime.Now - monitoredProcess.StartTime).ToString() },
                            { "LogOutput", monitoredProcess.LogBuilder.ToString() }
                        }
                    };

                    // 尝试获取更多信息
                    TryGetAdditionalCrashInfo(report);

                    // 分析根本原因
                    report.RootCause = AnalyzeRootCause(report);
                    report.Recommendations = GenerateRecommendations(report);

                    // 保存崩溃报告
                    SaveCrashReport(report);

                    CrashDetected?.Invoke(this, report);
                    Logger.Error($"[Crash] {report.ApplicationName} - {report.ExceptionType}: {report.ExceptionMessage}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error analyzing process exit", ex);
            }
        }

        private void TryGetAdditionalCrashInfo(CrashReport report)
        {
            try
            {
                // 尝试从Windows事件日志获取崩溃信息
                using var eventLog = new EventLog("Application");
                var entries = eventLog.Entries.Cast<EventLogEntry>()
                    .Where(e => e.TimeGenerated > report.CrashTime.AddMinutes(-5) &&
                                e.TimeGenerated <= report.CrashTime.AddMinutes(1) &&
                                e.EntryType == EventLogEntryType.Error &&
                                (e.Source.Contains(report.ApplicationName) ||
                                 e.Message.Contains(report.ApplicationName)))
                    .OrderByDescending(e => e.TimeGenerated)
                    .Take(5);

                foreach (var entry in entries)
                {
                    report.AdditionalInfo[$"Event_{entry.InstanceId}"] = entry.Message;
                }
            }
            catch { }

            try
            {
                // 检查应用程序日志文件
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CrashDumps");

                if (Directory.Exists(appDataPath))
                {
                    var dumpFiles = Directory.GetFiles(appDataPath, "*.dmp")
                        .Where(f => File.GetCreationTime(f) > report.CrashTime.AddMinutes(-5))
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .Take(3);

                    foreach (var dumpFile in dumpFiles)
                    {
                        report.AdditionalInfo[$"DumpFile_{Path.GetFileName(dumpFile)}"] = dumpFile;
                    }
                }
            }
            catch { }
        }

        private string AnalyzeRootCause(CrashReport report)
        {
            var causes = new List<string>();

            // 分析退出代码
            if (report.AdditionalInfo.TryGetValue("ExitCode", out var exitCode))
            {
                int code = int.Parse(exitCode);
                if (code == -1073741819) // 0xC0000005
                {
                    causes.Add("访问冲突 - 程序尝试访问未分配的内存地址");
                }
                else if (code == -1073741571) // 0xC00000FD
                {
                    causes.Add("堆栈溢出 - 可能是递归调用过深");
                }
                else if (code == -1073741515) // 0xC0000135
                {
                    causes.Add("缺少依赖的DLL文件");
                }
                else if (code == -1073741502) // 0xC0000142
                {
                    causes.Add("DLL初始化失败");
                }
                else if (code < 0)
                {
                    causes.Add($"系统错误 (HRESULT: 0x{code:X8})");
                }
            }

            // 分析日志输出
            if (report.AdditionalInfo.TryGetValue("LogOutput", out var logOutput))
            {
                if (logOutput.Contains("OutOfMemoryException") || logOutput.Contains("内存不足"))
                {
                    causes.Add("内存不足 - 程序耗尽了可用内存");
                }
                if (logOutput.Contains("FileNotFoundException") || logOutput.Contains("找不到文件"))
                {
                    causes.Add("缺少必要的文件或资源");
                }
                if (logOutput.Contains("UnauthorizedAccessException") || logOutput.Contains("访问被拒绝"))
                {
                    causes.Add("权限不足 - 程序没有足够的权限执行操作");
                }
            }

            if (causes.Count == 0)
            {
                causes.Add("未知原因 - 需要进一步分析");
            }

            return string.Join("; ", causes);
        }

        private List<string> GenerateRecommendations(CrashReport report)
        {
            var recommendations = new List<string>();

            if (report.RootCause.Contains("内存"))
            {
                recommendations.Add("关闭其他程序释放内存");
                recommendations.Add("增加虚拟内存大小");
                recommendations.Add("检查是否有内存泄漏");
            }

            if (report.RootCause.Contains("DLL") || report.RootCause.Contains("文件"))
            {
                recommendations.Add("重新安装应用程序");
                recommendations.Add("安装所需的运行库 (VC++ Redistributable)");
                recommendations.Add("检查系统文件完整性 (sfc /scannow)");
            }

            if (report.RootCause.Contains("权限"))
            {
                recommendations.Add("以管理员身份运行程序");
                recommendations.Add("检查文件和文件夹权限");
            }

            if (report.RootCause.Contains("访问冲突"))
            {
                recommendations.Add("更新显卡驱动程序");
                recommendations.Add("检查内存是否有问题");
                recommendations.Add("禁用数据执行保护(DEP)进行测试");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("查看应用程序的官方支持页面");
                recommendations.Add("联系软件开发商获取支持");
                recommendations.Add("尝试重新安装应用程序");
            }

            return recommendations;
        }

        private void SaveCrashReport(CrashReport report)
        {
            try
            {
                string reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashReports");
                Directory.CreateDirectory(reportsDir);

                string fileName = $"crash_{report.ApplicationName}_{report.CrashTime:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(reportsDir, fileName);

                var sb = new StringBuilder();
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine("应用程序崩溃报告");
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine();
                sb.AppendLine($"应用程序: {report.ApplicationName}");
                sb.AppendLine($"路径: {report.ApplicationPath}");
                sb.AppendLine($"崩溃时间: {report.CrashTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"进程ID: {report.ProcessId}");
                sb.AppendLine();
                sb.AppendLine($"异常类型: {report.ExceptionType}");
                sb.AppendLine($"异常消息: {report.ExceptionMessage}");
                sb.AppendLine();
                sb.AppendLine($"根本原因分析: {report.RootCause}");
                sb.AppendLine();
                sb.AppendLine("建议的解决方案:");
                foreach (var rec in report.Recommendations)
                {
                    sb.AppendLine($"  - {rec}");
                }
                sb.AppendLine();
                sb.AppendLine("附加信息:");
                foreach (var info in report.AdditionalInfo)
                {
                    sb.AppendLine($"  {info.Key}: {info.Value}");
                }
                sb.AppendLine();
                sb.AppendLine("=".PadRight(80, '='));

                File.WriteAllText(filePath, sb.ToString());
                Logger.Info($"Crash report saved: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error saving crash report", ex);
            }
        }

        public void Dispose()
        {
            StopMonitoring();

            lock (_lock)
            {
                foreach (var monitoredProcess in _monitoredProcesses.Values)
                {
                    try
                    {
                        if (!monitoredProcess.Process.HasExited)
                        {
                            monitoredProcess.Process.Dispose();
                        }
                    }
                    catch { }
                }
                _monitoredProcesses.Clear();
            }
        }
    }

    public class MonitoredProcess
    {
        public Process Process { get; set; } = null!;
        public string ExecutablePath { get; set; } = "";
        public DateTime StartTime { get; set; }
        public StringBuilder LogBuilder { get; set; } = new();
    }

    public class MonitoredProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public DateTime StartTime { get; set; }
        public bool IsRunning { get; set; }
    }

    public class ProcessEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
    }
}
