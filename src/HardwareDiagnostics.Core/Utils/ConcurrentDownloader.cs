using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HardwareDiagnostics.Core.Utils
{
    /// <summary>
    /// C# 并发下载器 - 最高支持 256 线程
    /// 使用分片下载技术加速大文件下载
    /// </summary>
    public class ConcurrentDownloader : IDisposable
    {
        private int _threadCount = 8;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _disposed;

        public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
        public event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;

        /// <summary>
        /// 设置下载线程数（1-256）
        /// </summary>
        public void SetThreadCount(int threads)
        {
            _threadCount = Math.Max(1, Math.Min(256, threads));
        }

        /// <summary>
        /// 获取当前线程数
        /// </summary>
        public int GetThreadCount() => _threadCount;

        /// <summary>
        /// 下载单个文件（使用多线程）
        /// </summary>
        public async Task<ConcurrentDownloadResult> DownloadAsync(string url, string outputPath, CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var result = new ConcurrentDownloadResult();
            var startTime = DateTime.Now;

            try
            {
                // 创建请求获取文件大小
                var request = WebRequest.CreateHttp(url);
                request.Method = "HEAD";

                using var response = (HttpWebResponse)await request.GetResponseAsync();
                
                if (response.ContentLength <= 0)
                {
                    // 不支持断点续传，使用单线程下载
                    return await DownloadSingleThreadAsync(url, outputPath, _cancellationTokenSource.Token);
                }

                var totalBytes = response.ContentLength;
                var fileName = Path.GetFileName(new Uri(url).AbsolutePath) ?? "download";
                
                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = Path.Combine(Path.GetTempPath(), fileName);
                }

                // 计算分片
                var chunks = CreateChunks(totalBytes, _threadCount);
                var tempFiles = new List<string>();

                // 并行下载各个分片
                var downloadTasks = chunks.Select((chunk, index) => 
                    DownloadChunkAsync(url, chunk, outputPath + $".part{index}", _cancellationTokenSource.Token));
                
                var chunkResults = await Task.WhenAll(downloadTasks);

                // 检查是否有失败的下载
                if (chunkResults.Any(r => !r.Success))
                {
                    result.Success = false;
                    result.ErrorMessage = "部分分片下载失败";
                    return result;
                }

                // 合并分片
                await MergeChunksAsync(chunkResults.Select(r => r.OutputPath!).ToList(), outputPath);

                // 清理临时文件
                foreach (var tempFile in chunkResults.Select(r => r.OutputPath!))
                {
                    try
                    {
                        if (File.Exists(tempFile))
                            File.Delete(tempFile);
                    }
                    catch { }
                }

                result.Success = true;
                result.OutputPath = outputPath;
                result.BytesDownloaded = totalBytes;
                result.Duration = DateTime.Now - startTime;
                result.ThreadsUsed = _threadCount;

                ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                {
                    BytesRead = totalBytes,
                    TotalBytes = totalBytes,
                    ProgressPercentage = 100
                });

                DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs
                {
                    Success = true,
                    OutputPath = outputPath
                });
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "下载已取消";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private async Task<ConcurrentDownloadResult> DownloadSingleThreadAsync(string url, string outputPath, CancellationToken ct)
        {
            var result = new ConcurrentDownloadResult();
            var request = WebRequest.CreateHttp(url);
            
            using var response = (HttpWebResponse)await request.GetResponseAsync();
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
            }

            result.Success = true;
            result.OutputPath = outputPath;
            result.BytesDownloaded = totalRead;

            return result;
        }

        private async Task<ConcurrentDownloadResult> DownloadChunkAsync(string url, Chunk chunk, string outputPath, CancellationToken ct)
        {
            var result = new ConcurrentDownloadResult();

            try
            {
                var request = WebRequest.CreateHttp(url);
                request.Method = "GET";
                request.AddRange("bytes", chunk.Start, chunk.End);

                using var response = (HttpWebResponse)await request.GetResponseAsync();
                using var responseStream = response.GetResponseStream();
                using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                }

                fileStream.Flush();

                result.Success = true;
                result.OutputPath = outputPath;
                result.BytesDownloaded = chunk.End - chunk.Start + 1;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private List<Chunk> CreateChunks(long totalBytes, int threadCount)
        {
            var chunks = new List<Chunk>();
            var chunkSize = totalBytes / threadCount;

            for (int i = 0; i < threadCount; i++)
            {
                var start = i * chunkSize;
                var end = (i == threadCount - 1) ? totalBytes - 1 : start + chunkSize - 1;

                chunks.Add(new Chunk
                {
                    Start = start,
                    End = end,
                    Index = i
                });
            }

            return chunks;
        }

        private async Task MergeChunksAsync(List<string> chunkFiles, string outputPath)
        {
            using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

            foreach (var chunkFile in chunkFiles.OrderBy(f => f))
            {
                using var inputStream = new FileStream(chunkFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                await inputStream.CopyToAsync(outputStream);
            }

            await outputStream.FlushAsync();
        }

        /// <summary>
        /// 取消下载
        /// </summary>
        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }

    public class ConcurrentDownloadResult
    {
        public bool Success { get; set; }
        public string? OutputPath { get; set; }
        public long BytesDownloaded { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public int ThreadsUsed { get; set; }
    }

    internal class Chunk
    {
        public long Start { get; set; }
        public long End { get; set; }
        public int Index { get; set; }
    }
}
