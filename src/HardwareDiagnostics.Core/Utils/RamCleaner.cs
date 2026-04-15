using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace HardwareDiagnostics.Core.Utils
{
    /// <summary>
    /// RAM 实时清理器 - 检测并清理高占用进程
    /// </summary>
    public class RamCleaner : IDisposable
    {
        private Timer? _monitoringTimer;
        private bool _isMonitoring;
        private readonly List<int> _whitelistedProcesses = new();
        private readonly object _lock = new();

        // 内存清理 API
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        public event EventHandler<RamCleanedEventArgs>? RamCleaned;
        public event EventHandler<HighMemoryProcessEventArgs>? HighMemoryProcessDetected;

        /// <summary>
        /// 启动内存监控
        /// </summary>
        public void StartMonitoring(int checkIntervalSeconds = 10)
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            _monitoringTimer = new Timer(CheckMemoryUsage, null, TimeSpan.Zero, TimeSpan.FromSeconds(checkIntervalSeconds));
            
            Logger.Info($"RAM 清理监控已启动，检查间隔：{checkIntervalSeconds}秒");
        }

        /// <summary>
        /// 停止内存监控
        /// </summary>
        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
            
            Logger.Info("RAM 清理监控已停止");
        }

        /// <summary>
        /// 添加进程到白名单（不清理）
        /// </summary>
        public void AddToWhitelist(int processId)
        {
            lock (_lock)
            {
                if (!_whitelistedProcesses.Contains(processId))
                {
                    _whitelistedProcesses.Add(processId);
                }
            }
        }

        /// <summary>
        /// 从白名单移除进程
        /// </summary>
        public void RemoveFromWhitelist(int processId)
        {
            lock (_lock)
            {
                _whitelistedProcesses.Remove(processId);
            }
        }

        /// <summary>
        /// 检查是否在白名单中
        /// </summary>
        public bool IsWhitelisted(int processId)
        {
            lock (_lock)
            {
                return _whitelistedProcesses.Contains(processId);
            }
        }

        private void CheckMemoryUsage(object? state)
        {
            try
            {
                var totalPhysicalMemory = GetTotalPhysicalMemory();
                var availableMemory = GetAvailablePhysicalMemory();
                var memoryUsagePercent = ((totalPhysicalMemory - availableMemory) * 100) / totalPhysicalMemory;

                // 如果内存使用率超过 80%，开始清理
                if (memoryUsagePercent >= 80)
                {
                    var highMemoryProcesses = GetHighMemoryProcesses(totalPhysicalMemory);

                    foreach (var processInfo in highMemoryProcesses)
                    {
                        if (IsWhitelisted(processInfo.ProcessId))
                        {
                            continue;
                        }

                        HighMemoryProcessDetected?.Invoke(this, new HighMemoryProcessEventArgs
                        {
                            ProcessId = processInfo.ProcessId,
                            ProcessName = processInfo.ProcessName,
                            MemoryUsageMB = processInfo.MemoryUsageMB,
                            MemoryUsagePercent = processInfo.MemoryUsagePercent
                        });

                        // 判断是否为无用进程
                        if (IsUselessProcess(processInfo))
                        {
                            // 尝试清理内存
                            CleanProcessMemory(processInfo.ProcessId);
                            
                            RamCleaned?.Invoke(this, new RamCleanedEventArgs
                            {
                                ProcessId = processInfo.ProcessId,
                                ProcessName = processInfo.ProcessName,
                                CleanedMemoryMB = processInfo.MemoryUsageMB,
                                Action = "内存已清理"
                            });

                            Logger.Info($"清理进程 {processInfo.ProcessName} (PID: {processInfo.ProcessId}) 的内存，释放 {processInfo.MemoryUsageMB} MB");
                        }
                        else if (processInfo.MemoryUsagePercent >= 30 && !IsSystemCriticalProcess(processInfo.ProcessName))
                        {
                            // 对于占用极高但非关键的进程，可以选择终止
                            // 这里只记录，不自动终止，避免误杀
                            Logger.Warning($"检测到高内存占用进程：{processInfo.ProcessName} (PID: {processInfo.ProcessId}), 占用：{processInfo.MemoryUsagePercent}%");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("内存监控出错", ex);
            }
        }

        private bool IsUselessProcess(ProcessInfo processInfo)
        {
            // 判断进程是否无用的逻辑
            // 1. 不是系统关键进程
            // 2. 长时间无响应
            // 3. 已知的内存泄漏进程
            
            if (IsSystemCriticalProcess(processInfo.ProcessName))
            {
                return false;
            }

            // 检查进程是否无响应
            try
            {
                using var process = Process.GetProcessById(processInfo.ProcessId);
                if (process.Responding == false)
                {
                    return true;
                }
            }
            catch { }

            // 已知的内存泄漏进程列表（示例）
            var knownMemoryLeakProcesses = new[] { "chrome", "firefox", "iexplore", "msedge" };
            if (knownMemoryLeakProcesses.Contains(processInfo.ProcessName.ToLower()))
            {
                // 浏览器类进程，如果占用超过 2GB 且无响应，可以清理
                return processInfo.MemoryUsageMB >= 2048;
            }

            return false;
        }

        private bool IsSystemCriticalProcess(string processName)
        {
            var criticalProcesses = new[]
            {
                "system", "smss", "csrss", "wininit", "services", "lsass",
                "svchost", "explorer", "dwm", "winlogon", "runtimebroker",
                "searchui", "ctfmon", "taskmgr", "resource monitor"
            };

            return criticalProcesses.Contains(processName.ToLower());
        }

        private void CleanProcessMemory(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                var hProcess = process.Handle;

                if (hProcess != IntPtr.Zero)
                {
                    // 方法 1: 清空工作集
                    EmptyWorkingSet(hProcess);
                    
                    // 方法 2: 设置工作集大小为 0（强制释放）
                    SetProcessWorkingSetSize(hProcess, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"清理进程 {processId} 内存失败：{ex.Message}");
            }
        }

        private List<ProcessInfo> GetHighMemoryProcesses(long totalMemory)
        {
            var highMemoryProcesses = new List<ProcessInfo>();

            try
            {
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    try
                    {
                        var memoryUsage = process.WorkingSet64;
                        var memoryUsageMB = memoryUsage / (1024 * 1024);
                        var memoryUsagePercent = (memoryUsage * 100) / totalMemory;

                        // 只关注占用超过 5% 内存的进程
                        if (memoryUsagePercent >= 5)
                        {
                            highMemoryProcesses.Add(new ProcessInfo
                            {
                                ProcessId = process.Id,
                                ProcessName = process.ProcessName,
                                MemoryUsageMB = memoryUsageMB,
                                MemoryUsagePercent = (int)memoryUsagePercent
                            });
                        }
                    }
                    catch { }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch { }

            return highMemoryProcesses.OrderByDescending(p => p.MemoryUsagePercent).ToList();
        }

        private long GetTotalPhysicalMemory()
        {
            try
            {
                return new System.Diagnostics.PerformanceCounter("Memory", "Total Visible Memory").RawValue * 1024 * 1024;
            }
            catch
            {
                return System.GC.GetTotalMemory(false) * 1024 * 1024;
            }
        }

        private long GetAvailablePhysicalMemory()
        {
            try
            {
                return new System.Diagnostics.PerformanceCounter("Memory", "Available MBytes").RawValue * 1024 * 1024;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 立即清理所有可清理的内存
        /// </summary>
        public void CleanNow()
        {
            try
            {
                // 强制垃圾回收
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // 清理系统工作集
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, IntPtr.Zero, IntPtr.Zero);

                Logger.Info("立即内存清理完成");
            }
            catch (Exception ex)
            {
                Logger.Error("立即内存清理失败", ex);
            }
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }

    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public long MemoryUsageMB { get; set; }
        public int MemoryUsagePercent { get; set; }
    }

    public class RamCleanedEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public long CleanedMemoryMB { get; set; }
        public string Action { get; set; } = "";
    }

    public class HighMemoryProcessEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public long MemoryUsageMB { get; set; }
        public int MemoryUsagePercent { get; set; }
    }
}
