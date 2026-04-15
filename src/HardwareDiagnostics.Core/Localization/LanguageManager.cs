using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace HardwareDiagnostics.Core.Localization
{
    public enum Language
    {
        Chinese,
        English
    }

    public static class LanguageManager
    {
        private static Language _currentLanguage = Language.Chinese;
        private static readonly Dictionary<string, Dictionary<Language, string>> _translations = new();

        public static Language CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                _currentLanguage = value;
                Thread.CurrentThread.CurrentUICulture = value == Language.Chinese 
                    ? new CultureInfo("zh-CN") 
                    : new CultureInfo("en-US");
                LanguageChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static event EventHandler? LanguageChanged;

        static LanguageManager()
        {
            InitializeTranslations();
        }

        private static void InitializeTranslations()
        {
            // 主窗口
            AddTranslation("AppTitle", "硬件检测与系统维护工具", "Hardware Diagnostics & System Maintenance");
            AddTranslation("AuthorCredit", "此软件由 furry 皓予 vibe coding", "This software by furry Haoyu vibe coding");
            
            // 菜单
            AddTranslation("MenuHardware", "硬件检测", "Hardware Detection");
            AddTranslation("MenuSystem", "系统维护", "System Maintenance");
            AddTranslation("MenuMonitoring", "监控中心", "Monitoring Center");
            AddTranslation("MenuTools", "工具箱", "Toolbox");
            AddTranslation("MenuSettings", "设置", "Settings");
            AddTranslation("MenuHelp", "帮助", "Help");
            
            // 硬件检测
            AddTranslation("HardwareDeviceManager", "设备管理器集成", "Device Manager Integration");
            AddTranslation("HardwareCustomScan", "自定义硬件扫描", "Custom Hardware Scan");
            AddTranslation("HardwareStatus", "硬件状态", "Hardware Status");
            AddTranslation("HardwareDrivers", "驱动管理", "Driver Management");
            AddTranslation("HardwareHistory", "历史记录", "History");
            
            // 系统维护
            AddTranslation("SystemVCRuntime", "VC++运行库", "VC++ Runtime");
            AddTranslation("SystemDism", "DISM工具", "DISM Tool");
            AddTranslation("SystemErrorParser", "错误解析", "Error Parser");
            
            // 监控中心
            AddTranslation("MonitorAppCrash", "应用崩溃监控", "App Crash Monitor");
            AddTranslation("MonitorBSOD", "蓝屏检测", "BSOD Detection");
            AddTranslation("MonitorRealtime", "实时监控", "Real-time Monitor");
            
            // 通用按钮
            AddTranslation("BtnScan", "扫描", "Scan");
            AddTranslation("BtnRefresh", "刷新", "Refresh");
            AddTranslation("BtnInstall", "安装", "Install");
            AddTranslation("BtnUninstall", "卸载", "Uninstall");
            AddTranslation("BtnExport", "导出", "Export");
            AddTranslation("BtnAnalyze", "分析", "Analyze");
            AddTranslation("BtnStart", "开始", "Start");
            AddTranslation("BtnStop", "停止", "Stop");
            AddTranslation("BtnBrowse", "浏览", "Browse");
            AddTranslation("BtnClear", "清除", "Clear");
            
            // 状态
            AddTranslation("StatusNormal", "正常", "Normal");
            AddTranslation("StatusWarning", "警告", "Warning");
            AddTranslation("StatusError", "错误", "Error");
            AddTranslation("StatusUnknown", "未知", "Unknown");
            AddTranslation("StatusRunning", "运行中", "Running");
            AddTranslation("StatusStopped", "已停止", "Stopped");
        }

        private static void AddTranslation(string key, string chinese, string english)
        {
            _translations[key] = new Dictionary<Language, string>
            {
                { Language.Chinese, chinese },
                { Language.English, english }
            };
        }

        public static string GetString(string key)
        {
            if (_translations.TryGetValue(key, out var dict))
            {
                return dict.TryGetValue(_currentLanguage, out var value) ? value : dict[Language.Chinese];
            }
            return $"[{key}]";
        }

        public static string GetString(string key, params object[] args)
        {
            return string.Format(GetString(key), args);
        }
    }
}
