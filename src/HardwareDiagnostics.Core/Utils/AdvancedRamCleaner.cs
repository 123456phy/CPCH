using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HardwareDiagnostics.Core.Utils
{
    /// <summary>
    /// 高级 RAM 清理器 - 支持虚拟内存压缩、进程挂起/恢复
    /// 特别适用于大型前台任务时的内存优化
    /// </summary>
    public class AdvancedRamCleaner : IDisposable
    {
        private Timer? _monitoringTimer;
        private bool _isMonitoring;
        private readonly Dictionary<int, SuspendedProcessInfo> _suspendedProcesses = new();
        private readonly List<int> _whitelistedProcesses = new();
        private readonly object _lock = new();
        private bool _enableCompression = true;
        private bool _enableSuspension = true;
        private int _memoryThreshold = 80;
        private int _checkInterval = 10;

        // Windows API 声明
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        private static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll")]
        private static extern int NtResumeProcess(IntPtr processHandle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        private const uint PROCESS_SUSPEND_RESUME = 0x0800;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_SET_QUOTA = 0x0100;
        private const uint REALTIME_PRIORITY_CLASS = 0x00000100;
        private const uint HIGH_PRIORITY_CLASS = 0x00000080;
        private const uint NORMAL_PRIORITY_CLASS = 0x00000020;
        private const uint IDLE_PRIORITY_CLASS = 0x00000040;

        public event EventHandler<RamOptimizedEventArgs>? RamOptimized;
        public event EventHandler<ProcessSuspendedEventArgs>? ProcessSuspended;
        public event EventHandler<ProcessResumedEventArgs>? ProcessResumed;

        /// <summary>
        /// 配置设置
        /// </summary>
        public void Configure(bool enableCompression, bool enableSuspension, int memoryThreshold, int checkIntervalSeconds)
        {
            _enableCompression = enableCompression;
            _enableSuspension = enableSuspension;
            _memoryThreshold = Math.Max(50, Math.Min(95, memoryThreshold));
            _checkInterval = Math.Max(5, checkIntervalSeconds);

            Logger.Info($"高级 RAM 清理器配置：压缩={enableCompression}, 挂起={enableSuspension}, 阈值={memoryThreshold}%, 间隔={checkIntervalSeconds}秒");
        }

        /// <summary>
        /// 启动高级内存监控
        /// </summary>
        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            _monitoringTimer = new Timer(CheckAndOptimizeMemory, null, TimeSpan.Zero, TimeSpan.FromSeconds(_checkInterval));

            Logger.Info($"高级 RAM 清理监控已启动");
        }

        /// <summary>
        /// 停止内存监控
        /// </summary>
        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;

            // 恢复所有挂起的进程
            ResumeAllSuspendedProcesses();

            Logger.Info("高级 RAM 清理监控已停止，所有挂起的进程已恢复");
        }

        /// <summary>
        /// 添加进程到白名单
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
        /// 手动压缩指定进程的内存
        /// </summary>
        public bool CompressProcessMemory(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                var hProcess = OpenProcess(PROCESS_SET_QUOTA, false, processId);

                if (hProcess == IntPtr.Zero)
                    return false;

                // 将工作集压缩到最小（移至虚拟内存）
                bool result = SetProcessWorkingSetSize(hProcess, new IntPtr(-1), new IntPtr(-1));
                CloseHandle(hProcess);

                if (result)
                {
                    RamOptimized?.Invoke(this, new RamOptimizedEventArgs
                    {
                        ProcessId = processId,
                        ProcessName = process.ProcessName,
                        Action = "内存压缩至虚拟内存",
                        Timestamp = DateTime.Now
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Warning($"压缩进程 {processId} 内存失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 挂起指定进程
        /// </summary>
        public bool SuspendProcess(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                
                if (IsWhitelisted(processId) || IsSystemCriticalProcess(process.ProcessName))
                {
                    process.Dispose();
                    return false;
                }
                var hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, processId);

                if (hProcess == IntPtr.Zero)
                    return false;

                int result = NtSuspendProcess(hProcess);
                CloseHandle(hProcess);

                if (result == 0)
                {
                    lock (_lock)
                    {
                        _suspendedProcesses[processId] = new SuspendedProcessInfo
                        {
                            ProcessId = processId,
                            ProcessName = process.ProcessName,
                            SuspendedTime = DateTime.Now,
                            OriginalPriority = process.PriorityClass
                        };
                    }

                    ProcessSuspended?.Invoke(this, new ProcessSuspendedEventArgs
                    {
                        ProcessId = processId,
                        ProcessName = process.ProcessName,
                        SuspendedTime = DateTime.Now
                    });

                    Logger.Info($"进程 {process.ProcessName} (PID: {processId}) 已挂起");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"挂起进程 {processId} 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复指定进程
        /// </summary>
        public bool ResumeProcess(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                var hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, processId);

                if (hProcess == IntPtr.Zero)
                    return false;

                int result = NtResumeProcess(hProcess);
                CloseHandle(hProcess);

                if (result == 0)
                {
                    lock (_lock)
                    {
                        _suspendedProcesses.Remove(processId);
                    }

                    ProcessResumed?.Invoke(this, new ProcessResumedEventArgs
                    {
                        ProcessId = processId,
                        ProcessName = process.ProcessName,
                        ResumedTime = DateTime.Now
                    });

                    Logger.Info($"进程 {process.ProcessName} (PID: {processId}) 已恢复");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"恢复进程 {processId} 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复所有挂起的进程
        /// </summary>
        public void ResumeAllSuspendedProcesses()
        {
            List<int> processIds;
            lock (_lock)
            {
                processIds = _suspendedProcesses.Keys.ToList();
            }

            foreach (var processId in processIds)
            {
                ResumeProcess(processId);
            }
        }

        /// <summary>
        /// 获取所有挂起的进程
        /// </summary>
        public List<SuspendedProcessInfo> GetSuspendedProcesses()
        {
            lock (_lock)
            {
                return _suspendedProcesses.Values.ToList();
            }
        }

        /// <summary>
        /// 检查并优化内存
        /// </summary>
        private void CheckAndOptimizeMemory(object? state)
        {
            try
            {
                var memoryUsage = GetMemoryUsagePercent();

                // 如果内存使用率超过阈值，开始优化
                if (memoryUsage >= _memoryThreshold)
                {
                    var backgroundProcesses = GetBackgroundProcesses();

                    foreach (var process in backgroundProcesses)
                    {
                        if (IsWhitelisted(process.ProcessId) || IsSystemCriticalProcess(process.ProcessName))
                        {
                            continue;
                        }

                        // 第一步：尝试压缩内存
                        if (_enableCompression && process.MemoryUsageMB > 100)
                        {
                            CompressProcessMemory(process.ProcessId);
                        }

                        // 第二步：如果内存仍然紧张，挂起后台进程
                        if (_enableSuspension && memoryUsage >= _memoryThreshold + 10 && !IsForegroundProcess(process.ProcessId))
                        {
                            // 只挂起占用内存较大的后台进程
                            if (process.MemoryUsageMB > 200 && !_suspendedProcesses.ContainsKey(process.ProcessId))
                            {
                                SuspendProcess(process.ProcessId);
                            }
                        }
                    }
                }
                else if (memoryUsage < _memoryThreshold - 20)
                {
                    // 内存压力减轻，恢复部分挂起的进程
                    ResumeSuspendedProcessesGradually();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("内存优化出错", ex);
            }
        }

        /// <summary>
        /// 逐步恢复挂起的进程
        /// </summary>
        private void ResumeSuspendedProcessesGradually()
        {
            List<SuspendedProcessInfo> suspendedList;
            lock (_lock)
            {
                suspendedList = _suspendedProcesses.Values
                    .OrderBy(p => p.SuspendedTime)
                    .Take(3) // 每次最多恢复3个
                    .ToList();
            }

            foreach (var process in suspendedList)
            {
                // 如果挂起时间超过5分钟，恢复它
                if (DateTime.Now - process.SuspendedTime > TimeSpan.FromMinutes(5))
                {
                    ResumeProcess(process.ProcessId);
                }
            }
        }

        /// <summary>
        /// 获取后台进程列表
        /// </summary>
        private List<ProcessInfo> GetBackgroundProcesses()
        {
            var processes = new List<ProcessInfo>();

            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        // 跳过系统进程和当前进程
                        if (process.Id == Process.GetCurrentProcess().Id || IsSystemCriticalProcess(process.ProcessName))
                        {
                            process.Dispose();
                            continue;
                        }

                        var memoryMB = process.WorkingSet64 / (1024 * 1024);
                        if (memoryMB > 50) // 只关注占用超过50MB的进程
                        {
                            processes.Add(new ProcessInfo
                            {
                                ProcessId = process.Id,
                                ProcessName = process.ProcessName,
                                MemoryUsageMB = memoryMB
                            });
                        }

                        process.Dispose();
                    }
                    catch { }
                }
            }
            catch { }

            return processes.OrderByDescending(p => p.MemoryUsageMB).ToList();
        }

        /// <summary>
        /// 检查是否为前台进程
        /// </summary>
        private bool IsForegroundProcess(int processId)
        {
            try
            {
                // 获取当前前台窗口的进程ID
                var foregroundWindow = GetForegroundWindow();
                GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcessId);
                return processId == foregroundProcessId;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// 获取内存使用率
        /// </summary>
        private int GetMemoryUsagePercent()
        {
            try
            {
                var memInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
                var totalMemory = memInfo.TotalPhysicalMemory;
                var availableMemory = memInfo.AvailablePhysicalMemory;
                var usedMemory = totalMemory - availableMemory;
                return (int)((usedMemory * 100) / totalMemory);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 检查是否为系统关键进程
        /// </summary>
        private bool IsSystemCriticalProcess(string processName)
        {
            var criticalProcesses = new[]
            {
                "system", "smss", "csrss", "wininit", "services", "lsass",
                "svchost", "explorer", "dwm", "winlogon", "runtimebroker",
                "searchui", "ctfmon", "taskmgr", "hardwarediagnostics"
            };

            return criticalProcesses.Contains(processName.ToLower());
        }

        /// <summary>
        /// 检查是否在白名单中
        /// </summary>
        private bool IsWhitelisted(int processId)
        {
            lock (_lock)
            {
                return _whitelistedProcesses.Contains(processId);
            }
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }

    public class SuspendedProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public DateTime SuspendedTime { get; set; }
        public ProcessPriorityClass OriginalPriority { get; set; }
        public TimeSpan SuspendedDuration => DateTime.Now - SuspendedTime;
    }

    public class RamOptimizedEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string Action { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    public class ProcessSuspendedEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public DateTime SuspendedTime { get; set; }
    }

    public class ProcessResumedEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public DateTime ResumedTime { get; set; }
    }

    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public long MemoryUsageMB { get; set; }
    }
}
