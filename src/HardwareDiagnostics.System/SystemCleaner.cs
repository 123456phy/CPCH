using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HardwareDiagnostics.System
{
    /// <summary>
    /// 系统垃圾清理器 - 安全清理系统缓存和临时文件
    /// 绝不清理注册表或系统关键文件
    /// </summary>
    public class SystemCleaner
    {
        private readonly List<CleanableItem> _cleanableItems;
        private readonly object _lock = new object();
        private CancellationTokenSource? _cancellationTokenSource;

        public event EventHandler<CleanProgressEventArgs>? ProgressChanged;
        public event EventHandler<CleanCompletedEventArgs>? CleanCompleted;

        public SystemCleaner()
        {
            _cleanableItems = InitializeCleanableItems();
        }

        /// <summary>
        /// 初始化可清理项目列表（安全项目）
        /// </summary>
        private List<CleanableItem> InitializeCleanableItems()
        {
            var items = new List<CleanableItem>
            {
                // Windows 临时文件
                new CleanableItem
                {
                    Name = "Windows 临时文件",
                    Description = "清理 Windows 系统临时文件夹中的过期文件",
                    Category = CleanCategory.SystemCache,
                    SafeLevel = SafetyLevel.Safe,
                    GetSize = () => GetDirectorySize(Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Temp")),
                    CleanAction = () => CleanDirectory(Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Temp"), 7)
                },

                // 用户临时文件
                new CleanableItem
                {
                    Name = "用户临时文件",
                    Description = "清理当前用户的临时文件夹",
                    Category = CleanCategory.UserCache,
                    SafeLevel = SafetyLevel.Safe,
                    GetSize = () => GetDirectorySize(Path.Combine(Path.GetTempPath())),
                    CleanAction = () => CleanDirectory(Path.GetTempPath(), 3)
                },

                // 浏览器缓存（IE/Edge）
                new CleanableItem
                {
                    Name = "IE/Edge 浏览器缓存",
                    Description = "清理 Internet Explorer 和 Edge 浏览器的缓存文件",
                    Category = CleanCategory.BrowserCache,
                    SafeLevel = SafetyLevel.Safe,
                    GetSize = () => GetDirectorySize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "INetCache")),
                    CleanAction = () => CleanDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "INetCache"), 0)
                },

                // Windows 更新缓存
                new CleanableItem
                {
                    Name = "Windows 更新缓存",
                    Description = "清理 Windows 更新下载的临时文件（不影响已安装的更新）",
                    Category = CleanCategory.SystemCache,
                    SafeLevel = SafetyLevel.Safe,
                    GetSize = () => GetDirectorySize(Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "SoftwareDistribution", "Download")),
                    CleanAction = () => CleanDirectory(Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "SoftwareDistribution", "Download"), 0)
                },

                // 缩略图缓存
                new CleanableItem
                {
                    Name = "缩略图缓存",
                    Description = "清理 Windows 资源管理器的缩略图缓存",
                    Category = CleanCategory.SystemCache,
                    SafeLevel = SafetyLevel.Safe,
                    GetSize = () => GetDirectorySize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer")),
                    CleanAction = () => CleanThumbnails()
                },

                // 回收站
                new CleanableItem
                {
                    Name = "回收站",
                    Description = "清空回收站中的所有项目",
                    Category = CleanCategory.UserData,
                    SafeLevel = SafetyLevel.Warning,
                    GetSize = GetRecycleBinSize,
                    CleanAction = EmptyRecycleBin
                },

                // 系统日志文件
                new CleanableItem
                {
                    Name = "系统日志文件",
                    Description = "清理过期的系统日志文件（保留最近7天）",
                    Category = CleanCategory.SystemCache,
                    SafeLevel = SafetyLevel.Safe,
                    GetSize = () => GetDirectorySize(Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Logs")),
                    CleanAction = () => CleanDirectory(Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Logs"), 7)
                },

                // 应用程序临时数据
                new CleanableItem
                {
                    Name = "应用程序临时数据",
                    Description = "清理应用程序生成的临时数据文件",
                    Category = CleanCategory.AppCache,
                    SafeLevel = SafetyLevel.Safe,
                    GetSize = () => GetDirectorySize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp")),
                    CleanAction = () => CleanDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"), 3)
                },

                // 预读取文件
                new CleanableItem
                {
                    Name = "预读取文件",
                    Description = "清理 Windows 预读取缓存（会自动重建）",
                    Category = CleanCategory.SystemCache,
                    SafeLevel = SafetyLevel.Safe,
                    GetSize = () => GetDirectorySize(Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Prefetch")),
                    CleanAction = () => CleanDirectory(Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Prefetch"), 30)
                }
            };

            return items;
        }

        /// <summary>
        /// 获取所有可清理项目
        /// </summary>
        public List<CleanableItem> GetCleanableItems()
        {
            // 更新每个项目的大小
            foreach (var item in _cleanableItems)
            {
                try
                {
                    item.SizeBytes = item.GetSize();
                }
                catch
                {
                    item.SizeBytes = 0;
                }
            }
            return _cleanableItems;
        }

        /// <summary>
        /// 执行清理
        /// </summary>
        public async Task<CleanResult> CleanAsync(List<string> selectedItemNames, CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var result = new CleanResult();
            var startTime = DateTime.Now;

            var selectedItems = _cleanableItems.Where(i => selectedItemNames.Contains(i.Name)).ToList();
            int totalItems = selectedItems.Count;
            int currentItem = 0;
            long totalCleaned = 0;

            foreach (var item in selectedItems)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    result.WasCancelled = true;
                    break;
                }

                currentItem++;
                var progress = (int)((currentItem * 100.0) / totalItems);

                ProgressChanged?.Invoke(this, new CleanProgressEventArgs
                {
                    CurrentItem = item.Name,
                    ProgressPercentage = progress,
                    ItemsCompleted = currentItem,
                    TotalItems = totalItems
                });

                try
                {
                    long beforeSize = item.GetSize();
                    bool success = await Task.Run(() => item.CleanAction(), _cancellationTokenSource.Token);
                    long afterSize = item.GetSize();
                    long cleaned = beforeSize - afterSize;

                    if (success && cleaned > 0)
                    {
                        totalCleaned += cleaned;
                        result.SuccessfulItems.Add(new CleanedItemResult
                        {
                            Name = item.Name,
                            CleanedBytes = cleaned,
                            Success = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    result.FailedItems.Add(new CleanedItemResult
                    {
                        Name = item.Name,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            result.TotalCleanedBytes = totalCleaned;
            result.Duration = DateTime.Now - startTime;
            result.Success = result.FailedItems.Count == 0 || result.SuccessfulItems.Count > 0;

            CleanCompleted?.Invoke(this, new CleanCompletedEventArgs
            {
                Result = result
            });

            return result;
        }

        /// <summary>
        /// 安全清理目录（保留最近N天的文件）
        /// </summary>
        private bool CleanDirectory(string path, int keepDays)
        {
            if (!Directory.Exists(path))
                return true;

            var cutoffDate = DateTime.Now.AddDays(-keepDays);
            int failedCount = 0;

            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastAccessTime < cutoffDate)
                        {
                            // 不删除正在使用的文件
                            if (!IsFileLocked(fileInfo))
                            {
                                fileInfo.Delete();
                            }
                        }
                    }
                    catch
                    {
                        failedCount++;
                    }
                }

                // 清理空目录
                CleanEmptyDirectories(path);

                return failedCount < 10; // 允许少量失败
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 清理缩略图缓存
        /// </summary>
        private bool CleanThumbnails()
        {
            try
            {
                // 使用 Windows API 清理缩略图缓存
                var thumbCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer");
                
                // 删除缩略图缓存文件
                foreach (var file in Directory.GetFiles(thumbCachePath, "thumbcache_*.db"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取回收站大小
        /// </summary>
        private long GetRecycleBinSize()
        {
            try
            {
                long size = 0;
                // 回收站路径通常在 C:\$Recycle.Bin
                var recyclerPath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "$Recycle.Bin");
                
                if (Directory.Exists(recyclerPath))
                {
                    foreach (var file in Directory.GetFiles(recyclerPath, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            size += new FileInfo(file).Length;
                        }
                        catch { }
                    }
                }

                return size;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 清空回收站
        /// </summary>
        private bool EmptyRecycleBin()
        {
            try
            {
                // 使用 Windows API 清空回收站
                SHEmptyRecycleBin(IntPtr.Zero, null, RecycleBinFlags.SHERB_NOCONFIRMATION | RecycleBinFlags.SHERB_NOPROGRESSUI);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("shell32.dll")]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, RecycleBinFlags dwFlags);

        [Flags]
        private enum RecycleBinFlags : uint
        {
            SHERB_NOCONFIRMATION = 0x00000001,
            SHERB_NOPROGRESSUI = 0x00000002,
            SHERB_NOSOUND = 0x00000004
        }

        /// <summary>
        /// 获取目录大小
        /// </summary>
        private long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path))
                return 0;

            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        size += new FileInfo(file).Length;
                    }
                    catch { }
                }
            }
            catch { }

            return size;
        }

        /// <summary>
        /// 检查文件是否被锁定
        /// </summary>
        private bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
                return false;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// 清理空目录
        /// </summary>
        private void CleanEmptyDirectories(string path)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    CleanEmptyDirectories(dir);
                    
                    try
                    {
                        if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                        {
                            Directory.Delete(dir);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }
    }

    public class CleanableItem
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public CleanCategory Category { get; set; }
        public SafetyLevel SafeLevel { get; set; }
        public long SizeBytes { get; set; }
        public Func<long> GetSize { get; set; } = () => 0;
        public Func<bool> CleanAction { get; set; } = () => false;
    }

    public enum CleanCategory
    {
        SystemCache,
        UserCache,
        BrowserCache,
        AppCache,
        UserData
    }

    public enum SafetyLevel
    {
        Safe,       // 绝对安全
        Warning,    // 需谨慎
        Dangerous   // 危险（本程序不使用）
    }

    public class CleanResult
    {
        public bool Success { get; set; }
        public bool WasCancelled { get; set; }
        public long TotalCleanedBytes { get; set; }
        public TimeSpan Duration { get; set; }
        public List<CleanedItemResult> SuccessfulItems { get; set; } = new();
        public List<CleanedItemResult> FailedItems { get; set; } = new();
    }

    public class CleanedItemResult
    {
        public string Name { get; set; } = "";
        public long CleanedBytes { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public class CleanProgressEventArgs : EventArgs
    {
        public string CurrentItem { get; set; } = "";
        public int ProgressPercentage { get; set; }
        public int ItemsCompleted { get; set; }
        public int TotalItems { get; set; }
    }

    public class CleanCompletedEventArgs : EventArgs
    {
        public CleanResult Result { get; set; } = new();
    }
}
