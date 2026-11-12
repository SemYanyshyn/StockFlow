param(
    [string]$PostgreSqlBin = 'C:\Program Files\PostgreSQL\16\bin',
    [switch]$SkipWindowsChecks
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$clusterRoot = Join-Path ([IO.Path]::GetTempPath()) ('stockflow-check-' + [guid]::NewGuid().ToString('N'))
$dataRoot = Join-Path $clusterRoot 'data'
$logRoot = Join-Path $repositoryRoot 'TestResults/isolated-checks'
New-Item -ItemType Directory -Path $clusterRoot, $logRoot -Force | Out-Null
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$started = $false
$previousTestConnection = $env:STOCKFLOW_TEST_CONNECTION
function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}
function Invoke-Sql([string]$Database, [string]$File) {
    Invoke-Checked (Join-Path $PostgreSqlBin 'psql.exe') @('-w','-h','127.0.0.1','-p',"$port",'-U','stockflow_check','-d',$Database,'-v','ON_ERROR_STOP=1','-f',$File)
}
function Invoke-Statement([string]$Database, [string]$Sql) {
    Invoke-Checked (Join-Path $PostgreSqlBin 'psql.exe') @('-w','-h','127.0.0.1','-p',"$port",'-U','stockflow_check','-d',$Database,'-v','ON_ERROR_STOP=1','-c',$Sql)
}
Push-Location $repositoryRoot
try {
    Invoke-Checked (Join-Path $PostgreSqlBin 'initdb.exe') @('-D',$dataRoot,'--auth=trust','--encoding=UTF8','--locale=C','-U','stockflow_check')
    Invoke-Checked (Join-Path $PostgreSqlBin 'pg_ctl.exe') @('-D',$dataRoot,'-l',(Join-Path $clusterRoot 'server.log'),'-o',"-h 127.0.0.1 -p $port",'-w','start')
    $started = $true
    foreach ($database in @('stockflow_isolated_services','stockflow_isolated_ui','stockflow_isolated_legacy')) {
        Invoke-Statement 'postgres' "CREATE DATABASE $database;"
        Invoke-Sql $database 'database/01_schema.sql'
        Invoke-Sql $database 'database/02_seed.sql'
    }
    $env:STOCKFLOW_TEST_CONNECTION = "Host=127.0.0.1;Port=$port;Database=stockflow_isolated_services;Username=stockflow_check;Timeout=3;Command Timeout=15"
    Invoke-Checked 'dotnet' @('run','--project','tests/StockFlow.DatabaseChecks/StockFlow.DatabaseChecks.csproj','-c','Release')
    if (!$SkipWindowsChecks) {
        $env:STOCKFLOW_TEST_CONNECTION = "Host=127.0.0.1;Port=$port;Database=stockflow_isolated_ui;Username=stockflow_check;Timeout=3;Command Timeout=15"
        Invoke-Checked 'dotnet' @('run','--project','tests/StockFlow.WindowsChecks/StockFlow.WindowsChecks.csproj','-c','Release')
    }
    Invoke-Sql 'stockflow_isolated_legacy' 'tests/StockFlow.DatabaseChecks/legacy-fixture.sql'
    Invoke-Sql 'stockflow_isolated_legacy' 'database/03_update_demo_passwords.sql'
    Invoke-Sql 'stockflow_isolated_legacy' 'database/03_update_demo_passwords.sql'
    # Capture an expected migration failure without native stderr becoming a PowerShell exception.
    $psql = Join-Path $PostgreSqlBin 'psql.exe'
    $conflictLog = Join-Path $logRoot 'expected-category-conflict.log'
    $arguments = @('-w','-h','127.0.0.1','-p',"$port",'-U','stockflow_check','-d','stockflow_isolated_legacy','-v','ON_ERROR_STOP=1','-f','database/04_categories_case_insensitive.sql')
    $conflict = Start-Process -FilePath $psql -ArgumentList $arguments -WindowStyle Hidden -WorkingDirectory $repositoryRoot -Wait -PassThru -RedirectStandardOutput (Join-Path $logRoot 'category-migration.log') -RedirectStandardError $conflictLog
    if ($conflict.ExitCode -eq 0) { throw 'Conflicting category migration unexpectedly succeeded.' }
    if (!(Select-String -LiteralPath $conflictLog -Pattern 'Category names conflict' -Quiet)) { throw 'Migration failed for an unexpected reason.' }
    Invoke-Sql 'stockflow_isolated_legacy' 'tests/StockFlow.DatabaseChecks/legacy-assert-and-resolve.sql'
    Invoke-Sql 'stockflow_isolated_legacy' 'database/04_categories_case_insensitive.sql'
    Invoke-Sql 'stockflow_isolated_legacy' 'database/04_categories_case_insensitive.sql'
    Write-Output 'PASS legacy migrations: placeholders only, IDs/links/history preserved, conflicts reported without deletion, rerun succeeds after explicit rename.'
    Copy-Item -LiteralPath (Join-Path $clusterRoot 'server.log') -Destination (Join-Path $logRoot 'server.log')
    Write-Output 'All isolated checks passed.'
} finally {
    $env:STOCKFLOW_TEST_CONNECTION = $previousTestConnection
    if ($started) { & (Join-Path $PostgreSqlBin 'pg_ctl.exe') -D $dataRoot -m fast -w stop }
    Pop-Location
    Write-Output "Stopped isolated cluster retained for inspection: $clusterRoot"
}
