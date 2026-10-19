# Offline local client-server workspace / 离线本地双端工作区

This repository contains source code, a local launcher, an injection patch and deployment tooling for adapting an existing Windows client into a self-contained local client/server workspace for offline research and testing.

本仓库包含将已有 Windows 客户端适配为离线本地双端所需的源码、本地启动器、注入补丁和部署工具，用于离线研究与测试。

It does **not** distribute the target client, client assets, downloaded data, account data, captures or built runtime files.

本仓库**不**分发目标客户端、客户端资源、下载数据、账号数据、抓包或编译后的运行时文件。

## Current status / 当前状态

- Local server startup and client login are operational.
- The local workflow includes shop, inventory, combat-reward, ordinary gacha, weapon/accessory and supporter-card adaptations developed in this workspace.
- The launcher preserves the selected system proxy and VPN. Only the local-login endpoints are configured to bypass that proxy.
- Closing the client or GUI does not stop the server; stop the server from its own console when needed.

- 本地服务端启动与客户端登录已可用。
- 本工作区已包含商店、背包、战斗奖励、常规抽卡、武器/配件及后勤卡等离线适配。
- 启动器会保留当前系统代理与 VPN，仅让本地登录所需端点绕过代理。
- 退出客户端或 GUI 不会自动关闭服务端；需要停止服务端时，请在它自己的控制台中操作。

## Requirements / 环境要求

1. Windows 10/11 and the .NET 10 SDK. Confirm it is available with `dotnet --info`.
2. A compatible, normally installed Windows client. Its **root** must contain `Game\Binaries\Win64\Game.exe`.
3. A writable workspace path. An ASCII-only path such as `D:\LocalWorkspace` is recommended for Windows PowerShell compatibility.
4. Network access for the first server start when the public resource archive has not yet been downloaded.

1. Windows 10/11 与 .NET 10 SDK；可用 `dotnet --info` 确认。
2. 一份已正常安装且兼容的 Windows 客户端；其**根目录**内必须存在 `Game\Binaries\Win64\Game.exe`。
3. 可写入的工作区路径。为兼容 Windows PowerShell，推荐使用 `D:\LocalWorkspace` 这类纯 ASCII 路径。
4. 如果尚未下载公开资源档案，首次启动服务端时需要网络连接。

## First-time deployment / 首次部署

### 1. Clone and prepare / 克隆并准备目录

Clone this repository into a new writable directory. Do not place the client installation inside that directory, and do not copy a pre-existing `Server\` directory into it.

将仓库克隆到新的可写目录。不要将客户端安装目录放入该目录，也不要把已有的 `Server\` 运行时目录复制进来。

```powershell
git clone <your-repository-url> D:\LocalWorkspace
Set-Location D:\LocalWorkspace
```

Before continuing, close all client processes. Verify the chosen path is the **client root** rather than the `Game` folder itself:

继续前请关闭所有客户端进程。确认选择的是**客户端根目录**，而不是 `Game` 文件夹本身：

```powershell
Test-Path 'E:\Client\Game\Binaries\Win64\Game.exe'
```

The result must be `True`.

结果必须为 `True`。

### 2. Build the local runtime / 构建本地运行时

Allow script execution only for the current PowerShell window, then provide the verified client root to the setup script:

仅为当前 PowerShell 窗口允许脚本执行，然后把已验证的客户端根目录交给部署脚本：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Setup-LocalWorkspace.ps1 -ClientRoot 'E:\Client'
```

The script performs the following work:

脚本会完成以下工作：

1. Publishes `Server\MikuSB.exe`, the local server.
2. Publishes `Server\SnowLauncher.exe`, the injected client launcher.
3. Copies the tracked patch DLL to `Server\Patch\MikuSB-Patch.dll`.
4. Creates `Server\Config\Config.json` and writes the supplied client path plus a local `-userdir` path.
5. Publishes the GUI launcher and its required companion files at the repository root.

1. 发布本地服务端 `Server\MikuSB.exe`。
2. 发布注入式客户端启动器 `Server\SnowLauncher.exe`。
3. 将已追踪补丁 DLL 复制到 `Server\Patch\MikuSB-Patch.dll`。
4. 创建 `Server\Config\Config.json`，写入所提供的客户端路径及本地 `-userdir` 路径。
5. 在仓库根目录发布 GUI 启动器及其运行所需文件。

The selected client path is written only to ignored runtime state; it is never written into the source-controlled configuration template.

所选客户端路径只会写入被忽略的运行时状态，不会写入受版本控制的配置模板。

### 3. Configure local login routing / 配置本地登录路由

Run the GUI launcher created at the repository root and choose the combined start action. The first routing setup requests administrator permission to add local hosts entries, a loopback address and localhost port proxies.

从仓库根目录运行生成的 GUI 启动器，选择启动本地双端。首次配置登录路由时会请求管理员权限，以添加本地 hosts 条目、回环地址和本机端口转发。

The routing action does not disable or replace the selected VPN/system proxy. It preserves that setting and bypasses it only for the required local-login endpoints. Approve the Windows elevation prompt; if it is declined, the client cannot use the local login route.

该路由操作不会关闭或替换当前 VPN/系统代理；它会保留现有设置，仅让必要的本地登录端点绕过代理。请批准 Windows 提权提示；若拒绝，客户端无法使用本地登录路由。

### 4. First start / 首次启动

Keep the GUI open while it starts the server and client. If `Server\Resources` is missing, the server first retrieves the required public resource archive; wait for that operation to finish before treating startup as failed. Server logs are available under `Server\Config\Logs\`.

保持 GUI 打开，等待其启动服务端和客户端。如果缺少 `Server\Resources`，服务端会先获取所需的公开资源档案；完成前请不要将其视为启动失败。服务端日志位于 `Server\Config\Logs\`。

For separate starts, first complete the routing step, then use:

如需分别启动，请先完成路由配置，然后使用：

```text
Start-LocalServer.cmd
Start-LocalGame.cmd
```

The server is intentionally independent of the GUI and client lifecycle. Closing the client or GUI does not stop it.

服务端的生命周期刻意独立于 GUI 和客户端；关闭客户端或 GUI 不会停止服务端。

## Updating an existing runtime / 更新已有运行时

Close the client first, then run the same command with `-Rebuild`:

请先关闭客户端，然后使用同一命令加上 `-Rebuild`：

```powershell
.\Setup-LocalWorkspace.ps1 -ClientRoot 'E:\Client' -Rebuild
```

`-Rebuild` refreshes executable files, the patch DLL, the GUI and the configured client path. Player data, downloaded resources and other runtime configuration remain in place.

`-Rebuild` 会更新可执行文件、补丁 DLL、GUI 与已配置的客户端路径。玩家数据、下载资源和其他运行时配置会保留在原处。

## Repository layout / 仓库边界

| Path / 路径 | Purpose / 用途 |
| --- | --- |
| `MikuSB\` | Source code, source configuration template and tracked patch DLL / 源码、配置模板和已追踪补丁 DLL |
| Root scripts and docs / 根目录脚本与文档 | Deployment, login routing and handoff / 部署、登录路由与交接说明 |
| `data\`, `Server\`, `Patches\`, `Tools\`, captures | Ignored local client/runtime state / 被忽略的客户端与运行时本地状态 |

Never commit a client installation, downloaded resources, player database, captured traffic or generated build output. The `.gitignore` is intentionally scoped to protect those boundaries.

请勿提交客户端、下载资源、玩家数据库、抓包或生成的构建输出。`.gitignore` 已专门限定这些边界。

## Mobile terminal deployment / 移动终端部署支持

This workspace supports cross-compiling and running the local server standalone on ARM64 mobile terminal environments (e.g., Linux / POSIX user-space layers).

本工作区支持将本地服务端交叉编译并在 ARM64 架构的移动终端环境（如 Linux / POSIX 用户态层）中独立运行。

### 1. Build mobile runtime / 构建移动端运行时

Run the mobile publishing script from PowerShell:

在 PowerShell 中运行移动端发布脚本：

```powershell
.\Publish-MobileServer.ps1 -OutputDirectory Server_arm64
```

The script publishes a self-contained ARM64 runtime (`linux-arm64`) using directory deployment (`PublishSingleFile=false` to avoid loader page-alignment constraints on mobile environments), copies configuration templates, and generates an automated `start.sh` launcher.

该脚本会发布一个自包含的 ARM64 运行时（`linux-arm64`），采用目录部署形式（`PublishSingleFile=false`，以规避移动端环境下的加载器内存页对齐限制），复制配置模板并生成自动化的 `start.sh` 启动脚本。

### 2. Run on mobile terminal / 在移动终端中运行

1. Transfer the generated directory to the mobile environment.
2. In host environments lacking standard Glibc dynamic linkers, use a user-space runner (`glibc-runner` via `grun ./MikuSB`) or a container layer (`proot-distro`).
3. Ensure the required runtime environment variables are configured:
    - `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`
    - `TMPDIR` redirected to a writable path within the local sandbox.
4. Execute `./start.sh` to start the local service listening on loopback ports.

1. 将生成的目录传输到移动终端环境中。
2. 在缺少标准 Glibc 动态链接器的宿主环境中，可配合用户态运行器（通过 `glibc-runner` 执行 `grun ./MikuSB`）或轻量容器（如 `proot-distro`）提供标准环境。
3. 确保配置了必要的运行时环境变量：
    - `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`
    - `TMPDIR` 重定向至本地沙盒内的可写路径。
4. 执行 `./start.sh` 即可启动监听本地回环端口的服务。

## Known limitation / 已知限制

The server can provide the complete gacha pool list, but the client-side presentation/group table is encrypted or compiled. The GUI therefore does not deploy multi-period gacha-display patches: guessed or plaintext overrides can leave the client stuck at login completion. The functionality remains an analysis task until the original client format is reproduced.

服务端可以提供完整卡池列表，但客户端的展示/分组表为加密或编译格式。因此 GUI 暂不部署多期卡池展示补丁：猜测或明文覆盖会让客户端停在登录完成界面。在复原原始客户端格式前，该功能仅作为分析任务保留。

## Handoff / 交接

See [HANDOFF.md](HANDOFF.md) for the current implementation state, deployment verification record, rollback context and Windows PowerShell constraints.

当前实现状态、部署验证记录、回滚背景与 Windows PowerShell 注意事项请见 [HANDOFF.md](HANDOFF.md)。
