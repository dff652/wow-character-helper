$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $repo 'dist\diagnostics\CharacterVision.exe'
$p=Start-Process $exe -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode -ne 0){throw 'Diagnostic assertions failed; inspect dist/diagnostics/data/diagnostics/last-error.txt'}
$unsafe=Get-ChildItem (Join-Path $repo 'src\Diagnostics') -Filter '*.cs' | Select-String -Pattern 'SendInput|SetCursorPos|mouse_event|SetForegroundWindow|WriteProcessMemory'
if($unsafe){throw 'Unexpected control API in read-only diagnostics'}
Write-Output 'PASS: empty page, option vs selection, new names, dialog precedence; no game input APIs.'
