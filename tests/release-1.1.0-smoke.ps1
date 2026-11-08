param([string]$ApplicationPath='dist\1.1.0\ZhuomianNotes.exe',[string]$ExpectedVersion='1.1.0.0')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$folder=Join-Path $PSScriptRoot 'bin\release110-smoke'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
Copy-Item -LiteralPath (Join-Path $root $ApplicationPath) -Destination (Join-Path $folder 'ZhuomianNotes.exe') -Force
$fx=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','System.Xaml.dll') | ForEach-Object { '/r:'+(Join-Path $fx $_) }
& (Join-Path $fx 'csc.exe') /nologo /utf8output /target:exe ('/out:'+(Join-Path $folder 'ReleaseSmoke.exe')) ('/r:'+(Join-Path $folder 'ZhuomianNotes.exe')) $refs (Join-Path $PSScriptRoot 'Release110Smoke.cs')
if($LASTEXITCODE -ne 0){throw 'Release smoke compile failed'}
& (Join-Path $folder 'ReleaseSmoke.exe') $ExpectedVersion
if($LASTEXITCODE -ne 0){throw 'Release smoke failed'}


