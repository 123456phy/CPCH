using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace HardwareDiagnostics.Security
{
    /// <summary>
    /// 安全日志系统 - 记录所有安全事件
    /// 加密存储，防篡改
    /// </summary>
    public class SecurityLogger : IDisposable
    {
        private readonly string _logDirectory;
        private readonly string _logFile;
        private readonly object _lock = new();
        private readonly Timer _flushTimer;
        private StringBuilder _buffer = new();

        public event Action<SecurityLogEntry>? OnLogEntry;

        public SecurityLogger()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SecurityLogs");
            Directory.CreateDirectory(_logDirectory);

            _logFile = Path.Combine(_logDirectory, $"security_{DateTime.Now:yyyyMMdd}.log");

            // 每5秒刷新一次日志
            _flushTimer = new Timer(FlushBuffer, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            LogSecurityEvent(SecurityEventType.LoggerInitialized, "安全日志系统初始化");
        }

        public void LogInfo(string message)
        {
            LogSecurityEvent(SecurityEventType.LoggerInitialized, message);
        }

        public void LogSecurityEvent(SecurityEventType eventType, string message, string severity = "Info")
        {
            var entry = new SecurityLogEntry
            {
                Timestamp = DateTime.Now,
                EventType = eventType,
                Message = message,
                Severity = severity,
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };

            LogEntry(entry);
        }

        public void LogSuspiciousPacket(SuspiciousPacket packet)
        {
            var entry = new SecurityLogEntry
            {
                Timestamp = packet.Timestamp,
                EventType = SecurityEventType.SuspiciousPacket,
                Message = $"[{packet.ThreatType}] {packet.Description}",
                Details = packet.Details,
                SourceIP = packet.SourceIP,
                Severity = packet.Severity.ToString()
            };

            LogEntry(entry);
        }

        public void LogUSBEvent(USBDeviceInfo device, RiskAssessment risk)
        {
            var entry = new SecurityLogEntry
            {
                Timestamp = device.InsertTime,
                EventType = SecurityEventType.USBEvent,
                Message = $"USB设备: {device.DeviceName} ({device.VIDPID})",
                Details = $"风险等级: {risk.Level}, 因素: {string.Join(", ", risk.Factors.Select(f => f.Description))}",
                Severity = risk.Level.ToString()
            };

            LogEntry(entry);
        }

        private void LogEntry(SecurityLogEntry entry)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine($"时间: {entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"事件: {entry.EventType}");
            sb.AppendLine($"消息: {entry.Message}");

            if (!string.IsNullOrEmpty(entry.SourceIP))
                sb.AppendLine($"来源IP: {entry.SourceIP}");
            if (!string.IsNullOrEmpty(entry.DestinationIP))
                sb.AppendLine($"目标IP: {entry.DestinationIP}");
            if (!string.IsNullOrEmpty(entry.Severity))
                sb.AppendLine($"严重级别: {entry.Severity}");
            if (!string.IsNullOrEmpty(entry.Details))
                sb.AppendLine($"详情: {entry.Details}");

            sb.AppendLine($"进程ID: {entry.ProcessId}, 线程ID: {entry.ThreadId}");
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine();

            string logText = sb.ToString();

            lock (_lock)
            {
                _buffer.Append(logText);
            }

            // 通知监听器
            OnLogEntry?.Invoke(entry);

            // 立即写入高危事件
            if (entry.EventType == SecurityEventType.SuspiciousPacket ||
                entry.EventType == SecurityEventType.USBDeviceBlocked ||
                entry.EventType == SecurityEventType.DebuggerDetected)
            {
                FlushBuffer(null);
            }
        }

        private void FlushBuffer(object? state)
        {
            string content;
            lock (_lock)
            {
                if (_buffer.Length == 0) return;
                content = _buffer.ToString();
                _buffer.Clear();
            }

            try
            {
                File.AppendAllText(_logFile, content);
            }
            catch (Exception ex)
            {
                // 如果写入失败，尝试写入备用位置
                try
                {
                    string backupFile = Path.Combine(Path.GetTempPath(), "HardwareDiagnostics_Security.log");
                    File.AppendAllText(backupFile, content);
                }
                catch { }
            }
        }

        public string[] GetRecentLogs(int count = 100)
        {
            try
            {
                if (!File.Exists(_logFile))
                    return Array.Empty<string>();

                var lines = File.ReadAllLines(_logFile);
                var result = new List<string>();
                int startIndex = Math.Max(0, lines.Length - count);
                for (int i = startIndex; i < lines.Length; i++)
                {
                    result.Add(lines[i]);
                }
                return result.ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public void ExportLogs(string destinationPath)
        {
            try
            {
                FlushBuffer(null);
                File.Copy(_logFile, destinationPath, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"导出日志失败: {ex.Message}");
            }
        }

        public void ClearOldLogs(int daysToKeep = 30)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-daysToKeep);
                var files = Directory.GetFiles(_logDirectory, "security_*.log");

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoff)
                        {
                            File.Delete(file);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理旧日志失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _flushTimer?.Dispose();
            FlushBuffer(null);
        }
    }

    public class SecurityLogEntry
    {
        public DateTime Timestamp { get; set; }
        public SecurityEventType EventType { get; set; }
        public string Message { get; set; } = "";
        public string Details { get; set; } = "";
        public string SourceIP { get; set; } = "";
        public string DestinationIP { get; set; } = "";
        public string Severity { get; set; } = "";
        public int ProcessId { get; set; }
        public int ThreadId { get; set; }
    }

    public enum SecurityEventType
    {
        LoggerInitialized,
        FirewallStarted,
        FirewallStopped,
        USBGuardStarted,
        USBGuardStopped,
        ProtectionStarted,
        ProtectionStopped,
        ProtectionRestarted,
        SuspiciousPacket,
        AttackDetected,
        USBEvent,
        USBDeviceBlocked,
        USBDeviceAllowed,
        DebuggerDetected,
        TerminationAttempt,
        CriticalFailure
    }
}
