param([ValidateRange(1,150)][int]$Rounds=30,[ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Label='current')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$target=Join-Path $PSScriptRoot 'bin\window-resources'
New-Item -ItemType Directory -Force -Path $target,(Join-Path $root 'docs\verification-window-inventory') | Out-Null
$fx=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('System.dll','System.Core.dll','Microsoft.CSharp.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll') | ForEach-Object {'/r:'+(Join-Path $fx $_)}
$exe=Join-Path $target 'WindowResourceProbe.exe'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
& (Join-Path $fx 'csc.exe') /nologo /utf8output /target:winexe /main:WindowResourceProbe ('/out:'+$exe) ('/win32manifest:'+(Join-Path $root 'app.manifest')) $refs $sources (Join-Path $PSScriptRoot 'WindowResourceProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Resource regression compilation failed'}
$log=Join-Path $root ('docs\verification-window-inventory\'+$Label+'.log')
if(Test-Path -LiteralPath ($log+'.exit')){Remove-Item -LiteralPath ($log+'.exit')}
& $exe $Rounds $log
if($LASTEXITCODE -ne 0){throw 'Desktop test handoff failed'}
$deadline=[Diagnostics.Stopwatch]::StartNew()
while(!(Test-Path -LiteralPath ($log+'.exit')) -and $deadline.ElapsedMilliseconds -lt 45000){Start-Sleep -Milliseconds 100}
if(!(Test-Path -LiteralPath ($log+'.exit'))){throw 'Desktop resource regression did not finish'}
Get-Content -LiteralPath $log
if((Get-Content -LiteralPath ($log+'.exit') -Raw).Trim() -ne '0'){throw 'Resource regression failed'}
