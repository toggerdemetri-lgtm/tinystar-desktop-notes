param([string]$ApplicationPath = 'dist\1.1.0\ZhuomianNotes.exe', [switch]$HiddenWindow, [switch]$DesktopMode, [switch]$EditingNote, [switch]$RenamingCategory, [switch]$DisposedSource, [switch]$KilledParentProcess)
$ErrorActionPreference='Stop'
$projectDir=Split-Path $PSScriptRoot -Parent
$applicationFullPath=[IO.Path]::GetFullPath((Join-Path $projectDir $ApplicationPath))
$probeDir=Join-Path $PSScriptRoot 'bin\window-recovery'
New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
Copy-Item -LiteralPath $applicationFullPath -Destination (Join-Path $probeDir 'ZhuomianNotes.exe') -Force
$frameworkDir=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$probeRefs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll') | ForEach-Object {'/r:'+(Join-Path $frameworkDir $_)}
& (Join-Path $frameworkDir 'csc.exe') /nologo /target:exe /utf8output ('/out:'+(Join-Path $probeDir 'WindowRecoveryProbe.exe')) ('/r:'+(Join-Path $probeDir 'ZhuomianNotes.exe')) $probeRefs (Join-Path $PSScriptRoot 'WindowRecoveryProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Window recovery probe compilation failed'}
$probeArgs=@();if($HiddenWindow){$probeArgs+='--hidden'}
if($DesktopMode){$probeArgs+='--desktop'}
if($EditingNote){$probeArgs+='--editing'}
if($RenamingCategory){$probeArgs+='--rename'}
if($DisposedSource){$probeArgs+='--source-disposed'}
if($KilledParentProcess){$probeArgs+='--kill-parent-process'}
& (Join-Path $probeDir 'WindowRecoveryProbe.exe') @probeArgs
if($LASTEXITCODE -ne 0){throw 'Window recovery regression failed'}
