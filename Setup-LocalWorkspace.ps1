[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$ClientRoot,

    [switch]$Rebuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspaceRoot = Split-Path -Parent $PSCommandPath
$sourceRoot = Join-Path $workspaceRoot 'MikuSB'
$serverRoot = Join-Path $workspaceRoot 'Server'
$clientRootFull = [System.IO.Path]::GetFullPath($ClientRoot)
$gameExecutable = Join-Path $clientRootFull 'Game\Binaries\Win64\Game.exe'

function Invoke-DotnetPublish {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory
    )

    & dotnet publish $ProjectPath -c Release -r win-x64 --self-contained false -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $ProjectPath (exit code $LASTEXITCODE)."
    }
}

if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    throw "Missing source directory: $sourceRoot"
}

if (-not (Test-Path -LiteralPath $gameExecutable -PathType Leaf)) {
    throw "ClientRoot must contain Game\\Binaries\\Win64\\Game.exe. Received: $clientRootFull"
}

if ((Test-Path -LiteralPath $serverRoot) -and -not $Rebuild) {
    throw "Runtime directory already exists: $serverRoot. Use -Rebuild only when you intend to update its executable files while preserving local data."
}

if (Get-Process -Name 'Game' -ErrorAction SilentlyContinue) {
    throw 'Close the game client before preparing or updating the local workspace.'
}

New-Item -ItemType Directory -Path $serverRoot -Force | Out-Null

$serverProject = Join-Path $sourceRoot 'MikuSB\MikuSB.csproj'
$clientLauncherProject = Join-Path $sourceRoot 'MikuSB.ClientLauncher\MikuSB.ClientLauncher.csproj'
$localLauncherProject = Join-Path $sourceRoot 'MikuSB.LocalLauncher\MikuSB.LocalLauncher.csproj'

Write-Host '[1/4] Publishing local server...'
Invoke-DotnetPublish -ProjectPath $serverProject -OutputDirectory $serverRoot

Write-Host '[2/4] Publishing injected game launcher...'
Invoke-DotnetPublish -ProjectPath $clientLauncherProject -OutputDirectory $serverRoot

Write-Host '[3/4] Copying the tracked client patch...'
$patchSource = Join-Path $sourceRoot 'Patch\MikuSB-Patch.dll'
$patchDirectory = Join-Path $serverRoot 'Patch'
if (-not (Test-Path -LiteralPath $patchSource -PathType Leaf)) {
    throw "Tracked patch DLL is missing: $patchSource"
}
New-Item -ItemType Directory -Path $patchDirectory -Force | Out-Null
Copy-Item -LiteralPath $patchSource -Destination (Join-Path $patchDirectory 'MikuSB-Patch.dll') -Force

Write-Host '[4/4] Writing portable runtime configuration and GUI launcher...'
$configTemplate = Join-Path $sourceRoot 'Config\Config.json'
$configDirectory = Join-Path $serverRoot 'Config'
$configPath = Join-Path $configDirectory 'Config.json'
New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
if (Test-Path -LiteralPath $configPath -PathType Leaf) {
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
}
else {
    $config = Get-Content -LiteralPath $configTemplate -Raw | ConvertFrom-Json
}
$config.Loader.GamePath = $gameExecutable
$config.Loader.Arguments = @($config.Loader.Arguments | Where-Object { -not $_.StartsWith('-userdir=', [System.StringComparison]::OrdinalIgnoreCase) })
$config.Loader.Arguments += "-userdir=$(Join-Path $serverRoot 'Client_User_Data')"
$config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $configPath -Encoding UTF8

Invoke-DotnetPublish -ProjectPath $localLauncherProject -OutputDirectory $workspaceRoot

Write-Host ''
Write-Host 'Setup complete.'
Write-Host '1. Run SnowbreakLocalLauncher.exe and select Start local server and client.'
Write-Host '2. Approve the routing prompt once; it preserves the active VPN and system proxy.'
Write-Host '3. First server launch downloads the required public game-data resources when absent.'
