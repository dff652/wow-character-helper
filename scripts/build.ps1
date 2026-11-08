param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrEmpty($OutputDirectory)) { $OutputDirectory = Join-Path $repoRoot 'dist' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler not found.' }
$source = @(Get-ChildItem (Join-Path $repoRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$binary = Join-Path $OutputDirectory 'RetryPvp.exe'
$manifest = Join-Path $repoRoot 'src\app.manifest'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/out:$binary" "/win32manifest:$manifest" /r:System.Drawing.dll /r:System.Windows.Forms.dll $source
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\app.config') -Destination ($binary + '.config')
foreach ($directory in @('templates','docs')) {
    $destination = Join-Path $OutputDirectory $directory
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem (Join-Path $repoRoot $directory) -File | Copy-Item -Destination $destination
}
if (Test-Path -LiteralPath (Join-Path $repoRoot 'README.md')) { Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $OutputDirectory }
Write-Output "Built $binary"
