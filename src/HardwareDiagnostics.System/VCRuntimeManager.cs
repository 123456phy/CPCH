using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HardwareDiagnostics.Core.Models;
using HardwareDiagnostics.Core.Utils;
using Microsoft.Win32;

namespace HardwareDiagnostics.System
{
    public class VCRuntimeManager
    {
        private readonly Dictionary<string, string> _vcDownloadUrls = new()
        {
            { "VC++ 2005 x86", "https://download.microsoft.com/download/8/B/4/8B42259F-5D70-43F4-AC2E-4B208FD8D66A/vcredist_x86.exe" },
            { "VC++ 2005 x64", "https://download.microsoft.com/download/8/B/4/8B42259F-5D70-43F4-AC2E-4B208FD8D66A/vcredist_x64.exe" },
            { "VC++ 2008 x86", "https://download.microsoft.com/download/1/1/1/1116b75a-9ec3-481a-a3c8-1777b5381140/vcredist_x86.exe" },
            { "VC++ 2008 x64", "https://download.microsoft.com/download/d/2/4/d242c3fb-7389-4ad7-9791-21755e9f1cea/vcredist_x64.exe" },
            { "VC++ 2010 x86", "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x86.exe" },
            { "VC++ 2010 x64", "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x64.exe" },
            { "VC++ 2012 x86", "https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe" },
            { "VC++ 2012 x64", "https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x64.exe" },
            { "VC++ 2013 x86", "https://download.microsoft.com/download/2/E/6/2E61CFA4-993B-4DD4-91D0-6129BD4D1E49/vcredist_x86.exe" },
            { "VC++ 2013 x64", "https://download.microsoft.com/download/2/E/6/2E61CFA4-993B-4DD4-91D0-6129BD4D1E49/vcredist_x64.exe" },
            { "VC++ 2015-2022 x86", "https://aka.ms/vs/17/release/vc_redist.x86.exe" },
            { "VC++ 2015-2022 x64", "https://aka.ms/vs/17/release/vc_redist.x64.exe" }
        };

        public List<VCRuntimeInfo> ScanInstalledRuntimes()
        {
            var runtimes = new List<VCRuntimeInfo>();

            try
            {
                // 检查注册表中的已安装版本
                var registryPaths = new[]
                {
                    @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86",
                    @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
                    @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x86",
                    @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
                    @"SOFTWARE\Microsoft\VisualStudio\12.0\VC\Runtimes\x86",
                    @"SOFTWARE\Microsoft\VisualStudio\12.0\VC\Runtimes\x64",
                    @"SOFTWARE\Microsoft\VisualStudio\11.0\VC\Runtimes\x86",
                    @"SOFTWARE\Microsoft\VisualStudio\11.0\VC\Runtimes\x64",
                    @"SOFTWARE\Microsoft\VisualStudio\10.0\VC\VCRedist\x86",
                    @"SOFTWARE\Microsoft\VisualStudio\10.0\VC\VCRedist\x64",
                    @"SOFTWARE\Microsoft\VisualStudio\9.0\VC\VCRedist\x86",
                    @"SOFTWARE\Microsoft\VisualStudio\9.0\VC\VCRedist\x64"
                };

                foreach (var path in registryPaths)
                {
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(path);
                        if (key != null)
                        {
                            var version = key.GetValue("Version")?.ToString() ?? "";
                            var installed = key.GetValue("Installed")?.ToString() ?? "0";

                            if (installed == "1" || !string.IsNullOrEmpty(version))
                            {
                                var runtime = new VCRuntimeInfo
                                {
                                    Name = GetRuntimeNameFromPath(path),
                                    Version = version,
                                    Architecture = path.EndsWith("x64") ? "x64" : "x86",
                                    IsInstalled = true,
                                    InstallPath = path
                                };

                                runtimes.Add(runtime);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Error reading registry path: {path}, {ex.Message}");
                    }
                }

                // 检查已安装的程序列表
                var installedPrograms = GetInstalledPrograms();
                foreach (var program in installedPrograms)
                {
                    if (IsVCRuntime(program.Key))
                    {
                        var existing = runtimes.FirstOrDefault(r => r.Name.Contains(program.Key));
                        if (existing == null)
                        {
                            var runtime = new VCRuntimeInfo
                            {
                                Name = program.Key,
                                Version = program.Value,
                                IsInstalled = true,
                                InstallPath = program.Value
                            };
                            runtimes.Add(runtime);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error scanning VC++ runtimes", ex);
            }

            // 添加未安装的运行时
            foreach (var url in _vcDownloadUrls)
            {
                if (!runtimes.Any(r => r.Name.Contains(url.Key.Replace("VC++ ", "").Replace(" x86", "").Replace(" x64", ""))))
                {
                    var runtime = new VCRuntimeInfo
                    {
                        Name = url.Key,
                        Version = "",
                        Architecture = url.Key.Contains("x64") ? "x64" : "x86",
                        IsInstalled = false,
                        DownloadUrl = url.Value
                    };
                    runtimes.Add(runtime);
                }
            }

            return runtimes.OrderBy(r => r.Name).ToList();
        }

        public async Task<bool> DownloadAndInstallRuntimeAsync(VCRuntimeInfo runtime, IProgress<string>? progress = null)
        {
            if (string.IsNullOrEmpty(runtime.DownloadUrl))
            {
                progress?.Report("下载链接不可用");
                return false;
            }

            string tempPath = Path.Combine(Path.GetTempPath(), $"vcredist_{Guid.NewGuid()}.exe");

            try
            {
                progress?.Report($"正在下载 {runtime.Name}...");

                using var client = new WebClient();
                await client.DownloadFileTaskAsync(runtime.DownloadUrl, tempPath);

                progress?.Report($"下载完成，正在安装 {runtime.Name}...");

                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/install /quiet /norestart",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        progress?.Report($"{runtime.Name} 安装成功");
                        Logger.Info($"VC++ runtime installed: {runtime.Name}");
                        return true;
                    }
                    else
                    {
                        progress?.Report($"{runtime.Name} 安装失败，错误代码: {process.ExitCode}");
                        Logger.Error($"VC++ runtime installation failed: {runtime.Name}, ExitCode: {process.ExitCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"安装失败: {ex.Message}");
                Logger.Error("Error installing VC++ runtime", ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch { }
            }

            return false;
        }

        public async Task InstallAllMissingRuntimesAsync(IProgress<string>? progress = null)
        {
            var runtimes = ScanInstalledRuntimes();
            var missing = runtimes.Where(r => !r.IsInstalled).ToList();

            if (missing.Count == 0)
            {
                progress?.Report("所有VC++运行库已安装");
                return;
            }

            progress?.Report($"发现 {missing.Count} 个缺失的运行库");

            foreach (var runtime in missing)
            {
                await DownloadAndInstallRuntimeAsync(runtime, progress);
            }

            progress?.Report("所有缺失的运行库安装完成");
        }

        private Dictionary<string, string> GetInstalledPrograms()
        {
            var programs = new Dictionary<string, string>();

            try
            {
                string[] registryPaths = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var registryPath in registryPaths)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(registryPath);
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            try
                            {
                                using var subKey = key.OpenSubKey(subKeyName);
                                if (subKey != null)
                                {
                                    var displayName = subKey.GetValue("DisplayName")?.ToString();
                                    var displayVersion = subKey.GetValue("DisplayVersion")?.ToString();

                                    if (!string.IsNullOrEmpty(displayName))
                                    {
                                        programs[displayName] = displayVersion ?? "";
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting installed programs", ex);
            }

            return programs;
        }

        private bool IsVCRuntime(string programName)
        {
            string name = programName.ToLower();
            return name.Contains("microsoft visual c++") ||
                   name.Contains("vc++") ||
                   name.Contains("vcredist") ||
                   (name.Contains("visual c++") && name.Contains("redistributable"));
        }

        private string GetRuntimeNameFromPath(string path)
        {
            if (path.Contains("14.0")) return "VC++ 2015-2022" + (path.EndsWith("x64") ? " x64" : " x86");
            if (path.Contains("12.0")) return "VC++ 2013" + (path.EndsWith("x64") ? " x64" : " x86");
            if (path.Contains("11.0")) return "VC++ 2012" + (path.EndsWith("x64") ? " x64" : " x86");
            if (path.Contains("10.0")) return "VC++ 2010" + (path.EndsWith("x64") ? " x64" : " x86");
            if (path.Contains("9.0")) return "VC++ 2008" + (path.EndsWith("x64") ? " x64" : " x86");
            return "VC++ Runtime";
        }
    }
}
