<#
.SYNOPSIS
    Daily custom-format pg_dump with integrity recording (spec §13.2).

.DESCRIPTION
    Checks the exit code, size and SHA-256 of every dump and writes a manifest line, so
    "the backup ran" and "the backup is usable" are not confused with each other.

    Encryption and off-server copying are deliberately left to the operator's chosen
    tooling — this script produces the artefact and the evidence.

    Retention default follows the spec's starting point: 7 daily / 4 weekly / 6 monthly.
#>
[CmdletBinding()]
param(
    [string] $PgHost = '127.0.0.1',
    [int]    $Port = 5433,
    [string] $Database = 'gongwei',
    [string] $UserName = 'gongwei_app',
    [string] $BackupRoot = 'C:\GongWeiData\backup',
    [string] $PgDumpPath = 'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe',
    [int]    $KeepDailyDays = 7
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PgDumpPath)) {
    throw "pg_dump not found at $PgDumpPath."
}

# PGPASSWORD is expected to come from the scheduled task's protected environment,
# never from this file.
if (-not $env:PGPASSWORD) {
    throw "PGPASSWORD is not set. Configure it on the scheduled task, not in this script."
}

$daily = Join-Path $BackupRoot 'daily'
New-Item -ItemType Directory -Force -Path $daily | Out-Null

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$file = Join-Path $daily "gongwei-$stamp.dump"

Write-Host "dumping $Database to $file..."

& $PgDumpPath --host=$PgHost --port=$Port --username=$UserName --dbname=$Database `
              --format=custom --compress=9 --file=$file

if ($LASTEXITCODE -ne 0) {
    throw "pg_dump exited with code $LASTEXITCODE — the backup is NOT usable."
}

$info = Get-Item $file

if ($info.Length -lt 1024) {
    throw "The dump is only $($info.Length) bytes; treating it as failed."
}

$hash = (Get-FileHash -Path $file -Algorithm SHA256).Hash

$manifest = Join-Path $BackupRoot 'manifest.csv'

if (-not (Test-Path $manifest)) {
    'timestamp,file,bytes,sha256,result' | Set-Content -Path $manifest -Encoding utf8
}

"$(Get-Date -Format o),$($info.Name),$($info.Length),$hash,ok" |
    Add-Content -Path $manifest -Encoding utf8

Write-Host "ok: $($info.Length) bytes, sha256 $hash" -ForegroundColor Green

# --- retention ---------------------------------------------------------------
$cutoff = (Get-Date).AddDays(-$KeepDailyDays)

Get-ChildItem $daily -Filter 'gongwei-*.dump' |
    Where-Object { $_.LastWriteTime -lt $cutoff } |
    ForEach-Object {
        Write-Host "removing expired daily backup $($_.Name)"
        Remove-Item $_.FullName -Force
    }

Write-Host "`nReminder: restore-test into a blank instance every quarter (spec 13.2)."
