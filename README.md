# Snowbreak local workspace / 尘白禁区本地双端工作区

This repository contains the source, local launcher, injection patch and deployment tooling for turning an existing Windows client into a self-contained local client/server workspace for offline research and testing.

本仓库包含将已有 Windows 客户端改造成离线本地双端所需的服务端源码、本地启动器、注入补丁和部署工具，用于离线研究与测试。

It does **not** distribute any game client, game assets, downloaded master data, account data, captures or built runtime files.

本仓库**不**分发游戏客户端、美术与游戏资源、下载的数据表、账号数据、抓包或编译后的运行时文件。

## Current status / 当前状态

- Local server startup and client login are operational.
- The local workflow includes shop, inventory, combat-reward, ordinary gacha, weapon/accessory and supporter-card adaptations developed in this workspace.
- The launcher preserves the selected system proxy and VPN. Only the local-login game domains are configured to bypass that proxy.
- Closing the game client or GUI does not stop the server; stop the server from its own console when needed.

- 本地服务端启动与客户端登录已可用。
- 本工作区已包含商店、背包、战斗奖励、常规抽卡、武器/配件及后勤卡等离线适配。
- 启动器会保留当前系统代理与 VPN，仅让本地登录所需的游戏域名绕过代理。
- 退出客户端或 GUI 不会自动关闭服务端；需要停止服务端时，请在它自己的控制台中操作。

## Requirements / 环境要求

1. Windows 10/11 and the .NET 10 SDK.
2. A normal client installation whose root contains `Game\Binaries\Win64\Game.exe`.
3. A writable workspace path. An ASCII-only path such as `D:\Snow` is recommended for Windows PowerShell compatibility.

1. Windows 10/11 与 .NET 10 SDK。
2. 一份正常客户端，根目录内须存在 `Game\Binaries\Win64\Game.exe`。
3. 可写入的工作区路径。为兼容 Windows PowerShell，推荐使用 `D:\Snow` 这类纯 ASCII 路径。

## First-time setup / 首次部署

Clone the repository, open PowerShell at its root, and point the setup script to the **client root**, not the `Game` subdirectory:

克隆仓库后，在根目录打开 PowerShell，并将部署脚本指向**客户端根目录**，而不是 `Game` 子目录：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Setup-LocalWorkspace.ps1 -ClientRoot 'E:\Games\Snowbreak'
```

The script publishes the local server, injected client launcher and GUI; copies the tracked patch DLL; then writes the selected client path only to the ignored runtime config at `Server\Config\Config.json`.

脚本会发布本地服务端、注入式客户端启动器与 GUI，复制已追踪的补丁 DLL，并仅将所选客户端路径写入被忽略的运行时配置 `Server\Config\Config.json`。

For an existing runtime, use `-Rebuild`. Player data, resources and other configuration are preserved; the client path and `-userdir` argument are refreshed.

如需更新已有运行时，使用 `-Rebuild`。玩家数据、资源和其他配置会被保留；只刷新客户端路径与 `-userdir` 参数。

```powershell
.\Setup-LocalWorkspace.ps1 -ClientRoot 'E:\Games\Snowbreak' -Rebuild
```

## Starting the local pair / 启动本地双端

Start `SnowbreakLocalLauncher.exe` from the repository root and choose the combined start action. The first routing setup requests administrator permission to add the local hosts entries, loopback address and localhost port proxies. It keeps the current VPN/system proxy enabled and bypasses it only for the required local-login domains.

从仓库根目录启动 `SnowbreakLocalLauncher.exe` 并选择启动本地双端。首次配置登录路由时会请求管理员权限，用于写入本地 hosts、回环地址和本机端口转发；它会保留当前 VPN/系统代理，仅绕过必要的本地登录域名。

For separate launches, use:

若需要分别启动，可使用：

```text
Start-LocalServer.cmd
Start-LocalGame.cmd
```

If `Server\Resources` is absent, the first server start retrieves the required public resource archive. It remains local and is never tracked by Git.

若 `Server\Resources` 不存在，服务端首次启动会获取所需的公开资源档案。它只保留在本机，绝不会被 Git 追踪。

## Repository layout / 仓库边界

| Path / 路径 | Purpose / 用途 |
| --- | --- |
| `MikuSB\` | Source code, source configuration template and tracked patch DLL / 源码、配置模板和已追踪补丁 DLL |
| Root scripts and docs / 根目录脚本与文档 | Deployment, login routing and handoff / 部署、登录路由与交接说明 |
| `data\`, `Server\`, `Patches\`, `Tools\`, captures | Ignored local client/runtime state / 被忽略的客户端与运行时本地状态 |

Never commit a client installation, downloaded resources, player database, captured traffic or generated build output. The `.gitignore` is intentionally scoped to protect those boundaries.

请勿提交客户端、下载资源、玩家数据库、抓包或生成的构建输出。`.gitignore` 已专门限定这些边界。

## Known limitation / 已知限制

The server can provide the complete gacha pool list, but the client-side presentation/group table is encrypted or compiled. The GUI therefore does not deploy multi-period gacha-display patches: guessed or plaintext overrides can leave the client stuck at login completion. The functionality remains an analysis task until the original client format is reproduced.

服务端可以提供完整卡池列表，但客户端的展示/分组表为加密或编译格式。因此 GUI 暂不部署多期卡池展示补丁：猜测或明文覆盖会让客户端停在登录完成界面。在复原原始客户端格式前，该功能仅作为分析任务保留。

## Handoff / 交接

See [HANDOFF.md](HANDOFF.md) for the current implementation state, deployment verification record, rollback context and Windows PowerShell constraints.

当前实现状态、部署验证记录、回滚背景与 Windows PowerShell 注意事项请见 [HANDOFF.md](HANDOFF.md)。
