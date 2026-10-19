[CmdletBinding()]
param(
    [string]$OutputDirectory = "Server_arm64"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspaceRoot = Split-Path -Parent $PSCommandPath
$sourceRoot = Join-Path $workspaceRoot 'MikuSB'
$projectPath = Join-Path $sourceRoot 'MikuSB\MikuSB.csproj'
$configTemplate = Join-Path $sourceRoot 'Config\Config.json'
$hotfixTemplate = Join-Path $sourceRoot 'Config\Hotfix.json'

$targetOutput = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $workspaceRoot $OutputDirectory
}

Write-Host ">>> [1/3] Building ARM64 runtime (Self-Contained)..." -ForegroundColor Cyan
& dotnet publish $projectPath -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=false -o $targetOutput
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for ARM64 target."
}

Write-Host ">>> [2/3] Writing portable configuration..." -ForegroundColor Cyan
$configDir = Join-Path $targetOutput 'Config'
New-Item -ItemType Directory -Path $configDir -Force | Out-Null
Copy-Item -LiteralPath $configTemplate -Destination (Join-Path $configDir 'Config.json') -Force
Copy-Item -LiteralPath $hotfixTemplate -Destination (Join-Path $configDir 'Hotfix.json') -Force

Write-Host ">>> [3/3] Generating mobile start script..." -ForegroundColor Cyan
$startScriptContent = @'
#!/bin/sh
export PATH="$PREFIX/bin:$PATH"
export TMPDIR="${TMPDIR:-$PREFIX/tmp}"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
mkdir -p "$TMPDIR"

CURRENT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$CURRENT_DIR"

if command -v grun >/dev/null 2>&1; then
    exec grun ./MikuSB "$@"
elif command -v glibc-runner >/dev/null 2>&1; then
    exec glibc-runner ./MikuSB "$@"
else
    exec ./MikuSB "$@"
fi
'@

$startScriptPath = Join-Path $targetOutput 'start.sh'
[System.IO.File]::WriteAllText($startScriptPath, $startScriptContent.Replace("`r`n", "`n"), [System.Text.Encoding]::ASCII)

Write-Host "`nARM64 mobile runtime published successfully to: $targetOutput" -ForegroundColor Green
Write-Host "Transfer this directory to your mobile terminal environment and run ./start.sh" -ForegroundColor Yellow
