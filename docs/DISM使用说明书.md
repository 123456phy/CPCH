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
