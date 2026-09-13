using System;
using System.Collections.Generic;

namespace HardwareDiagnostics.System
{
    /// <summary>
    /// DISM 新手教程 - 提供详细的 DISM 命令说明。
    /// 写作原则：说人话（通俗比喻）+ 给原命令（专业准确）+ 讲风险（前置条件与回滚）。
    /// 后向兼容：类名、方法名、原有 6 个属性保持不变，只新增可选属性。
    /// </summary>
    public class DismTutorial
    {
        public static string GetIntroduction()
        {
            return @"DISM（部署映像服务和管理，Deployment Image Servicing and Management）新手教程
====================================================================

【一句话：DISM 是什么？】
把 Windows 系统想象成一栋精装房：日常使用会磨损墙面（系统文件损坏）、
堆积装修垃圾（组件存储膨胀）、缺门少窗（功能没装）。DISM 就是物业的
“维修工具箱”，直接修房子本体，而不动你屋里的家具（个人文件）。

【为什么需要它？】
1. 系统文件损坏时：更新失败、蓝屏、开始菜单打不开、应用商店闪退，
   很多时候是系统映像（房子的承重墙）坏了，DISM 负责修墙。
2. C 盘越用越满时：WinSxS 组件存储会存多份旧零件，DISM 负责清理。
3. 装特定软件时：缺少 .NET 3.5、Hyper-V、Telnet 等功能，DISM 负责加门窗。
4. 装机/运维时：备份驱动、离线打驱动、挂载镜像改系统，DISM 是主力。

【什么时候用 / 什么时候别用？】
要用：更新反复失败、sfc 修复不了、C 盘告急、功能装不上、驱动冲突。
别乱用：/ResetBase（拆掉旧零件的退路）、/Remove-Driver（拆承重墙）、
挂载镜像改官方包——做之前先建系统还原点、接上电源、留足磁盘空间。

【DISM 和 SFC 的关系（记住顺序：先 DISM，后 SFC）】
SFC = 屋内检查员：对照本地蓝图修你家墙面，修不好会说“无法修复”。
DISM = 工程队：先把蓝图本身（组件存储）修好，再让 SFC 进场。
标准连招：DISM /RestoreHealth 修完 → sfc /scannow 扫一遍 → 重启再看。

【通用前提】
• 绝大多数命令要管理员权限（右键“以管理员身份运行”）。
• /RestoreHealth 联网时走 Windows Update；没网就用 /Source 指安装镜像。
• 耗时：查询类 1-3 分钟，修复类 10-60 分钟，清理类 5-30 分钟。
• Win7 及以上才有完整 DISM；XP 请跳过本模块。
";
        }

        public static List<DismTutorialItem> GetTutorials()
        {
            return new List<DismTutorialItem>
            {
                // ---------- 系统映像维护（10） ----------
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "快速检查有没有坏（推荐先跑这个）",
                    Command = "/Online /Cleanup-Image /CheckHealth",
                    Description = "像用体温计快速量体温：只看有没有已知的损坏标记，不翻箱倒柜，1-5 分钟出结果。",
                    Usage = "适用于：日常巡检、更新失败后的第一步。普通用户也能跑。",
                    Duration = "1-5 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员权限；Win7+。",
                    ExampleOutput = "“可以修复组件存储”=有问题；“未检测到组件存储损坏”=健康。",
                    Recovery = "只是检查，不改系统，不用回滚。"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "全面扫描哪里坏了",
                    Command = "/Online /Cleanup-Image /ScanHealth",
                    Description = "像给系统拍 CT：逐块扫描映像，告诉你坏没坏、坏在哪，但不动手术。",
                    Usage = "适用于：CheckHealth 报有问题、蓝屏/功能异常后定位。",
                    Duration = "5-20 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员权限；保持电源连接，中途别关机。",
                    ExampleOutput = "进度到 100% 后报告“组件存储可以修复/不可修复”。",
                    Recovery = "只读扫描，无需回滚；扫出问题再做 RestoreHealth。"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "自动修复系统映像（最常用）",
                    Command = "/Online /Cleanup-Image /RestoreHealth",
                    Description = "像叫工程队上门：自动从 Windows Update 拉好零件，把坏墙换掉。",
                    Usage = "适用于：系统文件损坏、蓝屏、更新失败、sfc 修不好时。",
                    Duration = "10-60 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员权限；建议联网；笔记本接电源。",
                    ExampleOutput = "“还原操作已成功完成”=修好了；卡在 62.3% 多为正常，耐心等。",
                    Recovery = "修完跑 sfc /scannow 验证；仍失败改用 /Source 离线修复。"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "没网时用安装镜像修复（离线修复）",
                    Command = "/Online /Cleanup-Image /RestoreHealth /Source:WIM:X:\\sources\\install.wim:1 /LimitAccess",
                    Description = "像自带材料修房：不用等网购零件，直接用 U 盘/ISO 里的 install.wim 当料仓。",
                    Usage = "适用于：无网络、Windows Update 被禁、企业内网机。",
                    Duration = "10-30 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "准备与本机版本/架构一致的 ISO 并记下盘符；确认 install.wim 的 Index（一般选 1 或对应版本）。",
                    ExampleOutput = "成功则提示还原完成；报 0x800f081f 多半是镜像版本不对，换对版本重试。",
                    Recovery = "不改个人文件；失败就换镜像源或去掉 /LimitAccess 联网重试。"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "只用本地源、不碰外网",
                    Command = "/Online /Cleanup-Image /RestoreHealth /LimitAccess",
                    Description = "像关门施工：禁止去微软服务器下载，只用本机/局域网已有的源。",
                    Usage = "适用于：企业环境、流量敏感、想复现问题时隔离变量。",
                    Duration = "10-30 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "先配好本地源（/Source），否则可能报找不到源。",
                    ExampleOutput = "找不到源会报 0x800f081f，属预期内，加 /Source 再跑。",
                    Recovery = "去掉 /LimitAccess 允许联网即回退到常规修复。"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "看看 C 盘被谁吃了（组件存储分析）",
                    Command = "/Online /Cleanup-Image /AnalyzeComponentStore",
                    Description = "像查物业账单：告诉你 WinSxS 实际占多少、能回收多少。",
                    Usage = "适用于：C 盘飘红、想知道清理值不值。",
                    Duration = "2-10 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员权限即可。",
                    ExampleOutput = "关注“建议清理组件存储：是/否”和可回收大小。",
                    Recovery = "只读分析；决定清理再跑 StartComponentCleanup。"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "清理旧零件（安全清理）",
                    Command = "/Online /Cleanup-Image /StartComponentCleanup",
                    Description = "像请保洁：扔掉已被取代的旧版本组件，当前系统和回滚能力不受影响。",
                    Usage = "适用于：C 盘空间紧张、打完大更新后例行清理。",
                    Duration = "5-30 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员权限；别在打更新途中跑。",
                    ExampleOutput = "进度到 100% 即完成，一般回收几百 MB 到数 GB。",
                    Recovery = "安全操作；更新卸载能力保留，无需回滚。"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "深度清理并封存（不可逆，慎点）",
                    Command = "/Online /Cleanup-Image /StartComponentCleanup /ResetBase",
                    Description = "像拆掉旧楼梯：把所有旧版本底座删掉，省地方，但以后不能卸载已装更新。",
                    Usage = "适用于：C 盘严重不足、系统已稳定运行很久、封装母盘前。",
                    Duration = "10-60 分钟",
                    RiskLevel = "高风险 - 清理后无法卸载已安装更新",
                    Prerequisites = "确认近两周系统稳定；已建还原点/镜像备份；不断电。",
                    ExampleOutput = "完成后 WinSxS 明显变小；这就是预期效果。",
                    Recovery = "不可逆！后悔只能重装或还原备份。拿不准就用不带 /ResetBase 的版本。"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "SFC 黄金搭档：一键先修蓝图再验墙",
                    Command = "DISM /Online /Cleanup-Image /RestoreHealth && sfc /scannow",
                    Description = "标准连招：DISM 修蓝图（组件存储），SFC 按新蓝图验墙（系统文件）。",
                    Usage = "适用于：sfc 报“无法修复”、反复蓝屏、更新后异常。",
                    Duration = "20-90 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员权限；两条命令按顺序跑，中间别重启打断。",
                    ExampleOutput = "SFC 最后报“已修复/未发现违规”即闭环。",
                    Recovery = "仍报无法修复→用 /Source 离线再跑一轮→收集 CBS.log 找人看。"
                },
                new DismTutorialItem
                {
                    Category = "系统映像维护",
                    Title = "查查修坏没：看 DISM 日志",
                    Command = "notepad C:\\Windows\\Logs\\DISM\\dism.log",
                    Description = "像看施工监理日志：报错代码和卡住的真相都在这里，不用瞎猜。",
                    Usage = "适用于：DISM 报错、进度长期不动、要贴日志求助时。",
                    Duration = "2 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员权限打开记事本；关注 Error/Warning 与 0x 开头代码。",
                    ExampleOutput = "搜 0x800f081f（缺源）、0x800f0906（缺 NetFx3 源）、1726（RPC 失败）。",
                    Recovery = "日志只读；对症换源/联网/重跑即可。"
                },
                // ---------- Windows 功能管理（6） ----------
                new DismTutorialItem
                {
                    Category = "Windows 功能管理",
                    Title = "看看家里有什么门窗（功能列表）",
                    Command = "/Online /Get-Features /Format:Table",
                    Description = "像看户型图：列出所有可选功能及启用/禁用状态，装软件前先查。",
                    Usage = "适用于：装老软件提示缺 .NET 3.5、想开 Hyper-V/WSL 前确认名字。",
                    Duration = "1-3 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员权限；记下 FeatureName（大小写不敏感）。",
                    ExampleOutput = "表格里找 NetFx3、Microsoft-Hyper-V 等行的“状态”列。",
                    Recovery = "只读查询，无需回滚。"
                },
                new DismTutorialItem
                {
                    Category = "Windows 功能管理",
                    Title = "查某个功能的说明书",
                    Command = "/Online /Get-FeatureInfo /FeatureName:NetFx3",
                    Description = "像看单个门窗的规格书：这个功能是干嘛的、依赖谁、开了多大。",
                    Usage = "适用于：不敢乱开功能，先看依赖和说明。",
                    Duration = "1-2 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "先用 /Get-Features 拿到准确的功能名。",
                    ExampleOutput = "显示名称、说明、状态，依赖项会提示要加 /All。",
                    Recovery = "只读查询。"
                },
                new DismTutorialItem
                {
                    Category = "Windows 功能管理",
                    Title = "装老软件必备：启用 .NET 3.5",
                    Command = "/Online /Enable-Feature /FeatureName:NetFx3 /All /Source:X:\\sources\\sxs /LimitAccess",
                    Description = "像给老家具配钥匙：很多财务/工控老软件非它不可；带 /All 自动装依赖，/Source 指安装盘里的 sxs 文件夹。",
                    Usage = "适用于：打开老软件报缺 .NET 2.0/3.5 时。",
                    Duration = "2-10 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "准备同版本 ISO 并找到 sources\\sxs；Win10/11 在线也行，但离线最稳。",
                    ExampleOutput = "报 0x800f0906 就是没给对源，检查盘符和 sxs 路径。",
                    Recovery = "装错可 /Disable-Feature 关掉；系统不受影响。"
                },
                new DismTutorialItem
                {
                    Category = "Windows 功能管理",
                    Title = "启用 Hyper-V / WSL / 沙盒（虚拟化三件套）",
                    Command = "/Online /Enable-Feature /FeatureName:Microsoft-Hyper-V /All",
                    Description = "像加盖一间样板间：Hyper-V 跑虚拟机，WSL 跑 Linux，沙盒跑一次性测试。",
                    Usage = "适用于：开发、学 Linux、试可疑软件。功能名：Microsoft-Hyper-V、Microsoft-Windows-Subsystem-Linux、Containers-DisposableClientVM。",
                    Duration = "2-10 分钟 + 一次重启",
                    RiskLevel = "中风险 - 需 CPU 开 VT-x/AMD-V，不兼容时可能蓝屏",
                    Prerequisites = "BIOS 开虚拟化；家庭版无 Hyper-V/沙盒（WSL2 可用）；改前建还原点。",
                    ExampleOutput = "提示“需要重启”属正常，重启后在功能列表确认“已启用”。",
                    Recovery = "出问题进安全模式 /Disable-Feature 关掉，或系统还原。"
                },
                new DismTutorialItem
                {
                    Category = "Windows 功能管理",
                    Title = "关掉不用的功能（减负）",
                    Command = "/Online /Disable-Feature /FeatureName:TelnetClient",
                    Description = "像拆掉从不用的旧窗：Telnet、SMBv1、旧版 IE 这些又老又险，关掉更安全。",
                    Usage = "适用于：等保/企业收紧、安全加固。常用：TelnetClient、SMB1Protocol。",
                    Duration = "2-10 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "确认没老设备/软件依赖它（如老打印机、工控机可能还用 SMBv1）。",
                    ExampleOutput = "提示成功即可；对应软件打不开就是关对了，按需再开。",
                    Recovery = "随时 /Enable-Feature 装回来。"
                },
                new DismTutorialItem
                {
                    Category = "Windows 功能管理",
                    Title = "彻底移除功能包（连安装源一起删）",
                    Command = "/Online /Disable-Feature /FeatureName:NetFx3 /Remove",
                    Description = "像连窗框一起拆走：功能文件从本地删掉，以后要用必须有外来源。省空间，但留后患。",
                    Usage = "适用于：封装精简镜像、磁盘极小设备。",
                    Duration = "2-10 分钟",
                    RiskLevel = "中风险 - 下次启用必须提供 /Source",
                    Prerequisites = "日常电脑别用这个；封装机用完记得测试。",
                    ExampleOutput = "成功后 Get-FeatureInfo 显示“已禁用且负载已删除”。",
                    Recovery = "后悔就准备 ISO，用 /Enable-Feature + /Source 装回。"
                },
                // ---------- 驱动程序管理（5） ----------
                new DismTutorialItem
                {
                    Category = "驱动程序管理",
                    Title = "看看装了哪些第三方驱动",
                    Command = "/Online /Get-Drivers /Format:Table",
                    Description = "像翻物业的维修记录：oem0.inf、oem1.inf……每个对应一个第三方驱动，冲突排查先看它。",
                    Usage = "适用于：蓝屏、设备叹号、更新驱动前先认人。",
                    Duration = "1-3 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员权限；把“原始文件名 oemXX.inf”和设备对上号。",
                    ExampleOutput = "表格列出已发布名称、原始文件名、厂商、日期版本。",
                    Recovery = "只读查询。"
                },
                new DismTutorialItem
                {
                    Category = "驱动程序管理",
                    Title = "看单个驱动的底细",
                    Command = "/Online /Get-DriverInfo /Driver:oem12.inf",
                    Description = "像查身份证：这个 oemXX.inf 是谁家的、支不支持你这台机，一目了然。",
                    Usage = "适用于：删驱动前确认，别删错把网卡/显卡干掉。",
                    Duration = "1-2 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "先 /Get-Drivers 拿到准确的 oem 号。",
                    ExampleOutput = "显示驱动类、厂商、版本、签名状态。",
                    Recovery = "只读查询。"
                },
                new DismTutorialItem
                {
                    Category = "驱动程序管理",
                    Title = "批量装驱动（装机员最爱）",
                    Command = "/Online /Add-Driver /Driver:D:\\Drivers /Recurse",
                    Description = "像一键换全屋门锁：把 D:\\Drivers 下所有子文件夹的驱动全装上，/Recurse 就是“翻遍子文件夹”。",
                    Usage = "适用于：重装后批量补驱动、离线部署多台同型号机器。",
                    Duration = "2-15 分钟",
                    RiskLevel = "中风险 - 驱动不对会蓝屏/没网",
                    Prerequisites = "驱动必须匹配系统版本和架构（x64 别喂 x86）；先备份（见导出驱动条）；不断电。",
                    ExampleOutput = "逐个报告“已安装”，失败会点名是哪个 inf。",
                    Recovery = "进安全模式 /Remove-Driver 删问题驱动，或系统还原。"
                },
                new DismTutorialItem
                {
                    Category = "驱动程序管理",
                    Title = "删掉捣乱的驱动（回滚冲突）",
                    Command = "/Online /Remove-Driver /Driver:oem12.inf",
                    Description = "像拔掉短路的插排：oem 号就是插排编号，拔错会断电（设备罢工）。",
                    Usage = "适用于：更新驱动后蓝屏/没声/没网，锁定元凶后移除。",
                    Duration = "1-5 分钟 + 重启",
                    RiskLevel = "高风险 - 删错网卡/显卡驱动会进不了系统",
                    Prerequisites = "先 /Get-DriverInfo 确认；准备好官网版驱动和还原点；别删正在用的启动关键驱动。",
                    ExampleOutput = "提示成功后重启，设备管理器里该设备会变叹号待重装。",
                    Recovery = "重启后装回官网驱动；进不去就安全模式或系统还原。"
                },
                new DismTutorialItem
                {
                    Category = "驱动程序管理",
                    Title = "动手术前先备份：导出全部第三方驱动",
                    Command = "/Online /Export-Driver /Destination:D:\\DriverBackup",
                    Description = "像装修前拍照存档：把当前所有第三方驱动打包到文件夹，重装直接喂回去。",
                    Usage = "适用于：重装前、批量改驱动前、给同型号机器做驱动库。",
                    Duration = "2-10 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "目标盘空间充足（一般几百 MB）；文件夹提前建好。",
                    ExampleOutput = "每个驱动一个文件夹，含 inf+cat+sys，重装时配合 /Add-Driver /Recurse 用。",
                    Recovery = "只读导出；备份本身就是回滚手段。"
                },
                // ---------- 包与更新管理（4） ----------
                new DismTutorialItem
                {
                    Category = "包与更新管理",
                    Title = "看看装了哪些更新包",
                    Command = "/Online /Get-Packages /Format:Table",
                    Description = "像翻购物小票：每个 KB 更新对应一个包，更新失败先对账。",
                    Usage = "适用于：某个 KB 装不上、想卸载问题更新前先找包名。",
                    Duration = "1-3 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员权限；包名很长，复制别手打。",
                    ExampleOutput = "找 Package_for_KBxxxx ~31bf3856… 这样的身份标识。",
                    Recovery = "只读查询。"
                },
                new DismTutorialItem
                {
                    Category = "包与更新管理",
                    Title = "查单个更新包的详情",
                    Command = "/Online /Get-PackageInfo /PackageName:Package_for_KB5034441~31bf3856ad364e35~amd64~~10.0.1.0",
                    Description = "像看小票明细：这个包装上没、能不能卸、要不要重启。",
                    Usage = "适用于：卸载前确认“可卸载：是/否”。",
                    Duration = "1-2 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "包名从 /Get-Packages 完整复制。",
                    ExampleOutput = "状态、安装时间、是否需重启一目了然。",
                    Recovery = "只读查询。"
                },
                new DismTutorialItem
                {
                    Category = "包与更新管理",
                    Title = "手动装一个更新包（.msu/.cab）",
                    Command = "/Online /Add-Package /PackagePath:D:\\Patches\\KB5034441.msu",
                    Description = "像手动贴一张邮票：Windows Update 抽风时，把下载好的补丁亲手贴上去。",
                    Usage = "适用于：内网机、更新服务坏了、只要装这一个补丁。",
                    Duration = "5-20 分钟 + 可能重启",
                    RiskLevel = "中风险 - 补丁不对版本会装不上，白等",
                    Prerequisites = "补丁版本/架构与系统一致；/PreventPending 可防“有挂起操作”报错；不断电。",
                    ExampleOutput = "成功提示完成，失败多为版本不对或有挂起重启没做。",
                    Recovery = "用 /Remove-Package 卸掉，或系统还原。"
                },
                new DismTutorialItem
                {
                    Category = "包与更新管理",
                    Title = "卸载捣乱的更新",
                    Command = "/Online /Remove-Package /PackageName:Package_for_KBxxxx~31bf3856ad364e35~amd64~~10.0.1.0 /NoRestart",
                    Description = "像退掉一件过敏的衣服：更新后蓝屏/打印机挂了，先退货保命。",
                    Usage = "适用于：更新后出问题、需要先恢复生产再慢慢查。",
                    Duration = "5-20 分钟",
                    RiskLevel = "中风险 - /ResetBase 后或“不可卸载”的包退不掉",
                    Prerequisites = "确认包“可卸载”；/NoRestart 只是推迟重启，最终还得重启生效。",
                    ExampleOutput = "提示成功后重启验证问题是否消失。",
                    Recovery = "问题解决就暂停该更新；没解决就暂停更新+收集日志求助。"
                },
                // ---------- 应用与预配（2） ----------
                new DismTutorialItem
                {
                    Category = "应用管理",
                    Title = "修应用商店/系统应用闪退",
                    Command = "/Online /Get-ProvisionedAppxPackages | Select-String Microsoft.WindowsStore",
                    Description = "像查小区配套清单：应用商店、照片、计算器这些系统应用叫预配包，坏了要先认尸再重装。",
                    Usage = "适用于：应用商店打不开、系统应用闪退、sysprep 报错（先删用户装的 Appx）。",
                    Duration = "2-5 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "管理员 PowerShell 跑更顺；别乱删全家桶，删错开始菜单会残。",
                    ExampleOutput = "列出包全名，配合 Remove-ProvisionedAppxPackage 移除问题包后重装。",
                    Recovery = "从应用商店重装；大不了 /RestoreHealth 修一轮。"
                },
                new DismTutorialItem
                {
                    Category = "应用管理",
                    Title = "看系统补丁式应用（App Patch）",
                    Command = "/Online /Get-AppPatchInfo",
                    Description = "像看家具的保修贴：某些传统程序的 MSP 补丁信息，排查安装包打架用。",
                    Usage = "适用于：Office/老程序打补丁失败时对账。",
                    Duration = "1-3 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "知道出问题的程序名，定向查更快。",
                    ExampleOutput = "列出已应用的补丁项，失败项会暴露版本错位。",
                    Recovery = "只读查询；修用安装包官方修复流程。"
                },
                // ---------- 高级映像操作（5） ----------
                new DismTutorialItem
                {
                    Category = "高级操作",
                    Title = "看 ISO 里有几个系统版本",
                    Command = "/Get-ImageInfo /ImageFile:X:\\sources\\install.wim",
                    Description = "像看菜单：一个 install.wim 里可能藏着家庭版/专业版多个 Index，装错 Index 等于点错菜。",
                    Usage = "适用于：离线修复选 /Source:…:N 的 N、重装前确认版本。",
                    Duration = "1-2 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "先把 ISO 双击挂载拿到盘符；ESD 文件同理（部分命令对 ESD 有限制）。",
                    ExampleOutput = "Index:1 名称:家庭版，Index:2 名称:专业版……修复时照抄。",
                    Recovery = "只读查询。"
                },
                new DismTutorialItem
                {
                    Category = "高级操作",
                    Title = "挂载镜像改系统（只读先练手）",
                    Command = "/Mount-Image /ImageFile:X:\\sources\\install.wim /Index:1 /MountDir:C:\\Mount /ReadOnly",
                    Description = "像把样板间钥匙借来看房：/ReadOnly 只看不改，练熟了再去掉只读真动手。",
                    Usage = "适用于：想往镜像里加驱动/补丁/改配置的封装前演练。",
                    Duration = "2-10 分钟",
                    RiskLevel = "中风险 - 挂载目录要空文件夹，磁盘要够大",
                    Prerequisites = "C:\\Mount 为空；磁盘剩余 > 镜像解开两倍；记下 Index。",
                    ExampleOutput = "提示挂载成功，C:\\Mount 里出现 Windows 目录即对。",
                    Recovery = "只读挂载直接 /Unmount-Image /Discard 卸载；卡住用 /Cleanup-Mountpoints。"
                },
                new DismTutorialItem
                {
                    Category = "高级操作",
                    Title = "改完保存并卸载（真动手）",
                    Command = "/Unmount-Image /MountDir:C:\\Mount /Commit",
                    Description = "像装修完交房：把挂载期间的修改写回 wim，写一半断电会烂尾，务必接电。",
                    Usage = "适用于：加完驱动/补丁/应答文件后封版。",
                    Duration = "5-30 分钟",
                    RiskLevel = "中风险 - 提交中途断电/关机可能损坏镜像",
                    Prerequisites = "先备份原 wim；提交前 /Cleanup-Image /CheckHealth 看一眼挂载系统。",
                    ExampleOutput = "提示卸载并提交成功，wim 时间戳更新。",
                    Recovery = "提交前备份原文件就是后悔药；失败用备份覆盖。"
                },
                new DismTutorialItem
                {
                    Category = "高级操作",
                    Title = "改崩了？放弃修改直接卸载",
                    Command = "/Unmount-Image /MountDir:C:\\Mount /Discard",
                    Description = "像装修翻车直接毛坯还原：挂载里的改动全扔掉，镜像保持原样。",
                    Usage = "适用于：改错、蓝屏验证失败、不想要了。",
                    Duration = "2-10 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "确认真的不想要了；卸载后挂载目录可删。",
                    ExampleOutput = "提示卸载成功，镜像文件大小不变。",
                    Recovery = "本身就是回滚动作；卸载不掉再 /Cleanup-Mountpoints。"
                },
                new DismTutorialItem
                {
                    Category = "高级操作",
                    Title = "挂载卡死后的扫把：清理无效挂载点",
                    Command = "/Cleanup-Mountpoints",
                    Description = "像物业清理僵尸车：上次异常关机/断电留下的半截挂载，一键扫掉。",
                    Usage = "适用于：挂载/卸载报错“已挂载/目录非空”、wim 被占用删不掉时。",
                    Duration = "1-5 分钟",
                    RiskLevel = "低风险",
                    Prerequisites = "确认没有正在进行的挂载提交；管理员权限。",
                    ExampleOutput = "提示清理完成，再重新挂载一般就顺了。",
                    Recovery = "只做清理；镜像坏了还是坏了，别指望它修镜像。"
                }
            };
        }

        public static string GetCommonScenarios()
        {
            return @"【常见使用场景：照着抄作业】

场景 1：Windows 更新反复失败（0x800f 开头居多）
  1. 管理员终端跑 /CheckHealth（1 分钟，看有没有标记损坏）。
  2. 有问题→ /ScanHealth（细扫，5-20 分钟）。
  3. → /RestoreHealth（联网修，10-60 分钟，卡 62.3% 别动）。
  4. 修完跑 sfc /scannow，再重启重试更新。
  5. 还失败：看 C:\Windows\Logs\DISM\dism.log 搜 0x 代码，缺源就换 /Source 离线修。
  小白要点：更新失败先别重装，八成是蓝图坏了，修蓝图就行。

场景 2：C 盘飘红、空间越用越少
  1. /AnalyzeComponentStore（看能回收多少）。
  2. /StartComponentCleanup（安全清理，更新还能卸）。
  3. 稳定运行两周以上且空间仍紧→才考虑 /ResetBase（不可逆！）。
  4. 再跑系统自带磁盘清理（cleanmgr）收尾。
  小白要点：/ResetBase 是单程票，点了就不能卸更新，犹豫就别点。

场景 3：蓝屏 / 系统文件损坏 / sfc 说修不好
  1. /RestoreHealth 先修蓝图。
  2. sfc /scannow 再验墙。
  3. 还蓝屏：用 WinDbg 或本工具 BSOD 模块看 dmp，锁定驱动再 /Get-Drivers→/Remove-Driver。
  4. 离线兜底：进 WinRE 或用 ISO 的 /Source 离线修。
  小白要点：顺序别反，先 DISM 后 SFC，否则 SFC 对着坏蓝图白忙。

场景 4：没网 / 内网机 / Windows Update 被禁
  1. 准备同版本 ISO，双击挂载记盘符（如 X:）。
  2. /Get-ImageInfo 看 Index，记下对应版本的数字。
  3. /RestoreHealth /Source:WIM:X:\sources\install.wim:N /LimitAccess（N 换成你的 Index）。
  4. 0x800f081f=源不对版，换 ISO 再试；成功后再 sfc 验证。
  小白要点：离线修九成问题出在“版本不对”，家庭版别拿专业版的 wim。

场景 5：老软件喊缺 .NET 3.5（含 2.0）
  1. /Get-Features 确认 NetFx3 状态。
  2. 有 ISO 就 /Enable-Feature /FeatureName:NetFx3 /All /Source:X:\sources\sxs /LimitAccess。
  3. 0x800f0906=没给 sxs 源，检查路径是不是 sources\sxs。
  4. 装完重开软件验证。
  小白要点：Win10/11 默认没它，老财务/工控软件几乎必装。

场景 6：更新驱动后蓝屏 / 设备叹号（驱动冲突）
  1. /Get-Drivers 找到嫌疑 oemXX.inf。
  2. /Get-DriverInfo /Driver:oemXX.inf 确认厂商版本（别删错网卡）。
  3. /Remove-Driver /Driver:oemXX.inf，重启。
  4. 去官网装回稳定版；重装前先 /Export-Driver 备份。
  小白要点：删驱动是高风险操作，删前备份+还原点，网卡驱动先下载好放桌面。

场景 7：应用商店 / 系统应用打不开闪退
  1. 先跑场景 3 的 DISM+SFC 连招修底座。
  2. 管理员 PowerShell 查预配包：Get-ProvisionedAppxPackages 看商店包状态。
  3. 移除问题预配包后去商店重装；sysprep 报错多是用户装的 Appx 捣乱，先删。
  4. 还不行：新建本地账户验证是不是用户配置烂了。
  小白要点：别批量删系统应用，开始菜单会残；一个个来。

场景 8：给镜像加驱动 / 封装母盘（进阶）
  1. 复制 install.wim 出来备份（原文件就是后悔药）。
  2. /Get-ImageInfo 确认 Index；空文件夹 C:\Mount 准备好。
  3. 先 /Mount-Image … /ReadOnly 练手，看得到 Windows 目录算成功。
  4. 真动手去掉 /ReadOnly，加驱动/补丁，/CheckHealth 自检。
  5. /Unmount-Image /Commit 提交（接电！别中途关机），失败则 /Discard 回滚+备份覆盖。
  小白要点：这是工程师活，第一次务必只读演练，别拿唯一原盘开刀。

【保命五条】
⚠️ 大招（/ResetBase、删驱动、提交镜像）前先建系统还原点+备份。
⚠️ 笔记本接电源，修复/提交中途断电最伤。
⚠️ 进度条 lâu bất động（62%、100%）多为正常，先看日志别手贱关机。
⚠️ 报错先抄 0x 代码+看 dism.log，再搜/再问，别连点三次大招。
⚠️ 个人文件 DISM 一般不动，但“一般”不是“一定”，重要数据先备份。
";
        }

        /// <summary>风险速查：三档风险一句话讲清。</summary>
        public static string GetRiskGuide()
        {
            return @"低风险：查询类（Get-/Check/Scan/Analyze）和常规修复（RestoreHealth/StartComponentCleanup），放心跑。
中风险：加驱动、装功能、装包、挂载镜像——做之前备份+还原点，错了能回。
高风险：/ResetBase（卸更新能力永久丢失）、删驱动（设备罢工）、提交镜像中途断电（镜像损坏）。拿不准就别点，先问人。";
        }

        /// <summary>DISM vs SFC 一句话对照，防用错。</summary>
        public static string GetDismVsSfc()
        {
            return @"DISM 修蓝图（组件存储 WinSxS），SFC 按蓝图修墙（系统文件）。顺序：先 DISM /RestoreHealth，再 sfc /scannow。SFC 说修不好，九成是蓝图先坏了。";
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
        /// <summary>动手前要准备什么（权限/镜像/备份/电源）。</summary>
        public string Prerequisites { get; set; } = "";
        /// <summary>跑完长什么样算成功，报错长什么样。</summary>
        public string ExampleOutput { get; set; } = "";
        /// <summary>搞砸了怎么回滚。</summary>
        public string Recovery { get; set; } = "";
    }
}
