<#
.SYNOPSIS
    Writes the connection string and the LINE channel secret into the API site's IIS
    configuration, prompting for each so neither is ever typed on a command line.

.DESCRIPTION
    Secrets live in IIS configuration rather than appsettings.json (spec §11): a release
    folder gets overwritten on every deploy and can end up in a backup or a Git working
    tree, whereas applicationHost.config does not travel.

    The values are stored as ASP.NET Core Module environment variables on the site, which
    the configuration system reads with the same precedence as any environment variable.
    Double underscore is the separator for a nested key: ConnectionStrings__GameDb becomes
    ConnectionStrings:GameDb.

    Nothing is echoed and nothing is written to disk by this script. Re-run it to rotate
    a value — for example after issuing a new channel secret in the LINE console.

.EXAMPLE
    .\set-app-settings.ps1
#>
[CmdletBinding()]
param(
    [string] $SiteName = 'GongWei.Api',
    [string] $DataRoot = 'C:\GongWeiData',

    # Skip a prompt when only one value needs rotating.
    [switch] $SkipConnectionString,
    [switch] $SkipChannelSecret
)

$ErrorActionPreference = 'Stop'

# One expression per line: Windows PowerShell 5.1 cannot parse a member access that
# starts on a continuation line, which silently made this check a syntax error.
$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)

if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell session.'
}

Import-Module WebAdministration

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    throw "Site $SiteName does not exist. Run install-sites.ps1 first."
}

$appcmd = Join-Path $env:SystemRoot 'System32\inetsrv\appcmd.exe'

if (-not (Test-Path $appcmd)) {
    throw "appcmd.exe not found. Is the IIS management console installed?"
}

function Read-Secret {
    param([string] $Prompt)

    $secure = Read-Host -Prompt $Prompt -AsSecureString

    # Marshal rather than ConvertFrom-SecureString -AsPlainText: this must work on
    # Windows PowerShell 5.1, where that parameter does not exist.
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)

    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Set-SiteEnvironmentVariable {
    param(
        [string] $Name,
        [string] $Value
    )

    # environmentVariables is a COLLECTION inside the aspNetCore section, not a section of
    # its own. Passing it as /section: makes appcmd fail with "Unknown config section" and
    # exit 2 — which is silent unless the exit code is checked, so it is checked here.
    $section = 'system.webServer/aspNetCore'

    # Remove first: appcmd appends, so setting a value twice would produce a duplicate
    # entry and IIS would refuse to start the site with a config error. A missing entry
    # makes this call fail, which is expected and ignored.
    & $appcmd set config $SiteName `
        "-section:$section" `
        "/-environmentVariables.[name='$Name']" `
        /commit:apphost 2>$null | Out-Null

    $output = & $appcmd set config $SiteName `
        "-section:$section" `
        "/+environmentVariables.[name='$Name',value='$Value']" `
        /commit:apphost 2>&1

    if ($LASTEXITCODE -ne 0) {
        # $output can echo the value back, so it is deliberately not included here.
        throw "appcmd failed setting $Name (exit $LASTEXITCODE)."
    }

    Write-Host "  set $Name" -ForegroundColor Green
}

Write-Host ''
Write-Host "Configuring $SiteName." -ForegroundColor Cyan
Write-Host 'Input is hidden. Nothing is echoed or written to disk.'
Write-Host ''

if (-not $SkipConnectionString) {
    Write-Host 'Connection string, for example:'
    Write-Host '  Host=127.0.0.1;Port=5433;Database=gongwei;Username=gongwei_app;Password=...'

    $connectionString = Read-Secret 'ConnectionStrings__GameDb'

    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw 'Empty connection string; nothing was changed.'
    }

    if ($connectionString -notmatch 'Database\s*=\s*gongwei\b') {
        # The instance also hosts optilogin, ttsp, payment and others. Pointing this
        # application at one of them by mistake is not a recoverable error.
        throw 'That connection string does not target the gongwei database. Refusing to set it.'
    }

    Set-SiteEnvironmentVariable -Name 'ConnectionStrings__GameDb' -Value $connectionString
    $connectionString = $null
}

if (-not $SkipChannelSecret) {
    Write-Host ''
    Write-Host 'LINE channel secret, from the LINE Developers console (Basic settings).'

    $channelSecret = Read-Secret 'LineLogin__ChannelSecret'

    if ([string]::IsNullOrWhiteSpace($channelSecret)) {
        throw 'Empty channel secret; nothing was changed.'
    }

    if ($channelSecret.Length -lt 24) {
        throw 'That is too short to be a LINE channel secret. Refusing to set it.'
    }

    Set-SiteEnvironmentVariable -Name 'LineLogin__ChannelSecret' -Value $channelSecret
    $channelSecret = $null
}

# Not secrets, but they must match the deployment rather than appsettings.json defaults.
Set-SiteEnvironmentVariable -Name 'ASPNETCORE_ENVIRONMENT' -Value 'Production'
Set-SiteEnvironmentVariable -Name 'DataProtection__KeyRingPath' -Value "$DataRoot\keys"

[GC]::Collect()

Write-Host ''
Write-Host 'Recycling the app pool so the new values are read...'
Restart-WebAppPool -Name 'GongWeiApiPool'

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
Write-Host 'Verify with: .\verify-endpoint.ps1'
