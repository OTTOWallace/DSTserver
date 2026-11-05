# DSTserver · 饥荒联机版开服器

> by大肥鱼deepseek

Windows 版《饥荒联机版》专用服务器（Don't Starve Together Dedicated Server）图形化启动器。

## 功能

- 一键安装 / 更新饥荒专用服务器（SteamCMD + app 343050）
- 服务器启停（Master + Caves 双分片）、实时日志
- 输入创意工坊 mod 编号直接下载，增删 / 启用停用 / 批量全选删除
- 独立 Mod 管理窗口：自动解析 `modinfo.lua` 的 `configuration_options`，动态生成配置控件并写入 `modoverrides.lua`
- 自动识别创意工坊 **v1 打包格式（legacy.bin）** 并解压转换为 v2 目录结构
- 世界生成变量编辑（20 项，写入 `worldgenoverride.lua`）
- 5 个存档槽位切换
- 在线玩家列表实时刷新（名称 + SteamID），选中玩家快捷给予物品（60+ 物品下拉）
- 服务器控制台指令直发、一键存档（c_save）、服务器内回档（c_rollback）
- 定时备份 / 定时检查 mod 更新（默认各 24 小时，可配置），按日期回档窗口
- 服务器令牌冲突（E_ROWID_EXIST）弹窗提示
- 窗口自愈看门狗（防前台锁定导致的窗口隐藏）

## 前置依赖

| 依赖 | 说明 |
|---|---|
| Windows 10 1909+ / Windows 11（x64） | 也支持 Server 2019/2022/2025 |
| .NET 10 桌面运行时 | 从 [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) 安装，或安装 Visual Studio 2026 自带 |
| SteamCMD | 首次点「安装 / 更新饥荒服务器」会自动下载到程序目录 |
| 饥荒专用服务器本体 | 自动安装（约 4.3 GB） |
| Klei 服务器令牌 | 到 [Klei 账户页](https://accounts.klei.com/account/game/servers?game=DST) 生成，粘贴进启动器 |

启动器自身**无需管理员权限**；家庭版/专业版首次开服按防火墙提示放行即可，Server 版需手动放行 UDP 10999/10998。

## 快速开始

```powershell
# 1. 一键安装服务器本体（等价于启动器里的安装按钮）
.\bootstrap.ps1

# 2. 编译并运行（需要 .NET 10 SDK）
cd DSTserver
dotnet build -c Debug
dotnet run --project DSTserver.csproj
```

或者直接运行发布版：`DSTserver\bin\Release\net10.0-windows\publish\DSTserver.exe`

## 发布

```powershell
# 框架依赖版（目标机器需装 .NET 10 桌面运行时）
dotnet publish DSTserver\DSTserver.csproj -c Release --no-self-contained

# 自包含单文件版（目标机器无需 .NET；需要网络下载运行时包）
dotnet publish DSTserver\DSTserver.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 目录结构

```
DSTserver/          主程序（WPF / .NET 10，零第三方 NuGet 依赖，可离线构建）
DSTserver.Tests/    单元/集成测试（19 项）
DSTserver-cpp/      早期 C++ Win32 骨架（历史备份）
bootstrap.ps1       一键装 SteamCMD + 饥荒专用服务器
```

## 数据位置

- 服务器程序：`<程序目录>\runtime\steamcmd\...`
- 存档与配置：`文档\Klei\DoNotStarveTogether\Cluster_1`（槽位 1~5 对应 Cluster_1~5）
- 备份：`<程序目录>\backups\Cluster_N\`

## 安全提示

- 本地配置 `settings.json` 含 Klei 令牌，已加入 .gitignore，切勿提交
- 更换令牌后需重启服务器生效
