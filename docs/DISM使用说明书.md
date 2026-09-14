# DISM 使用说明书（说人话版）

> 适用：Windows 7 及以上。绝大多数命令需要**管理员权限**（右键“以管理员身份运行”）。
> 标准连招：**先 DISM 修蓝图，再 SFC 验墙**：`DISM /Online /Cleanup-Image /RestoreHealth` → `sfc /scannow` → 重启。
> 完整 32 条命令对照见程序内 `DismTutorial.GetTutorials()`，本文件可独立阅读、打印贴机房。

## 0. 一句话理解 DISM

把系统比作精装房：系统文件是墙面，组件存储（WinSxS）是蓝图仓库，
Windows 功能是门窗，驱动是水电。DISM 就是物业维修队——修墙、清垃圾、
加门窗、查水电，只动房子不动你的家具（个人文件）。

| 工具 | 通俗角色 | 管什么 |
|---|---|---|
| DISM | 工程队 | 修蓝图（组件存储）、清理 WinSxS、管理功能/驱动/更新包/镜像 |
| SFC | 室内质检员 | 按蓝图检查墙面（系统文件），蓝图坏了它也修不好 |
|CHKDSK| 结构检测 | 查硬盘物理/文件系统坏道，与 DISM 互补 |

## 1. 动手前须知（保命五条）

1. 笔记本**接电源**，修复/清理/提交镜像中途断电最伤。
2. 大招（`/ResetBase`、删驱动、提交镜像）前先**系统还原点 + 重要数据备份**。
3. 进度卡在 62.3%、100% 很久多为**正常**，先看 `C:\Windows\Logs\DISM\dism.log`，别手贱强制关机。
4. 报错先抄 **`0x` 代码**：`0x800f081f`=缺源/版本不对，`0x800f0906`=缺 NetFx3 的 sxs 源，`1726`=RPC/服务异常。
5. 版本要对：离线修复的 ISO 必须与本机**版本+架构**一致（家庭版别拿专业版的 wim）。

## 2. 最常用的 5 条（背下来）

```cmd
:: 1. 快速体检（1-5 分钟）
DISM /Online /Cleanup-Image /CheckHealth

:: 2. 全面 CT（5-20 分钟，只看不动）
DISM /Online /Cleanup-Image /ScanHealth

:: 3. 自动修复（10-60 分钟，最常用）
DISM /Online /Cleanup-Image /RestoreHealth

:: 4. 修完必跑（验证墙面）
sfc /scannow

:: 5. C 盘告急先分析再清理
DISM /Online /Cleanup-Image /AnalyzeComponentStore
DISM /Online /Cleanup-Image /StartComponentCleanup
```

## 3. 没网 / 内网机离线修复

```cmd
:: 先看 ISO 里哪个 Index 对应你的版本
DISM /Get-ImageInfo /ImageFile:X:\sources\install.wim

:: N 换成你的 Index（如 1）
DISM /Online /Cleanup-Image /RestoreHealth /Source:WIM:X:\sources\install.wim:1 /LimitAccess
```

`/Source` = 自带材料，`/LimitAccess` = 不许联网。两者常配合，企业内网标配。

## 4. C 盘清理：安全版 vs 不可逆版

- 安全：`/StartComponentCleanup`——扔旧零件，更新还能卸载，随便用。
- 不可逆：`/StartComponentCleanup /ResetBase`——连退路一起拆，**以后无法卸载已装更新**。
  系统稳定两周以上、空间实在不够、或封装母盘前才用，用前备份。

## 5. 老软件缺 .NET 3.5

```cmd
DISM /Online /Enable-Feature /FeatureName:NetFx3 /All /Source:X:\sources\sxs /LimitAccess
```

注意路径是 `sources\sxs`（不是 `sources\install.wim`）。`0x800f0906` 就是这个源没给对。

## 6. 驱动冲突：先认人再动手（高风险）

```cmd
DISM /Online /Get-Drivers /Format:Table
DISM /Online /Get-DriverInfo /Driver:oem12.inf
:: 确认元凶后（网卡/显卡先下载好官网版放桌面，建还原点）：
DISM /Online /Remove-Driver /Driver:oem12.inf
:: 重装前备份是好习惯：
DISM /Online /Export-Driver /Destination:D:\DriverBackup
```

## 7. 更新包：对账、单装、退货

```cmd
DISM /Online /Get-Packages /Format:Table
DISM /Online /Get-PackageInfo /PackageName:Package_for_KBxxxx~31bf3856ad364e35~amd64~~10.0.1.0
DISM /Online /Add-Package /PackagePath:D:\Patches\KBxxxx.msu
DISM /Online /Remove-Package /PackageName:Package_for_KBxxxx~31bf3856ad364e35~amd64~~10.0.1.0 /NoRestart
```

`/ResetBase` 之后或标记“不可卸载”的包退不掉，属预期。

## 8. 镜像挂载（进阶，先只读练手）

```cmd
DISM /Mount-Image /ImageFile:X:\sources\install.wim /Index:1 /MountDir:C:\Mount /ReadOnly
DISM /Unmount-Image /MountDir:C:\Mount /Discard
:: 真动手：去掉 /ReadOnly，改完提交（接电！）
DISM /Unmount-Image /MountDir:C:\Mount /Commit
:: 卡死清僵尸挂载：
DISM /Cleanup-Mountpoints
```

`C:\Mount` 必须是空文件夹，磁盘剩余建议大于镜像解开后两倍。提交前备份原 wim。

## 9. 八大场景速查

1. **更新失败**：Check → Scan → Restore → sfc → 重启 → 还失败看 dism.log。
2. **C 盘满**：Analyze → StartComponentCleanup → 仍紧且稳定两周才 ResetBase。
3. **蓝屏/sfc 修不好**：Restore 先修蓝图，再 sfc，再查 dmp 锁定驱动。
4. **没网修**：同版本 ISO + 对 Index + `/Source … /LimitAccess`。
5. **缺 NetFx3**：`/Enable-Feature … /Source:X:\sources\sxs`。
6. **驱动打架**：Get-Drivers 认 oem 号 → Get-DriverInfo 确认 → 备份 → Remove → 官网版装回。
7. **商店闪退**：DISM+SFC 连招 → 查预配包 → 重装，别批量删系统应用。
8. **封装镜像**：备份 wim → 只读挂载练手 → 真改 → 自检 → Commit（断电必烂尾）。

## 10. 风险三档

- **低风险**：查询类和常规修复/安全清理，放心跑。
- **中风险**：加驱动、装功能/补丁、挂载——备份+还原点，错了能回。
- **高风险**：`/ResetBase`、删驱动、提交镜像中途断电——拿不准先问人。

---

## 11. 功能包 Capabilities（Win10/11，微软官方文档口径）

> 参考：[DISM Capabilities Package Servicing Command-Line Options](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-capabilities-package-servicing-command-line-options)（Microsoft Learn）。
> 概念：Capability 是“不指定版本号就要服务”的包类型（如语言、.NET、OpenSSH），DISM 会自动找最新版。
> 铁律：每条命令必须带 `/Online` 或 `/Image:<路径>`；源查找顺序固定为 **`/Source` 指定位置 → 组策略位置 → Windows Update**（在线且无 `/LimitAccess` 时）。

```cmd
:: 查菜单（状态列：已安装 / 不存在）
DISM /Online /Get-Capabilities
:: 查菜品详情（包名里的 ~~~~ 一个都不能少）
DISM /Online /Get-CapabilityInfo /CapabilityName:OpenSSH.Client~~~~0.0.1.0
:: 点菜：装 OpenSSH 客户端（入门首练）
DISM /Online /Add-Capability /CapabilityName:OpenSSH.Client~~~~0.0.1.0
:: 内网机：走指定源且不许联网
DISM /Online /Add-Capability /CapabilityName:Language.Basic~~~en-US~0.0.1.0 /Source:\\server\share /LimitAccess
:: 退菜：一次可写多个 /CapabilityName
DISM /Online /Remove-Capability /CapabilityName:Language.Basic~~~en-US~0.0.1.0
```

小白要点：`~~~~0.0.1.0` 是通配写法，照抄即可；装失败先看有没有被组策略禁、再看 dism.log。

## 12. 版本 Edition（转正/升级，微软官方文档口径）

> 参考：[DISM Windows Edition-Servicing Command-Line Options](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-windows-edition-servicing-command-line-options)（Microsoft Learn）。
> 在线可用：`/Get-CurrentEdition`、`/Get-TargetEditions`、`/Set-ProductKey`、`/Set-Edition`（在线转高版本必须同时 `/AcceptEula /ProductKey`）。
> 三条红线：**只能往高转、不能降级**；已转过的镜像不要再转；从家族最低版本起转。**域控制器禁止把评估版转正式版**（先迁 FSMO，微软原话）。

```cmd
:: 看房产证（带 Eval = 试用版）
DISM /Online /Get-CurrentEdition
:: 问售楼处能换多大户型（列表里没有就只能重装）
DISM /Online /Get-TargetEditions
:: Server 评估版转正四步（先 GetEula 存协议看完，再 AcceptEula+Key 执行，完事重启）
DISM /Online /Set-Edition:ServerDatacenter /GetEula:C:\license.rtf
DISM /Online /Set-Edition:ServerDatacenter /ProductKey:XXXXX-XXXXX-XXXXX-XXXXX-XXXXX /AcceptEula
```

## 13. 预配应用 Appx（微软官方文档口径）

> 参考：[DISM App Package Servicing Command-Line Options](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-app-package--appx-or-appxbundle--servicing-command-line-options)（Microsoft Learn）。
> 在线/离线都支持：`/Get-ProvisionedAppxPackages`、`/Add-ProvisionedAppxPackage`、`/Remove-ProvisionedAppxPackage`。
> **官方强调的大坑**：`/Remove-ProvisionedAppxPackage` 只取消“新用户”的预配，已注册到现有用户的必须再用 PowerShell `Remove-AppxPackage` 逐个删，否则删不干净。

```cmd
:: 查交房标配（新用户会自动装的）
DISM /Online /Get-ProvisionedAppxPackages
:: 以后交房别配这件家具（包名从上一条完整复制）
DISM /Online /Remove-ProvisionedAppxPackage /PackageName:Microsoft.XboxApp_xxx_neutral_~_8wekyb3d8bbwe
# 现有账户再补一刀（PowerShell）：
Get-AppxPackage *Xbox* | Remove-AppxPackage
```

## 14. 映像导出与拆分（Image Management 口径）

```cmd
:: 查停车场：谁占着挂载目录（卡死先看它）
DISM /Get-MountedImageInfo
:: 熄火重打：抢救半截挂载
DISM /Remount-Image /MountDir:C:\Mount
:: 中场存档：保存但不卸载，继续改
DISM /Commit-Image /MountDir:C:\Mount
:: 只夹爱吃的菜：多 Index 的 ISO 只导出专业版（/Compress:max 最省，/CheckIntegrity 防坏盘）
DISM /Export-Image /SourceImageFile:X:\sources\install.wim /SourceIndex:2 /DestinationImageFile:D:\pro-only.wim /Compress:max /CheckIntegrity
:: 拆行李：FAT32 U 盘单文件不超 4GB，切 3800MB 一片，setup 自动拼回
DISM /Split-Image /ImageFile:D:\pro-only.wim /SWMFile:E:\install.swm /FileSize:3800
```

以上命令均已接入本程序：主界面 → DISM 快捷命令 → **“功能包与版本”选项卡**一键执行，`DismManager` 对应方法、`DismTutorial` 对应条目同步配套，程序内说明与本文档同口径。

---

## 附：SFC 手册（DISM 修完必须验墙，不看这章前面白修）

SFC（System File Checker）= 墙面质检员：拿每个系统文件的指纹（哈希）和蓝图仓库（WinSxS）里的正版指纹比对，对不上就覆盖。只动系统文件，不动个人文件。

```cmd
:: 先只看不动（几分钟，零风险）
sfc /verifyonly
:: 再动手修（10-30 分钟，管理员权限，卡住正常）
sfc /scannow
```

结论翻译（本程序会自动判读，这里教你人肉判读）：

| 看到这句话 | 意思 | 下一步 |
|---|---|---|
| Windows 资源保护找到了损坏文件并成功修复 | 修好了 | 重启观察 |
| 未发现完整性冲突 | 没病 | 收工 |
| 找到了损坏文件但无法修复 | 仓库先坏了，SFC 无米下锅 | 回头跑 DISM /RestoreHealth，再跑 sfc |
| 无法执行请求的操作 | 权限不够 | 右键以管理员身份运行 |

明细日志：`C:\Windows\Logs\CBS\CBS.log`（求助时和 dism.log 一起打包，见本程序“体检报告”）。

## 附：还原点三板斧（动手术前先按快门）

还原点 = 系统的快照（系统文件+注册表+已装程序状态），卷影复制（VSS）实现。只保系统不保个人文件；占系统盘空间，太满/太老会被自动删。

```powershell
# 管理员 PowerShell，顺序照敲：
Enable-ComputerRestore -Drive 'C:\'
Checkpoint-Computer -Description '动手术前' -RestorePointType 'MODIFY_SETTINGS'
Get-ComputerRestorePoint | Select-Object SequenceNumber,Description,CreationTime
rstrui.exe   # 真出事了，打开它选个点回去
```

本程序“还原点 🛟”窗干的就是这四件事，顶部有原理、每步可复制命令——点一遍，下次自己敲。
