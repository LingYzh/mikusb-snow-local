[CmdletBinding()]
param(
    [switch]$Elevated
)

$ErrorActionPreference = 'Stop'

$Hosts = @(
    'cbjq-serverlist.xoyocdn.com',
    'cbjq-client.xoyocdn.com',
    'cbjq-client-qq.xoyocdn.com',
    'cbjq-client-hsyq.xoyocdn.com',
    'js2sdk.xoyo.com',
    'sh-jxsj.xgsdk.com',
    'passport.xoyo.com',
    'xgsdk.xoyo.games',
    'xqdata.xoyo.games',
    'sh-qrcode.xoyo.com'
)
$HostsPath = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-HostsConfigured {
    if (-not (Test-Path -LiteralPath $HostsPath)) { return $false }
    $content = Get-Content -LiteralPath $HostsPath -Raw
    foreach ($hostName in $Hosts) {
        if ($content -notmatch "(?im)^\s*127\.0\.0\.1\s+$([regex]::Escape($hostName))(\s|$)") {
            return $false
        }
    }
    return $true
}

function Test-LoopbackConfigured {
    return $null -ne (Get-NetIPAddress -IPAddress '42.192.24.211' -ErrorAction SilentlyContinue)
}

function Test-PortProxyConfigured {
    $output = (& netsh interface portproxy show v4tov4 2>$null | Out-String)
    return (Test-PortProxyRule -Output $output -Port 443) -and
           (Test-PortProxyRule -Output $output -Port 18443)
}

function Test-PortProxyRule {
    param([string]$Output, [int]$Port)
    return $Output -match ("127\.0\.0\.1\s+{0}\s+127\.0\.0\.1\s+13443" -f $Port)
}

function Add-GameProxyBypass {
    # Keep the user's current proxy/VPN selected.  Only the local-server login
    # hostnames bypass it, so they can reach hosts + portproxy on this machine.
    $path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
    $current = (Get-ItemProperty -Path $path -Name ProxyOverride -ErrorAction SilentlyContinue).ProxyOverride
    $entries = New-Object System.Collections.Generic.List[string]
    foreach ($entry in ($current -split ';')) {
        $trimmed = $entry.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed) -and -not $entries.Contains($trimmed)) {
            [void]$entries.Add($trimmed)
        }
    }
    foreach ($hostName in $Hosts) {
        if (-not $entries.Contains($hostName)) {
            [void]$entries.Add($hostName)
        }
    }

    Set-ItemProperty -Path $path -Name ProxyOverride -Value ($entries -join ';')
}

function Write-LocalHosts {
    $lines = if (Test-Path -LiteralPath $HostsPath) {
        Get-Content -LiteralPath $HostsPath
    } else {
        @()
    }

    $kept = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        $managed = $trimmed -match '#\s*(MikuSB|Snowbreak Local Server)'
        if (-not $managed) {
            foreach ($hostName in $Hosts) {
                if ($trimmed -match [regex]::Escape($hostName)) {
                    $managed = $true
                    break
                }
            }
        }
        if (-not $managed) { [void]$kept.Add($line) }
    }

    while ($kept.Count -gt 0 -and [string]::IsNullOrWhiteSpace($kept[$kept.Count - 1])) {
        $kept.RemoveAt($kept.Count - 1)
    }
    [void]$kept.Add('')
    [void]$kept.Add('# Snowbreak Local Server')
    foreach ($hostName in $Hosts) {
        [void]$kept.Add("127.0.0.1 $hostName # MikuSB")
    }
    Set-Content -LiteralPath $HostsPath -Value $kept -Encoding Ascii
}

function Ensure-LocalRouting {
    if (-not (Test-LoopbackConfigured)) {
        & netsh interface ipv4 add address 'Loopback Pseudo-Interface 1' 42.192.24.211 255.255.255.255
        if ($LASTEXITCODE -ne 0 -or -not (Test-LoopbackConfigured)) {
            throw '无法绑定官方登录 IP 到 Loopback Pseudo-Interface 1。'
        }
    }

    Write-LocalHosts

    $portProxy = (& netsh interface portproxy show v4tov4 2>$null | Out-String)
    if (-not (Test-PortProxyRule -Output $portProxy -Port 443)) {
        & netsh interface portproxy add v4tov4 listenaddress=127.0.0.1 listenport=443 connectaddress=127.0.0.1 connectport=13443
        if ($LASTEXITCODE -ne 0) { throw '无法创建 443 -> 13443 端口转发。' }
    }
    if (-not (Test-PortProxyRule -Output $portProxy -Port 18443)) {
        & netsh interface portproxy add v4tov4 listenaddress=127.0.0.1 listenport=18443 connectaddress=127.0.0.1 connectport=13443
        if ($LASTEXITCODE -ne 0) { throw '无法创建 18443 -> 13443 端口转发。' }
    }
}

Add-GameProxyBypass
$needsAdmin = -not (Test-HostsConfigured) -or -not (Test-LoopbackConfigured) -or -not (Test-PortProxyConfigured)

if ($needsAdmin -and -not (Test-Administrator)) {
    $arguments = "-NoProfile -EP Bypass -File `"$PSCommandPath`" -Elevated"
    $powerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $process = Start-Process -FilePath $powerShell -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    exit $process.ExitCode
}

if ($needsAdmin) {
    Ensure-LocalRouting
}

Write-Host '本地服登录重定向已就绪：系统代理/VPN 保持启用，仅本地服登录域名绕过代理。'
