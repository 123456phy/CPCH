using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;
using Microsoft.Win32;

namespace HardwareDiagnostics.Security
{
    /// <summary>
    /// USB安全卫士 - 检测BadUSB/变形虫/HID攻击设备
    /// 拦截可疑USB设备，防止自动化攻击
    /// </summary>
    public class USBSecurityGuard : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, ref uint requiredSize, IntPtr deviceInfoData);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid interfaceClassGuid;
            public uint flags;
            public IntPtr reserved;
        }

        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        private static readonly Guid GUID_DEVINTERFACE_USB_DEVICE = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");

        private ManagementEventWatcher? _insertWatcher;
        private ManagementEventWatcher? _removeWatcher;
        private readonly List<USBDeviceInfo> _suspiciousDevices = new();
        private readonly List<USBDeviceInfo> _blockedDevices = new();
        private readonly SecurityLogger _securityLogger;
        private readonly object _lock = new();
        private bool _isRunning;

        // 已知攻击设备VID/PID数据库
        private readonly HashSet<string> _knownBadUSBDevices = new(StringComparer.OrdinalIgnoreCase)
        {
            // Rubber Ducky (Hak5)
            "VID_03EB&PID_2042",
            "VID_03EB&PID_2066",
            // Digispark
            "VID_16D0&PID_0753",
            // Arduino Leonardo (常被用于BadUSB)
            "VID_2341&PID_0036",
            "VID_2341&PID_8036",
            // Teensy
            "VID_16C0&PID_0478",
            "VID_16C0&PID_0483",
            // Malduino
            "VID_1B4F&PID_9206",
            // 其他可疑设备
            "VID_046D&PID_C52B", // Logitech Unifying (可能被劫持)
        };

        // HID键盘设备VID列表（需要特别关注）
        private readonly HashSet<string> _suspiciousHidVendors = new(StringComparer.OrdinalIgnoreCase)
        {
            "VID_03EB", // Atmel (Digispark)
            "VID_16D0", // MCS
            "VID_16C0", // Van Ooijen Technische Informatica (Teensy)
            "VID_2341", // Arduino
            "VID_1B4F", // SparkFun
        };

        public event EventHandler<USBDeviceEventArgs>? SuspiciousDeviceDetected;
        public event EventHandler<USBDeviceEventArgs>? DeviceBlocked;
        public event EventHandler<USBDeviceEventArgs>? DeviceAllowed;

        public bool IsRunning => _isRunning;
        public IReadOnlyList<USBDeviceInfo> SuspiciousDevices => _suspiciousDevices.AsReadOnly();
        public IReadOnlyList<USBDeviceInfo> BlockedDevices => _blockedDevices.AsReadOnly();

        public USBSecurityGuard()
        {
            _securityLogger = new SecurityLogger();
        }

        public void StartMonitoring()
        {
            lock (_lock)
            {
                if (_isRunning) return;

                try
                {
                    // 创建设备插入事件监视器
                    var insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
                    _insertWatcher = new ManagementEventWatcher(insertQuery);
                    _insertWatcher.EventArrived += OnDeviceInserted;
                    _insertWatcher.Start();

                    // 创建设备移除事件监视器
                    var removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
                    _removeWatcher = new ManagementEventWatcher(removeQuery);
                    _removeWatcher.EventArrived += OnDeviceRemoved;
                    _removeWatcher.Start();

                    _isRunning = true;
                    Logger.Info("USB Security Guard started");
                    _securityLogger.LogSecurityEvent(SecurityEventType.USBGuardStarted, "USB安全卫士已启动");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to start USB security guard", ex);
                    throw;
                }
            }
        }

        public void StopMonitoring()
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                _insertWatcher?.Stop();
                _insertWatcher?.Dispose();
                _insertWatcher = null;

                _removeWatcher?.Stop();
                _removeWatcher?.Dispose();
                _removeWatcher = null;

                _isRunning = false;
                Logger.Info("USB Security Guard stopped");
                _securityLogger.LogSecurityEvent(SecurityEventType.USBGuardStopped, "USB安全卫士已停止");
            }
        }

        private void OnDeviceInserted(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var targetInstance = e.NewEvent["TargetInstance"] as ManagementBaseObject;
                if (targetInstance == null) return;

                string deviceId = targetInstance["DeviceID"]?.ToString() ?? "";
                string deviceClass = targetInstance["PNPClass"]?.ToString() ?? "";
                string deviceName = targetInstance["Name"]?.ToString() ?? "Unknown Device";

                // 只关注USB设备
                if (!IsUSBDevice(deviceClass, deviceId)) return;

                Logger.Info($"USB device inserted: {deviceName} ({deviceId})");

                // 分析设备
                var deviceInfo = AnalyzeDevice(targetInstance);

                // 评估风险
                var risk = EvaluateRisk(deviceInfo);

                if (risk.Level >= RiskLevel.High)
                {
                    HandleSuspiciousDevice(deviceInfo, risk);
                }
                else
                {
                    // 低风险设备，允许使用
                    deviceInfo.Status = USBDeviceStatus.Allowed;
                    DeviceAllowed?.Invoke(this, new USBDeviceEventArgs { Device = deviceInfo });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error processing USB device insertion", ex);
            }
        }

        private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var targetInstance = e.NewEvent["TargetInstance"] as ManagementBaseObject;
                if (targetInstance == null) return;

                string deviceId = targetInstance["DeviceID"]?.ToString() ?? "";
                Logger.Debug($"USB device removed: {deviceId}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error processing USB device removal", ex);
            }
        }

        private USBDeviceInfo AnalyzeDevice(ManagementBaseObject device)
        {
            var info = new USBDeviceInfo
            {
                DeviceId = device["DeviceID"]?.ToString() ?? "",
                Name = device["Name"]?.ToString() ?? "Unknown Device",
                Description = device["Description"]?.ToString() ?? "",
                Manufacturer = device["Manufacturer"]?.ToString() ?? "",
                DeviceClass = device["PNPClass"]?.ToString() ?? "",
                HardwareId = device["HardwareID"]?.ToString() ?? "",
                InsertTime = DateTime.Now,
                Status = USBDeviceStatus.Pending
            };

            // 解析VID和PID
            ParseVidPid(info);

            // 检测设备类型
            DetectDeviceType(info);

            // 获取额外属性
            GetDeviceProperties(info);

            return info;
        }

        private void ParseVidPid(USBDeviceInfo device)
        {
            try
            {
                // 从DeviceID解析VID和PID
                // 格式: USB\VID_XXXX&PID_YYYY\...
                var parts = device.DeviceId.Split('\\');
                if (parts.Length >= 2)
                {
                    var vidPidPart = parts[1];
                    var vidMatch = System.Text.RegularExpressions.Regex.Match(vidPidPart, @"VID_([0-9A-F]{4})");
                    var pidMatch = System.Text.RegularExpressions.Regex.Match(vidPidPart, @"PID_([0-9A-F]{4})");

                    if (vidMatch.Success)
                        device.VID = vidMatch.Groups[1].Value;
                    if (pidMatch.Success)
                        device.PID = pidMatch.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error parsing VID/PID: {ex.Message}");
            }
        }

        private void DetectDeviceType(USBDeviceInfo device)
        {
            string deviceClass = device.DeviceClass.ToLower();
            string name = device.Name.ToLower();

            if (deviceClass.Contains("keyboard") || name.Contains("keyboard"))
                device.Type = USBDeviceType.Keyboard;
            else if (deviceClass.Contains("mouse") || name.Contains("mouse"))
                device.Type = USBDeviceType.Mouse;
            else if (deviceClass.Contains("hid") && name.Contains("keyboard"))
                device.Type = USBDeviceType.HIDKeyboard;
            else if (name.Contains("storage") || deviceClass.Contains("disk"))
                device.Type = USBDeviceType.Storage;
            else if (deviceClass.Contains("hub"))
                device.Type = USBDeviceType.Hub;
            else if (name.Contains("arduino") || device.Manufacturer?.ToLower().Contains("arduino") == true)
                device.Type = USBDeviceType.Arduino;
            else
                device.Type = USBDeviceType.Other;
        }

        private void GetDeviceProperties(USBDeviceInfo device)
        {
            try
            {
                // 查询注册表获取更多信息
                string regPath = $"SYSTEM\\CurrentControlSet\\Enum\\USB\\{device.VID}_{device.PID}";
                using var key = Registry.LocalMachine.OpenSubKey(regPath);
                if (key != null)
                {
                    device.FriendlyName = key.GetValue("FriendlyName")?.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error getting device properties: {ex.Message}");
            }
        }

        private RiskAssessment EvaluateRisk(USBDeviceInfo device)
        {
            var assessment = new RiskAssessment { Device = device };
            var factors = new List<RiskFactor>();

            // 1. 检查是否在已知恶意设备列表
            string vidPid = $"VID_{device.VID}&PID_{device.PID}";
            if (_knownBadUSBDevices.Contains(vidPid))
            {
                factors.Add(new RiskFactor
                {
                    Type = RiskFactorType.KnownBadUSB,
                    Level = RiskLevel.Critical,
                    Description = "设备在已知BadUSB攻击设备数据库中"
                });
            }

            // 2. 检查是否是可疑HID键盘
            if (device.Type == USBDeviceType.HIDKeyboard || device.Type == USBDeviceType.Keyboard)
            {
                if (_suspiciousHidVendors.Contains($"VID_{device.VID}"))
                {
                    factors.Add(new RiskFactor
                    {
                        Type = RiskFactorType.SuspiciousHID,
                        Level = RiskLevel.High,
                        Description = $"HID键盘来自可疑厂商: {device.Manufacturer}"
                    });
                }

                // 检查是否是新出现的键盘（可能的攻击）
                if (IsNewKeyboardDevice(device))
                {
                    factors.Add(new RiskFactor
                    {
                        Type = RiskFactorType.NewKeyboard,
                        Level = RiskLevel.Medium,
                        Description = "检测到新的键盘设备"
                    });
                }
            }

            // 3. 检查是否是Arduino/开发板
            if (device.Type == USBDeviceType.Arduino)
            {
                factors.Add(new RiskFactor
                {
                    Type = RiskFactorType.DevelopmentBoard,
                    Level = RiskLevel.High,
                    Description = "检测到开发板设备，可能被用于BadUSB攻击"
                });
            }

            // 4. 检查设备名称异常
            if (IsSuspiciousDeviceName(device.Name))
            {
                factors.Add(new RiskFactor
                {
                    Type = RiskFactorType.SuspiciousName,
                    Level = RiskLevel.Medium,
                    Description = "设备名称包含可疑关键词"
                });
            }

            // 5. 检查复合设备（可能是HID+存储）
            if (device.DeviceClass.ToLower().Contains("composite"))
            {
                factors.Add(new RiskFactor
                {
                    Type = RiskFactorType.CompositeDevice,
                    Level = RiskLevel.Medium,
                    Description = "检测到复合USB设备"
                });
            }

            assessment.Factors = factors;
            assessment.Level = factors.Count > 0 ? factors.Max(f => f.Level) : RiskLevel.Low;

            return assessment;
        }

        private bool IsNewKeyboardDevice(USBDeviceInfo device)
        {
            // 检查是否是今天第一次插入的键盘
            // 实际实现可以查询历史记录
            return true; // 简化处理，实际应该查询数据库
        }

        private bool IsSuspiciousDeviceName(string name)
        {
            string[] suspiciousKeywords = { "rubber", "ducky", "badusb", "payload", "injector", "teensy" };
            string lowerName = name.ToLower();
            return suspiciousKeywords.Any(k => lowerName.Contains(k));
        }

        private void HandleSuspiciousDevice(USBDeviceInfo device, RiskAssessment risk)
        {
            device.Status = USBDeviceStatus.Suspicious;
            device.RiskLevel = risk.Level;
            device.RiskDescription = string.Join("; ", risk.Factors.Select(f => f.Description));

            lock (_suspiciousDevices)
            {
                _suspiciousDevices.Add(device);
            }

            // 记录安全日志
            _securityLogger.LogUSBEvent(device, risk);

            // 触发事件
            SuspiciousDeviceDetected?.Invoke(this, new USBDeviceEventArgs { Device = device, Risk = risk });

            Logger.Warning($"[SECURITY] Suspicious USB device detected: {device.Name} ({device.VID}:{device.PID}) - Risk: {risk.Level}");

            // 自动阻断高危设备
            if (risk.Level >= RiskLevel.Critical)
            {
                BlockDevice(device);
            }
        }

        public void BlockDevice(USBDeviceInfo device)
        {
            try
            {
                // 禁用设备
                DisableDevice(device.DeviceId);

                device.Status = USBDeviceStatus.Blocked;
                lock (_blockedDevices)
                {
                    _blockedDevices.Add(device);
                }

                _securityLogger.LogSecurityEvent(SecurityEventType.USBDeviceBlocked,
                    $"USB设备被阻断: {device.Name} ({device.VID}:{device.PID})");

                DeviceBlocked?.Invoke(this, new USBDeviceEventArgs { Device = device });

                Logger.Info($"USB device blocked: {device.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error blocking USB device", ex);
            }
        }

        public void AllowDevice(USBDeviceInfo device)
        {
            try
            {
                // 启用设备
                EnableDevice(device.DeviceId);

                device.Status = USBDeviceStatus.Allowed;

                _securityLogger.LogSecurityEvent(SecurityEventType.USBDeviceAllowed,
                    $"USB设备被允许: {device.Name} ({device.VID}:{device.PID})");

                DeviceAllowed?.Invoke(this, new USBDeviceEventArgs { Device = device });

                Logger.Info($"USB device allowed: {device.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error allowing USB device", ex);
            }
        }

        private void DisableDevice(string deviceId)
        {
            try
            {
                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    obj.InvokeMethod("Disable", null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error disabling device", ex);
            }
        }

        private void EnableDevice(string deviceId)
        {
            try
            {
                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    obj.InvokeMethod("Enable", null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error enabling device", ex);
            }
        }

        private bool IsUSBDevice(string deviceClass, string deviceId)
        {
            return deviceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) ||
                   deviceClass.ToLower().Contains("usb");
        }

        public void Dispose()
        {
            StopMonitoring();
            _securityLogger?.Dispose();
        }
    }

    public class USBDeviceInfo
    {
        public string DeviceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string DeviceName => Name;
        public string VIDPID => $"{VID}:{PID}";
        public string Description { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string DeviceClass { get; set; } = "";
        public string HardwareId { get; set; } = "";
        public string FriendlyName { get; set; } = "";
        public string VID { get; set; } = "";
        public string PID { get; set; } = "";
        public USBDeviceType Type { get; set; }
        public USBDeviceStatus Status { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public string RiskDescription { get; set; } = "";
        public DateTime InsertTime { get; set; }
    }

    public enum USBDeviceType
    {
        Keyboard,
        HIDKeyboard,
        Mouse,
        Storage,
        Hub,
        Arduino,
        Other
    }

    public enum USBDeviceStatus
    {
        Pending,
        Allowed,
        Suspicious,
        Blocked
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class RiskAssessment
    {
        public USBDeviceInfo Device { get; set; } = null!;
        public RiskLevel Level { get; set; }
        public List<RiskFactor> Factors { get; set; } = new();
    }

    public class RiskFactor
    {
        public RiskFactorType Type { get; set; }
        public RiskLevel Level { get; set; }
        public string Description { get; set; } = "";
    }

    public enum RiskFactorType
    {
        KnownBadUSB,
        SuspiciousHID,
        NewKeyboard,
        DevelopmentBoard,
        SuspiciousName,
        CompositeDevice
    }

    public class USBDeviceEventArgs : EventArgs
    {
        public USBDeviceInfo Device { get; set; } = null!;
        public RiskAssessment? Risk { get; set; }
    }
}
