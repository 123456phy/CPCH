using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;

namespace HardwareDiagnostics.Core.Utils
{
    public static class MemoryOptimizer
    {
        private const long MaxMemoryBytes = 300 * 1024 * 1024; // 300MB
        private static Timer? _memoryCheckTimer;
        private static readonly object _lock = new();

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
            try
            {
                var proc = Process.GetCurrentProcess();
                long memoryBytes = proc.WorkingSet64;

                if (memoryBytes > MaxMemoryBytes)
                {
                    ForceGarbageCollection();
                    TrimWorkingSet();
                }
            }
            catch { }
        }

        public static void ForceGarbageCollection()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
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
            return Process.GetCurrentProcess().WorkingSet64;
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
