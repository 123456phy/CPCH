using System;
using System.IO;
using System.Xml.Serialization;

namespace HardwareDiagnostics.Core.Utils
{
    /// <summary>
    /// 应用设置 - XML 持久化到 %AppData%\CPCH\settings.xml，重启后生效。
    /// 所有读写都带 try/catch，文件坏了就回默认值，绝不崩主程序。
    /// </summary>
    [Serializable]
    public class AppSettings
    {
        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CPCH");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.xml");

        private static AppSettings? _current;
        private static readonly object _lock = new();

        /// <summary>全局单例（首次访问时从磁盘加载）。</summary>
        public static AppSettings Current
        {
            get
            {
                lock (_lock)
                {
                    if (_current == null)
                        _current = Load();
                    return _current;
                }
            }
        }

        // ---------- 常规 ----------
        public bool AutoStartWithWindows { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;

        // ---------- 通知 ----------
        public bool EnableToast { get; set; } = true;
        public bool EnableTrayBalloon { get; set; } = true;
        public int ToastSeconds { get; set; } = 4;

        // ---------- 监控 ----------
        public int RamCheckIntervalSeconds { get; set; } = 10;
        public bool ShowWidgetOnStart { get; set; } = false;
        public bool WidgetTopMost { get; set; } = true;

        // ---------- DISM ----------
        public string DismSourcePath { get; set; } = "";

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var ser = new XmlSerializer(typeof(AppSettings));
                    using var fs = File.OpenRead(SettingsFile);
                    if (ser.Deserialize(fs) is AppSettings s)
                    {
                        s.Normalize();
                        return s;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"读取设置失败，使用默认值：{ex.Message}");
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Normalize();
                Directory.CreateDirectory(SettingsDir);
                var ser = new XmlSerializer(typeof(AppSettings));
                string tmp = SettingsFile + ".tmp";
                using (var fs = File.Create(tmp))
                {
                    ser.Serialize(fs, this);
                }
                File.Copy(tmp, SettingsFile, true);
                File.Delete(tmp);
            }
            catch (Exception ex)
            {
                Logger.Warning($"保存设置失败：{ex.Message}");
            }
        }

        /// <summary>重载磁盘文件（设置页保存后调用，刷新单例）。</summary>
        public static void Reload()
        {
            lock (_lock) { _current = Load(); }
        }

        private void Normalize()
        {
            if (RamCheckIntervalSeconds < 5) RamCheckIntervalSeconds = 5;
            if (RamCheckIntervalSeconds > 3600) RamCheckIntervalSeconds = 3600;
            if (ToastSeconds < 2) ToastSeconds = 2;
            if (ToastSeconds > 30) ToastSeconds = 30;
            if (DismSourcePath == null) DismSourcePath = "";
        }
    }
}
