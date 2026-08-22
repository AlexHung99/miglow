<#
.SYNOPSIS
    Checks the origin and the Cloudflare edge separately, so a failure points at one of
    them rather than at "the site is down".

.DESCRIPTION
    The two halves fail differently and the distinction is what saves time:

      origin fails, edge 525   -> no HTTPS binding or no certificate for this host name
      origin fails, edge 502   -> binding exists, the application is not running
      origin ok,    edge 525   -> Cloudflare cannot trust the certificate (check SSL mode)
      both ok                  -> done

.EXAMPLE
    .\verify-endpoint.ps1
    .\verify-endpoint.ps1 -HostName gongwei-api.miglow.vip -OriginAddress <origin-ip>
#>
[CmdletBinding()]
param(
    [string] $HostName = 'gongwei-api.miglow.vip',
    [string] $OriginAddress = '127.0.0.1',
    # Everything lives under /api/v1, health endpoints included (api_v1_v1.1 §1).
    [string] $Path = '/api/v1/meta'
)

$ErrorActionPreference = 'Continue'

# Windows PowerShell 5.1 still defaults to SSL3/TLS1.0 for Invoke-WebRequest, which
# Cloudflare refuses. Without this the edge check fails for a reason that has nothing to
# do with the site being tested.
[Net.ServicePointManager]::SecurityProtocol =
    [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls11

function Write-Result {
    param([string] $Label, [bool] $Ok, [string] $Detail)

    $mark = if ($Ok) { 'PASS' } else { 'FAIL' }
    $colour = if ($Ok) { 'Green' } else { 'Red' }

    Write-Host ("{0,-4}  {1,-34} {2}" -f $mark, $Label, $Detail) -ForegroundColor $colour
}

Write-Host ''
Write-Host "Checking $HostName$Path" -ForegroundColor Cyan
Write-Host ('-' * 72)

# --- 1. is anything listening on 443 at the origin ---------------------------
$tcp = Test-NetConnection -ComputerName $OriginAddress -Port 443 -WarningAction SilentlyContinue
Write-Result 'origin TCP 443' $tcp.TcpTestSucceeded $OriginAddress

# --- 2. does the origin complete a TLS handshake for THIS host name ----------
#
# The handshake is the interesting part. IIS resets the connection when no binding
# matches the SNI host name, which is exactly what Cloudflare reports as 525.
$tlsOk = $false
$tlsDetail = 'not attempted'
$originHttpOk = $false
$originHttpDetail = 'not attempted'

if ($tcp.TcpTestSucceeded) {
    $client = New-Object Net.Sockets.TcpClient

    try {
        $client.Connect($OriginAddress, 443)

        # Accept any certificate: a Cloudflare Origin Certificate is not publicly trusted
        # by design, so validating it here would report a failure that is not one.
        $stream = New-Object Net.Security.SslStream($client.GetStream(), $false, { $true })
        $stream.AuthenticateAsClient($HostName)

        $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]$stream.RemoteCertificate

        $tlsOk = $true
        $tlsDetail = "$($certificate.Subject), expires $($certificate.NotAfter.ToString('yyyy-MM-dd'))"

        # The request goes down this same socket rather than through Invoke-WebRequest.
        # Invoke-WebRequest would resolve the host name through DNS and land on Cloudflare,
        # which is the very thing this check exists to bypass. Windows PowerShell 5.1 has
        # no equivalent of curl --resolve, so the request is written by hand.
        try {
            $request = "GET $Path HTTP/1.1`r`nHost: $HostName`r`nConnection: close`r`n`r`n"
            $bytes = [Text.Encoding]::ASCII.GetBytes($request)

            $stream.Write($bytes, 0, $bytes.Length)
            $stream.Flush()

            $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::UTF8)
            $statusLine = $reader.ReadLine()

            if ($statusLine -match '^HTTP/\d\.\d\s+(\d{3})') {
                $originStatus = [int]$Matches[1]
                $originHttpOk = $originStatus -eq 200
                $originHttpDetail = "HTTP $originStatus"
            }
            else {
                $originHttpDetail = "unrecognised response: $statusLine"
            }

            $reader.Dispose()
        }
        catch {
            $originHttpDetail = $_.Exception.Message
        }

        $stream.Dispose()
    }
    catch {
        # No ?. or ?? here: this script has to run under Windows PowerShell 5.1, which
        # parses neither operator.
        if ($_.Exception.InnerException) {
            $tlsDetail = $_.Exception.InnerException.Message
        }
        else {
            $tlsDetail = $_.Exception.Message
        }
    }
    finally {
        $client.Dispose()
    }
}

Write-Result 'origin TLS (SNI match)' $tlsOk $tlsDetail
Write-Result 'origin application' $originHttpOk $originHttpDetail

# --- 4. and through Cloudflare -----------------------------------------------
$edgeOk = $false
$edgeDetail = 'not attempted'

try {
    $response = Invoke-WebRequest -Uri "https://$HostName$Path" -UseBasicParsing -TimeoutSec 20
    $edgeOk = $response.StatusCode -eq 200
    $edgeDetail = "HTTP $($response.StatusCode)"
}
catch {
    # Captured before the switch: inside a switch block $_ is the switch input, not the
    # error record, so referring to $_.Exception in the default arm would be silently empty.
    $failure = $_
    $status = $failure.Exception.Response.StatusCode.value__

    $edgeDetail = switch ($status) {
        525 { '525 - Cloudflare could not complete TLS with the origin' }
        526 { '526 - origin certificate rejected; SSL mode is Full (strict)' }
        502 { '502 - reached the origin, the application did not answer' }
        521 { '521 - origin refused the connection' }
        default {
            if ($status) { "HTTP $status" } else { $failure.Exception.Message }
        }
    }
}

Write-Result 'through Cloudflare' $edgeOk $edgeDetail

Write-Host ('-' * 72)

if ($edgeOk) {
    Write-Host ''
    Write-Host 'Origin and edge both healthy.' -ForegroundColor Green
    Write-Host 'Next: sign in through LINE once, then run grant-super-admin.'
    exit 0
}

Write-Host ''

if (-not $tlsOk) {
    Write-Host 'The origin has no working HTTPS binding for this host name.' -ForegroundColor Yellow
    Write-Host '  .\import-origin-certificate.ps1 -CertificatePath .\origin.pem -PrivateKeyPath .\origin.key'
    Write-Host '  .\install-sites.ps1 -CertificateThumbprint <thumbprint>'
}
elseif (-not $originHttpOk) {
    Write-Host 'TLS works but the application is not answering.' -ForegroundColor Yellow
    Write-Host '  .\publish.ps1'
    Write-Host '  .\set-app-settings.ps1'
    Write-Host '  Then read C:\GongWeiData\logs for the startup error.'
}
else {
    Write-Host 'The origin is healthy; the problem is between Cloudflare and the origin.' -ForegroundColor Yellow
    Write-Host '  Check the Cloudflare SSL/TLS encryption mode (Full or Full (strict)).'
}

exit 1
