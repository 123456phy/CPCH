using System;
using System.IO;
using System.Text;
using System.Threading;

namespace HardwareDiagnostics.Core.Utils
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public static class Logger
    {
        private static readonly string _logDirectory;
        private static readonly object _lock = new();
        private static LogLevel _minLevel = LogLevel.Info;

        static Logger()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(_logDirectory);
        }

        public static void SetMinLevel(LogLevel level)
        {
            _minLevel = level;
        }

        public static void Debug(string message) => Log(LogLevel.Debug, message);
        public static void Info(string message) => Log(LogLevel.Info, message);
        public static void Warning(string message) => Log(LogLevel.Warning, message);
        public static void Error(string message) => Log(LogLevel.Error, message);
        public static void Error(string message, Exception ex) => Log(LogLevel.Error, $"{message}: {ex}");
        public static void Fatal(string message) => Log(LogLevel.Fatal, message);

        private static void Log(LogLevel level, string message)
        {
            if (level < _minLevel) return;

            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(" [");
            sb.Append(level.ToString().ToUpper());
            sb.Append("] ");
            sb.Append(message);

            string logLine = sb.ToString();

            lock (_lock)
            {
                try
                {
                    string logFile = Path.Combine(_logDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");
                    File.AppendAllText(logFile, logLine + Environment.NewLine);
                }
                catch { }
            }
        }

        public static void LogHardwareEvent(string hardwareName, string eventType, string details)
        {
            Log(LogLevel.Info, $"[Hardware] {hardwareName} - {eventType}: {details}");
        }

        public static void LogCrash(string appName, string exceptionType, string message)
        {
            Log(LogLevel.Error, $"[Crash] {appName} - {exceptionType}: {message}");
        }

        public static void LogBSOD(string bugCheckCode, string causedByDriver)
        {
            Log(LogLevel.Fatal, $"[BSOD] BugCheck: {bugCheckCode}, Driver: {causedByDriver}");
        }
    }
}
