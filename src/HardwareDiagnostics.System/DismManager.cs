using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.System
{
    public class DismOperationResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public int ExitCode { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class DismManager
    {
        private readonly string _dismPath;

        public DismManager()
        {
            _dismPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe");
        }

        public bool IsDismAvailable()
        {
            return File.Exists(_dismPath);
        }

        public async Task<DismOperationResult> ExecuteCommandAsync(string arguments, IProgress<string>? progress = null)
        {
            var result = new DismOperationResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _dismPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    Verb = "runas"
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    result.Error = "无法启动DISM进程";
                    return result;
                }

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        progress?.Report(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        progress?.Report($"错误: {e.Data}");
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.ExitCode = process.ExitCode;
                result.Output = outputBuilder.ToString();
                result.Error = errorBuilder.ToString();
                result.Success = process.ExitCode == 0;

                Logger.Info($"DISM command executed: {arguments}, ExitCode: {result.ExitCode}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.Error = ex.Message;
                result.Success = false;
                Logger.Error("Error executing DISM command", ex);
            }

            return result;
        }

        #region 系统映像维护

        public async Task<DismOperationResult> ScanHealthAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在扫描系统映像健康状况...");
            return await ExecuteCommandAsync("/Online /Cleanup-Image /ScanHealth", progress);
        }

        public async Task<DismOperationResult> CheckHealthAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在检查系统映像健康状况...");
            return await ExecuteCommandAsync("/Online /Cleanup-Image /CheckHealth", progress);
        }

        public async Task<DismOperationResult> RestoreHealthAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在修复系统映像...");
            return await ExecuteCommandAsync("/Online /Cleanup-Image /RestoreHealth", progress);
        }

        public async Task<DismOperationResult> RestoreHealthWithSourceAsync(string sourcePath, IProgress<string>? progress = null)
        {
            progress?.Report($"正在使用源映像修复系统: {sourcePath}");
            return await ExecuteCommandAsync($"/Online /Cleanup-Image /RestoreHealth /Source:{sourcePath}", progress);
        }

        public async Task<DismOperationResult> RestoreHealthWithLimitAsync(int limitPercent, IProgress<string>? progress = null)
        {
            progress?.Report($"正在修复系统映像（限制{limitPercent}%）...");
            return await ExecuteCommandAsync($"/Online /Cleanup-Image /RestoreHealth /LimitAccess", progress);
        }

        public async Task<DismOperationResult> AnalyzeComponentStoreAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在分析组件存储...");
            return await ExecuteCommandAsync("/Online /Cleanup-Image /AnalyzeComponentStore", progress);
        }

        public async Task<DismOperationResult> StartComponentCleanupAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在清理组件存储...");
            return await ExecuteCommandAsync("/Online /Cleanup-Image /StartComponentCleanup", progress);
        }

        public async Task<DismOperationResult> StartComponentCleanupResetBaseAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在清理组件存储并重置基础映像...");
            return await ExecuteCommandAsync("/Online /Cleanup-Image /StartComponentCleanup /ResetBase", progress);
        }

        public async Task<DismOperationResult> ReduceComponentStoreAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在缩减组件存储大小...");
            return await ExecuteCommandAsync("/Online /Cleanup-Image /StartComponentCleanup /ResetBase", progress);
        }

        #endregion

        #region Windows功能管理

        public async Task<DismOperationResult> GetFeaturesAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取Windows功能列表...");
            return await ExecuteCommandAsync("/Online /Get-Features", progress);
        }

        public async Task<DismOperationResult> GetFeatureInfoAsync(string featureName, IProgress<string>? progress = null)
        {
            progress?.Report($"正在获取功能信息: {featureName}");
            return await ExecuteCommandAsync($"/Online /Get-FeatureInfo /FeatureName:{featureName}", progress);
        }

        public async Task<DismOperationResult> EnableFeatureAsync(string featureName, IProgress<string>? progress = null)
        {
            progress?.Report($"正在启用功能: {featureName}");
            return await ExecuteCommandAsync($"/Online /Enable-Feature /FeatureName:{featureName} /All", progress);
        }

        public async Task<DismOperationResult> EnableFeatureWithSourceAsync(string featureName, string sourcePath, IProgress<string>? progress = null)
        {
            progress?.Report($"正在启用功能: {featureName}");
            return await ExecuteCommandAsync($"/Online /Enable-Feature /FeatureName:{featureName} /All /Source:{sourcePath} /LimitAccess", progress);
        }

        public async Task<DismOperationResult> DisableFeatureAsync(string featureName, IProgress<string>? progress = null)
        {
            progress?.Report($"正在禁用功能: {featureName}");
            return await ExecuteCommandAsync($"/Online /Disable-Feature /FeatureName:{featureName}", progress);
        }

        public async Task<DismOperationResult> DisableFeatureRemoveAsync(string featureName, IProgress<string>? progress = null)
        {
            progress?.Report($"正在禁用并移除功能: {featureName}");
            return await ExecuteCommandAsync($"/Online /Disable-Feature /FeatureName:{featureName} /Remove", progress);
        }

        #endregion

        #region 包管理

        public async Task<DismOperationResult> GetPackagesAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取已安装包列表...");
            return await ExecuteCommandAsync("/Online /Get-Packages", progress);
        }

        public async Task<DismOperationResult> GetPackagesWithFormatAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取已安装包列表(格式化)...");
            return await ExecuteCommandAsync("/Online /Get-Packages /Format:Table", progress);
        }

        public async Task<DismOperationResult> GetPackageInfoAsync(string packageName, IProgress<string>? progress = null)
        {
            progress?.Report($"正在获取包信息: {packageName}");
            return await ExecuteCommandAsync($"/Online /Get-PackageInfo /PackageName:{packageName}", progress);
        }

        public async Task<DismOperationResult> RemovePackageAsync(string packageName, IProgress<string>? progress = null)
        {
            progress?.Report($"正在移除包: {packageName}");
            return await ExecuteCommandAsync($"/Online /Remove-Package /PackageName:{packageName}", progress);
        }

        public async Task<DismOperationResult> RemovePackageNoRestartAsync(string packageName, IProgress<string>? progress = null)
        {
            progress?.Report($"正在移除包(不重启): {packageName}");
            return await ExecuteCommandAsync($"/Online /Remove-Package /PackageName:{packageName} /NoRestart", progress);
        }

        public async Task<DismOperationResult> AddPackageAsync(string packagePath, IProgress<string>? progress = null)
        {
            progress?.Report($"正在添加包: {packagePath}");
            return await ExecuteCommandAsync($"/Online /Add-Package /PackagePath:{packagePath}", progress);
        }

        public async Task<DismOperationResult> AddPackagePreventPendingAsync(string packagePath, IProgress<string>? progress = null)
        {
            progress?.Report($"正在添加包(阻止挂起): {packagePath}");
            return await ExecuteCommandAsync($"/Online /Add-Package /PackagePath:{packagePath} /PreventPending", progress);
        }

        #endregion

        #region 驱动程序管理

        public async Task<DismOperationResult> GetDriversAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取驱动程序列表...");
            return await ExecuteCommandAsync("/Online /Get-Drivers", progress);
        }

        public async Task<DismOperationResult> GetDriversWithFormatAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取驱动程序列表(格式化)...");
            return await ExecuteCommandAsync("/Online /Get-Drivers /Format:Table", progress);
        }

        public async Task<DismOperationResult> GetDriverInfoAsync(string driverPath, IProgress<string>? progress = null)
        {
            progress?.Report($"正在获取驱动程序信息: {driverPath}");
            return await ExecuteCommandAsync($"/Online /Get-DriverInfo /Driver:{driverPath}", progress);
        }

        public async Task<DismOperationResult> AddDriverAsync(string driverPath, bool forceUnsigned = false, IProgress<string>? progress = null)
        {
            progress?.Report($"正在添加驱动程序: {driverPath}");
            string args = $"/Online /Add-Driver /Driver:{driverPath}";
            if (forceUnsigned)
            {
                args += " /ForceUnsigned";
            }
            return await ExecuteCommandAsync(args, progress);
        }

        public async Task<DismOperationResult> AddDriverRecurseAsync(string driverPath, IProgress<string>? progress = null)
        {
            progress?.Report($"正在递归添加驱动程序: {driverPath}");
            return await ExecuteCommandAsync($"/Online /Add-Driver /Driver:{driverPath} /Recurse", progress);
        }

        public async Task<DismOperationResult> RemoveDriverAsync(string driverPath, IProgress<string>? progress = null)
        {
            progress?.Report($"正在移除驱动程序: {driverPath}");
            return await ExecuteCommandAsync($"/Online /Remove-Driver /Driver:{driverPath}", progress);
        }

        public async Task<DismOperationResult> ExportDriverAsync(string destinationPath, IProgress<string>? progress = null)
        {
            progress?.Report($"正在导出驱动程序到: {destinationPath}");
            return await ExecuteCommandAsync($"/Online /Export-Driver /Destination:{destinationPath}", progress);
        }

        #endregion

        #region 映像管理

        public async Task<DismOperationResult> GetImageInfoAsync(string imagePath, IProgress<string>? progress = null)
        {
            progress?.Report($"正在获取映像信息: {imagePath}");
            return await ExecuteCommandAsync($"/Get-ImageInfo /ImageFile:{imagePath}", progress);
        }

        public async Task<DismOperationResult> GetImageInfoWithIndexAsync(string imagePath, int index, IProgress<string>? progress = null)
        {
            progress?.Report($"正在获取映像信息(索引{index}): {imagePath}");
            return await ExecuteCommandAsync($"/Get-ImageInfo /ImageFile:{imagePath} /Index:{index}", progress);
        }

        public async Task<DismOperationResult> MountImageAsync(string imagePath, string mountDir, int imageIndex = 1, IProgress<string>? progress = null)
        {
            progress?.Report($"正在挂载映像: {imagePath}");
            return await ExecuteCommandAsync($"/Mount-Image /ImageFile:{imagePath} /Index:{imageIndex} /MountDir:{mountDir}", progress);
        }

        public async Task<DismOperationResult> MountImageReadOnlyAsync(string imagePath, string mountDir, int imageIndex = 1, IProgress<string>? progress = null)
        {
            progress?.Report($"正在以只读模式挂载映像: {imagePath}");
            return await ExecuteCommandAsync($"/Mount-Image /ImageFile:{imagePath} /Index:{imageIndex} /MountDir:{mountDir} /ReadOnly", progress);
        }

        public async Task<DismOperationResult> UnmountImageAsync(string mountDir, bool commit = false, IProgress<string>? progress = null)
        {
            progress?.Report($"正在卸载映像: {mountDir}");
            string args = $"/Unmount-Image /MountDir:{mountDir}";
            if (commit)
            {
                args += " /Commit";
            }
            else
            {
                args += " /Discard";
            }
            return await ExecuteCommandAsync(args, progress);
        }

        public async Task<DismOperationResult> CleanupMountPointsAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在清理无效的挂载点...");
            return await ExecuteCommandAsync("/Cleanup-Mountpoints", progress);
        }

        public async Task<DismOperationResult> CleanupImageAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在执行完整的系统映像清理和修复...");

            // 1. 扫描健康状态
            var scanResult = await ScanHealthAsync(progress);
            if (!scanResult.Success)
            {
                // 2. 如果扫描发现问题，执行修复
                progress?.Report("发现系统映像问题，开始修复...");
                var restoreResult = await RestoreHealthAsync(progress);
                if (!restoreResult.Success)
                {
                    return restoreResult;
                }
            }

            // 3. 分析组件存储
            var analyzeResult = await AnalyzeComponentStoreAsync(progress);

            // 4. 清理组件存储
            var cleanupResult = await StartComponentCleanupAsync(progress);

            progress?.Report("系统映像清理和修复完成");

            return new DismOperationResult
            {
                Success = true,
                Output = $"扫描结果: {scanResult.Output}\n清理结果: {cleanupResult.Output}",
                Duration = scanResult.Duration + cleanupResult.Duration
            };
        }

        #endregion

        #region 应用补丁

        public async Task<DismOperationResult> GetAppPatchesAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取应用补丁列表...");
            return await ExecuteCommandAsync("/Online /Get-AppPatchInfo", progress);
        }

        public async Task<DismOperationResult> GetAppInfoAsync(string appPath, IProgress<string>? progress = null)
        {
            progress?.Report($"正在获取应用信息: {appPath}");
            return await ExecuteCommandAsync($"/Online /Get-AppInfo /Path:{appPath}", progress);
        }

        #endregion

        #region 国际设置

        public async Task<DismOperationResult> GetIntlSettingsAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取国际设置...");
            return await ExecuteCommandAsync("/Online /Get-Intl", progress);
        }

        public async Task<DismOperationResult> SetUILangAsync(string language, IProgress<string>? progress = null)
        {
            progress?.Report($"正在设置UI语言: {language}");
            return await ExecuteCommandAsync($"/Online /Set-UILang:{language}", progress);
        }

        public async Task<DismOperationResult> SetUserLocaleAsync(string locale, IProgress<string>? progress = null)
        {
            progress?.Report($"正在设置用户区域: {locale}");
            return await ExecuteCommandAsync($"/Online /Set-UserLocale:{locale}", progress);
        }

        public async Task<DismOperationResult> SetSysLocaleAsync(string locale, IProgress<string>? progress = null)
        {
            progress?.Report($"正在设置系统区域: {locale}");
            return await ExecuteCommandAsync($"/Online /Set-SysLocale:{locale}", progress);
        }

        public async Task<DismOperationResult> SetInputLocaleAsync(string inputLocale, IProgress<string>? progress = null)
        {
            progress?.Report($"正在设置输入法区域: {inputLocale}");
            return await ExecuteCommandAsync($"/Online /Set-InputLocale:{inputLocale}", progress);
        }

        public async Task<DismOperationResult> SetTimeZoneAsync(string timeZone, IProgress<string>? progress = null)
        {
            progress?.Report($"正在设置时区: {timeZone}");
            return await ExecuteCommandAsync($"/Online /Set-TimeZone:{timeZone}", progress);
        }

        #endregion

        #region 功能包 Capabilities（微软官方文档口径）
        // 文档：DISM Capabilities Package Servicing Command-Line Options
        // 要点：每个命令必须带 /Online 或 /Image；/Source 可多个；/LimitAccess 禁止联网找源。
        // 源查找顺序：/Source 指定位置 → 组策略位置 → Windows Update（在线且无 /LimitAccess 时）。

        public async Task<DismOperationResult> GetCapabilitiesAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取功能包（Capabilities）列表...");
            return await ExecuteCommandAsync("/Online /Get-Capabilities /Format:Table", progress);
        }

        public async Task<DismOperationResult> GetCapabilityInfoAsync(string capabilityName, IProgress<string>? progress = null)
        {
            progress?.Report($"正在获取功能包信息: {capabilityName}");
            return await ExecuteCommandAsync($"/Online /Get-CapabilityInfo /CapabilityName:{capabilityName}", progress);
        }

        public async Task<DismOperationResult> AddCapabilityAsync(string capabilityName, string? sourcePath = null, bool limitAccess = false, IProgress<string>? progress = null)
        {
            progress?.Report($"正在安装功能包: {capabilityName}");
            var args = new StringBuilder($"/Online /Add-Capability /CapabilityName:{capabilityName}");
            if (!string.IsNullOrWhiteSpace(sourcePath))
                args.Append($" /Source:{sourcePath}");
            if (limitAccess)
                args.Append(" /LimitAccess");
            return await ExecuteCommandAsync(args.ToString(), progress);
        }

        /// <summary>一键安装 OpenSSH 客户端（最常用的 Capability 入门示例）。</summary>
        public async Task<DismOperationResult> AddOpenSshClientAsync(IProgress<string>? progress = null)
        {
            return await AddCapabilityAsync("OpenSSH.Client~~~~0.0.1.0", null, false, progress);
        }

        public async Task<DismOperationResult> RemoveCapabilityAsync(string capabilityName, IProgress<string>? progress = null)
        {
            progress?.Report($"正在移除功能包: {capabilityName}");
            return await ExecuteCommandAsync($"/Online /Remove-Capability /CapabilityName:{capabilityName}", progress);
        }

        #endregion

        #region 版本 Edition（微软官方文档口径）
        // 文档：DISM Windows Edition-Servicing Command-Line Options
        // 在线可用：/Get-CurrentEdition /Get-TargetEditions /Set-ProductKey /Set-Edition（在线转高版本需 /AcceptEula + /ProductKey）。
        // 规则：只能往高版本转，不能降级；已转过的镜像不要再转，建议从家族最低版本起转。

        public async Task<DismOperationResult> GetCurrentEditionAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取当前系统版本...");
            return await ExecuteCommandAsync("/Online /Get-CurrentEdition", progress);
        }

        public async Task<DismOperationResult> GetTargetEditionsAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取可转换的目标版本...");
            return await ExecuteCommandAsync("/Online /Get-TargetEditions", progress);
        }

        public async Task<DismOperationResult> SetProductKeyAsync(string productKey, IProgress<string>? progress = null)
        {
            progress?.Report("正在设置产品密钥...");
            return await ExecuteCommandAsync($"/Online /Set-ProductKey:{productKey}", progress);
        }

        #endregion

        #region 预配应用 Appx（微软官方文档口径）
        // 文档：DISM App Package (.appx or .appxbundle) Servicing Command-Line Options
        // 注意：/Remove-ProvisionedAppxPackage 只取消“新用户的预配”，已注册到现有用户
        // 的应用要用 PowerShell Remove-AppxPackage 逐个删，否则删不干净。

        public async Task<DismOperationResult> GetProvisionedAppxPackagesAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取预配应用包列表...");
            return await ExecuteCommandAsync("/Online /Get-ProvisionedAppxPackages", progress);
        }

        public async Task<DismOperationResult> RemoveProvisionedAppxPackageAsync(string packageName, IProgress<string>? progress = null)
        {
            progress?.Report($"正在取消预配应用: {packageName}");
            return await ExecuteCommandAsync($"/Online /Remove-ProvisionedAppxPackage /PackageName:{packageName}", progress);
        }

        #endregion

        #region 映像导出与拆分（微软官方 Image Management 口径）
        // /Get-MountedImageInfo：查看当前挂载了哪些映像（卡死先看它）。
        // /Remount-Image：挂载目录异常时重新挂载。
        // /Commit-Image：保存挂载中的修改但不卸载（/Unmount /Commit 是一步到位版）。
        // /Export-Image：把某个 Index 导出成新 wim（/Compress:max 最省，/CheckIntegrity 防烂盘）。
        // /Split-Image：把大 wim 按 /FileSize(MB) 切成 swm（FAT32 U 盘装机必备，单文件超 4GB 时）。

        public async Task<DismOperationResult> GetMountedImageInfoAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在获取已挂载映像信息...");
            return await ExecuteCommandAsync("/Get-MountedImageInfo", progress);
        }

        public async Task<DismOperationResult> RemountImageAsync(string mountDir, IProgress<string>? progress = null)
        {
            progress?.Report($"正在重新挂载映像: {mountDir}");
            return await ExecuteCommandAsync($"/Remount-Image /MountDir:{mountDir}", progress);
        }

        public async Task<DismOperationResult> CommitImageAsync(string mountDir, IProgress<string>? progress = null)
        {
            progress?.Report($"正在提交挂载映像的修改: {mountDir}");
            return await ExecuteCommandAsync($"/Commit-Image /MountDir:{mountDir}", progress);
        }

        public async Task<DismOperationResult> ExportImageAsync(string sourceImageFile, int sourceIndex, string destImageFile, IProgress<string>? progress = null)
        {
            progress?.Report($"正在导出映像索引 {sourceIndex} 到: {destImageFile}");
            return await ExecuteCommandAsync($"/Export-Image /SourceImageFile:{sourceImageFile} /SourceIndex:{sourceIndex} /DestinationImageFile:{destImageFile} /Compress:max /CheckIntegrity", progress);
        }

        public async Task<DismOperationResult> SplitImageAsync(string imageFile, string swmFile, int fileSizeMB, IProgress<string>? progress = null)
        {
            progress?.Report($"正在拆分映像为 {fileSizeMB}MB 分片: {swmFile}");
            return await ExecuteCommandAsync($"/Split-Image /ImageFile:{imageFile} /SWMFile:{swmFile} /FileSize:{fileSizeMB}", progress);
        }

        #endregion

        #region 快捷命令集合

        public async Task<DismOperationResult> QuickScanAndRepairAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在执行快速扫描和修复...");

            // 检查健康
            var checkResult = await CheckHealthAsync(progress);
            if (!checkResult.Success)
            {
                // 需要修复
                progress?.Report("发现系统映像损坏，开始修复...");
                return await RestoreHealthAsync(progress);
            }

            return checkResult;
        }

        public async Task<DismOperationResult> QuickCleanupAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在执行快速清理...");
            return await StartComponentCleanupAsync(progress);
        }

        public async Task<DismOperationResult> DeepCleanupAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在执行深度清理...");

            // 分析组件存储
            var analyzeResult = await AnalyzeComponentStoreAsync(progress);

            // 清理并重置基础
            var cleanupResult = await StartComponentCleanupResetBaseAsync(progress);

            return new DismOperationResult
            {
                Success = cleanupResult.Success,
                Output = $"分析结果:\n{analyzeResult.Output}\n\n清理结果:\n{cleanupResult.Output}",
                Duration = analyzeResult.Duration + cleanupResult.Duration
            };
        }

        public async Task<DismOperationResult> ExportAllDriversAsync(string destinationPath, IProgress<string>? progress = null)
        {
            progress?.Report("正在导出所有驱动程序...");

            // 确保目录存在
            Directory.CreateDirectory(destinationPath);

            return await ExportDriverAsync(destinationPath, progress);
        }

        public async Task<DismOperationResult> ResetWindowsUpdateComponentsAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在重置Windows更新组件...");

            // 清理组件存储
            var cleanupResult = await StartComponentCleanupAsync(progress);

            // 扫描健康
            var scanResult = await ScanHealthAsync(progress);

            return new DismOperationResult
            {
                Success = scanResult.Success,
                Output = $"清理完成，扫描结果:\n{scanResult.Output}",
                Duration = cleanupResult.Duration + scanResult.Duration
            };
        }

        #endregion

        public List<DismFeature> ParseFeatures(string output)
        {
            var features = new List<DismFeature>();
            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                if (line.StartsWith("功能名称 : "))
                {
                    var feature = new DismFeature
                    {
                        Name = line.Substring("功能名称 : ".Length).Trim()
                    };
                    features.Add(feature);
                }
                else if (line.StartsWith("状态 : ") && features.Count > 0)
                {
                    features[features.Count - 1].State = line.Substring("状态 : ".Length).Trim();
                }
            }

            return features;
        }

        public List<DismPackage> ParsePackages(string output)
        {
            var packages = new List<DismPackage>();
            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                if (line.StartsWith("包身份标识 : "))
                {
                    var package = new DismPackage
                    {
                        PackageName = line.Substring("包身份标识 : ".Length).Trim()
                    };
                    packages.Add(package);
                }
                else if (line.StartsWith("状态 : ") && packages.Count > 0)
                {
                    packages[packages.Count - 1].State = line.Substring("状态 : ".Length).Trim();
                }
            }

            return packages;
        }
    }

    public class DismFeature
    {
        public string Name { get; set; } = "";
        public string State { get; set; } = "";
    }

    public class DismPackage
    {
        public string PackageName { get; set; } = "";
        public string State { get; set; } = "";
    }
}
