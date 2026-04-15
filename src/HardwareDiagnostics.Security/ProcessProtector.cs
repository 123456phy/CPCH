using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.Security
{
    /// <summary>
    /// 进程保护器 - 防止第三方软件强制终止本进程
    /// 多层防护机制
    /// </summary>
    public class ProcessProtector : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessPriorityClass(IntPtr hProcess, out uint lpPriorityClass);

        [DllImport("ntdll.dll")]
        private static extern int NtSetInformationProcess(IntPtr processHandle, int processInformationClass, ref uint processInformation, int processInformationLength);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref uint processInformation, int processInformationLength, out int returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, out bool pbDebuggerPresent);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void OutputDebugString(string lpOutputString);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        private const uint REALTIME_PRIORITY_CLASS = 0x00000100;
        private const uint HIGH_PRIORITY_CLASS = 0x00000080;
        private const int PROCESS_PROTECTION_LEVEL = 0x3D; // ProcessProtectionLevel
        private const uint PROCESS_PROTECTION_LEVEL_NONE = 0;
        private const uint PROCESS_PROTECTION_LEVEL_LIGHT = 1;
        private const uint PROCESS_PROTECTION_LEVEL_FULL = 2;

        private readonly SecurityLogger _securityLogger;
        private Thread? _protectionThread;
        private Thread? _watchdogThread;
        private bool _isRunning;
        private readonly object _lock = new();
        private int _restartCount = 0;
        private const int MAX_RESTARTS = 3;

        public event EventHandler<ProtectionEventArgs>? ProtectionTriggered;
        public event EventHandler<DebuggerDetectedEventArgs>? DebuggerDetected;
        public event EventHandler? ProcessTerminated;

        public bool IsRunning => _isRunning;
        public int ProtectionLevel { get; private set; }

        public ProcessProtector()
        {
            _securityLogger = new SecurityLogger();
        }

        public void StartProtection()
        {
            lock (_lock)
            {
                if (_isRunning) return;

                try
                {
                    Logger.Info("Starting process protection...");

                    // Level 1: 提升进程优先级
                    SetHighPriority();

                    // Level 2: 设置进程保护
                    SetProcessProtection();

                    // Level 3: 启动防护线程
                    _isRunning = true;
                    _protectionThread = new Thread(ProtectionThreadProc)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.Highest
                    };
                    _protectionThread.Start();

                    // Level 4: 启动看门狗线程
                    _watchdogThread = new Thread(WatchdogThreadProc)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.Highest
                    };
                    _watchdogThread.Start();

                    // Level 5: 注册窗口保护
                    RegisterWindowProtection();

                    _securityLogger.LogSecurityEvent(SecurityEventType.ProtectionStarted,
                        $"进程保护已启动，级别: {ProtectionLevel}");

                    Logger.Info($"Process protection started at level {ProtectionLevel}");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to start process protection", ex);
                }
            }
        }

        public void StopProtection()
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                _isRunning = false;
                _protectionThread?.Join(1000);
                _watchdogThread?.Join(1000);

                Logger.Info("Process protection stopped");
                _securityLogger.LogSecurityEvent(SecurityEventType.ProtectionStopped, "进程保护已停止");
            }
        }

        private void SetHighPriority()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                process.PriorityClass = ProcessPriorityClass.RealTime;
                Logger.Info("Process priority set to RealTime");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not set realtime priority: {ex.Message}");
                try
                {
                    var process = Process.GetCurrentProcess();
                    process.PriorityClass = ProcessPriorityClass.High;
                    Logger.Info("Process priority set to High");
                }
                catch { }
            }
        }

        private void SetProcessProtection()
        {
            try
            {
                IntPtr hProcess = GetCurrentProcess();

                // 尝试设置Windows保护进程（需要管理员权限）
                uint protectionLevel = PROCESS_PROTECTION_LEVEL_FULL;
                int result = NtSetInformationProcess(hProcess, PROCESS_PROTECTION_LEVEL, ref protectionLevel, sizeof(int));

                if (result == 0) // STATUS_SUCCESS
                {
                    ProtectionLevel = 2;
                    Logger.Info("Process protection level set to FULL");
                }
                else
                {
                    // 尝试轻量级保护
                    protectionLevel = PROCESS_PROTECTION_LEVEL_LIGHT;
                    result = NtSetInformationProcess(hProcess, PROCESS_PROTECTION_LEVEL, ref protectionLevel, sizeof(int));

                    if (result == 0)
                    {
                        ProtectionLevel = 1;
                        Logger.Info("Process protection level set to LIGHT");
                    }
                    else
                    {
                        ProtectionLevel = 0;
                        Logger.Warning("Could not set process protection level");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error setting process protection", ex);
            }
        }

        private void ProtectionThreadProc()
        {
            int checkInterval = 100; // 100ms检查一次
            int antiDebugCounter = 0;

            while (_isRunning)
            {
                try
                {
                    // 1. 反调试检测
                    if (++antiDebugCounter >= 10) // 每秒检查一次
                    {
                        antiDebugCounter = 0;
                        CheckDebugger();
                    }

                    // 2. 检测进程是否被挂起
                    CheckProcessSuspended();

                    // 3. 检测内存异常
                    CheckMemoryIntegrity();

                    // 4. 检测窗口消息钩子
                    CheckWindowHooks();

                    Thread.Sleep(checkInterval);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error in protection thread", ex);
                }
            }
        }

        private void WatchdogThreadProc()
        {
            // 看门狗线程 - 监控主保护线程
            while (_isRunning)
            {
                try
                {
                    if (_protectionThread != null && !_protectionThread.IsAlive)
                    {
                        Logger.Warning("Protection thread died, restarting...");
                        _securityLogger.LogSecurityEvent(SecurityEventType.ProtectionRestarted, "保护线程异常，正在重启");

                        if (_restartCount < MAX_RESTARTS)
                        {
                            _restartCount++;
                            _protectionThread = new Thread(ProtectionThreadProc)
                            {
                                IsBackground = true,
                                Priority = ThreadPriority.Highest
                            };
                            _protectionThread.Start();
                        }
                        else
                        {
                            Logger.Error("Max protection restarts reached");
                            HandleCriticalProtectionFailure();
                        }
                    }

                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error in watchdog thread", ex);
                }
            }
        }

        private void CheckDebugger()
        {
            try
            {
                // 方法1: IsDebuggerPresent
                if (IsDebuggerPresent())
                {
                    HandleDebuggerDetected("IsDebuggerPresent");
                    return;
                }

                // 方法2: CheckRemoteDebuggerPresent
                if (CheckRemoteDebuggerPresent(GetCurrentProcess(), out bool isDebuggerPresent) && isDebuggerPresent)
                {
                    HandleDebuggerDetected("CheckRemoteDebuggerPresent");
                    return;
                }

                // 方法3: 检测调试寄存器
                if (AreDebugRegistersSet())
                {
                    HandleDebuggerDetected("DebugRegisters");
                    return;
                }

                // 方法4: 时间差检测
                if (DetectTimingAttack())
                {
                    HandleDebuggerDetected("TimingAttack");
                    return;
                }

                // 方法5: 父进程检测
                if (IsParentProcessSuspicious())
                {
                    HandleDebuggerDetected("SuspiciousParent");
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error checking debugger: {ex.Message}");
            }
        }

        private bool AreDebugRegistersSet()
        {
            try
            {
                // 简化的调试寄存器检测
                // 实际实现需要内联汇编或使用GetThreadContext
                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool DetectTimingAttack()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < 1000; i++) { }
                sw.Stop();

                // 如果执行时间异常长，可能被调试
                return sw.ElapsedMilliseconds > 100;
            }
            catch
            {
                return false;
            }
        }

        private bool IsParentProcessSuspicious()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var parentProcess = GetParentProcess(currentProcess);

                if (parentProcess != null)
                {
                    string[] suspiciousParents = { "cheatengine", "x64dbg", "ollydbg", "idaq", "immunity", "windbg" };
                    string parentName = parentProcess.ProcessName.ToLower();

                    return suspiciousParents.Any(p => parentName.Contains(p));
                }
            }
            catch { }

            return false;
        }

        private Process? GetParentProcess(Process process)
        {
            try
            {
                // 使用WMI查询父进程
                string query = $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {process.Id}";
                using var searcher = new System.Management.ManagementObjectSearcher(query);
                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    uint parentId = (uint)obj["ParentProcessId"];
                    return Process.GetProcessById((int)parentId);
                }
            }
            catch { }

            return null;
        }

        private void HandleDebuggerDetected(string method)
        {
            Logger.Warning($"[SECURITY] Debugger detected using method: {method}");
            _securityLogger.LogSecurityEvent(SecurityEventType.DebuggerDetected,
                $"检测到调试器，检测方法: {method}");

            DebuggerDetected?.Invoke(this, new DebuggerDetectedEventArgs
            {
                DetectionMethod = method,
                Timestamp = DateTime.Now
            });

            // 反制措施
            TakeAntiDebugAction();
        }

        private void TakeAntiDebugAction()
        {
            try
            {
                // 1. 通知用户
                ProtectionTriggered?.Invoke(this, new ProtectionEventArgs
                {
                    Type = ProtectionEventType.DebuggerDetected,
                    Message = "检测到调试器！程序可能被恶意分析。"
                });

                // 2. 混淆执行流程
                ConfuseExecution();

                // 3. 延迟响应（增加调试难度）
                Thread.Sleep(new Random().Next(100, 500));
            }
            catch (Exception ex)
            {
                Logger.Error("Error taking anti-debug action", ex);
            }
        }

        private void ConfuseExecution()
        {
            // 简单的执行流混淆
            var junk = new byte[1024];
            new Random().NextBytes(junk);
        }

        private void CheckProcessSuspended()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                // 检测进程是否被挂起（通过检查线程状态）
                foreach (ProcessThread thread in process.Threads)
                {
                    // 简化的检测，实际应该使用NtQuerySystemInformation
                }
            }
            catch { }
        }

        private void CheckMemoryIntegrity()
        {
            try
            {
                // 检测关键内存区域是否被修改
                // 实际实现需要计算代码段的哈希值
            }
            catch { }
        }

        private void CheckWindowHooks()
        {
            try
            {
                // 检测是否有恶意窗口钩子
                // 实际实现需要调用GetWindowsHookEx等API
            }
            catch { }
        }

        private void RegisterWindowProtection()
        {
            try
            {
                // 注册窗口消息处理，防止被关闭
                if (Application.OpenForms.Count > 0)
                {
                    var mainForm = Application.OpenForms[0];
                    mainForm.FormClosing += (s, e) =>
                    {
                        // 检查是否是用户正常关闭还是强制关闭
                        if (e.CloseReason == CloseReason.WindowsShutDown ||
                            e.CloseReason == CloseReason.TaskManagerClosing)
                        {
                            Logger.Warning($"[SECURITY] Process termination attempt: {e.CloseReason}");
                            _securityLogger.LogSecurityEvent(SecurityEventType.TerminationAttempt,
                                $"检测到进程终止尝试，原因: {e.CloseReason}");

                            ProtectionTriggered?.Invoke(this, new ProtectionEventArgs
                            {
                                Type = ProtectionEventType.TerminationAttempt,
                                Message = $"检测到进程终止尝试: {e.CloseReason}"
                            });

                            // 阻止关闭
                            e.Cancel = true;
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error registering window protection", ex);
            }
        }

        private void HandleCriticalProtectionFailure()
        {
            try
            {
                Logger.Error("Critical protection failure!");
                _securityLogger.LogSecurityEvent(SecurityEventType.CriticalFailure, "关键保护机制失效");

                ProtectionTriggered?.Invoke(this, new ProtectionEventArgs
                {
                    Type = ProtectionEventType.CriticalFailure,
                    Message = "安全保护机制遭遇严重故障，建议立即重启程序！"
                });

                ProcessTerminated?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }

        public void Dispose()
        {
            StopProtection();
            _securityLogger?.Dispose();
        }
    }

    public class ProtectionEventArgs : EventArgs
    {
        public ProtectionEventType Type { get; set; }
        public string Message { get; set; } = "";
    }

    public enum ProtectionEventType
    {
        DebuggerDetected,
        TerminationAttempt,
        MemoryTampering,
        SuspiciousActivity,
        CriticalFailure
    }

    public class DebuggerDetectedEventArgs : EventArgs
    {
        public string DetectionMethod { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
