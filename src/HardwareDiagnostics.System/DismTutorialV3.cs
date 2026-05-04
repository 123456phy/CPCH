using System;
using System.Collections.Generic;
using System.Text;

namespace HardwareDiagnostics.System
{
    public class DismTutorialV3
    {
        public static string GetFullTutorial()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine(GetHeader());
            sb.AppendLine(GetWhatIsDism());
            sb.AppendLine(GetQuickStart());
            sb.AppendLine(GetHealthCheckSection());
            sb.AppendLine(GetCleanupSection());
            sb.AppendLine(GetFeatureSection());
            sb.AppendLine(GetPackageSection());
            sb.AppendLine(GetDriverSection());
            sb.AppendLine(GetImageSection());
            sb.AppendLine(GetTroubleshootingSection());
            sb.AppendLine(GetQuickReference());
            
            return sb.ToString();
        }

        private static string GetHeader()
        {
            return @"
================================================================================
                    DISM 部署映像服务和管理 - 终极使用指南
              基于微软官方文档 | 比官方更明了、详细、人性化
================================================================================

【DISM 是什么？】
DISM (Deployment Image Servicing and Management) 是 Windows 内置的命令行工具，
用于维护、修复和准备 Windows 映像。

【它能做什么？】
  [OK] 检查并修复系统文件损坏（不用重装系统！）
  [OK] 清理 WinSxS 组件存储释放磁盘空间
  [OK] 启用/禁用 Windows 功能（如 .NET Framework）
  [OK] 安装/卸载更新包和驱动程序
  [OK] 管理 Windows 映像文件（WIM/VHD/VHDX）

【DISM 在哪里？】
  位置：C:\Windows\System32\DISM.exe
  所有 Windows 10/11 都自带，无需安装

【运行要求】
  [!] 必须以管理员身份运行命令提示符
  [!] 在线修复需要网络连接（或使用本地源）
  [!] 建议在电源连接状态下操作

";
        }

        private static string GetWhatIsDism()
        {
            return @"
================================================================================
                        第一章：DISM 基础概念
================================================================================

【核心概念】

1. 组件存储 (Component Store / WinSxS)
   --------------------------------------
   Windows 的系统文件仓库，位于 C:\Windows\WinSxS
   * 包含系统文件的所有版本
   * 允许系统回滚更新
   * 随着时间增长会占用大量空间
   * DISM 可以安全清理其中的旧版本

2. 映像 (Image)
   --------------------------------------
   Windows 系统的完整副本，常见格式：
   * WIM (Windows Imaging Format) - .wim 文件
   * VHD/VHDX (虚拟硬盘) - .vhd/.vhdx 文件
   * FFU (Full Flash Update) - .ffu 文件

3. 在线 vs 离线
   --------------------------------------
   * /Online  - 操作当前运行的系统（最常用）
   * /Image   - 操作未运行的系统映像（高级用法）

【命令基本结构】

  DISM {目标} {操作} [参数]

  示例：
  DISM /Online /Cleanup-Image /CheckHealth
  |    |       |             |
  |    |       |             +-- 具体操作
  |    |       +-- 操作类别
  |    +-- 目标（当前系统）
  +-- 命令工具

";
        }

        private static string GetQuickStart()
        {
            return @"
================================================================================
                    第二章：快速入门 - 常用场景
================================================================================

【场景 1：系统变慢，怀疑文件损坏】
--------------------------------------
步骤 1（快速检查）：
  DISM /Online /Cleanup-Image /CheckHealth

步骤 2（如发现问题，进行修复）：
  DISM /Online /Cleanup-Image /RestoreHealth

步骤 3（修复后检查系统文件）：
  sfc /scannow

【场景 2：C盘空间不足】
--------------------------------------
步骤 1（分析可清理空间）：
  DISM /Online /Cleanup-Image /AnalyzeComponentStore

步骤 2（执行清理）：
  DISM /Online /Cleanup-Image /StartComponentCleanup

【场景 3：安装 .NET Framework 3.5】
--------------------------------------
在线安装：
  DISM /Online /Enable-Feature /FeatureName:NetFx3 /All

离线安装（使用 Windows 安装盘）：
  DISM /Online /Enable-Feature /FeatureName:NetFx3 /All /Source:D:\sources\sxs /LimitAccess

【场景 4：导出所有驱动备份】
--------------------------------------
  DISM /Online /Export-Driver /Destination:D:\DriversBackup

";
        }

        private static string GetHealthCheckSection()
        {
            return @"
================================================================================
              第三章：系统健康检查与修复（最常用功能）
================================================================================

【3.1 快速健康检查 /CheckHealth】
--------------------------------------
用途：快速检查组件存储是否损坏
速度：10-30 秒
风险：无（只读操作）

命令：
  DISM /Online /Cleanup-Image /CheckHealth

输出解读：
  * No component store corruption detected. 
    -> 组件存储正常，无需修复
  
  * The component store is repairable. 
    -> 发现问题，可以修复，继续执行 /RestoreHealth
  
  * The component store is not repairable. 
    -> 严重损坏，可能需要重装系统

【3.2 深度扫描 /ScanHealth】
--------------------------------------
用途：深度扫描组件存储的完整性
速度：5-15 分钟
风险：无（只读操作）

命令：
  DISM /Online /Cleanup-Image /ScanHealth

与 /CheckHealth 的区别：
  * /CheckHealth 只是快速检查标记位
  * /ScanHealth 会实际扫描所有组件
  * 如果 /CheckHealth 通过但系统仍有问题，运行此命令

【3.3 系统修复 /RestoreHealth】
--------------------------------------
用途：修复组件存储中的损坏
速度：10-30 分钟（取决于网络速度）
风险：低（会自动创建还原点）

命令：
  DISM /Online /Cleanup-Image /RestoreHealth

工作原理：
  1. 连接到 Windows Update 服务
  2. 下载损坏文件的正确版本
  3. 替换损坏的组件

离线修复（无网络时使用）：
  DISM /Online /Cleanup-Image /RestoreHealth /Source:WIM:D:\sources\install.wim:1 /LimitAccess

参数说明：
  * /Source:WIM: 指定 WIM 文件路径
  * :1 表示使用映像索引 1（通常是专业版）
  * /LimitAccess 阻止连接到 Windows Update

【3.4 完整修复流程】
--------------------------------------
标准修复流程（推荐）：

  1. DISM /Online /Cleanup-Image /CheckHealth
     -> 快速检查

  2. DISM /Online /Cleanup-Image /ScanHealth
     -> 深度扫描（如快速检查通过但问题仍存在）

  3. DISM /Online /Cleanup-Image /RestoreHealth
     -> 在线修复

  4. sfc /scannow
     -> 扫描并修复系统文件

【3.5 常见错误及解决】
--------------------------------------
错误 0x800f081f（找不到源文件）：
  原因：无法从 Windows Update 下载修复文件
  解决：使用离线修复，指定本地 install.wim 文件

错误 0x800f0906（无法下载源文件）：
  原因：网络问题或 Windows Update 服务异常
  解决：
    1. 检查网络连接
    2. 重置 Windows Update 组件
    3. 使用离线修复

错误 87（参数错误）：
  原因：命令语法错误
  解决：检查空格和参数拼写

";
        }

        private static string GetCleanupSection()
        {
            return @"
================================================================================
                    第四章：组件存储清理
================================================================================

【4.1 为什么要清理？】
--------------------------------------
WinSxS 文件夹（C:\Windows\WinSxS）会不断增长，包含：
  * 系统文件的多个版本
  * 已安装更新的备份
  * 卸载更新所需的文件

随着时间推移，可能占用 10-20GB 甚至更多空间。

【4.2 分析组件存储 /AnalyzeComponentStore】
--------------------------------------
用途：分析 WinSxS 文件夹，显示清理建议
速度：1-3 分钟
风险：无（只读操作）

命令：
  DISM /Online /Cleanup-Image /AnalyzeComponentStore

输出示例：
  Windows 资源保护找到了损坏文件，但其中有一些文件无法修复。
  
  组件存储 (WinSxS) 信息：
  * Windows 资源保护找到了损坏文件...
  
  组件存储的实际大小：7.5 GB
  与 Windows 共享的文件：4.2 GB
  备份和已禁用的功能：2.1 GB
  缓存和临时数据：1.2 GB
  
  上次清理日期：2024-01-15
  可回收的程序包数量：5
  建议的清理操作：StartComponentCleanup

【4.3 标准清理 /StartComponentCleanup】
--------------------------------------
用途：安全清理组件存储中的旧版本文件
速度：5-15 分钟
风险：低（清理后无法卸载已安装更新）

命令：
  DISM /Online /Cleanup-Image /StartComponentCleanup

清理内容：
  * 删除被取代的组件版本
  * 清理临时文件
  * 删除过期的缓存

注意事项：
  * 清理后无法卸载之前的 Windows 更新
  * 如果系统运行正常，可以安全执行
  * 建议每 3-6 个月执行一次

【4.4 深度清理 /ResetBase】
--------------------------------------
用途：彻底清理，将组件存储重置到最新版本
速度：10-30 分钟
风险：中（清理后无法回滚任何更新）

命令：
  DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase

与标准清理的区别：
  * 标准清理：保留最近更新的回滚能力
  * 深度清理：删除所有旧版本，无法回滚任何更新

警告：
  * 执行后无法卸载任何已安装的更新
  * 仅在系统稳定运行一段时间后再执行
  * 可以释放更多空间（通常多 1-3GB）

【4.5 清理建议】
--------------------------------------
何时清理：
  * C盘空间不足时
  * WinSxS 文件夹超过 10GB
  * 系统已稳定运行 3-6 个月

清理前准备：
  1. 确保系统运行正常
  2. 创建系统还原点
  3. 备份重要数据

清理频率：
  * 轻度用户：每年 1-2 次
  * 重度用户：每 3-6 个月
  * 服务器：谨慎操作，建议每年 1 次

";
        }

        private static string GetFeatureSection()
        {
            return @"
================================================================================
                    第五章：Windows 功能管理
================================================================================

【5.1 什么是 Windows 功能？】
--------------------------------------
Windows 可选组件，如：
  * .NET Framework 3.5
  * Hyper-V
  * Windows 沙盒
  * IIS (Internet Information Services)
  * Telnet 客户端
  * 等等

【5.2 列出所有功能 /Get-Features】
--------------------------------------
用途：查看系统中所有可选功能及其状态
速度：10-30 秒

命令：
  DISM /Online /Get-Features

输出解读：
  功能名称 : TelnetClient
  状态 : 已禁用
  
  功能名称 : NetFx3
  状态 : 已启用

状态说明：
  * 已启用 - 功能已安装并可用
  * 已禁用 - 功能未安装
  * 已启用并挂起 - 等待重启后生效
  * 已禁用并挂起 - 等待重启后移除

【5.3 查看功能详情 /Get-FeatureInfo】
--------------------------------------
用途：查看特定功能的详细信息

命令：
  DISM /Online /Get-FeatureInfo /FeatureName:NetFx3

输出内容：
  * 功能名称
  * 显示名称
  * 描述
  * 是否需要重启
  * 依赖的功能
  * 状态

【5.4 启用功能 /Enable-Feature】
--------------------------------------
用途：安装并启用 Windows 功能
速度：1-10 分钟（取决于功能大小）
风险：低

基本命令：
  DISM /Online /Enable-Feature /FeatureName:NetFx3

启用并包含所有子功能：
  DISM /Online /Enable-Feature /FeatureName:NetFx3 /All

离线安装（使用安装介质）：
  DISM /Online /Enable-Feature /FeatureName:NetFx3 /All /Source:D:\sources\sxs /LimitAccess

常用功能名称：
  * NetFx3 - .NET Framework 3.5
  * TelnetClient - Telnet 客户端
  * Microsoft-Hyper-V-All - Hyper-V
  * Containers-DisposableClientVM - Windows 沙盒
  * IIS-WebServerRole - IIS Web 服务器

【5.5 禁用功能 /Disable-Feature】
--------------------------------------
用途：卸载 Windows 功能
速度：1-5 分钟
风险：低（确保不卸载正在使用的功能）

命令：
  DISM /Online /Disable-Feature /FeatureName:TelnetClient

删除功能文件（释放空间）：
  DISM /Online /Disable-Feature /FeatureName:TelnetClient /Remove

警告：
  * 某些功能被其他功能依赖
  * 禁用前确认没有程序正在使用
  * /Remove 会删除文件，之后需要安装源才能重新启用

【5.6 功能管理最佳实践】
--------------------------------------
启用功能前：
  1. 查看功能详情，了解用途
  2. 检查系统要求
  3. 确保有安装源（离线安装时）

禁用功能前：
  1. 确认没有程序依赖此功能
  2. 考虑是否只是暂时不用
  3. 不确定时，不要加 /Remove 参数

";
        }

        private static string GetPackageSection()
        {
            return @"
================================================================================
                    第六章：包管理（更新和补丁）
================================================================================

【6.1 什么是包？】
--------------------------------------
Windows 更新包，格式包括：
  * .cab - Cabinet 文件（压缩包）
  * .msu - Windows 更新独立安装程序
  * .wim - Windows 映像文件

【6.2 列出已安装包 /Get-Packages】
--------------------------------------
用途：查看系统中已安装的更新包
速度：10-30 秒

命令：
  DISM /Online /Get-Packages

输出示例：
  包标识 : Package_for_KB5028185~31bf3856ad364e35~amd64~~19041.3324.1.1
  状态 : 已安装
  发布日期 : 2023-07-11

状态说明：
  * 已安装 - 包已安装
  * 已安装并挂起 - 等待重启
  * 已卸载并挂起 - 等待重启完成卸载

【6.3 查看包详情 /Get-PackageInfo】
--------------------------------------
用途：查看特定包的详细信息

命令：
  DISM /Online /Get-PackageInfo /PackageName:Package_for_KB5028185~31bf3856ad364e35~amd64~~19041.3324.1.1

或使用路径：
  DISM /Online /Get-PackageInfo /PackagePath:C:\Updates\update.msu

【6.4 安装包 /Add-Package】
--------------------------------------
用途：安装 .cab 或 .msu 更新包
速度：取决于包大小
风险：中（确保包来源可信）

安装 .cab 包：
  DISM /Online /Add-Package /PackagePath:C:\Updates\package.cab

安装 .msu 包：
  DISM /Online /Add-Package /PackagePath:C:\Updates\update.msu

安装多个包：
  DISM /Online /Add-Package /PackagePath:C:\Updates\*.cab

不重启继续：
  DISM /Online /Add-Package /PackagePath:C:\Updates\package.cab /NoRestart

【6.5 卸载包 /Remove-Package】
--------------------------------------
用途：卸载已安装的更新包
速度：1-5 分钟
风险：中（可能影响系统稳定性）

命令：
  DISM /Online /Remove-Package /PackageName:Package_for_KB5028185~31bf3856ad364e35~amd64~~19041.3324.1.1

注意：
  * 使用 /Get-Packages 获取准确的包名称
  * 卸载安全更新可能使系统易受攻击
  * 某些包无法卸载（系统关键更新）

【6.6 包管理注意事项】
--------------------------------------
安装包前：
  * 验证包来源可信
  * 检查包与系统版本兼容
  * 备份重要数据

卸载包前：
  * 确认包不是关键安全更新
  * 了解卸载可能带来的影响
  * 创建系统还原点

";
        }

        private static string GetDriverSection()
        {
            return @"
================================================================================
                    第七章：驱动程序管理
================================================================================

【7.1 列出已安装驱动 /Get-Drivers】
--------------------------------------
用途：查看系统中所有已安装的驱动程序
速度：10-30 秒

基本命令：
  DISM /Online /Get-Drivers

显示所有驱动（包括内置）：
  DISM /Online /Get-Drivers /All

格式化输出：
  DISM /Online /Get-Drivers /Format:Table

输出字段说明：
  * 已发布的名称 - 驱动的唯一标识符
  * 原始文件名 - 驱动文件的原始名称
  * 收件箱 - 是否为 Windows 内置驱动
  * 类名称 - 驱动类别（如显示适配器、网络适配器）
  * 提供者名称 - 驱动开发商
  * 日期 - 驱动发布日期
  * 版本 - 驱动版本号

【7.2 查看驱动详情 /Get-DriverInfo】
--------------------------------------
用途：查看特定驱动的详细信息

命令：
  DISM /Online /Get-DriverInfo /Driver:oem1.inf

输出内容：
  * 驱动名称和版本
  * 硬件 ID
  * 兼容 ID
  * 驱动文件列表
  * 签名信息

【7.3 添加驱动 /Add-Driver】
--------------------------------------
用途：向离线映像添加驱动程序
速度：取决于驱动数量
风险：中（确保驱动与硬件兼容）

注意：此命令主要用于离线映像，当前运行系统请使用设备管理器

添加单个驱动：
  DISM /Image:C:\Offline /Add-Driver /Driver:C:\Drivers\driver.inf

添加文件夹中所有驱动：
  DISM /Image:C:\Offline /Add-Driver /Driver:C:\Drivers /Recurse

强制添加未签名驱动：
  DISM /Image:C:\Offline /Add-Driver /Driver:C:\Drivers\driver.inf /ForceUnsigned

【7.4 删除驱动 /Remove-Driver】
--------------------------------------
用途：从离线映像中删除驱动程序

命令：
  DISM /Image:C:\Offline /Remove-Driver /Driver:oem1.inf

警告：
  * 删除关键驱动可能导致映像无法启动
  * 删除前确认驱动不是必需的
  * 建议先备份映像

【7.5 导出驱动 /Export-Driver】
--------------------------------------
用途：导出所有已安装驱动到指定文件夹
速度：2-10 分钟（取决于驱动数量）
风险：无

命令：
  DISM /Online /Export-Driver /Destination:D:\DriversBackup

用途：
  * 备份当前系统驱动
  * 为系统重装做准备
  * 迁移驱动到其他计算机

导出后文件夹结构：
  D:\DriversBackup\
    ├── oem1.inf
    ├── oem1.inf.cat
    ├── oem1.inf.sys
    ├── oem2.inf
    └── ...

【7.6 驱动管理最佳实践】
--------------------------------------
导出驱动：
  * 定期备份驱动（每季度一次）
  * 重装系统前必须备份
  * 保存到外部存储设备

添加驱动：
  * 仅从官方来源获取驱动
  * 验证驱动与硬件和系统版本兼容
  * 优先使用设备管理器安装

";
        }

        private static string GetImageSection()
        {
            return @"
================================================================================
                    第八章：映像文件操作（高级）
================================================================================

【8.1 什么是映像文件？】
--------------------------------------
包含完整 Windows 系统的文件，常用于：
  * 系统部署和恢复
  * 制作自定义 Windows 安装盘
  * 备份和还原系统
  * 虚拟机创建

常见格式：
  * .wim - Windows 映像格式（最常用）
  * .vhd/.vhdx - 虚拟硬盘
  * .ffu - 完整闪存更新（用于移动设备）
  * .swm - 分割的 WIM 文件

【8.2 查看映像信息 /Get-ImageInfo】
--------------------------------------
用途：查看 WIM 文件中包含的映像信息
速度：几秒钟
风险：无

命令：
  DISM /Get-ImageInfo /ImageFile:D:\sources\install.wim

输出内容：
  * 映像索引号
  * 映像名称（如 Windows 11 专业版）
  * 映像描述
  * 映像大小
  * 体系结构（x86/x64/ARM64）
  * 版本号

【8.3 挂载映像 /Mount-Image】
--------------------------------------
用途：将 WIM 文件挂载到文件夹，以便查看和修改
速度：1-5 分钟
风险：中（修改不当可能损坏映像）

命令：
  DISM /Mount-Image /ImageFile:D:\sources\install.wim /Index:1 /MountDir:C:\Mount

参数说明：
  * /ImageFile - WIM 文件路径
  * /Index - 映像索引号（从 /Get-ImageInfo 获取）
  * /MountDir - 挂载目标文件夹（必须为空）

只读挂载：
  DISM /Mount-Image /ImageFile:D:\sources\install.wim /Index:1 /MountDir:C:\Mount /ReadOnly

【8.4 卸载映像 /Unmount-Image】
--------------------------------------
用途：卸载已挂载的映像

保存更改：
  DISM /Unmount-Image /MountDir:C:\Mount /Commit

放弃更改：
  DISM /Unmount-Image /MountDir:C:\Mount /Discard

警告：
  * 确保没有程序正在访问挂载文件夹
  * 未卸载前不要删除挂载文件夹
  * 使用 /Commit 前确认更改正确

【8.5 导出映像 /Export-Image】
--------------------------------------
用途：将映像从一个 WIM 导出到另一个 WIM
速度：取决于映像大小

命令：
  DISM /Export-Image /SourceImageFile:D:\sources\install.wim /SourceIndex:1 /DestinationImageFile:D:\custom.wim

压缩导出（减小文件大小）：
  DISM /Export-Image /SourceImageFile:D:\sources\install.wim /SourceIndex:1 /DestinationImageFile:D:\custom.wim /Compress:max

压缩级别：
  * none - 不压缩（最快）
  * fast - 快速压缩（平衡）
  * max - 最大压缩（最小文件但较慢）

【8.6 分割映像 /Split-Image】
--------------------------------------
用途：将大 WIM 文件分割成多个小文件
速度：取决于映像大小

命令：
  DISM /Split-Image /ImageFile:D:\sources\install.wim /SWMFile:D:\split\install.swm /FileSize:4000

参数说明：
  * /FileSize - 每个分割文件的大小（MB）
  * 适用于 FAT32 文件系统（单文件最大 4GB）

输出文件：
  * install.swm
  * install2.swm
  * install3.swm
  * ...

【8.7 应用映像 /Apply-Image】
--------------------------------------
用途：将 WIM 映像应用到磁盘分区
速度：10-30 分钟
风险：高（会覆盖目标分区数据）

命令：
  DISM /Apply-Image /ImageFile:D:\sources\install.wim /Index:1 /ApplyDir:W:\

参数说明：
  * /ApplyDir - 目标分区（通常是已格式化的系统分区）

应用并验证：
  DISM /Apply-Image /ImageFile:D:\sources\install.wim /Index:1 /ApplyDir:W:\ /Verify

【8.8 捕获映像 /Capture-Image】
--------------------------------------
用途：将磁盘分区捕获为 WIM 文件
速度：10-30 分钟
风险：中（确保捕获时系统未被修改）

命令：
  DISM /Capture-Image /ImageFile:D:\backup.wim /CaptureDir:C:\ /Name:WindowsBackup

参数说明：
  * /CaptureDir - 要捕获的源分区
  * /Name - 映像名称（显示在映像信息中）
  * /Description - 映像描述

排除文件：
  DISM /Capture-Image /ImageFile:D:\backup.wim /CaptureDir:C:\ /Name:WindowsBackup /ConfigFile:C:\exclude.xml

【8.9 映像操作注意事项】
--------------------------------------
挂载映像前：
  * 确保有足够的磁盘空间
  * 挂载文件夹必须存在且为空
  * 建议使用空文件夹（如 C:\Mount）

修改映像时：
  * 可以添加驱动、更新、语言包
  * 可以启用/禁用功能
  * 不要删除关键系统文件

捕获映像前：
  * 清理临时文件
  * 运行磁盘清理
  * 确保系统处于干净状态
  * 使用 sysprep 准备系统（用于部署）

";
        }

        private static string GetTroubleshootingSection()
        {
            return @"
================================================================================
                第九章：故障排除与实战案例
================================================================================

【案例 1：系统文件损坏导致程序崩溃】
--------------------------------------
症状：
  * 多个程序随机崩溃
  * 系统更新失败
  * 事件查看器显示文件损坏错误

解决步骤：
  1. DISM /Online /Cleanup-Image /CheckHealth
     -> 显示组件存储可修复
  
  2. DISM /Online /Cleanup-Image /RestoreHealth
     -> 等待修复完成（约 15 分钟）
  
  3. sfc /scannow
     -> 验证系统文件完整性
  
  4. 重启计算机

【案例 2：Windows 更新反复失败】
--------------------------------------
症状：
  * 更新下载成功但安装失败
  * 错误代码 0x800f081f
  * 更新历史显示多个失败记录

解决步骤：
  1. DISM /Online /Cleanup-Image /ScanHealth
     -> 深度扫描组件存储
  
  2. DISM /Online /Cleanup-Image /RestoreHealth
     -> 修复组件存储
  
  3. 如果仍失败，使用离线修复：
     DISM /Online /Cleanup-Image /RestoreHealth /Source:WIM:D:\sources\install.wim:1 /LimitAccess
  
  4. 再次尝试 Windows 更新

【案例 3：C盘空间严重不足】
--------------------------------------
症状：
  * C盘只剩几百 MB
  * WinSxS 文件夹超过 15GB
  * 系统运行缓慢

解决步骤：
  1. DISM /Online /Cleanup-Image /AnalyzeComponentStore
     -> 分析可清理空间
  
  2. 如果显示可回收空间 > 2GB：
     DISM /Online /Cleanup-Image /StartComponentCleanup
  
  3. 如果系统稳定运行超过 6 个月：
     DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase
  
  4. 运行磁盘清理工具清理临时文件

【案例 4：安装 .NET Framework 3.5 失败】
--------------------------------------
症状：
  * 程序提示需要 .NET 3.5
  * Windows 更新安装失败
  * 控制面板安装失败

解决步骤：

  在线安装：
  DISM /Online /Enable-Feature /FeatureName:NetFx3 /All

  离线安装（使用安装介质）：
  DISM /Online /Enable-Feature /FeatureName:NetFx3 /All /Source:D:\sources\sxs /LimitAccess

【常见错误代码速查】
--------------------------------------
  +---------------+------------------------+---------------------------+
  | 错误代码      | 原因                   | 解决方案                  |
  +---------------+------------------------+---------------------------+
  | 0x800f081f    | 找不到源文件           | 使用 /Source 指定         |
  | 0x800f0906    | 无法下载源文件         | 检查网络或使用离线        |
  | 0x800f0922    | CBS 损坏               | 运行 /RestoreHealth       |
  | 0x80073712    | 组件存储损坏           | 运行 /RestoreHealth       |
  | 87            | 参数错误               | 检查命令语法              |
  | 112           | 磁盘空间不足           | 清理磁盘空间              |
  +---------------+------------------------+---------------------------+

【维护建议】
--------------------------------------
每月维护：
  1. 运行 DISM /Online /Cleanup-Image /CheckHealth
  2. 如需要，运行 /RestoreHealth
  3. 运行 sfc /scannow

每季度维护：
  1. 运行 /AnalyzeComponentStore 分析空间
  2. 如空间紧张，运行 /StartComponentCleanup
  3. 使用磁盘清理工具清理临时文件

";
        }

        private static string GetQuickReference()
        {
            return @"
================================================================================
                        附录：快速参考卡
================================================================================

【系统健康检查】
================================================================================
快速检查    DISM /Online /Cleanup-Image /CheckHealth
深度扫描    DISM /Online /Cleanup-Image /ScanHealth
在线修复    DISM /Online /Cleanup-Image /RestoreHealth
离线修复    DISM /Online /Cleanup-Image /RestoreHealth /Source:WIM:X:\sources\install.wim:1 /LimitAccess

【组件存储清理】
================================================================================
分析空间    DISM /Online /Cleanup-Image /AnalyzeComponentStore
安全清理    DISM /Online /Cleanup-Image /StartComponentCleanup
深度清理    DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase

【Windows 功能管理】
================================================================================
列出功能    DISM /Online /Get-Features
查看详情    DISM /Online /Get-FeatureInfo /FeatureName:<名称>
启用功能    DISM /Online /Enable-Feature /FeatureName:<名称> /All
禁用功能    DISM /Online /Disable-Feature /FeatureName:<名称>

【包管理】
================================================================================
列出包      DISM /Online /Get-Packages
查看详情    DISM /Online /Get-PackageInfo /PackageName:<名称>
安装包      DISM /Online /Add-Package /PackagePath:<路径>
卸载包      DISM /Online /Remove-Package /PackageName:<名称>

【驱动管理】
================================================================================
列出驱动    DISM /Online /Get-Drivers
添加驱动    DISM /Image:<路径> /Add-Driver /Driver:<路径> /Recurse
删除驱动    DISM /Image:<路径> /Remove-Driver /Driver:<名称>
导出驱动    DISM /Online /Export-Driver /Destination:<路径>

【映像文件操作】
================================================================================
查看信息    DISM /Get-ImageInfo /ImageFile:<路径>
挂载映像    DISM /Mount-Image /ImageFile:<路径> /Index:<N> /MountDir:<路径>
卸载保存    DISM /Unmount-Image /MountDir:<路径> /Commit
卸载放弃    DISM /Unmount-Image /MountDir:<路径> /Discard
导出映像    DISM /Export-Image /SourceImageFile:<路径> /SourceIndex:<N> /DestinationImageFile:<路径>
分割映像    DISM /Split-Image /ImageFile:<路径> /SWMFile:<路径> /FileSize:<MB>
应用映像    DISM /Apply-Image /ImageFile:<路径> /Index:<N> /ApplyDir:<路径>
捕获映像    DISM /Capture-Image /ImageFile:<路径> /CaptureDir:<路径> /Name:<名称>

【常用组合命令】
================================================================================
标准修复流程：
  DISM /Online /Cleanup-Image /RestoreHealth && sfc /scannow

完整检查流程：
  DISM /Online /Cleanup-Image /CheckHealth
  DISM /Online /Cleanup-Image /ScanHealth
  DISM /Online /Cleanup-Image /RestoreHealth
  sfc /scannow

清理并修复：
  DISM /Online /Cleanup-Image /StartComponentCleanup
  DISM /Online /Cleanup-Image /RestoreHealth

【全局参数】
================================================================================
/Online          - 操作当前运行的系统
/Image:<路径>    - 操作离线映像
/LogPath:<路径>  - 指定日志文件路径
/Quiet           - 静默模式
/NoRestart       - 阻止自动重启
/English         - 以英文显示输出

【重要提示】
================================================================================
* 所有命令需要管理员权限
* 路径含空格时用双引号包裹
* 命令参数不区分大小写
* 建议操作前创建系统还原点
* 修复操作可能需要网络连接

================================================================================
                        本文档基于微软官方文档编写
              https://learn.microsoft.com/zh-cn/windows-hardware/manufacture
================================================================================
";
        }
    }
}
