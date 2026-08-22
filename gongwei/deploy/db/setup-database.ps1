<#
.SYNOPSIS
    Creates the gongwei database and application role, applies migrations and seeds
    reference data.

.DESCRIPTION
    Run this yourself — it prompts for passwords interactively and writes the resulting
    connection string into dotnet user-secrets. No secret is ever written into the repo
    or into appsettings.json (spec 2.3, 11).

    You need:
      * the PostgreSQL superuser password (only used by this script, never stored)
      * a password you choose for the gongwei_app application role

.EXAMPLE
    ./deploy/db/setup-database.ps1 -Port 5433
#>
[CmdletBinding()]
param(
    [string] $PgHost = '127.0.0.1',
    [int]    $Port = 5433,
    [string] $SuperUser = 'postgres',
    [string] $Database = 'gongwei',
    [string] $AppRole = 'gongwei_app',
    [string] $PsqlPath = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if (-not (Test-Path $PsqlPath)) {
    throw "psql not found at $PsqlPath. Pass -PsqlPath with the correct location."
}

Write-Host "== 宮闈浮生 database setup ==" -ForegroundColor Cyan
Write-Host "target: $PgHost`:$Port/$Database"

$superSecure = Read-Host -Prompt "PostgreSQL '$SuperUser' password" -AsSecureString
$superPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($superSecure))

$appSecure = Read-Host -Prompt "Password to set for the '$AppRole' application role" -AsSecureString
$appPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($appSecure))

if ([string]::IsNullOrWhiteSpace($appPlain)) {
    throw "The application role password cannot be empty."
}

try {
    $env:PGPASSWORD = $superPlain

    # --- role and database -------------------------------------------------
    Write-Host "`n[1/5] creating role and database..." -ForegroundColor Yellow

    $bootstrap = @"
DO `$`$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '$AppRole') THEN
        CREATE ROLE $AppRole LOGIN PASSWORD '$($appPlain -replace "'", "''")';
    ELSE
        ALTER ROLE $AppRole LOGIN PASSWORD '$($appPlain -replace "'", "''")';
    END IF;
END
`$`$;
"@

    $bootstrap | & $PsqlPath -h $PgHost -p $Port -U $SuperUser -d postgres -v ON_ERROR_STOP=1 -q -f -
    if ($LASTEXITCODE -ne 0) { throw "Failed to create the application role." }

    $exists = & $PsqlPath -h $PgHost -p $Port -U $SuperUser -d postgres -tAc `
        "SELECT 1 FROM pg_database WHERE datname = '$Database'"

    if ($exists -ne '1') {
        & $PsqlPath -h $PgHost -p $Port -U $SuperUser -d postgres -v ON_ERROR_STOP=1 -q `
            -c "CREATE DATABASE $Database OWNER $AppRole ENCODING 'UTF8'"
        if ($LASTEXITCODE -ne 0) { throw "Failed to create database $Database." }
        Write-Host "      created database $Database"
    } else {
        Write-Host "      database $Database already exists"
    }

    # --- extension + schema ownership --------------------------------------
    Write-Host "[2/5] preparing schema..." -ForegroundColor Yellow

    & $PsqlPath -h $PgHost -p $Port -U $SuperUser -d $Database -v ON_ERROR_STOP=1 -q `
        -c "CREATE EXTENSION IF NOT EXISTS pgcrypto" `
        -c "CREATE SCHEMA IF NOT EXISTS game AUTHORIZATION $AppRole" `
        -c "GRANT ALL ON SCHEMA game TO $AppRole"
    if ($LASTEXITCODE -ne 0) { throw "Failed to prepare the game schema." }

    # --- migrations ---------------------------------------------------------
    Write-Host "[3/5] applying EF Core migrations..." -ForegroundColor Yellow

    $connection = "Host=$PgHost;Port=$Port;Database=$Database;Username=$AppRole;Password=$appPlain"
    $env:GONGWEI_DESIGN_CONNECTION = $connection

    Push-Location $repoRoot
    try {
        dotnet ef database update `
            --project src/GongWei.Infrastructure `
            --startup-project src/GongWei.Infrastructure
        if ($LASTEXITCODE -ne 0) { throw "dotnet ef database update failed." }
    } finally {
        Pop-Location
    }

    # --- reference data -----------------------------------------------------
    Write-Host "[4/5] seeding reference data..." -ForegroundColor Yellow

    # seed_rules is idempotent and only touches mutable master data (README_v1.1 §5).
    $env:PGPASSWORD = $appPlain
    & $PsqlPath -h $PgHost -p $Port -U $AppRole -d $Database -v ON_ERROR_STOP=1 -q `
        -f (Join-Path $repoRoot 'db/authoritative/v1.1/seed_rules_v1.1.sql')
    if ($LASTEXITCODE -ne 0) { throw "Seeding rule data failed." }

    # seed_npcs is listed as step 7 of the v1.1 delivery package but was not shipped with
    # it. Run it here once it arrives; it must only insert missing NPC codes.
    $npcSeed = Join-Path $repoRoot 'db/authoritative/v1.1/seed_npcs_v1.1.sql'
    if (Test-Path $npcSeed) {
        & $PsqlPath -h $PgHost -p $Port -U $AppRole -d $Database -v ON_ERROR_STOP=1 -q -f $npcSeed
        if ($LASTEXITCODE -ne 0) { throw "Seeding NPC data failed." }
    } else {
        Write-Warning "seed_npcs_v1.1.sql not found — NPC content will be empty until it is supplied."
    }

    # --- store the connection string in user-secrets ------------------------
    Write-Host "[5/5] writing the connection string to user-secrets..." -ForegroundColor Yellow

    Push-Location $repoRoot
    try {
        foreach ($project in @(
            'src/GongWei.Api',
            'src/GongWei.Admin',
            'src/GongWei.Worker'
        )) {
            dotnet user-secrets --project $project set 'ConnectionStrings:GongWei' $connection | Out-Null
            Write-Host "      $project"
        }
    } finally {
        Pop-Location
    }

    Write-Host "`nDone." -ForegroundColor Green
    Write-Host "The connection string lives in user-secrets only — it is not in the repo."
    Write-Host "Next: dotnet run --project src/GongWei.Api  then GET /health/ready"
}
finally {
    # Never leave credentials in the session environment.
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:GONGWEI_DESIGN_CONNECTION -ErrorAction SilentlyContinue
    $superPlain = $null
    $appPlain = $null
    [GC]::Collect()
}
