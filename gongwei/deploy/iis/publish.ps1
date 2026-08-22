<#
.SYNOPSIS
    Publishes GongWei.Api (and optionally GongWei.Admin) into their IIS site folders.

.DESCRIPTION
    Stops the app pool, copies the published output, drops the matching web.config in, and
    starts the pool again. Configuration set by set-app-settings.ps1 lives in IIS, not in
    the release folder, so it survives every redeploy.

    Only the API is published by default: as of v1.1 the Admin site does not compile
    (task #15) and would fail the build step.

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -IncludeAdmin
#>
[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string] $SiteRoot = 'C:\GongWeiSites',
    [switch] $IncludeAdmin
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

$targets = @(
    @{
        Project   = 'src\GongWei.Api'
        Site      = 'GongWei.Api'
        Pool      = 'GongWeiApiPool'
        WebConfig = 'api.web.config'
    }
)

if ($IncludeAdmin) {
    $targets += @{
        Project   = 'src\GongWei.Admin'
        Site      = 'GongWei.Admin'
        Pool      = 'GongWeiAdminPool'
        WebConfig = 'admin.web.config'
    }
}

foreach ($target in $targets) {
    $projectPath = Join-Path $RepositoryRoot $target.Project
    $stagingPath = Join-Path $env:TEMP ("gongwei-publish-{0}" -f $target.Site)
    $sitePath = Join-Path $SiteRoot $target.Site

    Write-Host ''
    Write-Host "=== $($target.Site) ===" -ForegroundColor Cyan

    if (Test-Path $stagingPath) {
        Remove-Item $stagingPath -Recurse -Force
    }

    # Publish to staging first. A failed build must not leave a half-copied site behind.
    Write-Host 'building...'
    dotnet publish $projectPath --configuration Release --output $stagingPath --nologo

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $($target.Project); nothing was deployed."
    }

    # appsettings.json ships, but it holds no secrets — those come from IIS configuration.
    # A stray Development file would override Production settings, so it never travels.
    Get-ChildItem $stagingPath -Filter 'appsettings.Development.json' -Recurse |
        Remove-Item -Force

    Write-Host "stopping $($target.Pool)..."

    if ((Get-WebAppPoolState -Name $target.Pool).Value -ne 'Stopped') {
        Stop-WebAppPool -Name $target.Pool

        # The pool releases its file locks asynchronously; copying too early fails on
        # the main assembly with a sharing violation.
        $deadline = (Get-Date).AddSeconds(30)

        while ((Get-WebAppPoolState -Name $target.Pool).Value -ne 'Stopped') {
            if ((Get-Date) -gt $deadline) {
                throw "App pool $($target.Pool) did not stop within 30 seconds."
            }

            Start-Sleep -Milliseconds 500
        }
    }

    try {
        Write-Host 'copying...'

        # /MIR mirrors, so files removed from the build are removed from the site too.
        # logs is excluded because it is written at runtime by the site itself.
        robocopy $stagingPath $sitePath /MIR /NFL /NDL /NJH /NJS /NP /XD logs | Out-Null

        # robocopy exit codes below 8 are success; 8 and above are real failures.
        $robocopyExit = $LASTEXITCODE

        if ($robocopyExit -ge 8) {
            throw "robocopy failed with exit code $robocopyExit."
        }

        # robocopy returns 1 when it copied something, which is success but would become
        # this script's own exit code and read as a failed deploy to whatever called it.
        # Overwrite it with a genuine zero.
        cmd /c exit 0

        Copy-Item (Join-Path $PSScriptRoot $target.WebConfig) `
                  (Join-Path $sitePath 'web.config') -Force
    }
    finally {
        Write-Host "starting $($target.Pool)..."
        Start-WebAppPool -Name $target.Pool
    }

    Remove-Item $stagingPath -Recurse -Force
    Write-Host "$($target.Site) deployed." -ForegroundColor Green
}

Write-Host ''
Write-Host 'Publish complete. Verify with: .\verify-endpoint.ps1' -ForegroundColor Green

exit 0
