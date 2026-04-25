using System;
using System.Collections.Generic;
using System.Text;

namespace HardwareDiagnostics.System
{
    /// <summary>
    /// DISM 教程 V2 - 更人性化、贴合实际的教程
    /// 包含常用电脑疑难杂症排除方案
    /// </summary>
    public class DismTutorialV2
    {
        public static string GetIntroduction()
        {
            return @"🛠️ DISM 工具使用指南 - 让你的电脑重获新生

========================================

👋 嗨！我是你的电脑维修助手

DISM（部署映像服务和管理）是 Windows 内置的超级工具，就像电脑的'急救医生'。
不用重装系统，就能修复大部分系统问题！

🎯 什么时候需要用到 DISM？

💻 电脑变慢、卡顿，像蜗牛一样
🔵 蓝屏死机，错误代码满天飞  
🔄 Windows 更新总是失败
❌ 软件打不开，提示缺少文件
🐌 开机越来越慢，要喝好几杯咖啡
🔧 系统文件损坏，程序崩溃

🌟 DISM 能帮你做什么？

✅ 修复损坏的系统文件（不用重装！）
✅ 清理系统垃圾，释放磁盘空间
✅ 解决 Windows 更新问题
✅ 修复软件兼容性问题
✅ 恢复系统稳定性

⚠️ 使用前的温馨提示

1️⃣ 备份重要数据 - 虽然 DISM 很安全，但养成备份好习惯
2️⃣ 保持电源连接 - 笔记本请插上电源，别中途断电
3️⃣ 耐心等待 - 修复需要时间，去泡杯茶吧
4️⃣ 需要管理员权限 - 右键'以管理员身份运行'

🚀 快速开始

如果你是新手，建议按这个顺序操作：
1. 先运行 /CheckHealth - 快速体检
2. 有问题再运行 /ScanHealth - 详细检查  
3. 最后运行 /RestoreHealth - 自动修复

========================================
";
        }

        public static List<DismTutorialItemV2> GetTutorials()
        {
            return new List<DismTutorialItemV2>
            {
                // ========== 基础检查类 ==========
                new DismTutorialItemV2
                {
                    Category = "🔍 基础检查",
                    Title = "快速体检 - 30秒知道系统健康状况",
                    Command = "/Online /Cleanup-Image /CheckHealth",
                    Description = "像医生听诊一样，快速检查系统是否有问题",
                    WhenToUse = @"🤔 什么时候用：
• 电脑感觉'有点不对劲'
• 想快速知道系统是否健康
• 作为日常维护检查

📋 实际场景：
小明的电脑最近有点卡，他运行这个命令，30秒后显示'系统健康'，
说明问题不在系统文件，可能是软件或硬件问题。",
                    Duration = "⏱️ 约 30 秒 - 1 分钟",
                    RiskLevel = "🟢 零风险",
                    ExpectedOutput = "The component store is repairable - 需要修复\nNo component store corruption detected - 系统健康",
                    TroubleshootingTips = @"💡 如果显示需要修复：
继续运行 /ScanHealth 详细检查
然后运行 /RestoreHealth 自动修复"
                },

                new DismTutorialItemV2
                {
                    Category = "🔍 基础检查",
                    Title = "详细扫描 - 找出所有问题",
                    Command = "/Online /Cleanup-Image /ScanHealth",
                    Description = "全面扫描系统文件，找出所有损坏的地方",
                    WhenToUse = @"🤔 什么时候用：
• /CheckHealth 显示有问题
• 电脑经常蓝屏或崩溃
• 怀疑系统文件损坏
• 病毒查杀后系统异常

📋 实际场景：
小红的电脑蓝屏了，错误代码 0xc000000f。运行这个命令后，
发现系统文件损坏，然后用 /RestoreHealth 修复，问题解决了！",
                    Duration = "⏱️ 约 5 - 10 分钟",
                    RiskLevel = "🟢 零风险（只扫描，不修改）",
                    ExpectedOutput = "扫描完成后会显示系统健康状况",
                    TroubleshootingTips = @"💡 扫描结果解读：
• 显示损坏程度百分比
• 记录损坏的系统组件
• 为下一步修复做准备

⚠️ 注意：扫描期间电脑可能会变慢，这是正常的"
                },

                // ========== 系统修复类 ==========
                new DismTutorialItemV2
                {
                    Category = "🔧 系统修复",
                    Title = "自动修复 - 一键修复系统问题",
                    Command = "/Online /Cleanup-Image /RestoreHealth",
                    Description = "自动下载并替换损坏的系统文件，是 DISM 最强大的功能",
                    WhenToUse = @"🤔 什么时候用：
• /ScanHealth 发现系统损坏
• 电脑频繁蓝屏或死机
• Windows 更新总是失败
• 软件打不开，提示缺少 DLL
• 系统提示'系统文件损坏'

📋 实际场景：
小刚的电脑开机就蓝屏，安全模式也进不去。用 WinPE 启动后运行这个命令，
修复了损坏的系统文件，电脑恢复正常！",
                    Duration = "⏱️ 约 15 - 30 分钟（取决于网速）",
                    RiskLevel = "🟢 低风险",
                    ExpectedOutput = "The restore operation completed successfully - 修复成功",
                    TroubleshootingTips = @"💡 修复失败的解决办法：

1️⃣ 检查网络连接（需要从 Windows Update 下载文件）
2️⃣ 尝试使用本地源修复（见下一个命令）
3️⃣ 运行 sfc /scannow 辅助修复
4️⃣ 如果都失败，考虑使用系统还原或重置

🔧 常用组合拳：
DISM /Online /Cleanup-Image /RestoreHealth
然后运行：sfc /scannow
两个命令配合效果更好！"
                },

                new DismTutorialItemV2
                {
                    Category = "🔧 系统修复",
                    Title = "离线修复 - 没有网络也能修",
                    Command = "/Online /Cleanup-Image /RestoreHealth /Source:X:\\sources\\install.wim /LimitAccess",
                    Description = "使用 Windows 安装镜像作为修复源，不需要网络连接",
                    WhenToUse = @"🤔 什么时候用：
• 电脑没有网络连接
• Windows Update 无法访问
• 在线修复总是失败
• 企业内网环境
• 想使用特定版本的系统文件

📋 实际场景：
公司的电脑在内网，无法连接互联网。小王准备了 Windows 安装 U 盘，
使用这个命令成功修复了系统，不用叫 IT 部门！",
                    Duration = "⏱️ 约 10 - 20 分钟",
                    RiskLevel = "🟢 低风险",
                    ExpectedOutput = "使用本地 WIM 文件修复系统",
                    TroubleshootingTips = @"💡 准备工作：

1️⃣ 准备 Windows 安装镜像（ISO 或 U 盘）
2️⃣ 挂载 ISO 或插入 U 盘
3️⃣ 找到 sources\\install.wim 文件
4️⃣ 将命令中的 X: 替换为实际盘符

📁 常见位置：
• U 盘：E:\\sources\\install.wim
• 挂载的 ISO：D:\\sources\\install.wim
• 本地备份：C:\\WinBackup\\install.wim

⚠️ 注意：WIM 文件版本要和当前系统版本匹配！"
                },

                // ========== 疑难杂症解决方案 ==========
                new DismTutorialItemV2
                {
                    Category = "🚑 疑难杂症",
                    Title = "解决 Windows 更新失败（错误 0x800f081f 等）",
                    Command = @"/Online /Cleanup-Image /StartComponentCleanup
然后运行：
/Online /Cleanup-Image /RestoreHealth",
                    Description = "清理组件存储并修复系统，解决更新失败问题",
                    WhenToUse = @"🤔 什么时候用：
• Windows 更新总是失败
• 错误代码：0x800f081f、0x800f0922、0x800f0900
• 更新进度卡在 99%
• 提示'无法完成更新，正在撤销更改'
• 安装 .NET Framework 失败

📋 实际场景：
小李的电脑更新到 99% 就失败，反复重启。运行这两个命令后，
清理了 3GB 的损坏组件，更新成功！",
                    Duration = "⏱️ 约 20 - 40 分钟",
                    RiskLevel = "🟡 中低风险",
                    ExpectedOutput = "组件存储清理完成，系统修复成功",
                    TroubleshootingTips = @"💡 完整修复流程：

第 1 步：清理组件存储
DISM /Online /Cleanup-Image /StartComponentCleanup

第 2 步：修复系统
DISM /Online /Cleanup-Image /RestoreHealth

第 3 步：检查系统文件
sfc /scannow

第 4 步：重启电脑
shutdown /r /t 0

第 5 步：再次尝试 Windows 更新

🔧 如果还是失败：
• 使用 Windows 更新疑难解答
• 重置 Windows 更新组件
• 考虑使用媒体创建工具升级"
                },

                new DismTutorialItemV2
                {
                    Category = "🚑 疑难杂症",
                    Title = "解决软件打不开/缺少 DLL 文件",
                    Command = "/Online /Cleanup-Image /RestoreHealth",
                    Description = "修复损坏的系统组件和运行库，解决软件兼容性问题",
                    WhenToUse = @"🤔 什么时候用：
• 打开软件提示'缺少 MSVCP140.dll'
• 提示'应用程序无法正常启动 (0xc000007b)'
• 游戏打不开，提示缺少运行库
• 新安装的软件无法运行
• 系统提示'找不到入口点'

📋 实际场景：
小张安装了 Photoshop，打开时提示缺少 DLL。运行这个命令修复系统后，
Photoshop 正常启动，省下了重装系统的麻烦！",
                    Duration = "⏱️ 约 15 - 25 分钟",
                    RiskLevel = "🟢 低风险",
                    ExpectedOutput = "系统组件修复完成",
                    TroubleshootingTips = @"💡 配合 VC++ 运行库修复：

DISM 修复系统组件后，建议同时：
1️⃣ 安装最新的 VC++ 运行库（2005-2022）
2️⃣ 安装 .NET Framework 3.5 和 4.8
3️⃣ 安装 DirectX 运行库
4️⃣ 重启电脑

📦 常用运行库下载：
• VC++ Redistributable：微软官网
• .NET Framework：Windows 功能里启用
• DirectX：微软下载中心

🔧 如果还是缺少 DLL：
• 从其他正常电脑复制（同版本系统）
• 使用 DLL 修复工具
• 重新安装该软件"
                },

                new DismTutorialItemV2
                {
                    Category = "🚑 疑难杂症",
                    Title = "解决蓝屏死机（BSOD）问题",
                    Command = @"/Online /Cleanup-Image /CheckHealth
/Online /Cleanup-Image /ScanHealth  
/Online /Cleanup-Image /RestoreHealth
然后运行：sfc /scannow",
                    Description = "完整修复流程，解决因系统文件损坏导致的蓝屏",
                    WhenToUse = @"🤔 什么时候用：
• 电脑频繁蓝屏（一天好几次）
• 蓝屏错误代码：CRITICAL_PROCESS_DIED
• 蓝屏错误代码：SYSTEM_SERVICE_EXCEPTION
• 开机就蓝屏，进不了系统
• 安装软件/驱动后蓝屏

📋 实际场景：
小陈的电脑一天蓝屏 5-6 次，错误代码 CRITICAL_PROCESS_DIED。
按这个流程修复后，一周内没再蓝屏！",
                    Duration = "⏱️ 约 30 - 60 分钟",
                    RiskLevel = "🟢 低风险",
                    ExpectedOutput = "系统修复完成，蓝屏问题得到解决",
                    TroubleshootingTips = @"💡 完整蓝屏修复方案：

第 1 步：进入安全模式
开机时按 F8 或 Shift+重启

第 2 步：运行 DISM 修复
DISM /Online /Cleanup-Image /RestoreHealth

第 3 步：运行 SFC 扫描
sfc /scannow

第 4 步：检查磁盘错误
chkdsk C: /f /r

第 5 步：更新驱动程序
特别是显卡、网卡、芯片组驱动

第 6 步：检查内存
运行 Windows 内存诊断

🔍 蓝屏分析工具：
• BlueScreenView - 查看蓝屏记录
• WinDbg - 专业分析工具
• 系统事件查看器 - 查看错误日志

⚠️ 如果 DISM 无法解决：
• 可能是硬件问题（内存、硬盘）
• 需要检查最近安装的软件/驱动
• 考虑系统还原或重置"
                },

                // ========== 磁盘清理类 ==========
                new DismTutorialItemV2
                {
                    Category = "🧹 磁盘清理",
                    Title = "C 盘空间不足？一键清理系统垃圾",
                    Command = "/Online /Cleanup-Image /AnalyzeComponentStore",
                    Description = "分析组件存储占用，了解可以清理的空间",
                    WhenToUse = @"🤔 什么时候用：
• C 盘空间不足，变红了
• 想知道系统文件占了多少空间
• 准备清理前评估一下
• 系统升级后想清理旧版本

📋 实际场景：
小周的 C 盘只剩 2GB 空间，系统警告。运行这个命令后发现
组件存储占了 15GB，其中 8GB 可以安全清理！",
                    Duration = "⏱️ 约 2 - 5 分钟",
                    RiskLevel = "🟢 零风险（只分析，不清理）",
                    ExpectedOutput = "显示组件存储大小和可回收空间",
                    TroubleshootingTips = @"💡 分析报告解读：

Component Store 实际大小：XX GB
备份和禁用功能占用：XX GB
缓存和临时数据：XX GB
可回收空间：XX GB ⬅️ 这个就是要清理的

🧹 清理方案选择：
• 空间紧张：运行 /StartComponentCleanup
• 空间严重不足：运行 /StartComponentCleanup /ResetBase
• 升级后清理：使用磁盘清理工具清理系统文件"
                },

                new DismTutorialItemV2
                {
                    Category = "🧹 磁盘清理",
                    Title = "安全清理系统组件存储",
                    Command = "/Online /Cleanup-Image /StartComponentCleanup",
                    Description = "清理过时的系统组件和临时文件，释放磁盘空间",
                    WhenToUse = @"🤔 什么时候用：
• C 盘空间不足
• 系统升级后想清理旧版本
• 组件存储占用过大
• 想保持系统整洁

📋 实际场景：
小赵的 C 盘快满了，运行这个命令清理了 5GB 的系统垃圾，
C 盘从红色变回蓝色！",
                    Duration = "⏱️ 约 5 - 15 分钟",
                    RiskLevel = "🟢 低风险",
                    ExpectedOutput = "组件存储清理完成",
                    TroubleshootingTips = @"💡 清理效果：

✅ 清理内容：
• 旧的 Windows 更新备份
• 过时的系统组件
• 临时安装文件
• 缓存数据

⚠️ 注意事项：
• 清理后无法卸载最近的更新
• 如果需要回滚更新，请先卸载更新再清理
• 建议系统稳定运行一段时间后再清理

🚀 深度清理（慎用）：
/StartComponentCleanup /ResetBase
这个会清理更多，但之后无法卸载任何更新！"
                },

                // ========== 功能管理类 ==========
                new DismTutorialItemV2
                {
                    Category = "⚙️ 功能管理",
                    Title = "安装 .NET Framework 3.5（解决老软件兼容）",
                    Command = "/Online /Enable-Feature /FeatureName:NetFx3 /All /Source:X:\\sources\\sxs /LimitAccess",
                    Description = "启用 .NET Framework 3.5，解决老软件无法运行的问题",
                    WhenToUse = @"🤔 什么时候用：
• 打开老软件提示需要 .NET 3.5
• 安装某些程序时提示缺少 .NET Framework
• 运行旧版游戏或工具
• Windows 10/11 默认没安装 3.5

📋 实际场景：
小钱需要运行一个 2010 年的设计软件，提示需要 .NET 3.5。
用这个命令安装后，软件正常运行！",
                    Duration = "⏱️ 约 2 - 5 分钟",
                    RiskLevel = "🟢 低风险",
                    ExpectedOutput = "功能启用成功",
                    TroubleshootingTips = @"💡 安装方式选择：

方式 1：在线安装（推荐）
DISM /Online /Enable-Feature /FeatureName:NetFx3 /All

方式 2：离线安装（无网络）
DISM /Online /Enable-Feature /FeatureName:NetFx3 /All /Source:D:\\sources\\sxs /LimitAccess

方式 3：通过控制面板
控制面板 → 程序 → 启用或关闭 Windows 功能 → 勾选 .NET Framework 3.5

📁 离线安装准备：
需要 Windows 安装镜像中的 sources\\sxs 文件夹

🔧 如果安装失败：
• 检查 Windows Update 服务是否运行
• 尝试离线安装方式
• 使用 .NET Framework 修复工具"
                },

                new DismTutorialItemV2
                {
                    Category = "⚙️ 功能管理",
                    Title = "查看和管理 Windows 功能",
                    Command = "/Online /Get-Features",
                    Description = "列出所有 Windows 功能及其状态，了解可以启用/禁用的功能",
                    WhenToUse = @"🤔 什么时候用：
• 想知道系统有哪些功能
• 准备启用或禁用某些功能
• 排查功能相关问题
• 优化系统性能

📋 实际场景：
小孙想禁用不需要的功能提升性能，先运行这个命令查看
所有功能，然后决定禁用哪些。",
                    Duration = "⏱️ 约 1 - 2 分钟",
                    RiskLevel = "🟢 零风险（只查看）",
                    ExpectedOutput = "显示所有 Windows 功能及其状态",
                    TroubleshootingTips = @"💡 常用功能管理：

启用功能：
/Online /Enable-Feature /FeatureName:功能名 /All

禁用功能：
/Online /Disable-Feature /FeatureName:功能名

📝 常用功能名：
• NetFx3 - .NET Framework 3.5
• IIS-WebServerRole - IIS 服务器
• Microsoft-Windows-Subsystem-Linux - WSL
• HypervisorPlatform - Hyper-V

⚠️ 禁用前注意：
• 不要禁用不确定的功能
• 系统关键功能无法禁用
• 禁用后可能需要重启"
                }
            };
        }

        public static string GetCommonScenarios()
        {
            var sb = new StringBuilder();
            sb.AppendLine("🎯 常见电脑问题快速解决方案");
            sb.AppendLine("========================================");
            sb.AppendLine();

            sb.AppendLine("【场景 1】电脑变慢、卡顿");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("症状：开机慢、打开软件慢、经常卡死");
            sb.AppendLine();
            sb.AppendLine("解决步骤：");
            sb.AppendLine("1. 运行：DISM /Online /Cleanup-Image /RestoreHealth");
            sb.AppendLine("2. 运行：sfc /scannow");
            sb.AppendLine("3. 运行：DISM /Online /Cleanup-Image /StartComponentCleanup");
            sb.AppendLine("4. 使用磁盘清理工具清理临时文件");
            sb.AppendLine("5. 重启电脑");
            sb.AppendLine();
            sb.AppendLine("💡 额外建议：");
            sb.AppendLine("• 检查启动项，禁用不必要的程序");
            sb.AppendLine("• 卸载不常用的软件");
            sb.AppendLine("• 检查是否有病毒或恶意软件");
            sb.AppendLine();

            sb.AppendLine("【场景 2】Windows 更新失败");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("症状：更新进度卡住、提示错误代码、反复重启");
            sb.AppendLine();
            sb.AppendLine("解决步骤：");
            sb.AppendLine("1. 运行：DISM /Online /Cleanup-Image /StartComponentCleanup");
            sb.AppendLine("2. 运行：DISM /Online /Cleanup-Image /RestoreHealth");
            sb.AppendLine("3. 运行：sfc /scannow");
            sb.AppendLine("4. 重启电脑");
            sb.AppendLine("5. 再次尝试 Windows 更新");
            sb.AppendLine();
            sb.AppendLine("💡 如果还是失败：");
            sb.AppendLine("• 使用 Windows 更新疑难解答");
            sb.AppendLine("• 手动下载更新安装包");
            sb.AppendLine("• 使用媒体创建工具升级");
            sb.AppendLine();

            sb.AppendLine("【场景 3】软件打不开/缺少 DLL");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("症状：提示缺少 MSVCP140.dll、0xc000007b 错误");
            sb.AppendLine();
            sb.AppendLine("解决步骤：");
            sb.AppendLine("1. 运行：DISM /Online /Cleanup-Image /RestoreHealth");
            sb.AppendLine("2. 安装 VC++ Redistributable（2005-2022）");
            sb.AppendLine("3. 安装 .NET Framework 3.5 和 4.8");
            sb.AppendLine("4. 安装 DirectX 运行库");
            sb.AppendLine("5. 重启电脑");
            sb.AppendLine();
            sb.AppendLine("💡 下载地址：");
            sb.AppendLine("• VC++：微软官网下载中心");
            sb.AppendLine("• .NET：Windows 功能中启用");
            sb.AppendLine("• DirectX：微软下载中心");
            sb.AppendLine();

            sb.AppendLine("【场景 4】蓝屏死机（BSOD）");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("症状：蓝屏、自动重启、错误代码");
            sb.AppendLine();
            sb.AppendLine("解决步骤：");
            sb.AppendLine("1. 进入安全模式");
            sb.AppendLine("2. 运行：DISM /Online /Cleanup-Image /RestoreHealth");
            sb.AppendLine("3. 运行：sfc /scannow");
            sb.AppendLine("4. 运行：chkdsk C: /f /r");
            sb.AppendLine("5. 更新所有驱动程序");
            sb.AppendLine("6. 检查内存和硬盘健康");
            sb.AppendLine();
            sb.AppendLine("💡 常见蓝屏代码：");
            sb.AppendLine("• CRITICAL_PROCESS_DIED - 系统进程崩溃");
            sb.AppendLine("• SYSTEM_SERVICE_EXCEPTION - 驱动问题");
            sb.AppendLine("• IRQL_NOT_LESS_OR_EQUAL - 内存或驱动问题");
            sb.AppendLine();

            sb.AppendLine("【场景 5】C 盘空间不足");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("症状：C 盘变红、系统提示空间不足");
            sb.AppendLine();
            sb.AppendLine("解决步骤：");
            sb.AppendLine("1. 运行：DISM /Online /Cleanup-Image /AnalyzeComponentStore");
            sb.AppendLine("2. 运行：DISM /Online /Cleanup-Image /StartComponentCleanup");
            sb.AppendLine("3. 使用磁盘清理工具清理系统文件");
            sb.AppendLine("4. 卸载不常用的软件");
            sb.AppendLine("5. 移动大文件到其他分区");
            sb.AppendLine();
            sb.AppendLine("💡 深度清理（慎用）：");
            sb.AppendLine("DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase");
            sb.AppendLine("⚠️ 执行后无法卸载 Windows 更新！");
            sb.AppendLine();

            sb.AppendLine("========================================");
            sb.AppendLine("📝 使用建议");
            sb.AppendLine();
            sb.AppendLine("1. 定期维护（每月一次）：");
            sb.AppendLine("   /CheckHealth → /ScanHealth → /RestoreHealth");
            sb.AppendLine();
            sb.AppendLine("2. 出问题时的急救流程：");
            sb.AppendLine("   DISM 修复 → SFC 扫描 → 重启 → 测试");
            sb.AppendLine();
            sb.AppendLine("3. 预防胜于治疗：");
            sb.AppendLine("   • 定期创建系统还原点");
            sb.AppendLine("   • 保持系统更新");
            sb.AppendLine("   • 安装可靠的杀毒软件");
            sb.AppendLine("   • 不要安装来路不明的软件");
            sb.AppendLine();
            sb.AppendLine("========================================");

            return sb.ToString();
        }

        public static string GetQuickReference()
        {
            return @"📋 DISM 快速参考卡

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔍 检查类
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
快速检查：DISM /Online /Cleanup-Image /CheckHealth
详细扫描：DISM /Online /Cleanup-Image /ScanHealth

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔧 修复类
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
在线修复：DISM /Online /Cleanup-Image /RestoreHealth
离线修复：DISM /Online /Cleanup-Image /RestoreHealth /Source:X:\sources\install.wim /LimitAccess

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🧹 清理类
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
分析空间：DISM /Online /Cleanup-Image /AnalyzeComponentStore
安全清理：DISM /Online /Cleanup-Image /StartComponentCleanup
深度清理：DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚙️ 功能管理
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
查看功能：DISM /Online /Get-Features
启用功能：DISM /Online /Enable-Feature /FeatureName:功能名 /All
禁用功能：DISM /Online /Disable-Feature /FeatureName:功能名

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🚑 急救组合
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
标准修复流程：
1. DISM /Online /Cleanup-Image /RestoreHealth
2. sfc /scannow
3. 重启电脑

解决更新失败：
1. DISM /Online /Cleanup-Image /StartComponentCleanup
2. DISM /Online /Cleanup-Image /RestoreHealth
3. sfc /scannow
4. 重启后更新

========================================
💡 提示：所有命令都需要管理员权限运行！
========================================
";
        }
    }

    public class DismTutorialItemV2
    {
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Command { get; set; } = "";
        public string Description { get; set; } = "";
        public string WhenToUse { get; set; } = "";
        public string Duration { get; set; } = "";
        public string RiskLevel { get; set; } = "";
        public string ExpectedOutput { get; set; } = "";
        public string TroubleshootingTips { get; set; } = "";
    }
}
