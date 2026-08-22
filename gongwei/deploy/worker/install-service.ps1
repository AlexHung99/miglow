<#
.SYNOPSIS
    Installs GongWei.Worker as a Windows Service.

.DESCRIPTION
    Scheduling must not depend on an IIS application pool, which recycles and can idle
    out — the worker runs as its own service (spec 2.3, 10).
#>
[CmdletBinding()]
param(
    [string] $ServiceName = 'GongWeiWorker',
    [string] $InstallPath = 'C:\GongWeiSites\GongWei.Worker',
    [string] $DisplayName = '宮闈浮生 背景工作',
    [string] $Description = 'Outbox dispatch and scheduled jobs for GongWeiFuSheng.'
)

$ErrorActionPreference = 'Stop'

# One expression per line: Windows PowerShell 5.1 cannot parse a member access that
# starts on a continuation line, which silently made this check a syntax error.
$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)

if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell session."
}

$exePath = Join-Path $InstallPath 'GongWei.Worker.exe'

if (-not (Test-Path $exePath)) {
    throw "GongWei.Worker.exe was not found at $exePath. Publish the worker first."
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existing) {
    Write-Host "stopping existing service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

New-Service -Name $ServiceName `
            -BinaryPathName "`"$exePath`"" `
            -DisplayName $DisplayName `
            -Description $Description `
            -StartupType Automatic | Out-Null

# Restart on failure rather than leaving the outbox undrained; reset the counter daily
# so a transient blip does not permanently exhaust the retry budget.
sc.exe failure $ServiceName reset= 86400 actions= restart/30000/restart/60000/restart/120000 | Out-Null

Start-Service -Name $ServiceName

Write-Host "`n$ServiceName installed and started." -ForegroundColor Green
Write-Host "Logs: C:\GongWeiData\logs\worker-*.log"
Write-Host "Give the service account read access to its configuration and the media volume."
