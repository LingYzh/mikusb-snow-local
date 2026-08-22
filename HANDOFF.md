# Local workspace handoff

Updated: 2026-08-23

## Current result

The local client can start and log in again. The project is being reshaped so a clone plus an already-installed normal client can recreate the local two-process setup without committing the client, downloaded resources, runtime database, packet captures, PAK experiments or tools.

The authoritative source tree is `MikuSB\`. `Server\` and `data\` are local runtime directories and are intentionally ignored.

## Reproducible deployment

Use `Setup-LocalWorkspace.ps1 -ClientRoot <normal-client-root>` from the repository root. It publishes:

- `Server\MikuSB.exe` — local server;
- `Server\SnowLauncher.exe` — injected client launcher;
- `Server\Patch\MikuSB-Patch.dll` — tracked patch payload;
- `SnowbreakLocalLauncher.exe` — GUI launcher.

It creates an ignored `Server\Config\Config.json` and inserts only the supplied client path. At first start, the server retrieves its publicly hosted resource archive when needed.

`Configure-LocalLoginRouting.ps1` is the only administrator step. It adds the required local hosts, loopback IP and localhost port proxies, while retaining the active VPN/system proxy and bypassing only the local game-login domains. It uses the absolute Windows PowerShell path and `-EP Bypass` so it does not depend on shell PATH or a non-ASCII script filename.

## Verified/local behavior

- Starting and logging in are working again.
- Shop purchases, combat rewards, inventory normalization, ordinary gacha draws/side rewards, GM bootstrap materials, weapon/accessory and supporter-card changes are implemented in the dirty source changes under `MikuSB\`.
- Game exit and GUI exit intentionally do **not** stop the server. Earlier PID/name-based shutdown attempts were removed because they could terminate the server while the client was still active.

## Active limitation: gacha display layout

`Gacha_GetPoolList` now returns all server-side gacha IDs, but the client-side group/presentation table is encrypted/compiled. The GUI can inspect and group the resource data, yet it must not deploy plaintext Lua/table content to the live client. A diagnostic patch that did so caused login to stall and was removed. The currently shipped `Patch_OfflineGachaDisplay-WindowsNoEditor_999_P.pak`, if present in a local runtime, is deliberately inert and must not be treated as a functional display override.

Continue this work only after reproducing the original encrypted client format. Do not test table overrides against the main client without copying/removing the PAK first and validating a login rollback path.

## PowerShell/launcher constraints

- Keep `.cmd`, `.bat` and PowerShell filenames ASCII when Windows PowerShell 5.1 is involved; use BOM if such source needs Chinese text.
- Invoke PowerShell from batch via its absolute `System32\WindowsPowerShell\v1.0\powershell.exe` path and `-EP Bypass`.
- Do not assign automatic PowerShell variables such as `$Host` or `$PID`.
- Do not use transient process names, PIDs or WMI alone to decide when to shut down the server.
- Parenthesize PowerShell pipeline results before combining them with `-join`.
- `New-Item` uses `-Path`, not `-LiteralPath`.

The older `MikuSB\HANDOFF.md` remains a historical handoff from before this root-level migration; this file is the current one.
