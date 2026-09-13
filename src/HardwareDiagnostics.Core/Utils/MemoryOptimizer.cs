using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;

namespace HardwareDiagnostics.Core.Utils
{
    public static class MemoryOptimizer
    {
        private const long MaxMemoryBytes = 150 * 1024 * 1024; // 150MB：自己先瘦身
        private static Timer? _memoryCheckTimer;
        private static readonly object _lock = new();
        private static int _checkRunning; // 防重入：Timer 回调串行化

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        [DllImport("kernel32.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        public static void StartMonitoring()
        {
            lock (_lock)
            {
                _memoryCheckTimer ??= new Timer(CheckMemory, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
            }
        }

        public static void StopMonitoring()
        {
            lock (_lock)
            {
                _memoryCheckTimer?.Dispose();
                _memoryCheckTimer = null;
            }
        }

        private static void CheckMemory(object? state)
        {
            // 防重入：上一次还没跑完就跳过本次，避免 Timer 堆积
            if (Interlocked.Exchange(ref _checkRunning, 1) == 1) return;
            try
            {
                long memoryBytes = GetCurrentMemoryUsage();

                if (memoryBytes > MaxMemoryBytes)
                {
                    ForceGarbageCollection();
                    TrimWorkingSet();
                }
            }
            catch { }
            finally { Interlocked.Exchange(ref _checkRunning, 0); }
        }

        public static void ForceGarbageCollection()
        {
            // 单次 Gen2 Optimized 回收即可；双 Collect + WaitForPendingFinalizers
            // 会阻塞 UI 线程数百毫秒，只在超限时做一次
            GC.Collect(2, GCCollectionMode.Optimized, false, false);
        }

        public static void TrimWorkingSet()
        {
            try
            {
                var proc = Process.GetCurrentProcess();
                EmptyWorkingSet(proc.Handle);
            }
            catch { }
        }

        public static long GetCurrentMemoryUsage()
        {
            // GC.GetTotalMemory 不创建 Process 句柄，开销远小于 GetCurrentProcess().WorkingSet64
            try { return GC.GetTotalMemory(false); }
            catch { return Process.GetCurrentProcess().WorkingSet64; }
        }

        public static string GetMemoryUsageText()
        {
            long bytes = GetCurrentMemoryUsage();
            return $"{bytes / (1024 * 1024)} MB";
        }

        public static bool IsMemoryUsageHigh()
        {
            return GetCurrentMemoryUsage() > MaxMemoryBytes;
        }
    }
}
