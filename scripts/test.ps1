param([string]$BuildDirectory = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrEmpty($BuildDirectory)) { $BuildDirectory = Join-Path $repoRoot 'dist' }
$exe = Join-Path $BuildDirectory 'RetryPvp.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build first with scripts/build.ps1.' }
$testRoot = Join-Path $repoRoot 'test-output'
foreach ($test in @(@('--config-test','config'),@('--layout-test','layout'),@('--task-ui-test','task-ui'),@('--launcher-test','launcher'),@('--startup-test','startup'))) {
    $destination = Join-Path $testRoot $test[1]
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $process = Start-Process -FilePath $exe -ArgumentList @($test[0],('"' + $destination + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Failed: $($test[0]); inspect $destination" }
    Write-Output "PASS: $($test[0])"
}
