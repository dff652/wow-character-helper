param([string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(!$OutputDirectory){$OutputDirectory=Join-Path $repo 'dist\diagnostics'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /codepage:65001 /optimize+ ('/out:'+(Join-Path $OutputDirectory 'CharacterVision.exe')) /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll (Join-Path $repo 'src\Diagnostics\*.cs')
if($LASTEXITCODE -ne 0){throw 'Diagnostics build failed'}
Copy-Item (Join-Path $PSScriptRoot 'recognize-image.ps1') $OutputDirectory -Force
Copy-Item (Join-Path $repo 'src\app.config') (Join-Path $OutputDirectory 'CharacterVision.exe.config') -Force
Write-Output ('Built '+(Join-Path $OutputDirectory 'CharacterVision.exe'))
