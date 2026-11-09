# DSTserver · 饥荒联机版开服器

> by大肥鱼deepseek · 仓库地址 https://github.com/OTTOWallace/DSTserver

![build](https://github.com/OTTOWallace/DSTserver/actions/workflows/build.yml/badge.svg)
![license](https://img.shields.io/badge/license-MIT-green)

Windows 版《饥荒联机版》（Don't Starve Together / DST）**专用服务器图形化启动器**。把开服的脏活累活全包了：装服务器、下 mod、改配置、建世界、备份回档、控制台管理，一个窗口全部搞定。

---

## 目录

- [功能一览](#功能一览)
- [环境要求](#环境要求)
- [快速开始](#快速开始)
- [使用手册](#使用手册)
  - [主窗口](#主窗口)
  - [Mod 管理](#mod-管理)
  - [世界设置](#世界设置)
  - [玩家与控制台](#玩家与控制台)
  - [备份与回档](#备份与回档)
- [创意工坊 v1（legacy.bin）自动转换](#创意工坊-v1legacybin-自动转换)
- [数据与文件位置](#数据与文件位置)
- [配置说明](#配置说明)
- [项目结构](#项目结构)
- [开发与构建](#开发与构建)
- [自动化构建（GitHub Actions）](#自动化构建github-actions)
- [常见问题（FAQ）](#常见问题faq)
- [License](#license)

---

## 功能一览

| 模块 | 功能 |
|---|---|
| 服务器 | 一键安装 / 更新专用服务器（SteamCMD + app 343050），Master + Caves 双分片启停，实时日志 |
| Mod | 输入创意工坊编号直接下载；增删 / 启用停用 / 全选批量删除；自动解析 `modinfo.lua` 配置项生成编辑控件 |
| 世界 | 20 项世界生成变量（世界大小、季节、昼夜、分支、资源再生等），地上/地下独立设置，分别写入 `worldgenoverride.lua` |
| 存档 | 5 个存档槽位独立切换；定时备份（可配周期）；按日期回档窗口；服务器内 `c_rollback` 快照回档；多槽位批量删除世界存档（删除前可选自动备份） |
| 控制台 | 指令直发服务器 stdin；一键存档 `c_save()`；在线玩家列表每 5 秒刷新；选中玩家快捷给予物品（60+ 物品下拉） |
| 定时任务 | 自动检查 mod 更新、自动备份（默认各 24 小时，可在 `settings.json` 调整） |
| 健壮性 | 令牌冲突（E_ROWID_EXIST）弹窗提示；窗口自愈看门狗（防前台锁定导致窗口隐藏）；zip-slip 与指令注入防护 |

## 环境要求

| 项 | 要求 |
|---|---|
| 操作系统 | Windows 10 1909+ / Windows 11（x64）；Server 2019 / 2022 / 2025 |
| 运行时 | [.NET 10 桌面运行时](https://dotnet.microsoft.com/download/dotnet/10.0)（开发/编译则需要 .NET 10 SDK） |
| 磁盘 | ≥ 6 GB（服务器本体约 4.3 GB） |
| 网络 | 能访问 Steam CDN（首次安装服务器时下载约 4.3 GB） |
| 权限 | 无需管理员权限（所有路径均在用户可写区域） |

> 家庭版 / 专业版首次开服按防火墙提示放行即可；Server 版需手动放行 UDP 10999 / 10998（Master/Caves 端口）。

## 快速开始

**方式一：直接跑发布版**

1. 下载 [Release](https://github.com/OTTOWallace/DSTserver/releases) 里的 zip，解压
2. 安装 .NET 10 桌面运行时（链接见上表）
3. 双击 `DSTserver.exe`，点「安装 / 更新饥荒服务器」，等下载完成
4. 去 [Klei 账户页](https://accounts.klei.com/account/game/servers?game=DST) 生成服务器令牌，粘贴进启动器
5. 点「启动服务器」

**方式二：从源码构建**

```powershell
git clone git@github.com:OTTOWallace/DSTserver.git
cd DSTserver

# 一键安装 SteamCMD + 饥荒专用服务器（约 4.3 GB）
.\bootstrap.ps1

# 编译并运行
dotnet build -c Debug
dotnet run --project DSTserver.csproj
```

## 使用手册

### 主窗口

- **存档槽位**：5 个独立存档（对应 `Cluster_1` ~ `Cluster_5`），切换前需先停服
- **服务器设置**：名称、密码、人数（1-64）、模式（生存/荒野/无尽）、PVP、无人暂停、洞穴开关
- **服务器令牌**：点「获取令牌」打开 Klei 页面，「从剪贴板粘贴令牌」一键填入
- **启动 / 停止**：启动时自动生成 `cluster.ini`、`server.ini`、`cluster_token.txt`
- **维护按钮**：立即备份 / 回档窗口 / 检查 mod 更新

### Mod 管理

- 顶部输入创意工坊编号 → 「下载 Mod」（v1 格式自动转换，见下文）
- 列表每行两个勾选框：**选择**（批量操作）与**启用**（服务器开关）
- 「全选 / 删除选中」批量管理；「检查全部 mod 更新」逐个重拉最新版
- 选中 mod 后右侧自动渲染其 `configuration_options` 的动态控件（下拉框 / 复选框 / 数字框），「保存配置」写入 Master + Caves 的 `modoverrides.lua`

### 世界设置

- 20 项 worldgen 变量，**地上（Master）与地下（Caves）分标签独立设置**，各自写入 `Master\worldgenoverride.lua` 与 `Caves\worldgenoverride.lua`
- **只在创建新世界时生效**：改完需删除旧存档（`Cluster_N\Master\save` 等）重新生成

### 玩家与控制台

- 在线玩家列表每 5 秒刷新（名称 + SteamID + NetID）
- 选中玩家 → 选物品、填数量 → 「给予」（按 `userid` 定向注入 `GiveItem` 指令）
- 控制台输入 Lua 指令回车即发；「一键存档」= `c_save()`；「服务器内回档」= `c_rollback(N)`

### 备份与回档

- 「立即备份」把 Master/Caves 打包为 zip（运行中也可备份，自动跳过被锁定的日志文件）
- 「回档窗口」按时间范围（1/3/7/30 天）筛选备份，选择后自动停服 → 解包覆盖 → 提示重启
- 「删除存档」勾选多个槽位批量删除世界存档（默认删除前自动备份一次），配置保留、世界重新生成
- 定时备份默认每 24 小时，服务器运行中自动顺延到停止后

## 创意工坊 v1（legacy.bin）自动转换

部分工坊物品（即使很热门）通过 SteamCMD 下载时只能拿到 **v1 打包格式 `_legacy.bin`**（本质是一个 ZIP，未按目录展开）。

本启动器内置自动转换：下载后检测到 `*.bin` → 校验 ZIP 头 → 解压还原 v2 散文件布局 → 包裹目录自动上提一层 → 复制时跳过 bin 壳。**全程无需手动处理**。

> 已实测转换成功的 v1 格式 mod：Quick Pick(501385076)、Extra Equip Slots(375850593)、Display food values(347079953)、Wormhole Marks(362175979)、Don't Drop Everything(661253977)。

## 数据与文件位置

| 内容 | 路径 |
|---|---|
| 服务器程序 / SteamCMD | `<程序目录>\runtime\steamcmd\` |
| 存档与配置 | `文档\Klei\DoNotStarveTogether\Cluster_1`（槽位 1~5） |
| mod 下载缓存 | `<steamcmd>\steamapps\workshop\content\322330\<id>\` |
| 服务器加载的 mod | `mods\workshop-<id>\`（开服后迁移到 `ugc_mods\Cluster_N\...\322330\<id>\`） |
| 备份 | `<程序目录>\backups\Cluster_N\yyyyMMdd_HHmmss\` |
| 启动器设置 | `<程序目录>\settings.json`（含 Klei 令牌，**勿提交/分享**） |

## 配置说明

`settings.json` 关键项：

```jsonc
{
  "SteamCmdDir": "",                 // 留空 = 程序目录\runtime\steamcmd
  "Cluster": {
    "ClusterName": "找啊找啊找饥友",
    "ClusterSlot": 1,                // 1~5
    "Token": "你的 Klei 令牌",
    "EnableCaves": true
  },
  "EnableScheduledBackup": true,
  "BackupIntervalHours": 24,         // 定时备份周期
  "EnableModAutoCheck": true,
  "ModCheckIntervalHours": 24        // 定时 mod 检查周期
}
```

## 项目结构

```
DSTserver/          主程序（WPF / .NET 10，零第三方 NuGet 依赖，可离线构建）
  Services/         核心服务：SteamCmd、Mod、备份、配置读写、Lua 解析、服务器控制
DSTserver.Tests/    单元/集成测试（19 项）
bootstrap.ps1       一键装 SteamCMD + 饥荒专用服务器
settings.example.json  配置模板（真实 settings.json 不入库）
.github/workflows/  CI 自动构建
```

## 开发与构建

```powershell
# 构建（Debug / Release，均 0 错误 0 警告）
dotnet build DSTserver/DSTserver.csproj -c Debug
dotnet build DSTserver/DSTserver.csproj -c Release

# 运行测试
dotnet run --project DSTserver.Tests/DSTserver.Tests.csproj -c Debug

# 发布（框架依赖，目标机器需 .NET 10 桌面运行时）
dotnet publish DSTserver/DSTserver.csproj -c Release --no-self-contained

# 发布（自包含单文件，目标机器无需 .NET；需能访问 NuGet 下载运行时包）
dotnet publish DSTserver/DSTserver.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

项目无第三方依赖，`NuGet.config` 已清空外部源，**断网也能完整构建**。

## 自动化构建（GitHub Actions）

推送到 `main` 自动执行：**Release 编译 → 跑 19 项测试 → 发布 → 上传产物**，可在 Actions 页下载 `DSTserver-release`。

推送 `v*` 标签（如 `git tag v1.0.0 && git push origin v1.0.0`）会自动创建 **GitHub Release** 并附带 zip 包。

## 常见问题（FAQ）

**Q：日志里出现 `-- type 'quit' to exit --` 是什么？**
SteamCMD 的控制台横幅，无害。新版本已过滤，界面只保留下载/错误信息。

**Q：开服后日志刷 `E_ROWID_EXIST`？**
你的服务器令牌正被另一个在线服务器占用。去 Klei 生成新令牌，或停掉另一台同令牌的服务器；启动器会弹窗提示。

**Q：mod 列表显示「未下载」但 mod 明明能用？**
服务器开服后会把 mod 迁到 `ugc_mods` 目录，列表会自动识别两处位置；刷新列表即可。

**Q：mod 下载后报 `_legacy.bin` 打不开？**
那是工坊 v1 打包格式，启动器会自动解压转换；若目录异常可删掉缓存目录后重新「检查 mod 更新」。

**Q：窗口偶尔闪一下不见了？**
Windows 前台锁定机制的偶发问题。内置看门狗 5 秒内自动恢复显示。

**Q：改完世界设置没生效？**
worldgen 变量只在创建新世界时生效，需删除旧存档重新生成。

## License

[MIT](./LICENSE) © 2026 大肥鱼 (OTTOWallace)
