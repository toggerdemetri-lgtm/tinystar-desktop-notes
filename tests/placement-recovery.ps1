param([string[]]$Scenarios=@('transient','move','resize','reset','normal','recovery','shutdown','loss','snap'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent;$target=Join-Path $PSScriptRoot 'bin\placement-recovery';New-Item -ItemType Directory -Path $target -Force | Out-Null
$seams=0
$sources=@(foreach($file in Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs'){
 $s=Get-Content -LiteralPath $file.FullName -Raw
 foreach($pair in @(@('Forms.Screen.AllScreens.Select(s=>s.WorkingArea).ToArray()','PlacementSimulation.Screens'),@('Forms.Screen.PrimaryScreen.WorkingArea','PlacementSimulation.Screens[0]'))){if($s.Contains($pair[0])){$seams++;$s=$s.Replace($pair[0],$pair[1])}}
 $copy=Join-Path $target $file.Name;Set-Content -LiteralPath $copy -Value $s -Encoding utf8;$copy
})
if($seams -lt 2){throw 'Expected production screen readings were not found'}
$fx=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319';$refs=@('System.dll','System.Core.dll','Microsoft.CSharp.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll') | ForEach-Object {'/r:'+(Join-Path $fx $_)}
$exe=Join-Path $target 'PlacementRecoveryTests.exe'
& (Join-Path $fx 'csc.exe') /nologo /utf8output /target:exe /main:PlacementRecoveryTests ('/out:'+$exe) ('/win32manifest:'+(Join-Path $root 'app.manifest')) ('/resource:'+(Join-Path $root 'assets\app.ico')+',App.ico') ('/resource:'+(Join-Path $root 'assets\shell-icon-preview\app.ico')+',Shell.ico') $refs $sources (Join-Path $PSScriptRoot 'PlacementRecoveryTests.cs')
if($LASTEXITCODE -ne 0){throw 'Placement regression compilation failed'}
foreach($scenario in $Scenarios){& $exe $scenario;if($LASTEXITCODE -ne 0){throw ('Placement regression failed: '+$scenario)}}


