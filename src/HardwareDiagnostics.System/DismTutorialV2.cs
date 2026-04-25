using System;
using System.Collections.Generic;
using System.Text;

namespace HardwareDiagnostics.System
{
    /// <summary>
    /// DISM 教程 V2 - 严谨完整版
    /// 基于 Microsoft Learn 官方文档整理
    /// 包含全部 DISM 命令用法及详细参数说明
    /// </summary>
    public class DismTutorialV2
    {
        public static string GetIntroduction()
        {
            return @"═══════════════════════════════════════════════════════════════════════════════
                              DISM 部署映像服务和管理工具 - 完整技术文档
═══════════════════════════════════════════════════════════════════════════════

【工具概述】
DISM (Deployment Image Servicing and Management) 是 Windows 操作系统内置的
命令行工具，用于维护、修复和准备 Windows 映像文件(.wim、.vhd、.vhdx、.esd)。

【适用场景】
• 在线系统修复（当前运行的操作系统）
• 离线映像维护（WIM/VHD/VHDX/ESD 文件）
• 系统组件存储修复与清理
• Windows 功能管理
• 驱动程序和更新包管理

【执行要求】
• 必须以管理员权限运行命令提示符或 PowerShell
• 在线操作需要稳定的网络连接（使用 /Source 指定本地源除外）
• 建议保持电源连接，避免操作中断

【重要提示】
• 所有命令参数不区分大小写
• 路径中包含空格时必须使用双引号包裹
• 修复操作前建议创建系统还原点
• 部分操作可能需要重启系统才能生效

═══════════════════════════════════════════════════════════════════════════════
";
        }

        public static List<DismTutorialItemV2> GetTutorials()
        {
            return new List<DismTutorialItemV2>
            {
                // ========== 系统健康检查与修复 ==========
                new DismTutorialItemV2
                {
                    Category = "系统健康检查与修复",
                    Title = "/CheckHealth - 快速检查映像健康状况",
                    Command = "DISM /Online /Cleanup-Image /CheckHealth",
                    FullSyntax = "DISM {/Image:<路径> | /Online} /Cleanup-Image /CheckHealth",
                    Parameters = @"必需参数：
  /Online          - 针对当前运行的操作系统
  /Image:<路径>    - 指定离线映像的挂载路径

可选参数：
  /LogPath:<路径>  - 指定日志文件路径
  /LogLevel:{Errors|Warnings|WarningsInfo} - 日志详细级别",
                    Description = "快速检查映像是否被标记为已损坏，以及损坏是否可修复。此操作仅读取映像状态标志，不执行实际扫描。",
                    WhenToUse = @"适用场景：
• 需要快速了解系统健康状况
• 作为日常维护检查
• 在运行完整扫描前的初步诊断
• 验证之前的修复操作是否成功",
                    Duration = "约 30 秒 - 1 分钟",
                    RiskLevel = "无风险（只读操作）",
                    ExpectedOutput = @"可能的输出结果：
• No component store corruption detected.
  - 未检测到组件存储损坏

• The component store is repairable.
  - 检测到损坏但可以修复

• The component store is not repairable.
  - 检测到损坏且无法修复（需考虑系统还原或重装）",
                    TroubleshootingTips = @"后续操作：
• 如显示可修复 → 运行 /RestoreHealth 进行修复
• 如显示无法修复 → 考虑使用系统还原点或重置系统
• 如显示健康但系统仍有问题 → 运行 /ScanHealth 深度扫描",
                    RelatedCommands = "/ScanHealth, /RestoreHealth"
                },

                new DismTutorialItemV2
                {
                    Category = "系统健康检查与修复",
                    Title = "/ScanHealth - 深度扫描组件存储",
                    Command = "DISM /Online /Cleanup-Image /ScanHealth",
                    FullSyntax = "DISM {/Image:<路径> | /Online} /Cleanup-Image /ScanHealth",
                    Parameters = @"必需参数：
  /Online          - 针对当前运行的操作系统
  /Image:<路径>    - 指定离线映像的挂载路径

可选参数：
  /LogPath:<路径>  - 指定日志文件路径
  /LogLevel:{Errors|Warnings|WarningsInfo}",
                    Description = "对组件存储执行深度扫描，检测损坏的系统文件和组件。此操作会实际检查文件完整性，比 /CheckHealth 更彻底。",
                    WhenToUse = @"适用场景：
• /CheckHealth 显示系统需要修复
• 系统频繁出现错误或崩溃
• Windows 更新反复失败
• 怀疑系统文件损坏但 /CheckHealth 显示正常",
                    Duration = "约 5 - 15 分钟",
                    RiskLevel = "无风险（只读操作）",
                    ExpectedOutput = @"扫描完成后显示：
• 扫描进度百分比
• 发现的损坏组件列表
• 损坏程度评估
• 是否可修复的状态",
                    TroubleshootingTips = @"注意事项：
• 扫描期间系统性能可能下降
• 扫描过程中请勿中断操作
• 扫描完成后建议立即运行 /RestoreHealth 修复",
                    RelatedCommands = "/CheckHealth, /RestoreHealth"
                },

                new DismTutorialItemV2
                {
                    Category = "系统健康检查与修复",
                    Title = "/RestoreHealth - 自动修复系统映像",
                    Command = "DISM /Online /Cleanup-Image /RestoreHealth",
                    FullSyntax = @"DISM {/Image:<路径> | /Online} /Cleanup-Image /RestoreHealth
  [/Source:<源路径>]
  [/LimitAccess]
  [/LogPath:<路径>]",
                    Parameters = @"必需参数：
  /Online          - 针对当前运行的操作系统
  /Image:<路径>    - 指定离线映像的挂载路径

可选参数：
  /Source:<源路径> - 指定修复源位置
    • 格式：/Source:WIM:X:\sources\install.wim:1
    • 格式：/Source:ESD:X:\sources\install.esd:1
    • 格式：/Source:C:\Windows\WinSxS
  
  /LimitAccess     - 阻止 DISM 使用 Windows Update 作为修复源
                   - 必须与 /Source 配合使用
  
  /LogPath:<路径>  - 指定日志文件路径",
                    Description = "扫描组件存储中的损坏，并自动执行修复操作。默认从 Windows Update 下载并替换损坏的文件。",
                    WhenToUse = @"适用场景：
• /ScanHealth 检测到系统损坏
• 系统文件损坏导致功能异常
• Windows 更新组件损坏
• 软件运行异常提示系统文件错误",
                    Duration = "约 15 - 30 分钟（取决于网络速度和损坏程度）",
                    RiskLevel = "低风险",
                    ExpectedOutput = @"成功输出：
The restore operation completed successfully.
The operation completed successfully.

失败输出：
• 错误代码和描述
• 无法访问 Windows Update
• 源文件不匹配等",
                    TroubleshootingTips = @"修复失败解决方案：

1. 检查网络连接
   - 确保能访问 Windows Update
   - 检查防火墙和代理设置

2. 使用本地源修复
   DISM /Online /Cleanup-Image /RestoreHealth 
     /Source:WIM:D:\sources\install.wim:1
     /LimitAccess

3. 指定当前系统作为源
   DISM /Online /Cleanup-Image /RestoreHealth
     /Source:C:\Windows\WinSxS
     /LimitAccess

4. 组合使用 SFC
   sfc /scannow",
                    RelatedCommands = "/CheckHealth, /ScanHealth, sfc /scannow"
                },

                // ========== 组件存储清理 ==========
                new DismTutorialItemV2
                {
                    Category = "组件存储清理",
                    Title = "/AnalyzeComponentStore - 分析组件存储空间",
                    Command = "DISM /Online /Cleanup-Image /AnalyzeComponentStore",
                    FullSyntax = "DISM /Online /Cleanup-Image /AnalyzeComponentStore",
                    Parameters = @"必需参数：
  /Online - 仅支持在线操作

可选参数：
  /LogPath:<路径>  - 指定日志文件路径",
                    Description = "分析 Windows 组件存储(WinSxS)文件夹的实际大小，显示备份组件、缓存和临时数据占用的空间，以及可以回收的空间。",
                    WhenToUse = @"适用场景：
• C 盘空间不足需要评估
• 系统升级后评估可清理空间
• 定期维护了解系统占用
• 准备执行清理前的空间评估",
                    Duration = "约 2 - 5 分钟",
                    RiskLevel = "无风险（只读分析）",
                    ExpectedOutput = @"典型输出示例：

Windows 资源管理器报告的 WinSxS 文件夹大小：XX.XX GB
WinSxS 文件夹的实际大小：XX.XX GB
  与 Windows 共享的文件：XX.XX GB
  备份和已禁用的功能：XX.XX GB
  缓存和临时数据：XX.XX GB
上次清理的日期：XXXX-XX-XX
可回收的程序包数量：XX
  使用 /StartComponentCleanup 可回收的空间：XX.XX GB

注意：使用 /StartComponentCleanup /ResetBase 可回收更多空间，
      但将阻止卸载当前的 Service Pack 和更新。",
                    TroubleshootingTips = @"分析结果解读：
• 实际大小 vs 报告大小 - 了解 WinSxS 的真实占用
• 备份和禁用功能 - 可通过清理释放
• 缓存和临时数据 - 安全清理对象
• 可回收空间 - 执行清理可释放的具体数值",
                    RelatedCommands = "/StartComponentCleanup"
                },

                new DismTutorialItemV2
                {
                    Category = "组件存储清理",
                    Title = "/StartComponentCleanup - 清理组件存储",
                    Command = "DISM /Online /Cleanup-Image /StartComponentCleanup",
                    FullSyntax = @"DISM /Online /Cleanup-Image /StartComponentCleanup
  [/ResetBase]
  [/Defer]",
                    Parameters = @"必需参数：
  /Online - 仅支持在线操作

可选参数：
  /ResetBase - 重置组件存储基础
             - 清理所有更新的旧版本
             - 释放最大空间
             - ⚠️ 执行后无法卸载任何更新！
  
  /Defer     - 延迟清理操作到下次维护窗口
             - 与任务计划程序配合使用
             - 减少对用户的影响
  
  /LogPath:<路径> - 指定日志文件路径",
                    Description = "清理组件存储中过时的系统组件和临时文件，释放磁盘空间。可以安全地删除旧版本组件的备份。",
                    WhenToUse = @"适用场景：
• C 盘空间不足需要释放空间
• 系统升级后清理旧版本组件
• 组件存储占用过大
• 定期系统维护",
                    Duration = "约 5 - 20 分钟",
                    RiskLevel = "低风险（标准清理）/ 中风险（带 /ResetBase）",
                    ExpectedOutput = @"成功输出：
The operation completed successfully.

清理内容：
• 过时的 Windows 更新备份
• 已替换的系统组件旧版本
• 临时安装文件
• 组件缓存数据",
                    TroubleshootingTips = @"重要注意事项：

⚠️ 标准清理 (/StartComponentCleanup)：
• 清理后无法卸载最近的更新
• 建议系统稳定运行一段时间后再执行
• 可安全释放数 GB 空间

⚠️⚠️ 深度清理 (/StartComponentCleanup /ResetBase)：
• 释放最大空间
• 执行后无法卸载任何 Windows 更新
• 执行后无法回滚到之前的系统版本
• 仅在确定系统稳定后使用

💡 建议操作流程：
1. 运行 /AnalyzeComponentStore 评估
2. 运行标准清理 /StartComponentCleanup
3. 系统稳定运行 1-2 周后
4. 如需更多空间，运行带 /ResetBase 的清理",
                    RelatedCommands = "/AnalyzeComponentStore"
                },

                // ========== Windows 功能管理 ==========
                new DismTutorialItemV2
                {
                    Category = "Windows 功能管理",
                    Title = "/Get-Features - 列出所有 Windows 功能",
                    Command = "DISM /Online /Get-Features",
                    FullSyntax = @"DISM {/Image:<路径> | /Online} /Get-Features
  [/Format:{Table|List}]
  [/English]",
                    Parameters = @"必需参数：
  /Online       - 针对当前运行的操作系统
  /Image:<路径> - 指定离线映像的挂载路径

可选参数：
  /Format:{Table|List} - 输出格式（表格或列表）
  /English             - 以英文显示功能名称
  /LogPath:<路径>      - 指定日志文件路径",
                    Description = "显示映像中所有 Windows 可选功能的状态列表，包括功能名称、状态和描述。",
                    WhenToUse = @"适用场景：
• 查看系统可用的所有功能
• 确认某个功能是否已启用
• 准备启用或禁用功能前的查询
• 排查功能相关问题",
                    Duration = "约 1 - 2 分钟",
                    RiskLevel = "无风险（只读操作）",
                    ExpectedOutput = @"输出格式示例：

功能名称 : TelnetClient
状态 : 已禁用

功能名称 : NetFx3
状态 : 已启用

功能名称 : Microsoft-Windows-Subsystem-Linux
状态 : 已禁用",
                    TroubleshootingTips = @"常用功能名称：
• NetFx3 - .NET Framework 3.5
• NetFx4-AdvSrvs - .NET Framework 4.x 高级服务
• IIS-WebServerRole - Internet Information Services
• Microsoft-Windows-Subsystem-Linux - WSL
• HypervisorPlatform - Hyper-V 平台
• VirtualMachinePlatform - 虚拟机平台
• Containers-DisposableClientVM - Windows Sandbox
• TFTP - TFTP 客户端
• TelnetClient - Telnet 客户端
• SMB1Protocol - SMB 1.0/CIFS 文件共享支持",
                    RelatedCommands = "/Enable-Feature, /Disable-Feature, /Get-FeatureInfo"
                },

                new DismTutorialItemV2
                {
                    Category = "Windows 功能管理",
                    Title = "/Enable-Feature - 启用 Windows 功能",
                    Command = "DISM /Online /Enable-Feature /FeatureName:NetFx3 /All",
                    FullSyntax = @"DISM {/Image:<路径> | /Online} /Enable-Feature /FeatureName:<名称>
  [/All]
  [/LimitAccess]
  [/Source:<源路径>]
  [/PackagePath:<路径>]
  [/NoRestart]
  [/RestartOnWarning]",
                    Parameters = @"必需参数：
  /Online                    - 针对当前运行的操作系统
  /Image:<路径>              - 指定离线映像的挂载路径
  /FeatureName:<名称>        - 要启用的功能名称

可选参数：
  /All                       - 启用所有父功能
  /LimitAccess               - 阻止访问 Windows Update
  /Source:<源路径>           - 指定功能源位置
    • 格式：/Source:X:\sources\sxs
  /PackagePath:<路径>        - 指定包含功能的 CAB 文件
  /NoRestart                 - 阻止自动重启
  /RestartOnWarning          - 即使警告也重启
  /LogPath:<路径>            - 指定日志文件路径",
                    Description = "在映像中启用指定的 Windows 可选功能。某些功能需要从 Windows Update 或本地源下载文件。",
                    WhenToUse = @"适用场景：
• 启用 .NET Framework 3.5 运行旧软件
• 启用 WSL (Windows Subsystem for Linux)
• 启用 Hyper-V 虚拟化功能
• 启用 IIS Web 服务器
• 启用其他可选组件",
                    Duration = "约 2 - 10 分钟",
                    RiskLevel = "低风险",
                    ExpectedOutput = @"成功输出：
The operation completed successfully.
Restart Windows to complete this operation.

或：
The operation completed successfully.
(无需重启)",
                    TroubleshootingTips = @"常见问题解决：

1. .NET Framework 3.5 安装失败
   在线安装：
   DISM /Online /Enable-Feature /FeatureName:NetFx3 /All
   
   离线安装（使用安装介质）：
   DISM /Online /Enable-Feature /FeatureName:NetFx3 /All
     /Source:D:\sources\sxs
     /LimitAccess

2. 找不到源文件
   • 确保 Windows Update 可访问
   • 提供正确的本地源路径
   • 检查源文件版本是否匹配

3. 功能启用后需要重启
   • 保存所有工作
   • 重启系统以完成功能启用",
                    RelatedCommands = "/Get-Features, /Disable-Feature, /Get-FeatureInfo"
                },

                new DismTutorialItemV2
                {
                    Category = "Windows 功能管理",
                    Title = "/Disable-Feature - 禁用 Windows 功能",
                    Command = "DISM /Online /Disable-Feature /FeatureName:TelnetClient",
                    FullSyntax = @"DISM {/Image:<路径> | /Online} /Disable-Feature /FeatureName:<名称>
  [/Remove]
  [/NoRestart]
  [/PackagePath:<路径>]",
                    Parameters = @"必需参数：
  /Online              - 针对当前运行的操作系统
  /Image:<路径>        - 指定离线映像的挂载路径
  /FeatureName:<名称>  - 要禁用的功能名称

可选参数：
  /Remove              - 从映像中完全删除功能文件
                       - 释放磁盘空间
                       - 之后需要源文件才能重新启用
  /NoRestart           - 阻止自动重启
  /PackagePath:<路径>  - 指定包含功能的 CAB 文件
  /LogPath:<路径>      - 指定日志文件路径",
                    Description = "在映像中禁用指定的 Windows 可选功能。可以选择仅禁用或完全删除功能文件。",
                    WhenToUse = @"适用场景：
• 禁用不需要的功能提升安全性
• 减少系统攻击面
• 释放磁盘空间（使用 /Remove）
• 排查功能冲突问题",
                    Duration = "约 1 - 5 分钟",
                    RiskLevel = "低风险（禁用）/ 中风险（使用 /Remove）",
                    ExpectedOutput = @"成功输出：
The operation completed successfully.

或（使用 /Remove）：
The operation completed successfully.
Feature files have been removed.
Restart Windows to complete this operation.",
                    TroubleshootingTips = @"注意事项：

⚠️ 标准禁用：
• 功能被禁用但文件保留
• 可随时重新启用
• 不释放磁盘空间

⚠️ 完全删除 (/Remove)：
• 从系统中删除功能文件
• 释放磁盘空间
• 重新启用时需要安装源
• 某些核心功能无法删除

💡 建议禁用的非必要功能：
• TelnetClient - Telnet 客户端（存在安全风险）
• TFTP - TFTP 客户端
• SMB1Protocol - SMB 1.0（存在安全风险，如不需要建议禁用）
• WorkFolders-Client - 工作文件夹客户端（如不使用）

❌ 不建议禁用的功能：
• 不确定功能用途的
• 系统核心组件
• 可能影响系统稳定性的功能",
                    RelatedCommands = "/Get-Features, /Enable-Feature"
                },

                // ========== 包管理 ==========
                new DismTutorialItemV2
                {
                    Category = "包管理",
                    Title = "/Get-Packages - 列出已安装的包",
                    Command = "DISM /Online /Get-Packages",
                    FullSyntax = @"DISM {/Image:<路径> | /Online} /Get-Packages
  [/Format:{Table|List}]
  [/English]",
                    Parameters = @"必需参数：
  /Online       - 针对当前运行的操作系统
  /Image:<路径> - 指定离线映像的挂载路径

可选参数：
  /Format:{Table|List} - 输出格式
  /English             - 以英文显示包名称
  /LogPath:<路径>      - 指定日志文件路径",
                    Description = "显示映像中已安装的所有包（更新、语言包、驱动等）的基本信息列表。",
                    WhenToUse = @"适用场景：
• 查看已安装的更新列表
• 查找特定包的完整名称
• 准备卸载包前的查询
• 排查包相关问题",
                    Duration = "约 1 - 3 分钟",
                    RiskLevel = "无风险（只读操作）",
                    ExpectedOutput = @"输出格式示例：

包标识 : Package_for_KB5028185~31bf3856ad364e35~amd64~~19041.3324.1.1
状态 : 已安装
发布日期 : 2023-07-11

包标识 : Microsoft-Windows-LanguageFeatures-Basic-zh-cn-Package
状态 : 已安装
发布日期 : 2023-06-15",
                    TroubleshootingTips = @"包名称格式说明：
Package_for_KB<编号>~<哈希>~<架构>~~<版本>

常见包类型：
• Package_for_KB* - Windows 更新包
• Microsoft-Windows-LanguageFeatures* - 语言功能包
• Microsoft-Windows-Client-LanguagePack* - 语言包
• Microsoft-OneCore* - OneCore 组件包",
                    RelatedCommands = "/Get-PackageInfo, /Add-Package, /Remove-Package"
                },

                new DismTutorialItemV2
                {
                    Category = "包管理",
                    Title = "/Add-Package - 安装 CAB/MSU 包",
                    Command = @"DISM /Online /Add-Package /PackagePath:C:\packages\update.msu",
                    FullSyntax = @"DISM {/Image:<路径> | /Online} /Add-Package
  /PackagePath:<路径>
  [/IgnoreCheck]
  [/PreventPending]
  [/NoRestart]
  [/Quiet]",
                    Parameters = @"必需参数：
  /Online                    - 针对当前运行的操作系统
  /Image:<路径>              - 指定离线映像的挂载路径
  /PackagePath:<路径>        - 指定 CAB 或 MSU 文件路径
                               可指定多个 /PackagePath 安装多个包

可选参数：
  /IgnoreCheck               - 忽略适用性检查
  /PreventPending            - 如有挂起操作则跳过安装
  /NoRestart                 - 阻止自动重启
  /Quiet                     - 静默安装（无输出）
  /LogPath:<路径>            - 指定日志文件路径",
                    Description = "向映像中添加指定的 CAB 或 MSU 包。支持安装 Windows 更新、语言包、驱动程序等。",
                    WhenToUse = @"适用场景：
• 离线安装 Windows 更新
• 安装语言包
• 向离线映像添加驱动
• 批量部署时预装更新",
                    Duration = "约 2 - 10 分钟（取决于包大小）",
                    RiskLevel = "中低风险",
                    ExpectedOutput = @"成功输出：
The operation completed successfully.
The changes will take effect after restart.

或：
The operation completed successfully.
(立即生效)",
                    TroubleshootingTips = @"注意事项：

⚠️ 包适用性：
• 默认会检查包是否适用于当前系统
• 使用 /IgnoreCheck 可跳过检查（不推荐）

⚠️ 依赖关系：
• 某些包有依赖关系，需要先安装依赖
• 安装失败时检查是否缺少先决条件

⚠️ 挂起操作：
• 如有其他挂起的更新操作，可能无法安装
• 使用 /PreventPending 跳过（可能产生问题）

💡 批量安装示例：
DISM /Online /Add-Package 
  /PackagePath:C:\Updates\KB5028185.msu
  /PackagePath:C:\Updates\KB5028186.msu
  /PackagePath:C:\Updates\KB5028187.msu",
                    RelatedCommands = "/Get-Packages, /Remove-Package, /Get-PackageInfo"
                },

                // ========== 驱动管理 ==========
                new DismTutorialItemV2
                {
                    Category = "驱动管理",
                    Title = "/Get-Drivers - 列出已安装的驱动程序",
                    Command = "DISM /Online /Get-Drivers",
                    FullSyntax = @"DISM {/Image:<路径> | /Online} /Get-Drivers
  [/Format:{Table|List}]
  [/English]",
                    Parameters = @"必需参数：
  /Online       - 针对当前运行的操作系统
  /Image:<路径> - 指定离线映像的挂载路径

可选参数：
  /Format:{Table|List} - 输出格式
  /English             - 以英文显示驱动信息
  /LogPath:<路径>      - 指定日志文件路径",
                    Description = "显示映像中已安装的所有第三方驱动程序信息，包括驱动名称、提供商、版本和状态。",
                    WhenToUse = @"适用场景：
• 查看已安装的驱动列表
• 排查驱动相关问题
• 准备导出或删除驱动前的查询
• 系统部署前检查驱动状态",
                    Duration = "约 1 - 2 分钟",
                    RiskLevel = "无风险（只读操作）",
                    ExpectedOutput = @"输出格式示例：

发布名称 : oem0.inf
原始文件名 : nvlddmkm.inf
收件箱 : 否
类名称 : 显示适配器
驱动程序版本 : 31.0.15.2802
提供商名称 : NVIDIA Corporation
日期 : 2023/5/18
状态 : 已安装",
                    TroubleshootingTips = @"输出字段说明：
• 发布名称 - 驱动的发布名称（oemX.inf）
• 原始文件名 - 驱动的原始 INF 文件名
• 收件箱 - 是否为 Windows 内置驱动
• 类名称 - 驱动所属设备类别
• 状态 - 驱动当前状态（已安装/已卸载）

💡 驱动管理建议：
• 定期检查和更新驱动
• 删除不必要的旧驱动
• 保留重要驱动的备份",
                    RelatedCommands = "/Add-Driver, /Remove-Driver, /Export-Driver"
                },

                new DismTutorialItemV2
                {
                    Category = "驱动管理",
                    Title = "/Add-Driver - 添加驱动程序",
                    Command = @"DISM /Online /Add-Driver /Driver:C:\Drivers\ /Recurse",
                    FullSyntax = @"DISM {/Image:<路径> | /Online} /Add-Driver
  /Driver:<路径>
  [/Recurse]
  [/ForceUnsigned]
  [/LogPath:<路径>]",
                    Parameters = @"必需参数：
  /Online         - 针对当前运行的操作系统
  /Image:<路径>   - 指定离线映像的挂载路径
  /Driver:<路径>  - 指定驱动 INF 文件或包含驱动的文件夹

可选参数：
  /Recurse        - 递归搜索子文件夹中的驱动
  /ForceUnsigned  - 强制安装未签名的驱动
                   ⚠️ 存在安全风险，仅在测试环境使用
  /LogPath:<路径> - 指定日志文件路径",
                    Description = "向映像中添加第三方驱动程序。支持添加单个驱动或批量添加整个文件夹中的驱动。",
                    WhenToUse = @"适用场景：
• 向离线映像注入驱动（部署准备）
• 安装未通过 Windows Update 提供的驱动
• 批量添加硬件驱动
• 创建自定义 Windows 镜像",
                    Duration = "约 1 - 5 分钟",
                    RiskLevel = "低风险（签名驱动）/ 高风险（未签名驱动）",
                    ExpectedOutput = @"成功输出：
The operation completed successfully.
Installed X of Y driver(s) successfully.

失败输出：
• 驱动不兼容
• 驱动已存在
• 签名验证失败（未使用 /ForceUnsigned）",
                    TroubleshootingTips = @"最佳实践：

1. 驱动准备：
   • 确保下载正确的驱动版本
   • 解压驱动包获取 INF 文件
   • 验证驱动与系统架构匹配（x64/x86）

2. 批量添加：
   DISM /Image:C:\Mount /Add-Driver
     /Driver:C:\Drivers\ /Recurse

3. 注意事项：
   • 优先使用 WHQL 签名驱动
   • 避免使用 /ForceUnsigned（安全风险）
   • 添加驱动后可能需要重启
   • 离线添加驱动后需重新捕获映像",
                    RelatedCommands = "/Get-Drivers, /Remove-Driver, /Export-Driver"
                },

                // ========== 映像文件操作 ==========
                new DismTutorialItemV2
                {
                    Category = "映像文件操作",
                    Title = "/Get-ImageInfo - 获取映像文件信息",
                    Command = @"DISM /Get-ImageInfo /ImageFile:D:\sources\install.wim",
                    FullSyntax = @"DISM /Get-ImageInfo
  /ImageFile:<路径>
  [/Index:<编号>]
  [/English]",
                    Parameters = @"必需参数：
  /ImageFile:<路径> - 指定 WIM、VHD、VHDX 或 ESD 文件路径

可选参数：
  /Index:<编号>     - 指定映像索引号获取详细信息
  /English          - 以英文显示信息
  /LogPath:<路径>   - 指定日志文件路径",
                    Description = "显示 WIM、ESD、VHD 或 VHDX 文件中包含的映像信息，包括索引号、名称、描述、版本等。",
                    WhenToUse = @"适用场景：
• 查看 WIM/ESD 文件包含的映像版本
• 确定要挂载或应用的映像索引
• 验证映像文件完整性
• 部署前确认映像信息",
                    Duration = "约 30 秒 - 2 分钟",
                    RiskLevel = "无风险（只读操作）",
                    ExpectedOutput = @"输出示例（WIM 文件）：

索引 : 1
名称 : Windows 10 Home
描述 : Windows 10 Home
大小 : 15,234,567,890 字节

索引 : 2
名称 : Windows 10 Pro
描述 : Windows 10 Pro
大小 : 15,876,543,210 字节

WIM 引导 : 否
WIM 架构 : x64
WIM 版本 : 10.0.19041.1",
                    TroubleshootingTips = @"重要信息解读：
• 索引号 - 挂载或应用映像时需要指定
• 名称/描述 - 识别映像版本和版本
• 架构 - x64 或 x86，必须与目标匹配
• 版本 - Windows 版本号

💡 常见索引对应：
• 索引 1 - 家庭版 (Home)
• 索引 2 - 专业版 (Pro)
• 索引 3 - 企业版 (Enterprise)
• 索引 4 - 教育版 (Education)
（具体取决于映像文件）",
                    RelatedCommands = "/Mount-Image, /Apply-Image, /Export-Image"
                },

                new DismTutorialItemV2
                {
                    Category = "映像文件操作",
                    Title = "/Mount-Image - 挂载映像文件",
                    Command = @"DISM /Mount-Image /ImageFile:D:\sources\install.wim /Index:1 /MountDir:C:\Mount",
                    FullSyntax = @"DISM /Mount-Image
  /ImageFile:<路径>
  {/Index:<编号> | /Name:<名称>}
  /MountDir:<路径>
  [/ReadOnly]
  [/Optimize]
  [/CheckIntegrity]
  [/LogPath:<路径>]",
                    Parameters = @"必需参数：
  /ImageFile:<路径>  - 指定 WIM、VHD、VHDX 或 ESD 文件路径
  /Index:<编号>      - 指定要挂载的映像索引号
  /Name:<名称>       - 或按名称指定映像
  /MountDir:<路径>   - 指定挂载目录（必须为空文件夹）

可选参数：
  /ReadOnly          - 以只读方式挂载
                     - 防止意外修改映像
  /Optimize          - 优化挂载性能
  /CheckIntegrity    - 检查 WIM 文件完整性
  /LogPath:<路径>    - 指定日志文件路径",
                    Description = "将 WIM、ESD、VHD 或 VHDX 文件中的指定映像挂载到本地目录，以便查看和修改内容。",
                    WhenToUse = @"适用场景：
• 向离线映像添加驱动或更新
• 修改 Windows 安装映像
• 提取映像中的文件
• 自定义 Windows 部署映像",
                    Duration = "约 2 - 10 分钟",
                    RiskLevel = "低风险",
                    ExpectedOutput = @"成功输出：
The operation completed successfully.

挂载后：
• 可在挂载目录中浏览映像内容
• 可添加/删除驱动、更新、功能
• 可修改系统文件和设置",
                    TroubleshootingTips = @"挂载前准备：

1. 创建空挂载目录：
   mkdir C:\Mount

2. 确保足够磁盘空间：
   • WIM 挂载需要额外空间解压文件
   • 建议预留 10-20GB 空间

3. 挂载目录要求：
   • 必须是空文件夹
   • 必须是 NTFS 分区
   • 不能是压缩或加密文件夹

⚠️ 重要提醒：
• 修改完成后必须卸载映像（/Unmount-Image）
• 不卸载直接关闭窗口会导致资源泄漏
• 可使用 /Cleanup-MountPoints 清理异常挂载

💡 常用挂载目录：
• C:\Mount
• C:\WinMount
• D:\ImageMount",
                    RelatedCommands = "/Unmount-Image, /Get-ImageInfo, /Commit-Image"
                },

                new DismTutorialItemV2
                {
                    Category = "映像文件操作",
                    Title = "/Unmount-Image - 卸载映像文件",
                    Command = @"DISM /Unmount-Image /MountDir:C:\Mount /Commit",
                    FullSyntax = @"DISM /Unmount-Image
  /MountDir:<路径>
  {/Commit | /Discard}
  [/Append]
  [/LogPath:<路径>]",
                    Parameters = @"必需参数：
  /MountDir:<路径>   - 指定挂载目录
  /Commit            - 保存对映像的更改
  /Discard           - 放弃对映像的更改

可选参数：
  /Append            - 将更改附加到 WIM（不覆盖原文件）
  /LogPath:<路径>    - 指定日志文件路径",
                    Description = "卸载之前挂载的映像，可以选择保存或放弃对映像所做的修改。",
                    WhenToUse = @"适用场景：
• 完成映像修改后保存更改
• 取消修改并恢复原始映像
• 清理挂载资源
• 准备部署修改后的映像",
                    Duration = "约 2 - 10 分钟（/Commit 需要更长时间）",
                    RiskLevel = "中风险（使用 /Commit 时）",
                    ExpectedOutput = @"成功输出：
The operation completed successfully.

或（保存更改）：
The operation completed successfully.
Image file has been updated.",
                    TroubleshootingTips = @"重要注意事项：

⚠️ /Commit vs /Discard：
• /Commit - 保存所有修改到映像文件
  - 耗时较长（需要重新打包 WIM）
  - 修改永久生效
  
• /Discard - 放弃所有修改
  - 快速完成
  - 映像恢复原始状态

⚠️ 常见错误：
• 挂载目录被占用 - 关闭所有打开的文件和窗口
• 权限不足 - 以管理员身份运行
• 磁盘空间不足 - 确保有足够空间保存修改

💡 建议操作流程：
1. 挂载映像（/Mount-Image）
2. 进行修改（添加驱动、更新等）
3. 测试验证修改
4. 卸载并保存（/Unmount-Image /Commit）
5. 验证修改后的映像",
                    RelatedCommands = "/Mount-Image, /Commit-Image, /Cleanup-MountPoints"
                },

                new DismTutorialItemV2
                {
                    Category = "映像文件操作",
                    Title = "/Export-Image - 导出映像",
                    Command = "DISM /Export-Image /SourceImageFile:install.wim /SourceIndex:1 /DestinationImageFile:export.wim",
                    FullSyntax = @"DISM /Export-Image
  /SourceImageFile:<路径>
  {/SourceIndex:<编号> | /SourceName:<名称>}
  /DestinationImageFile:<路径>
  [/Compress:{max|fast|none}]
  [/Bootable]
  [/CheckIntegrity]
  [/LogPath:<路径>]",
                    Parameters = @"必需参数：
  /SourceImageFile:<路径>        - 源映像文件路径
  /SourceIndex:<编号>            - 源映像索引号
  /SourceName:<名称>             - 或源映像名称
  /DestinationImageFile:<路径>   - 目标映像文件路径

可选参数：
  /Compress:{max|fast|none}      - 压缩类型
    • max  - 最大压缩（文件最小，耗时最长）
    • fast - 快速压缩（推荐）
    • none - 无压缩（文件最大，速度最快）
  /Bootable                      - 标记为可引导
  /CheckIntegrity                - 检查源映像完整性
  /LogPath:<路径>                - 指定日志文件路径",
                    Description = "将 WIM 文件中的指定映像导出到新 WIM 文件。可用于提取特定版本、合并映像或重新压缩。",
                    WhenToUse = @"适用场景：
• 从多版本 WIM 中提取单版本
• 重新压缩映像减小体积
• 合并多个映像到单个文件
• 创建自定义部署映像",
                    Duration = "约 5 - 30 分钟（取决于压缩级别和映像大小）",
                    RiskLevel = "低风险",
                    ExpectedOutput = @"成功输出：
The operation completed successfully.

导出后：
• 生成新的 WIM 文件
• 原文件保持不变
• 可删除原文件以释放空间",
                    TroubleshootingTips = @"压缩选项建议：

• /Compress:fast（推荐）
  - 平衡压缩率和速度
  - 适合大多数场景

• /Compress:max
  - 最大压缩
  - 适合网络部署或存储空间有限
  - 导出时间显著增加

• /Compress:none
  - 无压缩
  - 导出速度最快
  - 文件体积最大

💡 实用示例：

1. 提取专业版映像：
   DISM /Export-Image
     /SourceImageFile:install.wim
     /SourceIndex:2
     /DestinationImageFile:pro.wim
     /Compress:fast

2. 减小现有 WIM 体积：
   DISM /Export-Image
     /SourceImageFile:large.wim
     /SourceIndex:1
     /DestinationImageFile:small.wim
     /Compress:max",
                    RelatedCommands = "/Get-ImageInfo, /Split-Image, /Append-Image"
                },

                new DismTutorialItemV2
                {
                    Category = "映像文件操作",
                    Title = "/Split-Image - 分割映像文件",
                    Command = "DISM /Split-Image /ImageFile:install.wim /SWMFile:install.swm /FileSize:4000",
                    FullSyntax = @"DISM /Split-Image
  /ImageFile:<路径>
  /SWMFile:<路径>
  /FileSize:<MB>",
                    Parameters = @"必需参数：
  /ImageFile:<路径>  - 要分割的源 WIM 文件
  /SWMFile:<路径>    - 分割后的 SWM 文件路径和名称
                       会自动添加序号（如 install1.swm, install2.swm）
  /FileSize:<MB>     - 每个分割文件的最大大小（MB）

可选参数：
  /CheckIntegrity    - 检查源映像完整性
  /LogPath:<路径>    - 指定日志文件路径",
                    Description = "将大型 WIM 文件分割为多个较小的 SWM 文件，适用于 FAT32 文件系统（单文件最大 4GB）。",
                    WhenToUse = @"适用场景：
• 创建 FAT32 格式的安装 U 盘
• 分割超过 4GB 的 WIM 文件
• 网络传输大文件时分割
• 存储设备有单文件大小限制",
                    Duration = "约 5 - 20 分钟",
                    RiskLevel = "低风险",
                    ExpectedOutput = @"成功输出：
The operation completed successfully.

生成的文件：
• install.swm（第一部分）
• install2.swm（第二部分）
• install3.swm（第三部分）
...（根据文件大小）",
                    TroubleshootingTips = @"分割注意事项：

⚠️ 文件大小设置：
• FAT32 限制：/FileSize:4000（约 4GB）
• 建议留有余量：/FileSize:3800
• 考虑 UEFI 启动文件占用空间

⚠️ 使用分割文件：
• 安装时 DISM 会自动识别所有 SWM 文件
• 确保所有 SWM 文件在同一目录
• 不要重命名分割后的文件

💡 典型使用场景：

1. 创建 FAT32 安装 U 盘：
   DISM /Split-Image
     /ImageFile:sources\install.wim
     /SWMFile:E:\sources\install.swm
     /FileSize:3800

2. 应用分割映像：
   DISM /Apply-Image
     /ImageFile:E:\sources\install.swm
     /SWMFile:E:\sources\install*.swm
     /ApplyDir:C:\",
                    RelatedCommands = "/Export-Image, /Apply-Image"
                },

                // ========== 高级维护 ==========
                new DismTutorialItemV2
                {
                    Category = "高级维护",
                    Title = "/Cleanup-MountPoints - 清理异常挂载点",
                    Command = "DISM /Cleanup-MountPoints",
                    FullSyntax = "DISM /Cleanup-MountPoints [/LogPath:<路径>]",
                    Parameters = @"可选参数：
  /LogPath:<路径> - 指定日志文件路径",
                    Description = "删除与已损坏的挂载映像关联的所有资源。用于修复因异常中断导致的挂载问题。",
                    WhenToUse = @"适用场景：
• 挂载操作异常中断
• 无法卸载映像
• 提示挂载点已存在
• 清理残留的挂载资源
• 系统维护时",
                    Duration = "约 1 - 5 分钟",
                    RiskLevel = "中低风险",
                    ExpectedOutput = @"成功输出：
The operation completed successfully.
Mount points cleaned up successfully.

清理内容：
• 损坏的挂载点
• 残留的资源句柄
• 无效的挂载记录",
                    TroubleshootingTips = @"使用场景：

⚠️ 何时使用：
• 强制关闭挂载窗口后
• 系统崩溃后挂载异常
• 提示挂载点已存在
• 无法执行新的挂载操作

⚠️ 注意事项：
• 不会卸载正常挂载的映像
• 不会删除挂载目录中的文件
• 可能需要重启才能完全生效

💡 完整清理流程：
1. 尝试正常卸载：DISM /Unmount-Image /MountDir:C:\Mount /Discard
2. 如失败，运行：DISM /Cleanup-MountPoints
3. 如仍有问题，重启系统
4. 删除并重新创建挂载目录
5. 重新执行挂载操作",
                    RelatedCommands = "/Mount-Image, /Unmount-Image"
                }
            };
        }

        public static string GetCommonScenarios()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("                          常见故障排除场景与解决方案");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine("【场景 1】系统文件损坏修复（标准流程）");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine("症状：系统功能异常、程序崩溃、文件丢失");
            sb.AppendLine();
            sb.AppendLine("执行步骤：");
            sb.AppendLine("  1. DISM /Online /Cleanup-Image /CheckHealth");
            sb.AppendLine("     → 快速检查系统健康状况");
            sb.AppendLine();
            sb.AppendLine("  2. DISM /Online /Cleanup-Image /ScanHealth");
            sb.AppendLine("     → 深度扫描系统损坏");
            sb.AppendLine();
            sb.AppendLine("  3. DISM /Online /Cleanup-Image /RestoreHealth");
            sb.AppendLine("     → 自动修复损坏的组件");
            sb.AppendLine();
            sb.AppendLine("  4. sfc /scannow");
            sb.AppendLine("     → 扫描并修复系统文件");
            sb.AppendLine();
            sb.AppendLine("  5. 重启系统");
            sb.AppendLine();
            sb.AppendLine("预计时间：30 - 60 分钟");
            sb.AppendLine();

            sb.AppendLine("【场景 2】Windows 更新失败修复");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine("症状：更新失败、错误代码 0x800f081f/0x800f0922、更新进度卡住");
            sb.AppendLine();
            sb.AppendLine("执行步骤：");
            sb.AppendLine("  1. DISM /Online /Cleanup-Image /StartComponentCleanup");
            sb.AppendLine("     → 清理损坏的组件存储");
            sb.AppendLine();
            sb.AppendLine("  2. DISM /Online /Cleanup-Image /RestoreHealth");
            sb.AppendLine("     → 修复系统组件");
            sb.AppendLine();
            sb.AppendLine("  3. sfc /scannow");
            sb.AppendLine("     → 修复系统文件");
            sb.AppendLine();
            sb.AppendLine("  4. 重启系统");
            sb.AppendLine();
            sb.AppendLine("  5. 再次尝试 Windows 更新");
            sb.AppendLine();
            sb.AppendLine("预计时间：40 - 80 分钟");
            sb.AppendLine();

            sb.AppendLine("【场景 3】离线系统修复（无法启动系统）");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine("症状：系统无法启动、蓝屏、启动循环");
            sb.AppendLine();
            sb.AppendLine("执行步骤：");
            sb.AppendLine("  1. 使用 Windows 安装 U 盘启动");
            sb.AppendLine("  2. 选择修复计算机 → 疑难解答 → 命令提示符");
            sb.AppendLine("  3. 确定系统盘符（可能是 D: 或 E:）");
            sb.AppendLine(@"     dir C:\  （查看内容确认）");
            sb.AppendLine();
            sb.AppendLine(@"  4. DISM /Image:D:\ /Cleanup-Image /RestoreHealth");
            sb.AppendLine("     → 修复离线系统映像");
            sb.AppendLine();
            sb.AppendLine(@"  5. sfc /scannow /offbootdir=D:\ /offwindir=D:\Windows");
            sb.AppendLine("     → 离线修复系统文件");
            sb.AppendLine();
            sb.AppendLine("  6. 重启系统");
            sb.AppendLine();
            sb.AppendLine("预计时间：30 - 60 分钟");
            sb.AppendLine();

            sb.AppendLine("【场景 4】C 盘空间不足清理");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine("症状：C 盘变红、系统提示空间不足");
            sb.AppendLine();
            sb.AppendLine("执行步骤：");
            sb.AppendLine("  1. DISM /Online /Cleanup-Image /AnalyzeComponentStore");
            sb.AppendLine("     → 分析可清理空间");
            sb.AppendLine();
            sb.AppendLine("  2. DISM /Online /Cleanup-Image /StartComponentCleanup");
            sb.AppendLine("     → 安全清理组件存储");
            sb.AppendLine();
            sb.AppendLine("  3. （可选）DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase");
            sb.AppendLine("     → 深度清理（⚠️ 执行后无法卸载更新）");
            sb.AppendLine();
            sb.AppendLine("  4. 使用磁盘清理工具清理临时文件");
            sb.AppendLine();
            sb.AppendLine("预计释放空间：2 - 15 GB");
            sb.AppendLine();

            sb.AppendLine("【场景 5】安装 .NET Framework 3.5");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine("症状：运行老软件提示需要 .NET Framework 3.5");
            sb.AppendLine();
            sb.AppendLine("在线安装：");
            sb.AppendLine("  DISM /Online /Enable-Feature /FeatureName:NetFx3 /All");
            sb.AppendLine();
            sb.AppendLine("离线安装（使用安装介质）：");
            sb.AppendLine("  DISM /Online /Enable-Feature /FeatureName:NetFx3 /All");
            sb.AppendLine(@"    /Source:D:\sources\sxs");
            sb.AppendLine("    /LimitAccess");
            sb.AppendLine();
            sb.AppendLine("注意：将 D: 替换为实际的安装介质盘符");
            sb.AppendLine();

            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            return sb.ToString();
        }

        public static string GetQuickReference()
        {
            return @"═══════════════════════════════════════════════════════════════════════════════
                              DISM 命令快速参考卡
═══════════════════════════════════════════════════════════════════════════════

【系统健康检查】
─────────────────────────────────────────────────────────────────────────────
快速检查    DISM /Online /Cleanup-Image /CheckHealth
深度扫描    DISM /Online /Cleanup-Image /ScanHealth
在线修复    DISM /Online /Cleanup-Image /RestoreHealth
离线修复    DISM /Online /Cleanup-Image /RestoreHealth /Source:WIM:X:\sources\install.wim:1 /LimitAccess

【组件存储清理】
─────────────────────────────────────────────────────────────────────────────
分析空间    DISM /Online /Cleanup-Image /AnalyzeComponentStore
安全清理    DISM /Online /Cleanup-Image /StartComponentCleanup
深度清理    DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase

【Windows 功能管理】
─────────────────────────────────────────────────────────────────────────────
列出功能    DISM /Online /Get-Features
启用功能    DISM /Online /Enable-Feature /FeatureName:<名称> /All
禁用功能    DISM /Online /Disable-Feature /FeatureName:<名称>

【包管理】
─────────────────────────────────────────────────────────────────────────────
列出包      DISM /Online /Get-Packages
安装包      DISM /Online /Add-Package /PackagePath:<路径>

【驱动管理】
─────────────────────────────────────────────────────────────────────────────
列出驱动    DISM /Online /Get-Drivers
添加驱动    DISM /Online /Add-Driver /Driver:<路径> /Recurse

【映像文件操作】
─────────────────────────────────────────────────────────────────────────────
查看信息    DISM /Get-ImageInfo /ImageFile:<路径>
挂载映像    DISM /Mount-Image /ImageFile:<路径> /Index:<N> /MountDir:<路径>
卸载保存    DISM /Unmount-Image /MountDir:<路径> /Commit
卸载放弃    DISM /Unmount-Image /MountDir:<路径> /Discard
导出映像    DISM /Export-Image /SourceImageFile:<路径> /SourceIndex:<N> /DestinationImageFile:<路径>
分割映像    DISM /Split-Image /ImageFile:<路径> /SWMFile:<路径> /FileSize:<MB>

【维护命令】
─────────────────────────────────────────────────────────────────────────────
清理挂载点  DISM /Cleanup-MountPoints

═══════════════════════════════════════════════════════════════════════════════
【常用组合命令】

标准修复流程：
  DISM /Online /Cleanup-Image /RestoreHealth && sfc /scannow

完整检查流程：
  DISM /Online /Cleanup-Image /CheckHealth
  DISM /Online /Cleanup-Image /ScanHealth
  DISM /Online /Cleanup-Image /RestoreHealth
  sfc /scannow

═══════════════════════════════════════════════════════════════════════════════
【参数说明】

/Online          - 针对当前运行的操作系统
/Image:<路径>    - 指定离线映像路径
/LogPath:<路径>  - 指定日志文件路径
/Quiet           - 静默模式（无输出）
/NoRestart       - 阻止自动重启

═══════════════════════════════════════════════════════════════════════════════
【重要提示】

• 所有命令需要管理员权限
• 路径含空格时需用双引号包裹
• 命令参数不区分大小写
• 建议操作前创建系统还原点
• 修复操作可能需要网络连接

═══════════════════════════════════════════════════════════════════════════════
";
        }

        public static string GetCompleteCommandReference()
        {
            return @"═══════════════════════════════════════════════════════════════════════════════
                         DISM 完整命令参考（按功能分类）
═══════════════════════════════════════════════════════════════════════════════

【映像服务命令 - /Cleanup-Image】
─────────────────────────────────────────────────────────────────────────────
/CheckHealth     - 检查映像是否可修复
/ScanHealth      - 扫描组件存储损坏
/RestoreHealth   - 扫描并修复组件存储
/AnalyzeComponentStore - 分析组件存储空间
/StartComponentCleanup - 清理组件存储
  [/ResetBase]   - 重置组件基础（释放更多空间）
  [/Defer]       - 延迟到维护窗口执行

【包服务命令】
─────────────────────────────────────────────────────────────────────────────
/Get-Packages    - 列出已安装的包
/Get-PackageInfo - 获取包详细信息
/Add-Package     - 添加 CAB/MSU 包
  /PackagePath:<路径>
  [/IgnoreCheck] - 忽略适用性检查
  [/PreventPending] - 跳过挂起操作检查
/Remove-Package  - 移除包
  /PackageName:<名称> | /PackagePath:<路径>

【功能管理命令】
─────────────────────────────────────────────────────────────────────────────
/Get-Features    - 列出所有功能
/Get-FeatureInfo - 获取功能详细信息
/Enable-Feature  - 启用功能
  /FeatureName:<名称>
  [/All]         - 启用所有父功能
  [/Source:<路径>] - 指定源路径
  [/LimitAccess] - 阻止访问 Windows Update
/Disable-Feature - 禁用功能
  /FeatureName:<名称>
  [/Remove]      - 完全删除功能文件

【驱动管理命令】
─────────────────────────────────────────────────────────────────────────────
/Get-Drivers     - 列出已安装的驱动
/Add-Driver      - 添加驱动
  /Driver:<路径>
  [/Recurse]     - 递归搜索子文件夹
  [/ForceUnsigned] - 强制安装未签名驱动
/Remove-Driver   - 移除驱动
  /Driver:<发布名称>
/Export-Driver   - 导出驱动到文件夹
  /Destination:<路径>

【映像文件管理命令】
─────────────────────────────────────────────────────────────────────────────
/Get-ImageInfo   - 获取映像信息
  /ImageFile:<路径>
  [/Index:<N>]   - 指定索引获取详细信息
/Mount-Image     - 挂载映像
  /ImageFile:<路径>
  /Index:<N> | /Name:<名称>
  /MountDir:<路径>
  [/ReadOnly]    - 只读挂载
  [/CheckIntegrity] - 检查完整性
/Unmount-Image   - 卸载映像
  /MountDir:<路径>
  /Commit | /Discard
/Commit-Image    - 提交挂载映像的更改
  /MountDir:<路径>
/Apply-Image     - 应用映像到指定位置
  /ImageFile:<路径>
  /ApplyDir:<路径>
  [/Index:<N>]
/Export-Image    - 导出映像
  /SourceImageFile:<路径>
  /SourceIndex:<N>
  /DestinationImageFile:<路径>
  [/Compress:{max|fast|none}]
/Split-Image     - 分割映像
  /ImageFile:<路径>
  /SWMFile:<路径>
  /FileSize:<MB>
/Append-Image    - 附加映像到 WIM
  /ImageFile:<路径>
  /CaptureDir:<路径>
  /Name:<名称>
/Capture-Image   - 捕获映像
  /ImageFile:<路径>
  /CaptureDir:<路径>
  /Name:<名称>
  [/Compress:{max|fast|none}]

【维护命令】
─────────────────────────────────────────────────────────────────────────────
/Cleanup-MountPoints - 清理异常挂载点
/Remount-Image   - 恢复丢失的挂载
/Get-MountedImageInfo - 获取挂载映像信息

【国际设置命令】
─────────────────────────────────────────────────────────────────────────────
/Set-UILang      - 设置 UI 语言
/Set-UserLocale  - 设置用户区域
/Set-SysLocale   - 设置系统区域
/Set-InputLocale - 设置输入区域
/Set-TimeZone    - 设置时区

═══════════════════════════════════════════════════════════════════════════════
【全局选项】（适用于所有命令）
─────────────────────────────────────────────────────────────────────────────
/Online          - 针对当前运行的操作系统
/Image:<路径>    - 指定离线映像路径
/LogPath:<路径>  - 指定日志文件路径
/LogLevel:{Errors|Warnings|WarningsInfo} - 日志级别
/Quiet           - 静默模式
/NoRestart       - 阻止自动重启
/English         - 以英文显示输出

═══════════════════════════════════════════════════════════════════════════════
";
        }
    }

    public class DismTutorialItemV2
    {
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Command { get; set; } = "";
        public string FullSyntax { get; set; } = "";
        public string Parameters { get; set; } = "";
        public string Description { get; set; } = "";
        public string WhenToUse { get; set; } = "";
        public string Duration { get; set; } = "";
        public string RiskLevel { get; set; } = "";
        public string ExpectedOutput { get; set; } = "";
        public string TroubleshootingTips { get; set; } = "";
        public string RelatedCommands { get; set; } = "";
    }
}
