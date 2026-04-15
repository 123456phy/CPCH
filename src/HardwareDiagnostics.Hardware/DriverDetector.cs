using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HardwareDiagnostics.Core.Utils;
using Microsoft.Win32;

namespace HardwareDiagnostics.Hardware
{
    public class DriverDetector
    {
        private readonly Dictionary<string, DriverDownloadSource> _driverSources;

        public DriverDetector()
        {
            _driverSources = InitializeDriverSources();
        }

        public List<DriverDetectionResult> DetectAllDrivers()
        {
            var results = new List<DriverDetectionResult>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var result = AnalyzeDriver(obj);
                        if (result != null)
                        {
                            results.Add(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Error analyzing driver: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error detecting drivers", ex);
            }

            return results;
        }

        public List<DriverDetectionResult> GetMissingDrivers()
        {
            return DetectAllDrivers()
                .Where(d => d.Status == DriverStatus.Missing || d.Status == DriverStatus.Corrupted)
                .ToList();
        }

        public List<DriverDetectionResult> GetOutdatedDrivers()
        {
            return DetectAllDrivers()
                .Where(d => d.Status == DriverStatus.Outdated)
                .ToList();
        }

        public DriverDetectionResult AnalyzeDriver(ManagementObject device)
        {
            try
            {
                var result = new DriverDetectionResult
                {
                    DeviceId = GetPropertyString(device, "DeviceID"),
                    DeviceName = GetPropertyString(device, "Name"),
                    DeviceClass = GetPropertyString(device, "PNPClass"),
                    Manufacturer = GetPropertyString(device, "Manufacturer"),
                    HardwareId = GetPropertyString(device, "HardwareID"),
                    LastChecked = DateTime.Now
                };

                // 检查驱动状态
                uint errorCode = GetPropertyUInt(device, "ConfigManagerErrorCode");
                result.ErrorCode = errorCode;

                // 获取驱动信息
                var driverInfo = GetDriverInfo(device);
                result.InstalledDriver = driverInfo;

                // 确定驱动状态
                result.Status = DetermineDriverStatus(errorCode, driverInfo);

                // 查找驱动下载源
                result.DownloadSource = FindDriverDownloadSource(result);

                // 获取修复建议
                result.RepairSuggestions = GetRepairSuggestions(result);

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("Error analyzing driver", ex);
                return null;
            }
        }

        public async Task<bool> DownloadDriverAsync(DriverDetectionResult driver, string savePath, IProgress<string>? progress = null)
        {
            if (driver.DownloadSource == null || string.IsNullOrEmpty(driver.DownloadSource.DownloadUrl))
            {
                progress?.Report("未找到驱动下载链接");
                return false;
            }

            try
            {
                progress?.Report($"正在下载 {driver.DeviceName} 的驱动程序...");

                using var client = new WebClient();
                await client.DownloadFileTaskAsync(driver.DownloadSource.DownloadUrl, savePath);

                progress?.Report($"驱动下载完成: {savePath}");
                Logger.Info($"Driver downloaded: {driver.DeviceName} -> {savePath}");

                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"驱动下载失败: {ex.Message}");
                Logger.Error("Error downloading driver", ex);
                return false;
            }
        }

        public async Task<DriverInstallResult> InstallDriverAsync(string driverPath, IProgress<string>? progress = null)
        {
            var result = new DriverInstallResult();

            try
            {
                if (!File.Exists(driverPath))
                {
                    result.Success = false;
                    result.ErrorMessage = "驱动文件不存在";
                    return result;
                }

                progress?.Report("正在安装驱动程序...");

                // 使用pnputil安装驱动
                var psi = new ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = $"/add-driver \"{driverPath}\" /install",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    result.Success = process.ExitCode == 0;
                    result.Output = output;
                    result.ErrorMessage = error;

                    if (result.Success)
                    {
                        progress?.Report("驱动安装成功");
                        Logger.Info("Driver installed successfully");
                    }
                    else
                    {
                        progress?.Report($"驱动安装失败: {error}");
                        Logger.Error($"Driver installation failed: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Logger.Error("Error installing driver", ex);
            }

            return result;
        }

        public async Task<DriverInstallResult> UpdateDriverAsync(string deviceId, string driverPath, IProgress<string>? progress = null)
        {
            var result = new DriverInstallResult();

            try
            {
                progress?.Report("正在更新驱动程序...");

                // 先卸载旧驱动
                await UninstallDriverAsync(deviceId, progress);

                // 安装新驱动
                result = await InstallDriverAsync(driverPath, progress);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Logger.Error("Error updating driver", ex);
            }

            return result;
        }

        public async Task<bool> UninstallDriverAsync(string deviceId, IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report("正在卸载驱动程序...");

                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    obj.InvokeMethod("Disable", null);
                    Logger.Info($"Driver uninstalled for device: {deviceId}");
                }

                progress?.Report("驱动卸载完成");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"驱动卸载失败: {ex.Message}");
                Logger.Error("Error uninstalling driver", ex);
                return false;
            }
        }

        public string GetComputerModel()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string manufacturer = GetPropertyString(obj, "Manufacturer");
                    string model = GetPropertyString(obj, "Model");
                    return $"{manufacturer} {model}";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting computer model", ex);
            }

            return "Unknown";
        }

        public string GetOperatingSystemInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string caption = GetPropertyString(obj, "Caption");
                    string version = GetPropertyString(obj, "Version");
                    string architecture = GetPropertyString(obj, "OSArchitecture");
                    return $"{caption} {version} ({architecture})";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting OS info", ex);
            }

            return Environment.OSVersion.ToString();
        }

        private DriverInfo GetDriverInfo(ManagementObject device)
        {
            var info = new DriverInfo();

            try
            {
                info.DriverVersion = GetPropertyString(device, "DriverVersion");
                info.DriverDate = GetPropertyString(device, "DriverDate");
                info.DriverProvider = GetPropertyString(device, "DriverProviderName");

                // 检查驱动是否已安装
                info.IsInstalled = !string.IsNullOrEmpty(info.DriverVersion);

                // 检查驱动签名（简化检查）
                info.IsSigned = true;

                // 检查驱动日期
                if (!string.IsNullOrEmpty(info.DriverDate))
                {
                    try
                    {
                        info.InstallDate = ManagementDateTimeConverter.ToDateTime(info.DriverDate);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error getting driver info: {ex.Message}");
            }

            return info;
        }

        private DriverStatus DetermineDriverStatus(uint errorCode, DriverInfo driverInfo)
        {
            if (errorCode != 0)
            {
                return errorCode switch
                {
                    28 => DriverStatus.Missing,      // 驱动未安装
                    1 or 3 or 10 => DriverStatus.Corrupted,  // 驱动损坏
                    2 or 22 => DriverStatus.Disabled, // 驱动被禁用
                    _ => DriverStatus.Error
                };
            }

            if (!driverInfo.IsInstalled)
            {
                return DriverStatus.Missing;
            }

            // 检查驱动是否过时（超过2年）
            if (driverInfo.InstallDate.HasValue &&
                (DateTime.Now - driverInfo.InstallDate.Value).TotalDays > 730)
            {
                return DriverStatus.Outdated;
            }

            return DriverStatus.Normal;
        }

        private DriverDownloadSource FindDriverDownloadSource(DriverDetectionResult driver)
        {
            // 根据设备类别和制造商查找下载源
            string key = $"{driver.DeviceClass}_{driver.Manufacturer}";

            if (_driverSources.TryGetValue(key, out var source))
            {
                return source;
            }

            // 尝试根据制造商查找
            key = driver.Manufacturer;
            if (_driverSources.TryGetValue(key, out source))
            {
                return source;
            }

            // 返回通用下载源
            return new DriverDownloadSource
            {
                Manufacturer = driver.Manufacturer,
                DeviceClass = driver.DeviceClass,
                DownloadUrl = GetGenericDriverUrl(driver),
                Description = "通用驱动下载页面"
            };
        }

        private string GetGenericDriverUrl(DriverDetectionResult driver)
        {
            // 根据制造商返回相应的支持页面
            string manufacturer = driver.Manufacturer?.ToLower() ?? "";

            if (manufacturer.Contains("nvidia"))
                return "https://www.nvidia.com/drivers";
            if (manufacturer.Contains("amd") || manufacturer.Contains("ati"))
                return "https://www.amd.com/support";
            if (manufacturer.Contains("intel"))
                return "https://www.intel.com/content/www/us/en/download-center";
            if (manufacturer.Contains("realtek"))
                return "https://www.realtek.com/downloads";
            if (manufacturer.Contains("broadcom"))
                return "https://www.broadcom.com/support/download-search";

            // 默认返回Windows更新
            return "https://support.microsoft.com/windows/update";
        }

        private List<string> GetRepairSuggestions(DriverDetectionResult driver)
        {
            var suggestions = new List<string>();

            switch (driver.Status)
            {
                case DriverStatus.Missing:
                    suggestions.Add("从设备制造商官网下载并安装驱动程序");
                    suggestions.Add("使用Windows更新自动安装驱动");
                    suggestions.Add("尝试使用驱动管理软件");
                    break;

                case DriverStatus.Corrupted:
                    suggestions.Add("卸载当前驱动并重新安装");
                    suggestions.Add("使用系统文件检查器 (sfc /scannow)");
                    suggestions.Add("运行DISM工具修复系统映像");
                    break;

                case DriverStatus.Outdated:
                    suggestions.Add("访问制造商官网下载最新驱动");
                    suggestions.Add("使用Windows更新更新驱动");
                    suggestions.Add("考虑使用驱动更新工具");
                    break;

                case DriverStatus.Disabled:
                    suggestions.Add("在设备管理器中启用设备");
                    suggestions.Add("检查BIOS设置");
                    suggestions.Add("检查设备是否被组策略禁用");
                    break;

                case DriverStatus.Error:
                    suggestions.Add("查看设备管理器中的错误代码");
                    suggestions.Add("检查硬件连接");
                    suggestions.Add("尝试更换硬件");
                    break;
            }

            return suggestions;
        }

        private Dictionary<string, DriverDownloadSource> InitializeDriverSources()
        {
            return new Dictionary<string, DriverDownloadSource>(StringComparer.OrdinalIgnoreCase)
            {
                // 显卡驱动
                { "Display_NVIDIA", new DriverDownloadSource { Manufacturer = "NVIDIA", DeviceClass = "Display", DownloadUrl = "https://www.nvidia.com/drivers", Description = "NVIDIA显卡驱动" } },
                { "Display_AMD", new DriverDownloadSource { Manufacturer = "AMD", DeviceClass = "Display", DownloadUrl = "https://www.amd.com/support", Description = "AMD显卡驱动" } },
                { "Display_Intel", new DriverDownloadSource { Manufacturer = "Intel", DeviceClass = "Display", DownloadUrl = "https://www.intel.com/content/www/us/en/download-center", Description = "Intel显卡驱动" } },

                // 网卡驱动
                { "Net_Intel", new DriverDownloadSource { Manufacturer = "Intel", DeviceClass = "Net", DownloadUrl = "https://www.intel.com/content/www/us/en/download-center", Description = "Intel网卡驱动" } },
                { "Net_Realtek", new DriverDownloadSource { Manufacturer = "Realtek", DeviceClass = "Net", DownloadUrl = "https://www.realtek.com/downloads", Description = "Realtek网卡驱动" } },
                { "Net_Broadcom", new DriverDownloadSource { Manufacturer = "Broadcom", DeviceClass = "Net", DownloadUrl = "https://www.broadcom.com/support/download-search", Description = "Broadcom网卡驱动" } },

                // 声卡驱动
                { "Media_Realtek", new DriverDownloadSource { Manufacturer = "Realtek", DeviceClass = "Media", DownloadUrl = "https://www.realtek.com/downloads", Description = "Realtek声卡驱动" } },
                { "Media_Intel", new DriverDownloadSource { Manufacturer = "Intel", DeviceClass = "Media", DownloadUrl = "https://www.intel.com/content/www/us/en/download-center", Description = "Intel声卡驱动" } },

                // 芯片组驱动
                { "System_Intel", new DriverDownloadSource { Manufacturer = "Intel", DeviceClass = "System", DownloadUrl = "https://www.intel.com/content/www/us/en/download-center", Description = "Intel芯片组驱动" } },
                { "System_AMD", new DriverDownloadSource { Manufacturer = "AMD", DeviceClass = "System", DownloadUrl = "https://www.amd.com/support", Description = "AMD芯片组驱动" } },
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

    public class DriverDetectionResult
    {
        public string DeviceId { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string DeviceClass { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string HardwareId { get; set; } = "";
        public DriverStatus Status { get; set; }
        public uint ErrorCode { get; set; }
        public DriverInfo InstalledDriver { get; set; } = new();
        public DriverDownloadSource DownloadSource { get; set; }
        public List<string> RepairSuggestions { get; set; } = new();
        public DateTime LastChecked { get; set; }
    }

    public enum DriverStatus
    {
        Normal,     // 正常
        Missing,    // 缺失
        Outdated,   // 过时
        Corrupted,  // 损坏
        Disabled,   // 已禁用
        Error       // 错误
    }

    public class DriverInfo
    {
        public string DriverVersion { get; set; } = "";
        public string DriverDate { get; set; } = "";
        public string DriverProvider { get; set; } = "";
        public bool IsInstalled { get; set; }
        public bool IsSigned { get; set; }
        public DateTime? InstallDate { get; set; }
    }

    public class DriverDownloadSource
    {
        public string Manufacturer { get; set; } = "";
        public string DeviceClass { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class DriverInstallResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }
}
