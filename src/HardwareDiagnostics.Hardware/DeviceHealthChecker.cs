using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using HardwareDiagnostics.Core.Models;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.Hardware
{
    public class DeviceHealthChecker
    {
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint property, out uint propertyRegDataType, byte[] propertyBuffer, uint propertyBufferSize, out uint requiredSize);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        private static extern int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid classGuid;
            public uint devInst;
            public IntPtr reserved;
        }

        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_ALLCLASSES = 0x00000004;
        private const uint SPDRP_DEVICEDESC = 0x00000000;
        private const uint SPDRP_HARDWAREID = 0x00000001;
        private const uint SPDRP_SERVICE = 0x00000004;
        private const uint SPDRP_CLASS = 0x00000007;
        private const uint SPDRP_DRIVER = 0x00000009;
        private const uint SPDRP_MFG = 0x0000000B;
        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;

        public List<DeviceHealthInfo> CheckAllDevicesHealth()
        {
            var devices = new List<DeviceHealthInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var device = ConvertToHealthInfo(obj);
                        if (device != null)
                        {
                            devices.Add(device);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error converting device health info", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error checking devices health", ex);
            }

            return devices;
        }

        public List<DeviceHealthInfo> GetProblematicDevices()
        {
            return CheckAllDevicesHealth()
                .Where(d => d.Status != DeviceHealthStatus.Normal)
                .OrderByDescending(d => d.Status == DeviceHealthStatus.Error ? 2 : (d.Status == DeviceHealthStatus.Warning ? 1 : 0))
                .ToList();
        }

        public DeviceHealthInfo GetDeviceHealth(string deviceId)
        {
            try
            {
                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    return ConvertToHealthInfo(obj);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting device health", ex);
            }

            return null;
        }

        private DeviceHealthInfo ConvertToHealthInfo(ManagementObject obj)
        {
            try
            {
                uint errorCode = GetPropertyUInt(obj, "ConfigManagerErrorCode");
                var status = GetDeviceHealthStatus(errorCode);

                var device = new DeviceHealthInfo
                {
                    DeviceId = GetPropertyString(obj, "DeviceID"),
                    Name = GetPropertyString(obj, "Name"),
                    Description = GetPropertyString(obj, "Description"),
                    Manufacturer = GetPropertyString(obj, "Manufacturer"),
                    HardwareId = GetPropertyString(obj, "HardwareID"),
                    PNPClass = GetPropertyString(obj, "PNPClass"),
                    Service = GetPropertyString(obj, "Service"),
                    Status = status,
                    ErrorCode = errorCode,
                    ErrorDescription = GetErrorDescription(errorCode),
                    IsPresent = GetPropertyString(obj, "Status") != "Error",
                    LastChecked = DateTime.Now
                };

                // 获取驱动信息
                device.DriverInfo = GetDriverInfo(obj);

                // 获取设备能力
                device.Capabilities = GetDeviceCapabilities(obj);

                return device;
            }
            catch (Exception ex)
            {
                Logger.Error("Error converting to health info", ex);
                return null;
            }
        }

        private DriverHealthInfo GetDriverInfo(ManagementObject obj)
        {
            var driverInfo = new DriverHealthInfo();

            try
            {
                driverInfo.DriverVersion = GetPropertyString(obj, "DriverVersion");
                driverInfo.DriverDate = GetPropertyString(obj, "DriverDate");
                driverInfo.IsSigned = true; // 默认假设已签名

                // 检查驱动是否存在
                string driverPath = GetPropertyString(obj, "Driver");
                driverInfo.IsInstalled = !string.IsNullOrEmpty(driverPath);

                // 检查驱动是否最新（简化检查）
                driverInfo.IsUpToDate = CheckDriverUpToDate(driverInfo.DriverVersion, driverInfo.DriverDate);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error getting driver info: {ex.Message}");
            }

            return driverInfo;
        }

        private DeviceCapabilities GetDeviceCapabilities(ManagementObject obj)
        {
            var caps = new DeviceCapabilities();

            try
            {
                var capabilities = obj["Capabilities"] as ushort[];
                if (capabilities != null)
                {
                    caps.CanBeDisabled = capabilities.Contains((ushort)1);
                    caps.CanBeRemoved = capabilities.Contains((ushort)2);
                    caps.HasPowerManagement = capabilities.Contains((ushort)3);
                    caps.SupportsWakeUp = capabilities.Contains((ushort)4);
                }

                caps.IsDisabled = GetPropertyString(obj, "Status") == "Degraded";
                caps.IsRemoved = GetPropertyString(obj, "Status") == "Error";
            }
            catch { }

            return caps;
        }

        private bool CheckDriverUpToDate(string version, string date)
        {
            // 简化检查：如果驱动日期超过2年，可能不是最新的
            if (string.IsNullOrEmpty(date))
                return false;

            try
            {
                var driverDate = ManagementDateTimeConverter.ToDateTime(date);
                return (DateTime.Now - driverDate).TotalDays < 730; // 2年
            }
            catch
            {
                return true;
            }
        }

        private static DeviceHealthStatus GetDeviceHealthStatus(uint errorCode)
        {
            return errorCode switch
            {
                0 => DeviceHealthStatus.Normal,
                1 or 3 or 10 or 18 or 19 or 21 or 22 or 24 or 28 or 29 or 31 => DeviceHealthStatus.Error,
                2 or 12 or 14 or 34 => DeviceHealthStatus.Disabled,
                _ => DeviceHealthStatus.Warning
            };
        }

        private static string GetErrorDescription(uint errorCode)
        {
            return errorCode switch
            {
                0 => "设备运行正常",
                1 => "设备未正确配置",
                2 => "Windows无法加载该设备的驱动程序",
                3 => "该设备的驱动程序可能已损坏",
                10 => "设备无法启动",
                12 => "该设备无法找到足够的可用资源",
                14 => "设备无法正常工作",
                18 => "需要重新安装该设备的驱动程序",
                19 => "Windows无法启动这个硬件设备",
                21 => "Windows正在删除该设备",
                22 => "设备被禁用",
                28 => "该设备的驱动程序未安装",
                29 => "设备被固件禁用",
                31 => "该设备无法正常工作",
                34 => "设备需要手动配置",
                _ => $"未知错误代码: {errorCode}"
            };
        }

        private static string GetPropertyString(ManagementObject obj, string propertyName)
        {
            try
            {
                var value = obj[propertyName];
                return value?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static uint GetPropertyUInt(ManagementObject obj, string propertyName)
        {
            try
            {
                var value = obj[propertyName];
                return value != null ? Convert.ToUInt32(value) : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    public class DeviceHealthInfo
    {
        public string DeviceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string HardwareId { get; set; } = "";
        public string PNPClass { get; set; } = "";
        public string Service { get; set; } = "";
        public DeviceHealthStatus Status { get; set; }
        public uint ErrorCode { get; set; }
        public string ErrorDescription { get; set; } = "";
        public bool IsPresent { get; set; }
        public DateTime LastChecked { get; set; }
        public DriverHealthInfo DriverInfo { get; set; } = new();
        public DeviceCapabilities Capabilities { get; set; } = new();
    }

    public enum DeviceHealthStatus
    {
        Normal,     // 正常 - 绿色
        Warning,    // 警告 - 黄色
        Error,      // 错误 - 红色
        Disabled,   // 已禁用 - 灰色
        Unknown     // 未知 - 黑色
    }

    public class DriverHealthInfo
    {
        public string DriverVersion { get; set; } = "";
        public string DriverDate { get; set; } = "";
        public string DriverProvider { get; set; } = "";
        public bool IsInstalled { get; set; }
        public bool IsSigned { get; set; }
        public bool IsUpToDate { get; set; }
        public string DownloadUrl { get; set; } = "";
    }

    public class DeviceCapabilities
    {
        public bool CanBeDisabled { get; set; }
        public bool CanBeRemoved { get; set; }
        public bool HasPowerManagement { get; set; }
        public bool SupportsWakeUp { get; set; }
        public bool IsDisabled { get; set; }
        public bool IsRemoved { get; set; }
    }
}
