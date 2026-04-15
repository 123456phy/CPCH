using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HardwareDiagnostics.Core.Utils
{
    /// <summary>
    /// 下载管理器 - 支持多种下载工具和并发下载
    /// </summary>
    public class DownloadManager
    {
        private DownloadTool _selectedTool;
        private int _maxThreads = 8;
        private readonly string _downloadDirectory;

        public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
        public event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;

        public DownloadManager(string? downloadDirectory = null)
        {
            _downloadDirectory = downloadDirectory ?? Path.Combine(Path.GetTempPath(), "HardwareDiagnostics_Downloads");
            Directory.CreateDirectory(_downloadDirectory);
            _selectedTool = DownloadTool.BuiltIn;
        }

        /// <summary>
        /// 检测系统中的下载工具
        /// </summary>
        public static DownloadToolDetection DetectAvailableTools()
        {
            var result = new DownloadToolDetection();

            // 检测 curl
            try
            {
                var curlPath = FindInPath("curl.exe");
                if (!string.IsNullOrEmpty(curlPath))
                {
                    result.CurlAvailable = true;
                    result.CurlPath = curlPath;
                }
            }
            catch { }

            // 检测 wget
            try
            {
                var wgetPath = FindInPath("wget.exe");
                if (!string.IsNullOrEmpty(wgetPath))
                {
                    result.WgetAvailable = true;
                    result.WgetPath = wgetPath;
                }
            }
            catch { }

            // 检测 aria2c
            try
            {
                var aria2Path = FindInPath("aria2c.exe");
                if (!string.IsNullOrEmpty(aria2Path))
                {
                    result.Aria2Available = true;
                    result.Aria2Path = aria2Path;
                }
            }
            catch { }

            return result;
        }

        private static string? FindInPath(string fileName)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;

            foreach (var dir in pathEnv.Split(';'))
            {
                var fullPath = Path.Combine(dir.Trim(), fileName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            // 也检查当前目录
            var currentDir = Directory.GetCurrentDirectory();
            var currentPath = Path.Combine(currentDir, fileName);
            if (File.Exists(currentPath))
            {
                return currentPath;
            }

            return null;
        }

        /// <summary>
        /// 设置下载工具
        /// </summary>
        public void SetDownloadTool(DownloadTool tool, string? customPath = null)
        {
            if (tool == DownloadTool.Custom && !string.IsNullOrEmpty(customPath))
            {
                if (File.Exists(customPath))
                {
                    _selectedTool = tool;
                    CustomToolPath = customPath;
                }
                else
                {
                    throw new FileNotFoundException("指定的下载工具不存在", customPath);
                }
            }
            else
            {
                _selectedTool = tool;
            }
        }

        /// <summary>
        /// 设置最大线程数（1-256）
        /// </summary>
        public void SetMaxThreads(int threads)
        {
            _maxThreads = Math.Max(1, Math.Min(256, threads));
        }

        public string? CustomToolPath { get; private set; }

        /// <summary>
        /// 下载文件
        /// </summary>
        public async Task<DownloadResult> DownloadAsync(string url, string? outputPath = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                var fileName = Path.GetFileName(new Uri(url).AbsolutePath) ?? $"download_{DateTime.Now:yyyyMMdd_HHmmss}";
                outputPath = Path.Combine(_downloadDirectory, fileName);
            }

            try
            {
                return _selectedTool switch
                {
                    DownloadTool.Curl => await DownloadWithCurlAsync(url, outputPath, cancellationToken),
                    DownloadTool.Wget => await DownloadWithWgetAsync(url, outputPath, cancellationToken),
                    DownloadTool.Aria2 => await DownloadWithAria2Async(url, outputPath, cancellationToken),
                    DownloadTool.Custom => await DownloadWithCustomToolAsync(url, outputPath, cancellationToken),
                    DownloadTool.BuiltIn => await DownloadWithBuiltInAsync(url, outputPath, cancellationToken),
                    _ => await DownloadWithBuiltInAsync(url, outputPath, cancellationToken)
                };
            }
            catch (OperationCanceledException)
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "下载已取消"
                };
            }
            catch (Exception ex)
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<DownloadResult> DownloadWithCurlAsync(string url, string outputPath, CancellationToken ct)
        {
            var detection = DetectAvailableTools();
            if (!detection.CurlAvailable)
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "该功能无法使用，请联系管理员，错误原因：curl 未下载"
                };
            }

            var psi = new ProcessStartInfo
            {
                FileName = detection.CurlPath,
                Arguments = $"-L -# -o \"{outputPath}\" \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            return await RunExternalToolAsync(psi, ct);
        }

        private async Task<DownloadResult> DownloadWithWgetAsync(string url, string outputPath, CancellationToken ct)
        {
            var detection = DetectAvailableTools();
            if (!detection.WgetAvailable)
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "该功能无法使用，请联系管理员，错误原因：wget 未下载"
                };
            }

            var psi = new ProcessStartInfo
            {
                FileName = detection.WgetPath,
                Arguments = $"-O \"{outputPath}\" \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            return await RunExternalToolAsync(psi, ct);
        }

        private async Task<DownloadResult> DownloadWithAria2Async(string url, string outputPath, CancellationToken ct)
        {
            var detection = DetectAvailableTools();
            if (!detection.Aria2Available)
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "aria2c 未找到"
                };
            }

            var psi = new ProcessStartInfo
            {
                FileName = detection.Aria2Path,
                Arguments = $"-x {_maxThreads} -s {_maxThreads} -k 1M -o \"{outputPath}\" \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            return await RunExternalToolAsync(psi, ct);
        }

        private async Task<DownloadResult> DownloadWithCustomToolAsync(string url, string outputPath, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(CustomToolPath) || !File.Exists(CustomToolPath))
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "自定义下载工具路径无效"
                };
            }

            var psi = new ProcessStartInfo
            {
                FileName = CustomToolPath,
                Arguments = $"-o \"{outputPath}\" \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            return await RunExternalToolAsync(psi, ct);
        }

        private async Task<DownloadResult> DownloadWithBuiltInAsync(string url, string outputPath, CancellationToken ct)
        {
            var result = new DownloadResult();
            var startTime = DateTime.Now;

            try
            {
                var request = WebRequest.CreateHttp(url);
                request.Method = "GET";
                request.AllowAutoRedirect = true;

                using var response = (HttpWebResponse)await request.GetResponseAsync();
                var totalBytes = response.ContentLength;

                using var responseStream = response.GetResponseStream();
                using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (int)((totalRead * 100) / totalBytes);
                        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                        {
                            BytesRead = totalRead,
                            TotalBytes = totalBytes,
                            ProgressPercentage = progress
                        });
                    }
                }

                fileStream.Flush();

                result.Success = true;
                result.OutputPath = outputPath;
                result.BytesDownloaded = totalRead;
                result.Duration = DateTime.Now - startTime;

                DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs
                {
                    Success = true,
                    OutputPath = outputPath
                });
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }

            return result;
        }

        private async Task<DownloadResult> RunExternalToolAsync(ProcessStartInfo psi, CancellationToken ct)
        {
            var result = new DownloadResult();
            var startTime = DateTime.Now;

            using var process = Process.Start(psi);
            if (process == null)
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "无法启动下载进程"
                };
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() =>
            {
                process.WaitForExit();
            }, ct);

            result.Success = process.ExitCode == 0;
            result.OutputPath = psi.Arguments.Contains("-o") ? psi.Arguments.Split('"')[1] : "";
            result.Duration = DateTime.Now - startTime;
            result.ErrorMessage = errorBuilder.ToString();

            if (result.Success)
            {
                DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs
                {
                    Success = true,
                    OutputPath = result.OutputPath
                });
            }
            else
            {
                DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage
                });
            }

            return result;
        }

        /// <summary>
        /// 并发下载多个文件
        /// </summary>
        public async Task<List<DownloadResult>> DownloadMultipleAsync(List<string> urls, string outputDirectory, int? maxConcurrentDownloads = null)
        {
            var concurrentDownloads = maxConcurrentDownloads ?? Math.Min(_maxThreads, urls.Count);
            var semaphore = new SemaphoreSlim(concurrentDownloads);
            var results = new List<DownloadResult>();
            var tasks = new List<Task<DownloadResult>>();

            foreach (var url in urls)
            {
                await semaphore.WaitAsync();

                var task = Task.Run(async () =>
                {
                    try
                    {
                        return await DownloadAsync(url, Path.Combine(outputDirectory, Path.GetFileName(new Uri(url).AbsolutePath)));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                tasks.Add(task);
            }

            var completedTasks = await Task.WhenAll(tasks);
            results.AddRange(completedTasks);

            return results;
        }
    }

    public enum DownloadTool
    {
        BuiltIn,
        Curl,
        Wget,
        Aria2,
        Custom
    }

    public class DownloadToolDetection
    {
        public bool CurlAvailable { get; set; }
        public string? CurlPath { get; set; }
        public bool WgetAvailable { get; set; }
        public string? WgetPath { get; set; }
        public bool Aria2Available { get; set; }
        public string? Aria2Path { get; set; }
    }

    public class DownloadResult
    {
        public bool Success { get; set; }
        public string? OutputPath { get; set; }
        public long BytesDownloaded { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class DownloadProgressEventArgs : EventArgs
    {
        public long BytesRead { get; set; }
        public long TotalBytes { get; set; }
        public int ProgressPercentage { get; set; }
    }

    public class DownloadCompletedEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public string? OutputPath { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
