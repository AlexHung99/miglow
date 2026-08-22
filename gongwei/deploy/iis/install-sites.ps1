<#
.SYNOPSIS
    Creates the IIS sites, application pools, data directories and HTTPS bindings.

.DESCRIPTION
    Run once on the server, from an elevated session, after installing the ASP.NET Core
    Hosting Bundle. Publishing is a separate step (publish.ps1) so a redeploy never has to
    touch site configuration (spec §2.3).

    This script writes no secrets. The connection string and the LINE channel secret are
    supplied afterwards by set-app-settings.ps1.

.EXAMPLE
    .\install-sites.ps1 -CertificateThumbprint A1B2C3...
#>
[CmdletBinding()]
param(
    [string] $ApiHostName = 'gongwei-api.miglow.vip',
    [string] $AdminHostName = 'gongwei-admin.miglow.vip',
    [string] $SiteRoot = 'C:\GongWeiSites',
    [string] $DataRoot = 'C:\GongWeiData',

    # From import-origin-certificate.ps1, or any certificate covering both host names.
    [string] $CertificateThumbprint
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

if ($CertificateThumbprint) {
    $CertificateThumbprint = $CertificateThumbprint -replace '[^0-9A-Fa-f]', ''

    if (-not (Test-Path "Cert:\LocalMachine\My\$CertificateThumbprint")) {
        throw "No certificate with thumbprint $CertificateThumbprint in LocalMachine\My. " +
              'Run import-origin-certificate.ps1 first.'
    }
}

# --- data directories, all outside every web root (spec §2.3) ----------------
#
# keyring\api is the one that matters most: it seals the LINE login attempt payload.
# If it were per-process or inside the release folder, an app-pool recycle — or simply
# the next deploy — would fail every login that was mid-flight (line_login_v1.1 §8.4).
$directories = @(
    "$DataRoot\media",
    "$DataRoot\logs",
    "$DataRoot\keys",
    "$DataRoot\keyring\admin",
    "$DataRoot\backup"
)

foreach ($path in $directories) {
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Force -Path $path | Out-Null
        Write-Host "created $path"
    }
}

$sites = @(
    @{ Name = 'GongWei.Api';   HostName = $ApiHostName;   Pool = 'GongWeiApiPool' }
    @{ Name = 'GongWei.Admin'; HostName = $AdminHostName; Pool = 'GongWeiAdminPool' }
)

foreach ($site in $sites) {
    $physicalPath = Join-Path $SiteRoot $site.Name

    if (-not (Test-Path $physicalPath)) {
        New-Item -ItemType Directory -Force -Path $physicalPath | Out-Null
    }

    # --- application pool ---------------------------------------------------
    if (-not (Test-Path "IIS:\AppPools\$($site.Pool)")) {
        New-WebAppPool -Name $site.Pool | Out-Null
        Write-Host "created app pool $($site.Pool)"
    }

    # No managed code (Kestrel does the work), always running with preload so the first
    # request after a deploy is not the one that pays the startup cost.
    Set-ItemProperty "IIS:\AppPools\$($site.Pool)" managedRuntimeVersion ''
    Set-ItemProperty "IIS:\AppPools\$($site.Pool)" startMode 'AlwaysRunning'
    Set-ItemProperty "IIS:\AppPools\$($site.Pool)" processModel.idleTimeout ([TimeSpan]::Zero)

    # Scheduling lives in the Windows Service, so recycling here is safe and desirable.
    Set-ItemProperty "IIS:\AppPools\$($site.Pool)" recycling.periodicRestart.time ([TimeSpan]::FromHours(29))

    # --- site ---------------------------------------------------------------
    if (-not (Test-Path "IIS:\Sites\$($site.Name)")) {
        New-Website -Name $site.Name `
                    -PhysicalPath $physicalPath `
                    -ApplicationPool $site.Pool `
                    -HostHeader $site.HostName `
                    -Port 80 | Out-Null

        Write-Host "created site $($site.Name) for $($site.HostName)"
    }

    Set-ItemProperty "IIS:\Sites\$($site.Name)" applicationDefaults.preloadEnabled $true

    # --- HTTPS binding ------------------------------------------------------
    #
    # SslFlags 1 = SNI. Required: this box already serves api.miglow.vip on the same
    # address, and without SNI the second host name would take over the first one's
    # binding rather than sitting alongside it.
    if ($CertificateThumbprint) {
        $existing = Get-WebBinding -Name $site.Name -Protocol https -Port 443 `
                                   -HostHeader $site.HostName -ErrorAction SilentlyContinue

        if (-not $existing) {
            New-WebBinding -Name $site.Name -Protocol https -Port 443 `
                           -HostHeader $site.HostName -SslFlags 1 | Out-Null
            Write-Host "created HTTPS binding for $($site.HostName)"
        }

        $binding = Get-WebBinding -Name $site.Name -Protocol https -Port 443 `
                                  -HostHeader $site.HostName

        # Rebinding an existing binding is how a certificate renewal is applied, so this
        # runs every time rather than only on first creation.
        $binding.AddSslCertificate($CertificateThumbprint, 'My')
        Write-Host "bound certificate to $($site.HostName)"
    }
    else {
        Write-Warning "No -CertificateThumbprint given; $($site.HostName) will answer on 80 only."
        Write-Warning 'Cloudflare will report error 525 until the HTTPS binding exists.'
    }

    # --- filesystem permissions --------------------------------------------
    $identity = "IIS AppPool\$($site.Pool)"

    # Read and execute only: the application must not be able to rewrite itself.
    icacls $physicalPath /grant "${identity}:(OI)(CI)(RX)" /T | Out-Null

    # Logs are writable by both sites.
    icacls "$DataRoot\logs" /grant "${identity}:(OI)(CI)(M)" | Out-Null
}

# The API writes uploaded portraits; the admin site only reads them back.
icacls "$DataRoot\media" /grant 'IIS AppPool\GongWeiApiPool:(OI)(CI)(M)' | Out-Null
icacls "$DataRoot\media" /grant 'IIS AppPool\GongWeiAdminPool:(OI)(CI)(RX)' | Out-Null

# Each site owns its own key ring, and nothing else may read either. /inheritance:r drops
# the inherited Users ACE — a key ring readable by every local account would let any
# process on the box unseal a login attempt or forge an auth cookie.
icacls "$DataRoot\keys" /inheritance:r /grant 'IIS AppPool\GongWeiApiPool:(OI)(CI)(M)' /grant 'Administrators:(OI)(CI)(F)' | Out-Null
icacls "$DataRoot\keyring\admin" /inheritance:r /grant 'IIS AppPool\GongWeiAdminPool:(OI)(CI)(M)' /grant 'Administrators:(OI)(CI)(F)' | Out-Null

Write-Host ''
Write-Host 'Sites configured.' -ForegroundColor Green
Write-Host ''
Write-Host 'Next:'
Write-Host '  1. .\publish.ps1                 publish the application files'
Write-Host '  2. .\set-app-settings.ps1        connection string + LINE channel secret'
Write-Host '  3. .\verify-endpoint.ps1         confirm the origin answers TLS on 443'
Write-Host '  4. Set Cloudflare SSL/TLS mode to Full (strict)'
Write-Host '  5. ..\worker\install-service.ps1 install the worker'
