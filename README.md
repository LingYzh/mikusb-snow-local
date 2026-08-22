# Snowbreak local workspace

This repository tracks the source, patches, launchers and documentation needed to turn an existing, normal Windows game client into a local client/server workspace. It intentionally does **not** contain a game client, downloaded resources, player database, captures or built runtime output.

## First-time setup

1. Install the .NET 10 SDK on Windows.
2. Clone this repository to a writable ASCII-path folder, for example `D:\Snow`.
3. Provide a normal client installation whose root contains `Game\Binaries\Win64\Game.exe`.
4. In PowerShell, run:

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\Setup-LocalWorkspace.ps1 -ClientRoot 'E:\Games\Snowbreak'
   ```

   Add `-Rebuild` only to update an existing `Server\` runtime in place. Local player data, resources and configuration are preserved; only its configured client path and `-userdir` argument are refreshed.
5. Start `SnowbreakLocalLauncher.exe`. The first use requests elevation to install the local login routing. It leaves the selected system proxy and VPN enabled, adding bypasses only for the local login domains.

`Start-LocalServer.cmd` and `Start-LocalGame.cmd` are also available for separate launches. Closing the game or the GUI never stops the server; stop the server from its own console.

The first server startup downloads the public resource archive if `Server\Resources` is missing. This is the only bootstrap download; it is not tracked in Git.

## Repository boundaries

- `MikuSB\` — source code, tracked patch DLL, and source configuration template.
- Root scripts/docs — portable setup and local login routing.
- `data\`, `Server\`, `Patches\`, `Tools\`, captures and generated executables — deliberately ignored local state.

Do not commit a client installation, account data, resource archive or build output. The setup script writes the selected client path only to the ignored runtime config, never to source control.

## Current limitation

The GUI's multi-period gacha display editor is retained as an analysis interface only. The live client table is encrypted/compiled; deploying plaintext or a guessed table patch can prevent login. The current safe release does not install an active gacha display override.
