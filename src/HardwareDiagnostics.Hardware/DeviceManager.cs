using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using HardwareDiagnostics.Core.Models;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.Hardware
{
    public class DeviceManager
    {
        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_ALLCLASSES = 0x00000004;
        private const uint SPDRP_DEVICEDESC = 0x00000000;
        private const uint SPDRP_HARDWAREID = 0x00000001;
        private const uint SPDRP_COMPATIBLEIDS = 0x00000002;
        private const uint SPDRP_SERVICE = 0x00000004;
        private const uint SPDRP_CLASS = 0x00000007;
        private const uint SPDRP_CLASSGUID = 0x00000008;
        private const uint SPDRP_DRIVER = 0x00000009;
        private const uint SPDRP_MFG = 0x0000000B;
        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
        private const uint SPDRP_LOCATION_INFORMATION = 0x0000000D;
        private const uint SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E;
        private const uint SPDRP_CAPABILITIES = 0x0000000F;
        private const uint SPDRP_UI_NUMBER = 0x00000010;
        private const uint SPDRP_UPPERFILTERS = 0x00000011;
        private const uint SPDRP_LOWERFILTERS = 0x00000012;
        private const uint SPDRP_BUSTYPEGUID = 0x00000013;
        private const uint SPDRP_LEGACYBUSTYPE = 0x00000014;
        private const uint SPDRP_BUSNUMBER = 0x00000015;
        private const uint SPDRP_ENUMERATOR_NAME = 0x00000016;
        private const uint SPDRP_SECURITY = 0x00000017;
        private const uint SPDRP_SECURITY_SDS = 0x00000018;
        private const uint SPDRP_DEVTYPE = 0x00000019;
        private const uint SPDRP_EXCLUSIVE = 0x0000001A;
        private const uint SPDRP_CHARACTERISTICS = 0x0000001B;
        private const uint SPDRP_ADDRESS = 0x0000001C;
        private const uint SPDRP_UI_NUMBER_DESC_FORMAT = 0x0000001D;
        private const uint SPDRP_DEVICE_POWER_DATA = 0x0000001E;
        private const uint SPDRP_REMOVAL_POLICY = 0x0000001F;
        private const uint SPDRP_REMOVAL_POLICY_HW_DEFAULT = 0x00000020;
        private const uint SPDRP_REMOVAL_POLICY_OVERRIDE = 0x00000021;
        private const uint SPDRP_INSTALL_STATE = 0x00000022;
        private const uint SPDRP_LOCATION_PATHS = 0x00000023;

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint property, out uint propertyRegDataType, byte[] propertyBuffer, uint propertyBufferSize, out uint requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, byte[] deviceInstanceId, uint deviceInstanceIdSize, out uint requiredSize);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint property, out uint propertyRegDataType, IntPtr propertyBuffer, uint propertyBufferSize, out uint requiredSize);

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

        public List<HardwareInfo> GetAllDevices()
        {
            var devices = new List<HardwareInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var device = ConvertToHardwareInfo(obj);
                        if (device != null)
                        {
                            devices.Add(device);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error converting device", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting devices from WMI", ex);
            }

            return devices;
        }

        public List<HardwareInfo> GetDevicesByClass(string deviceClass)
        {
            var devices = new List<HardwareInfo>();

            try
            {
                string query = $"SELECT * FROM Win32_PnPEntity WHERE ClassGuid LIKE '%{deviceClass}%' OR PNPClass = '{deviceClass}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var device = ConvertToHardwareInfo(obj);
                        if (device != null)
                        {
                            devices.Add(device);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error converting device", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting devices by class", ex);
            }

            return devices;
        }

        public HardwareInfo? GetDeviceById(string deviceId)
        {
            try
            {
                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    return ConvertToHardwareInfo(obj);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting device by ID", ex);
            }

            return null;
        }

        public List<HardwareInfo> GetDevicesWithProblems()
        {
            var devices = new List<HardwareInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE ConfigManagerErrorCode != 0");
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var device = ConvertToHardwareInfo(obj);
                        if (device != null && device.Status != HardwareStatus.Normal)
                        {
                            devices.Add(device);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error converting device with problem", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting devices with problems", ex);
            }

            return devices;
        }

        private HardwareInfo? ConvertToHardwareInfo(ManagementObject obj)
        {
            try
            {
                var device = new HardwareInfo
                {
                    Name = GetPropertyString(obj, "Name"),
                    Description = GetPropertyString(obj, "Description"),
                    DeviceId = GetPropertyString(obj, "DeviceID"),
                    Manufacturer = GetPropertyString(obj, "Manufacturer"),
                    HardwareId = GetPropertyString(obj, "HardwareID"),
                    LocationInfo = GetPropertyString(obj, "LocationInformation"),
                    LastScanTime = DateTime.Now
                };

                string pnpClass = GetPropertyString(obj, "PNPClass");
                device.Type = GetHardwareType(pnpClass);

                uint statusCode = GetPropertyUInt(obj, "ConfigManagerErrorCode");
                device.Status = GetHardwareStatus(statusCode);

                string[] hardwareIds = obj["HardwareID"] as string[] ?? Array.Empty<string>();
                device.HardwareIds = hardwareIds.ToList();

                return device;
            }
            catch (Exception ex)
            {
                Logger.Error("Error converting ManagementObject to HardwareInfo", ex);
                return null;
            }
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

        private static HardwareType GetHardwareType(string pnpClass)
        {
            return pnpClass?.ToLower() switch
            {
                "processor" => HardwareType.Processor,
                "memory" => HardwareType.Memory,
                "hdc" or "scsiadapter" or "diskdrive" => HardwareType.Storage,
                "display" => HardwareType.GraphicsCard,
                "net" => HardwareType.Network,
                "media" => HardwareType.Audio,
                "usb" => HardwareType.USB,
                "bluetooth" => HardwareType.Bluetooth,
                "system" => HardwareType.Motherboard,
                _ => HardwareType.Other
            };
        }

        private static HardwareStatus GetHardwareStatus(uint errorCode)
        {
            return errorCode switch
            {
                0 => HardwareStatus.Normal,
                1 or 3 or 10 or 18 or 19 or 21 or 22 or 24 or 28 or 29 or 31 => HardwareStatus.Error,
                2 or 12 or 14 or 34 => HardwareStatus.Disabled,
                _ => HardwareStatus.Warning
            };
        }

        public static string GetStatusDescription(uint errorCode)
        {
            return errorCode switch
            {
                0 => "设备运行正常",
                1 => "设备未正确配置",
                2 => "Windows 无法加载该设备的驱动程序",
                3 => "该设备的驱动程序可能已损坏",
                10 => "设备无法启动",
                12 => "该设备无法找到足够的可用资源",
                14 => "设备无法正常工作",
                18 => "需要重新安装该设备的驱动程序",
                19 => "Windows 无法启动这个硬件设备",
                21 => "Windows 正在删除该设备",
                22 => "设备被禁用",
                28 => "该设备的驱动程序未安装",
                29 => "设备被固件禁用",
                31 => "该设备无法正常工作",
                _ => $"未知错误代码: {errorCode}"
            };
        }
    }
}
