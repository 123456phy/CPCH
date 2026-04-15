using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using HardwareDiagnostics.Core.Models;
using HardwareDiagnostics.Core.Utils;
using Microsoft.Win32;

namespace HardwareDiagnostics.Hardware
{
    public class CustomHardwareScanner
    {
        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll")]
        private static extern void GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        public List<HardwareInfo> ScanAllHardware()
        {
            var hardware = new List<HardwareInfo>();

            hardware.AddRange(ScanProcessors());
            hardware.AddRange(ScanMemory());
            hardware.AddRange(ScanMotherboard());
            hardware.AddRange(ScanStorage());
            hardware.AddRange(ScanGraphicsCards());
            hardware.AddRange(ScanNetworkAdapters());
            hardware.AddRange(ScanAudioDevices());
            hardware.AddRange(ScanUSBControllers());

            return hardware;
        }

        public List<HardwareInfo> ScanProcessors()
        {
            var processors = new List<HardwareInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var processor = new HardwareInfo
                    {
                        Type = HardwareType.Processor,
                        Name = GetPropertyString(obj, "Name"),
                        Description = GetPropertyString(obj, "Description"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer"),
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Status = GetPropertyUInt(obj, "ConfigManagerErrorCode") == 0 ? HardwareStatus.Normal : HardwareStatus.Error,
                        LastScanTime = DateTime.Now
                    };

                    processor.Properties["Architecture"] = GetPropertyString(obj, "Architecture");
                    processor.Properties["NumberOfCores"] = GetPropertyString(obj, "NumberOfCores");
                    processor.Properties["NumberOfLogicalProcessors"] = GetPropertyString(obj, "NumberOfLogicalProcessors");
                    processor.Properties["MaxClockSpeed"] = GetPropertyString(obj, "MaxClockSpeed");
                    processor.Properties["CurrentClockSpeed"] = GetPropertyString(obj, "CurrentClockSpeed");
                    processor.Properties["L2CacheSize"] = GetPropertyString(obj, "L2CacheSize");
                    processor.Properties["L3CacheSize"] = GetPropertyString(obj, "L3CacheSize");
                    processor.Properties["SocketDesignation"] = GetPropertyString(obj, "SocketDesignation");

                    processors.Add(processor);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error scanning processors", ex);
            }

            return processors;
        }

        public List<HardwareInfo> ScanMemory()
        {
            var memory = new List<HardwareInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
                int index = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    var mem = new HardwareInfo
                    {
                        Type = HardwareType.Memory,
                        Name = $"内存条 {++index}",
                        Description = GetPropertyString(obj, "Description"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer"),
                        DeviceId = GetPropertyString(obj, "Tag"),
                        Status = HardwareStatus.Normal,
                        LastScanTime = DateTime.Now
                    };

                    ulong capacity = 0;
                    try
                    {
                        capacity = Convert.ToUInt64(obj["Capacity"]);
                    }
                    catch { }

                    mem.Properties["Capacity"] = $"{capacity / (1024 * 1024 * 1024)} GB";
                    mem.Properties["Speed"] = GetPropertyString(obj, "Speed");
                    mem.Properties["MemoryType"] = GetPropertyString(obj, "MemoryType");
                    mem.Properties["FormFactor"] = GetPropertyString(obj, "FormFactor");
                    mem.Properties["BankLabel"] = GetPropertyString(obj, "BankLabel");

                    memory.Add(mem);
                }

                // 获取总内存信息
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(memStatus);
                GlobalMemoryStatusEx(ref memStatus);

                var totalMem = new HardwareInfo
                {
                    Type = HardwareType.Memory,
                    Name = "系统总内存",
                    Description = $"物理内存: {memStatus.ullTotalPhys / (1024 * 1024 * 1024)} GB, 可用: {memStatus.ullAvailPhys / (1024 * 1024 * 1024)} GB",
                    Status = HardwareStatus.Normal,
                    LastScanTime = DateTime.Now
                };
                totalMem.Properties["TotalPhysical"] = $"{memStatus.ullTotalPhys / (1024 * 1024 * 1024)} GB";
                totalMem.Properties["AvailablePhysical"] = $"{memStatus.ullAvailPhys / (1024 * 1024 * 1024)} GB";
                totalMem.Properties["MemoryLoad"] = $"{memStatus.dwMemoryLoad}%";

                memory.Insert(0, totalMem);
            }
            catch (Exception ex)
            {
                Logger.Error("Error scanning memory", ex);
            }

            return memory;
        }

        public List<HardwareInfo> ScanMotherboard()
        {
            var motherboards = new List<HardwareInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var mb = new HardwareInfo
                    {
                        Type = HardwareType.Motherboard,
                        Name = GetPropertyString(obj, "Product"),
                        Description = GetPropertyString(obj, "Description"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer"),
                        DeviceId = GetPropertyString(obj, "Tag"),
                        Status = HardwareStatus.Normal,
                        LastScanTime = DateTime.Now
                    };

                    mb.Properties["SerialNumber"] = GetPropertyString(obj, "SerialNumber");
                    mb.Properties["Version"] = GetPropertyString(obj, "Version");
                    mb.Properties["Model"] = GetPropertyString(obj, "Model");

                    motherboards.Add(mb);
                }

                // 获取BIOS信息
                using var biosSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
                foreach (ManagementObject obj in biosSearcher.Get())
                {
                    var bios = new HardwareInfo
                    {
                        Type = HardwareType.Motherboard,
                        Name = $"BIOS - {GetPropertyString(obj, "Name")}",
                        Description = GetPropertyString(obj, "Description"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer"),
                        Status = HardwareStatus.Normal,
                        LastScanTime = DateTime.Now
                    };

                    bios.Properties["Version"] = GetPropertyString(obj, "Version");
                    bios.Properties["SerialNumber"] = GetPropertyString(obj, "SerialNumber");
                    bios.Properties["ReleaseDate"] = GetPropertyString(obj, "ReleaseDate");
                    bios.Properties["SMBIOSBIOSVersion"] = GetPropertyString(obj, "SMBIOSBIOSVersion");

                    motherboards.Add(bios);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error scanning motherboard", ex);
            }

            return motherboards;
        }

        public List<HardwareInfo> ScanStorage()
        {
            var storage = new List<HardwareInfo>();

            try
            {
                // 物理磁盘
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var disk = new HardwareInfo
                    {
                        Type = HardwareType.Storage,
                        Name = GetPropertyString(obj, "Model"),
                        Description = GetPropertyString(obj, "Description"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer"),
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Status = GetPropertyUInt(obj, "ConfigManagerErrorCode") == 0 ? HardwareStatus.Normal : HardwareStatus.Error,
                        LastScanTime = DateTime.Now
                    };

                    ulong size = 0;
                    try
                    {
                        size = Convert.ToUInt64(obj["Size"]);
                    }
                    catch { }

                    disk.Properties["Size"] = $"{size / (1024 * 1024 * 1024)} GB";
                    disk.Properties["InterfaceType"] = GetPropertyString(obj, "InterfaceType");
                    disk.Properties["MediaType"] = GetPropertyString(obj, "MediaType");
                    disk.Properties["Partitions"] = GetPropertyString(obj, "Partitions");
                    disk.Properties["BytesPerSector"] = GetPropertyString(obj, "BytesPerSector");
                    disk.Properties["SectorsPerTrack"] = GetPropertyString(obj, "SectorsPerTrack");
                    disk.Properties["TracksPerCylinder"] = GetPropertyString(obj, "TracksPerCylinder");

                    storage.Add(disk);
                }

                // 逻辑磁盘
                using var logicalSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3");
                foreach (ManagementObject obj in logicalSearcher.Get())
                {
                    var logicalDisk = new HardwareInfo
                    {
                        Type = HardwareType.Storage,
                        Name = $"分区 {GetPropertyString(obj, "DeviceID")}",
                        Description = GetPropertyString(obj, "Description"),
                        Status = HardwareStatus.Normal,
                        LastScanTime = DateTime.Now
                    };

                    ulong totalSize = 0, freeSpace = 0;
                    try
                    {
                        totalSize = Convert.ToUInt64(obj["Size"]);
                        freeSpace = Convert.ToUInt64(obj["FreeSpace"]);
                    }
                    catch { }

                    logicalDisk.Properties["TotalSize"] = $"{totalSize / (1024 * 1024 * 1024)} GB";
                    logicalDisk.Properties["FreeSpace"] = $"{freeSpace / (1024 * 1024 * 1024)} GB";
                    logicalDisk.Properties["UsedSpace"] = $"{(totalSize - freeSpace) / (1024 * 1024 * 1024)} GB";
                    logicalDisk.Properties["FileSystem"] = GetPropertyString(obj, "FileSystem");
                    logicalDisk.Properties["VolumeName"] = GetPropertyString(obj, "VolumeName");
                    logicalDisk.Properties["VolumeSerialNumber"] = GetPropertyString(obj, "VolumeSerialNumber");

                    storage.Add(logicalDisk);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error scanning storage", ex);
            }

            return storage;
        }

        public List<HardwareInfo> ScanGraphicsCards()
        {
            var graphics = new List<HardwareInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var gpu = new HardwareInfo
                    {
                        Type = HardwareType.GraphicsCard,
                        Name = GetPropertyString(obj, "Name"),
                        Description = GetPropertyString(obj, "Description"),
                        Manufacturer = GetPropertyString(obj, "AdapterCompatibility"),
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Status = GetPropertyUInt(obj, "ConfigManagerErrorCode") == 0 ? HardwareStatus.Normal : HardwareStatus.Error,
                        LastScanTime = DateTime.Now
                    };

                    uint adapterRam = 0;
                    try
                    {
                        adapterRam = Convert.ToUInt32(obj["AdapterRAM"]);
                    }
                    catch { }

                    gpu.Properties["AdapterRAM"] = $"{adapterRam / (1024 * 1024)} MB";
                    gpu.Properties["VideoProcessor"] = GetPropertyString(obj, "VideoProcessor");
                    gpu.Properties["VideoModeDescription"] = GetPropertyString(obj, "VideoModeDescription");
                    gpu.Properties["CurrentHorizontalResolution"] = GetPropertyString(obj, "CurrentHorizontalResolution");
                    gpu.Properties["CurrentVerticalResolution"] = GetPropertyString(obj, "CurrentVerticalResolution");
                    gpu.Properties["CurrentRefreshRate"] = GetPropertyString(obj, "CurrentRefreshRate");
                    gpu.Properties["DriverVersion"] = GetPropertyString(obj, "DriverVersion");
                    gpu.Properties["DriverDate"] = GetPropertyString(obj, "DriverDate");

                    graphics.Add(gpu);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error scanning graphics cards", ex);
            }

            return graphics;
        }

        public List<HardwareInfo> ScanNetworkAdapters()
        {
            var networks = new List<HardwareInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var adapter = new HardwareInfo
                    {
                        Type = HardwareType.Network,
                        Name = GetPropertyString(obj, "Name"),
                        Description = GetPropertyString(obj, "Description"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer"),
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Status = GetPropertyUInt(obj, "ConfigManagerErrorCode") == 0 ? HardwareStatus.Normal : HardwareStatus.Error,
                        LastScanTime = DateTime.Now
                    };

                    adapter.Properties["MACAddress"] = GetPropertyString(obj, "MACAddress");
                    adapter.Properties["Speed"] = GetPropertyString(obj, "Speed");
                    adapter.Properties["AdapterType"] = GetPropertyString(obj, "AdapterType");
                    adapter.Properties["NetConnectionID"] = GetPropertyString(obj, "NetConnectionID");
                    adapter.Properties["NetEnabled"] = GetPropertyString(obj, "NetEnabled");
                    adapter.Properties["PhysicalAdapter"] = GetPropertyString(obj, "PhysicalAdapter");

                    networks.Add(adapter);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error scanning network adapters", ex);
            }

            return networks;
        }

        public List<HardwareInfo> ScanAudioDevices()
        {
            var audio = new List<HardwareInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SoundDevice");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var device = new HardwareInfo
                    {
                        Type = HardwareType.Audio,
                        Name = GetPropertyString(obj, "Name"),
                        Description = GetPropertyString(obj, "Description"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer"),
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Status = GetPropertyUInt(obj, "ConfigManagerErrorCode") == 0 ? HardwareStatus.Normal : HardwareStatus.Error,
                        LastScanTime = DateTime.Now
                    };

                    device.Properties["ProductName"] = GetPropertyString(obj, "ProductName");

                    audio.Add(device);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error scanning audio devices", ex);
            }

            return audio;
        }

        public List<HardwareInfo> ScanUSBControllers()
        {
            var usb = new List<HardwareInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_USBController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var controller = new HardwareInfo
                    {
                        Type = HardwareType.USB,
                        Name = GetPropertyString(obj, "Name"),
                        Description = GetPropertyString(obj, "Description"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer"),
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Status = GetPropertyUInt(obj, "ConfigManagerErrorCode") == 0 ? HardwareStatus.Normal : HardwareStatus.Error,
                        LastScanTime = DateTime.Now
                    };

                    usb.Add(controller);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error scanning USB controllers", ex);
            }

            return usb;
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
}
