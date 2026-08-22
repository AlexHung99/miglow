<#
.SYNOPSIS
    Imports a Cloudflare Origin Certificate into LocalMachine\My and prints its thumbprint.

.DESCRIPTION
    gongwei-api.miglow.vip sits behind Cloudflare's orange-cloud proxy, so the only client
    that ever completes a TLS handshake with this server is Cloudflare itself. That makes a
    Cloudflare Origin Certificate the right choice: it is free, lasts 15 years, can cover
    *.miglow.vip in one go, and Cloudflare trusts it even in Full (strict) mode.

    It is NOT trusted by browsers. That is fine here and is the reason the origin must stay
    unreachable except through Cloudflare — anyone hitting the IP directly would get a
    certificate warning, which is the signal that the proxy is being bypassed.

    Accepts either a single PEM file holding both blocks (-PemPath), which is how the
    Cloudflare dashboard is usually pasted into one file, or the certificate and key as
    separate files.

.PARAMETER PemPath
    One file containing both the CERTIFICATE and the PRIVATE KEY block.

.EXAMPLE
    .\import-origin-certificate.ps1 -PemPath C:\DOC\2dGameDoc\miglow_os_ca.txt

.EXAMPLE
    .\import-origin-certificate.ps1 -CertificatePath .\origin.pem -PrivateKeyPath .\origin.key

.NOTES
    Runs on Windows PowerShell 5.1 when openssl is available (Git for Windows ships one).
    Without openssl it needs PowerShell 7, because X509Certificate2.CreateFromPem is .NET 5
    and later and 5.1 runs on .NET Framework 4.8.
#>
[CmdletBinding(DefaultParameterSetName = 'Combined')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Combined')]
    [string] $PemPath,

    [Parameter(Mandatory, ParameterSetName = 'Split')]
    [string] $CertificatePath,

    [Parameter(Mandatory, ParameterSetName = 'Split')]
    [string] $PrivateKeyPath,

    [string] $FriendlyName = 'Cloudflare Origin - miglow.vip',

    # Path to openssl.exe if it is not on PATH.
    [string] $OpenSslPath
)

$ErrorActionPreference = 'Stop'

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)

if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell session.'
}

# --- read the PEM blocks -----------------------------------------------------
if ($PSCmdlet.ParameterSetName -eq 'Combined') {
    if (-not (Test-Path $PemPath)) {
        throw "File not found: $PemPath"
    }

    $content = [IO.File]::ReadAllText((Resolve-Path $PemPath).Path)
}
else {
    foreach ($path in @($CertificatePath, $PrivateKeyPath)) {
        if (-not (Test-Path $path)) {
            throw "File not found: $path"
        }
    }

    $content = [IO.File]::ReadAllText((Resolve-Path $CertificatePath).Path) + "`n" +
               [IO.File]::ReadAllText((Resolve-Path $PrivateKeyPath).Path)
}

# Singleline so . spans the base64 body; non-greedy so two certificates in one file do not
# collapse into a single match.
$certMatch = [regex]::Match(
    $content,
    '-----BEGIN CERTIFICATE-----.*?-----END CERTIFICATE-----',
    [Text.RegularExpressions.RegexOptions]::Singleline)

$keyMatch = [regex]::Match(
    $content,
    '-----BEGIN (?:RSA |EC )?PRIVATE KEY-----.*?-----END (?:RSA |EC )?PRIVATE KEY-----',
    [Text.RegularExpressions.RegexOptions]::Singleline)

if (-not $certMatch.Success) {
    throw 'No CERTIFICATE block found.'
}

if (-not $keyMatch.Success) {
    throw 'No PRIVATE KEY block found. Cloudflare shows the key once, at creation time.'
}

# --- convert to PFX ----------------------------------------------------------
#
# A random single-use password. The PFX exists only for the moment it takes to round-trip
# through the certificate store, so the password never needs to be recorded.
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$transitBytes = New-Object byte[] 32
$rng.GetBytes($transitBytes)
$transitPassword = [Convert]::ToBase64String($transitBytes)
$transitSecure = ConvertTo-SecureString -String $transitPassword -AsPlainText -Force

$workingDirectory = Join-Path $env:TEMP ("gongwei-cert-{0}" -f [guid]::NewGuid())
New-Item -ItemType Directory -Path $workingDirectory -Force | Out-Null

# Only this account may read the scratch directory while the key is briefly on disk.
icacls $workingDirectory /inheritance:r /grant "$($currentIdentity.Name):(OI)(CI)(F)" | Out-Null

$certFile = Join-Path $workingDirectory 'cert.pem'
$keyFile = Join-Path $workingDirectory 'key.pem'
$pfxFile = Join-Path $workingDirectory 'bundle.pfx'

try {
    # ASCII with no BOM: openssl rejects a PEM file that starts with a byte order mark.
    $noBom = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($certFile, $certMatch.Value + "`n", $noBom)
    [IO.File]::WriteAllText($keyFile, $keyMatch.Value + "`n", $noBom)

    if (-not $OpenSslPath) {
        $found = Get-Command openssl -ErrorAction SilentlyContinue

        if ($found) {
            $OpenSslPath = $found.Source
        }
        elseif (Test-Path 'C:\Program Files\Git\mingw64\bin\openssl.exe') {
            $OpenSslPath = 'C:\Program Files\Git\mingw64\bin\openssl.exe'
        }
        elseif (Test-Path 'C:\Program Files\Git\usr\bin\openssl.exe') {
            $OpenSslPath = 'C:\Program Files\Git\usr\bin\openssl.exe'
        }
    }

    if ($OpenSslPath -and (Test-Path $OpenSslPath)) {
        Write-Host "converting with $OpenSslPath"

        # -passout via env: so the password never appears in the process command line,
        # where any other user on the box could read it out of the process list.
        $env:GONGWEI_PFX_PASS = $transitPassword

        & $OpenSslPath pkcs12 -export `
            -in $certFile `
            -inkey $keyFile `
            -out $pfxFile `
            -passout env:GONGWEI_PFX_PASS

        $opensslExit = $LASTEXITCODE
        $env:GONGWEI_PFX_PASS = $null

        if ($opensslExit -ne 0 -or -not (Test-Path $pfxFile)) {
            throw "openssl failed to build the PFX (exit $opensslExit)."
        }
    }
    elseif ($PSVersionTable.PSVersion.Major -ge 7) {
        Write-Host 'converting with .NET CreateFromPem'

        $combined = [Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem(
            $certMatch.Value, $keyMatch.Value)

        $bytes = $combined.Export(
            [Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $transitPassword)

        [IO.File]::WriteAllBytes($pfxFile, $bytes)
    }
    else {
        throw 'Needs either openssl on PATH or PowerShell 7. Install one: winget install Microsoft.PowerShell'
    }

    $imported = Import-PfxCertificate `
        -FilePath $pfxFile `
        -CertStoreLocation 'Cert:\LocalMachine\My' `
        -Password $transitSecure

    $stored = Get-Item "Cert:\LocalMachine\My\$($imported.Thumbprint)"
    $stored.FriendlyName = $FriendlyName

    Write-Host ''
    Write-Host 'Imported into LocalMachine\My.' -ForegroundColor Green
    Write-Host "  Subject    : $($stored.Subject)"
    Write-Host "  Issuer     : $($stored.Issuer)"
    Write-Host "  Valid to   : $($stored.NotAfter.ToString('yyyy-MM-dd'))"
    Write-Host "  Private key: $($stored.HasPrivateKey)"

    $san = $stored.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' }

    if ($san) {
        Write-Host "  Hostnames  : $($san.Format($false))"
    }

    Write-Host ''
    Write-Host "Thumbprint: $($imported.Thumbprint)" -ForegroundColor Cyan

    if (-not $stored.HasPrivateKey) {
        throw 'The imported certificate has no private key; IIS cannot bind it.'
    }
}
finally {
    # Overwrite before unlinking: a deleted file is still recoverable off the disk.
    if (Test-Path $workingDirectory) {
        Get-ChildItem $workingDirectory -File | ForEach-Object {
            $blank = New-Object byte[] $_.Length
            [IO.File]::WriteAllBytes($_.FullName, $blank)
        }

        Remove-Item $workingDirectory -Recurse -Force
    }

    $transitPassword = $null
    $transitSecure = $null
    $env:GONGWEI_PFX_PASS = $null
    [GC]::Collect()
}

Write-Host ''
Write-Host 'Next:'
Write-Host "  .\install-sites.ps1 -CertificateThumbprint $($imported.Thumbprint)"
Write-Host ''
Write-Host 'Then delete the PEM file — the certificate now lives in the Windows store.'
