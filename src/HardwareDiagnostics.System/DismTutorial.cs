using System;
using System.Collections.Generic;

namespace HardwareDiagnostics.System
{
    /// <summary>
    /// DISM 新手教程 - 提供详细的 DISM 命令说明
    /// </summary>
    public class DismTutorial
    {
        public static string GetIntroduction()
        {
            return @"DISM（部署映像服务和管理）新手教程
=====================================

DISM 是 Windows 自带的强大系统维护工具，可以：
• 修复损坏的系统文件
• 管理 Windows 功能
• 管理驱动程序
• 管理应用程序
• 准备 Windows PE 映像

【基础命令分类】
1. 系统映像维护 - 修复和清理系统
2. Windows 功能管理 - 启用/禁用功能
3. 驱动程序管理 - 添加/删除驱动
4. 应用程序管理 - 管理 Appx 应用
5. 高级操作 - 挂载/卸载映像

【使用建议】
• 大多数命令需要管理员权限
• 执行修复操作前建议备份重要数据
• 某些操作可能需要重启系统
• 执行时间从几分钟到几小时不等
";
        }

        public static List<DismTutorialItem> GetTutorials()
        {
            return new List<DismTutorialItem>
            {
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "扫描系统健康",
                    Command = "/Online /Cleanup-Image /ScanHealth",
                    Description = "扫描系统映像文件是否有损坏，不执行修复",
                    Usage = "适用于：检查系统状态，了解是否有文件损坏",
                    Duration = "5-20 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "检查系统健康",
                    Command = "/Online /Cleanup-Image /CheckHealth",
                    Description = "快速检查系统映像是否损坏，不扫描所有文件",
                    Usage = "适用于：快速检查，日常维护",
                    Duration = "1-5 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "修复系统映像",
                    Command = "/Online /Cleanup-Image /RestoreHealth",
                    Description = "自动检测并修复损坏的系统文件，从 Windows Update 下载",
                    Usage = "适用于：系统文件损坏、蓝屏、功能异常",
                    Duration = "10-60 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "使用源文件修复",
                    Command = "/Online /Cleanup-Image /RestoreHealth /Source:X:\\sources\\install.wim",
                    Description = "使用指定的安装映像作为修复源，不依赖网络",
                    Usage = "适用于：无网络环境、Windows Update 失败",
                    Duration = "10-30 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "限制访问修复",
                    Command = "/Online /Cleanup-Image /RestoreHealth /LimitAccess",
                    Description = "不使用 Windows Update，仅使用本地源修复",
                    Usage = "适用于：防止连接微软服务器、企业环境",
                    Duration = "10-30 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "分析组件存储",
                    Command = "/Online /Cleanup-Image /AnalyzeComponentStore",
                    Description = "分析组件存储大小，识别可清理的内容",
                    Usage = "适用于：C 盘空间不足、了解存储占用",
                    Duration = "2-10 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "清理组件存储",
                    Command = "/Online /Cleanup-Image /StartComponentCleanup",
                    Description = "清理过时的系统文件，释放磁盘空间",
                    Usage = "适用于：C 盘空间清理、系统优化",
                    Duration = "5-30 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "深度清理并重置",
                    Command = "/Online /Cleanup-Image /StartComponentCleanup /ResetBase",
                    Description = "深度清理，删除所有旧版本组件，不可恢复",
                    Usage = "适用于：C 盘空间严重不足、系统升级后清理",
                    Duration = "10-60 分钟",
                    RiskLevel = "中风险 - 清理后无法卸载更新"
                },
                new DismTutorialItem
                {
                    Category = "Windows 功能管理",
                    Title = "查看功能列表",
                    Command = "/Online /Get-Features",
                    Description = "列出所有 Windows 功能及其状态",
                    Usage = "适用于：查看已安装功能、准备启用/禁用",
                    Duration = "1-2 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "Windows 功能管理",
                    Title = "启用功能",
                    Command = "/Online /Enable-Feature /FeatureName:NetFx3 /All",
                    Description = "启用指定的 Windows 功能（如.NET Framework 3.5）",
                    Usage = "适用于：安装需要特定功能的软件",
                    Duration = "2-10 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "Windows 功能管理",
                    Title = "禁用功能",
                    Command = "/Online /Disable-Feature /FeatureName:NetFx3",
                    Description = "禁用指定的 Windows 功能",
                    Usage = "适用于：减少系统负担、提高安全性",
                    Duration = "2-10 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "驱动程序管理",
                    Title = "查看第三方驱动",
                    Command = "/Online /Get-Drivers",
                    Description = "列出所有第三方驱动程序",
                    Usage = "适用于：查看驱动版本、准备更新驱动",
                    Duration = "1-3 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "驱动程序管理",
                    Title = "添加驱动",
                    Command = "/Online /Add-Driver /Driver:D:\\Drivers /Recurse",
                    Description = "从指定目录安装驱动程序",
                    Usage = "适用于：批量安装驱动、离线部署",
                    Duration = "2-10 分钟",
                    RiskLevel = "中风险 - 需确保驱动兼容性"
                },
                new DismTutorialItem
                {
                    Category = "驱动程序管理",
                    Title = "删除驱动",
                    Command = "/Online /Remove-Driver /Driver:oem12.inf",
                    Description = "删除指定的第三方驱动程序",
                    Usage = "适用于：驱动冲突、回滚驱动",
                    Duration = "1-5 分钟",
                    RiskLevel = "中风险 - 删除后设备可能无法使用"
                },
                new DismTutorialItem
                {
                    Category = "高级操作",
                    Title = "查看映像信息",
                    Command = "/Get-ImageInfo /ImageFile:X:\\sources\\install.wim",
                    Description = "查看 WIM 映像文件的详细信息",
                    Usage = "适用于：了解安装镜像版本、架构",
                    Duration = "1-2 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "高级操作",
                    Title = "挂载映像",
                    Command = "/Mount-Image /ImageFile:X:\\sources\\install.wim /Index:1 /MountDir:C:\\Mount",
                    Description = "挂载 WIM/ESD 映像文件到指定目录",
                    Usage = "适用于：修改系统镜像、自定义安装",
                    Duration = "2-10 分钟",
                    RiskLevel = "中风险 - 需要足够磁盘空间"
                },
                new DismTutorialItem
                {
                    Category = "高级操作",
                    Title = "保存并卸载映像",
                    Command = "/Unmount-Image /MountDir:C:\\Mount /Commit",
                    Description = "保存对挂载映像的修改并卸载",
                    Usage = "适用于：完成镜像修改后",
                    Duration = "5-30 分钟",
                    RiskLevel = "低风险"
                },
                new DismTutorialItem
                {
                    Category = "高级操作",
                    Title = "放弃修改卸载",
                    Command = "/Unmount-Image /MountDir:C:\\Mount /Discard",
                    Description = "放弃所有修改并卸载映像",
                    Usage = "适用于：修改失败或不想保留更改",
                    Duration = "2-10 分钟",
                    RiskLevel = "低风险"
                }
            };
        }

        public static string GetCommonScenarios()
        {
            return @"【常见使用场景】

场景 1: 系统更新失败
  1. 运行 /CheckHealth 快速检查
  2. 运行 /ScanHealth 详细扫描
  3. 运行 /RestoreHealth 修复系统
  4. 重启后重试更新

场景 2: C 盘空间不足
  1. 运行 /AnalyzeComponentStore 分析
  2. 运行 /StartComponentCleanup 清理
  3. 如需更多空间：/StartComponentCleanup /ResetBase
  4. 运行磁盘清理工具

场景 3: 系统文件损坏
  1. 运行 /RestoreHealth
  2. 如果失败，准备 Windows 安装镜像
  3. 运行 /RestoreHealth /Source:X:\\sources\\install.wim /LimitAccess
  4. 重启系统

场景 4: 安装.NET Framework 3.5
  1. 准备 Windows 安装镜像
  2. 运行 /Enable-Feature /FeatureName:NetFx3 /All /Source:X:\\sources\\sxs /LimitAccess
  3. 验证安装成功

场景 5: 驱动冲突
  1. 运行 /Get-Drivers 查看驱动
  2. 找到问题驱动的 oemXX.inf
  3. 运行 /Remove-Driver /Driver:oemXX.inf
  4. 重启系统

【注意事项】
⚠️ 执行修复操作时保持电源连接
⚠️ 某些操作需要联网（Windows Update）
⚠️ 深度清理后无法卸载已安装的更新
⚠️ 挂载映像需要足够磁盘空间
⚠️ 建议在执行重大操作前创建系统还原点
";
        }
    }

    public class DismTutorialItem
    {
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Command { get; set; } = "";
        public string Description { get; set; } = "";
        public string Usage { get; set; } = "";
        public string Duration { get; set; } = "";
        public string RiskLevel { get; set; } = "";
    }
}
