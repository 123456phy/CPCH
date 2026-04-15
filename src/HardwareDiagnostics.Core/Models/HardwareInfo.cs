using System;
using System.Collections.Generic;

namespace HardwareDiagnostics.Core.Models
{
    public enum HardwareType
    {
        Processor,
        Memory,
        Motherboard,
        GraphicsCard,
        Storage,
        Network,
        Audio,
        USB,
        Bluetooth,
        Other
    }

    public enum HardwareStatus
    {
        Normal,
        Warning,
        Error,
        Disabled,
        Unknown
    }

    public class HardwareInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public HardwareType Type { get; set; }
        public HardwareStatus Status { get; set; }
        public string Manufacturer { get; set; } = "";
        public string Model { get; set; } = "";
        public string DriverVersion { get; set; } = "";
        public DateTime DriverDate { get; set; }
        public string DeviceId { get; set; } = "";
        public string HardwareId { get; set; } = "";
        public string LocationInfo { get; set; } = "";
        public List<string> HardwareIds { get; set; } = new();
        public List<string> CompatibleIds { get; set; } = new();
        public Dictionary<string, string> Properties { get; set; } = new();
        public DateTime LastScanTime { get; set; }
        public List<HardwareEvent> EventHistory { get; set; } = new();
    }

    public class HardwareEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = "";
        public string Description { get; set; } = "";
        public string Source { get; set; } = "";
        public int EventId { get; set; }
        public string Level { get; set; } = "";
    }

    public class DriverInfo
    {
        public string DeviceName { get; set; } = "";
        public string DriverVersion { get; set; } = "";
        public DateTime DriverDate { get; set; }
        public string ProviderName { get; set; } = "";
        public string InfName { get; set; } = "";
        public bool IsSigned { get; set; }
        public string SignerName { get; set; } = "";
        public string DriverPath { get; set; } = "";
        public long DriverSize { get; set; }
    }

    public class CrashReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CrashTime { get; set; }
        public string ApplicationName { get; set; } = "";
        public string ApplicationPath { get; set; } = "";
        public string ExceptionType { get; set; } = "";
        public string ExceptionMessage { get; set; } = "";
        public string StackTrace { get; set; } = "";
        public string ModuleName { get; set; } = "";
        public long ModuleVersion { get; set; }
        public long Offset { get; set; }
        public string ProcessId { get; set; } = "";
        public string ThreadId { get; set; } = "";
        public Dictionary<string, string> AdditionalInfo { get; set; } = new();
        public string RootCause { get; set; } = "";
        public List<string> Recommendations { get; set; } = new();
    }

    public class BSODInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CrashTime { get; set; }
        public string BugCheckCode { get; set; } = "";
        public string BugCheckString { get; set; } = "";
        public string Parameter1 { get; set; } = "";
        public string Parameter2 { get; set; } = "";
        public string Parameter3 { get; set; } = "";
        public string Parameter4 { get; set; } = "";
        public string CausedByDriver { get; set; } = "";
        public string CausedByAddress { get; set; } = "";
        public string CrashAddress { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string FileDescription { get; set; } = "";
        public string FileVersion { get; set; } = "";
        public string Processor { get; set; } = "";
        public string CrashCounter { get; set; } = "";
        public string ProcessorsCount { get; set; } = "";
        public string MajorVersion { get; set; } = "";
        public string MinorVersion { get; set; } = "";
        public string DumpFileSize { get; set; } = "";
        public string DumpFileTime { get; set; } = "";
        public string UserFriendlyExplanation { get; set; } = "";
        public List<string> PossibleCauses { get; set; } = new();
        public List<string> Solutions { get; set; } = new();
    }

    public class VCRuntimeInfo
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Architecture { get; set; } = "";
        public bool IsInstalled { get; set; }
        public string InstallPath { get; set; } = "";
        public DateTime? InstallDate { get; set; }
        public string DownloadUrl { get; set; } = "";
    }

    public class SystemInfo
    {
        public string OSName { get; set; } = "";
        public string OSVersion { get; set; } = "";
        public string OSArchitecture { get; set; } = "";
        public string ComputerName { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Domain { get; set; } = "";
        public int ProcessorCount { get; set; }
        public long TotalPhysicalMemory { get; set; }
        public long AvailablePhysicalMemory { get; set; }
        public string SystemDirectory { get; set; } = "";
        public string WindowsDirectory { get; set; } = "";
        public DateTime BootTime { get; set; }
        public TimeSpan UpTime => DateTime.Now - BootTime;
    }
}
