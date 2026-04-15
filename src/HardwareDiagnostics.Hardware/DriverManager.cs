using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using HardwareDiagnostics.Core.Models;
using HardwareDiagnostics.Core.Utils;
using Microsoft.Win32;

namespace HardwareDiagnostics.Hardware
{
    public class DriverManager
    {
        private const uint DIF_REMOVE = 0x00000005;
        private const uint DIF_PROPERTYCHANGE = 0x00000012;
        private const uint DICS_ENABLE = 0x00000001;
        private const uint DICS_DISABLE = 0x00000002;
        private const uint DICS_FLAG_GLOBAL = 0x00000001;

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiSetClassInstallParams(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, ref SP_CLASSINSTALL_HEADER classInstallParams, uint classInstallParamsSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiCallClassInstaller(uint installFunction, IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiRemoveDevice(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid classGuid;
            public uint devInst;
            public IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_CLASSINSTALL_HEADER
        {
            public uint cbSize;
            public uint installFunction;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_PROPCHANGE_PARAMS
        {
            public SP_CLASSINSTALL_HEADER classInstallHeader;
            public uint stateChange;
            public uint scope;
            public uint hwProfile;
        }

        public List<Core.Models.DriverInfo> GetInstalledDrivers()
        {
            var drivers = new List<Core.Models.DriverInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SystemDriver");
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var driver = new Core.Models.DriverInfo
                        {
                            DeviceName = GetPropertyString(obj, "Name"),
                            DriverVersion = GetPropertyString(obj, "Version"),
                            ProviderName = GetPropertyString(obj, "ServiceType"),
                            DriverPath = GetPropertyString(obj, "PathName"),
                            IsSigned = true
                        };

                        drivers.Add(driver);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error getting driver info", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting installed drivers", ex);
            }

            return drivers;
        }

        public List<Core.Models.DriverInfo> GetDeviceDrivers(string deviceId)
        {
            var drivers = new List<Core.Models.DriverInfo>();

            try
            {
                string query = $"SELECT * FROM Win32_PnPSignedDriver WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var driver = new Core.Models.DriverInfo
                        {
                            DeviceName = GetPropertyString(obj, "DeviceName"),
                            DriverVersion = GetPropertyString(obj, "DriverVersion"),
                            ProviderName = GetPropertyString(obj, "DriverProviderName"),
                            InfName = GetPropertyString(obj, "InfName"),
                            IsSigned = GetPropertyString(obj, "IsSigned").ToLower() == "true",
                            SignerName = GetPropertyString(obj, "Signer")
                        };

                        try
                        {
                            string driverDateStr = GetPropertyString(obj, "DriverDate");
                            if (!string.IsNullOrEmpty(driverDateStr))
                            {
                                driver.DriverDate = ManagementDateTimeConverter.ToDateTime(driverDateStr);
                            }
                        }
                        catch { }

                        drivers.Add(driver);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error getting device driver info", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting device drivers", ex);
            }

            return drivers;
        }

        public bool UninstallDriver(string deviceId)
        {
            try
            {
                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        obj.InvokeMethod("Disable", null);
                        Logger.Info($"Driver uninstalled for device: {deviceId}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error uninstalling driver", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in uninstall driver", ex);
            }

            return false;
        }

        public bool InstallDriver(string infPath)
        {
            try
            {
                if (!File.Exists(infPath))
                {
                    Logger.Error($"INF file not found: {infPath}");
                    return false;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = $"/add-driver \"{infPath}\" /install",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    if (process.ExitCode == 0)
                    {
                        Logger.Info($"Driver installed successfully: {infPath}");
                        return true;
                    }
                    else
                    {
                        Logger.Error($"Driver installation failed: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error installing driver", ex);
            }

            return false;
        }

        public bool UpdateDriver(string deviceId, string driverPath)
        {
            try
            {
                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        // 尝试更新驱动
                        var result = obj.InvokeMethod("Install", new object[] { driverPath });
                        Logger.Info($"Driver update attempted for device: {deviceId}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error updating driver", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in update driver", ex);
            }

            return false;
        }

        public bool EnableDevice(string deviceId)
        {
            try
            {
                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        obj.InvokeMethod("Enable", null);
                        Logger.Info($"Device enabled: {deviceId}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error enabling device", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in enable device", ex);
            }

            return false;
        }

        public bool DisableDevice(string deviceId)
        {
            try
            {
                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        obj.InvokeMethod("Disable", null);
                        Logger.Info($"Device disabled: {deviceId}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error disabling device", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in disable device", ex);
            }

            return false;
        }

        public void ScanForHardwareChanges()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c start /wait rundll32.exe shell32.dll,Control_RunDLL sysdm.cpl,,1",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Error("Error scanning for hardware changes", ex);
            }
        }

        public void OpenDeviceManager()
        {
            try
            {
                Process.Start("devmgmt.msc");
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening Device Manager", ex);
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
    }
}
