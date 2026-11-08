param([string]$ApplicationPath,[switch]$DesktopMode,[switch]$Fractional,[switch]$Idle,[switch]$DetachCaret,[switch]$Blink)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$probeDir=Join-Path $PSScriptRoot 'bin\caret-rendering'
New-Item -ItemType Directory -Force -Path $probeDir | Out-Null
$fx=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('System.dll','System.Core.dll','Microsoft.CSharp.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','System.Xaml.dll') | ForEach-Object {'/r:'+(Join-Path $fx $_)}
if($ApplicationPath){Copy-Item -LiteralPath ([IO.Path]::GetFullPath((Join-Path $project $ApplicationPath))) -Destination (Join-Path $probeDir 'ZhuomianNotes.exe') -Force;$sources=@();$refs+=('/r:'+(Join-Path $probeDir 'ZhuomianNotes.exe'))}else{$sources=@(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' | ForEach-Object FullName)}
& (Join-Path $fx 'csc.exe') /nologo /utf8output /target:exe /main:CaretRenderingProbe ('/out:'+(Join-Path $probeDir 'CaretRenderingProbe.exe')) ('/win32manifest:'+(Join-Path $project 'app.manifest')) ('/resource:'+(Join-Path $project 'assets\app.ico')+',App.ico') ('/resource:'+(Join-Path $project 'assets\shell-icon-preview\app.ico')+',Shell.ico') $refs $sources (Join-Path $PSScriptRoot 'CaretRenderingProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Caret probe compile failed'}
$probeArgs=@();if($DesktopMode){$probeArgs+='--desktop'}
if($Blink){$probeArgs+='--blink'};if($DetachCaret){$probeArgs+='--detach-caret'};if($Fractional){$probeArgs+='--fractional'};if($Idle){$probeArgs+='--idle'}
& (Join-Path $probeDir 'CaretRenderingProbe.exe') @probeArgs
if($LASTEXITCODE -ne 0){throw 'Caret rendering regression failed'}





