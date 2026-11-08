param([string]$ApplicationPath='dist\1.1.4\ZhuomianNotes.exe')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$target=Join-Path $PSScriptRoot 'bin\startup-read'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -LiteralPath (Join-Path $root $ApplicationPath) -Destination (Join-Path $target 'ZhuomianNotes.exe') -Force
$fx=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('System.dll','System.Core.dll','Microsoft.CSharp.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object {'/r:'+(Join-Path $fx $_)}
& (Join-Path $fx 'csc.exe') /nologo /utf8output /target:exe /main:StartupReadProbe ('/out:'+(Join-Path $target 'StartupReadProbe.exe')) ('/r:'+(Join-Path $target 'ZhuomianNotes.exe')) $refs (Join-Path $PSScriptRoot 'StartupReadProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Startup probe compilation failed'}
& (Join-Path $target 'StartupReadProbe.exe')
if($LASTEXITCODE -ne 0){throw 'Startup read probe failed'}
