param([string]$MakeAppx)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$identity=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'identity.json') -Raw | ConvertFrom-Json
$version=[Version]$identity.Version
if($identity.Version -notmatch '^\d+\.\d+\.\d+\.0$' -or $version.Major -lt 1 -or $version.Major -gt 65535 -or $version.Minor -gt 65535 -or $version.Build -gt 65535){throw 'Store version must be Major.Minor.Build.0, with Major >= 1 and components <= 65535.'}
$applicationVersion=[regex]::Match((Get-Content -LiteralPath (Join-Path $root 'src\AssemblyInfo.cs') -Raw),'AssemblyVersion\("([0-9.]+)"\)').Groups[1].Value
if($applicationVersion -ne $identity.Version){throw 'Store identity version and application assembly version must match.'}
$release=Join-Path $root ('dist\store-'+$version.ToString(3))
$staging=Join-Path $release 'package'
$assets=Join-Path $staging 'Assets'
New-Item -ItemType Directory -Force -Path $assets,(Join-Path $root 'tests\bin') | Out-Null
if(-not $MakeAppx){$MakeAppx=Join-Path $root 'tools\vendor\sdk-buildtools-10.0.26100.7705\bin\10.0.26100.0\x64\makeappx.exe'}
if(-not(Test-Path -LiteralPath $MakeAppx)){throw 'Install official Microsoft SDK BuildTools in tools/vendor or pass -MakeAppx.'}
if((Get-AuthenticodeSignature -LiteralPath $MakeAppx).Status -ne 'Valid'){throw 'MakeAppx digital signature is not valid.'}
$fx=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('System.dll','System.Core.dll','Microsoft.CSharp.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','System.Xaml.dll') | ForEach-Object {'/r:'+(Join-Path $fx $_)}
$refs+=@('System.Runtime','System.Threading.Tasks') | ForEach-Object {'/r:'+(Join-Path "$env:WINDIR\Microsoft.NET\assembly\GAC_MSIL\$_\v4.0_4.0.0.0__b03f5f7f11d50a3a" ($_+'.dll'))}
$refs+=@('Windows.ApplicationModel.winmd','Windows.Foundation.winmd','Windows.Storage.winmd') | ForEach-Object {'/r:'+(Join-Path "$env:WINDIR\System32\WinMetadata" $_)}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$sources+=Join-Path $PSScriptRoot 'StorePlatform.cs'
& (Join-Path $fx 'csc.exe') /nologo /target:winexe /platform:x64 /define:STORE_PACKAGE /optimize+ /utf8output ('/win32manifest:'+(Join-Path $root 'app.manifest')) ('/win32icon:'+(Join-Path $root 'assets\shell-icon-preview\app.ico')) ('/resource:'+(Join-Path $root 'assets\app.ico')+',App.ico') ('/resource:'+(Join-Path $root 'assets\shell-icon-preview\app.ico')+',Shell.ico') ('/out:'+(Join-Path $staging 'ZhuomianNotes.exe')) $refs $sources
if($LASTEXITCODE -ne 0){throw 'Store application compilation failed.'}
& (Join-Path $fx 'csc.exe') /nologo /target:exe ('/out:'+(Join-Path $root 'tests\bin\IconBuilder.exe')) $refs (Join-Path $root 'tools\IconBuilder.cs')
if($LASTEXITCODE -ne 0){throw 'Icon builder compilation failed.'}
& (Join-Path $root 'tests\bin\IconBuilder.exe') $assets --store-assets
if($LASTEXITCODE -ne 0){throw 'Store artwork generation failed.'}
# Never copy the EXE installer, development tools, personal data or test data into this folder.
$manifest=@"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap uap5 rescap">
  <Identity Name="$($identity.IdentityName)" Publisher="$($identity.Publisher)" Version="$($identity.Version)" ProcessorArchitecture="x64" />
  <Properties><DisplayName>$($identity.DisplayName)</DisplayName><PublisherDisplayName>$($identity.PublisherDisplayName)</PublisherDisplayName><Logo>Assets\Logo50.png</Logo></Properties>
  <Resources><Resource Language="zh-CN" /></Resources>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
  <Applications>
    <Application Id="TinyStar" Executable="ZhuomianNotes.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="$($identity.DisplayName)" Description="桌面便签与待办事项管理" BackgroundColor="#191C21" Square150x150Logo="Assets\Logo150.png" Square44x44Logo="Assets\Logo44.png" />
      <Extensions><uap5:Extension Category="windows.startupTask" Executable="ZhuomianNotes.exe" EntryPoint="Windows.FullTrustApplication"><uap5:StartupTask TaskId="TinyStarStartup" Enabled="false" DisplayName="$($identity.DisplayName)" /></uap5:Extension></Extensions>
    </Application>
  </Applications>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
"@
[IO.File]::WriteAllText((Join-Path $staging 'AppxManifest.xml'),$manifest,[Text.UTF8Encoding]::new($false))
$config='<configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup></configuration>'
[IO.File]::WriteAllText((Join-Path $staging 'ZhuomianNotes.exe.config'),$config,[Text.UTF8Encoding]::new($false))
$package=Join-Path $release ('TinyStar_'+$identity.Version+'_x64.msix')
$allowed=@('AppxManifest.xml','ZhuomianNotes.exe','ZhuomianNotes.exe.config','Assets\Logo44.png','Assets\Logo50.png','Assets\Logo150.png','Assets\Logo310.png','Assets\Logo512.png')
$unexpected=@(Get-ChildItem -LiteralPath $staging -File -Recurse | Where-Object {$_.FullName.Substring($staging.Length+1) -notin $allowed})
if($unexpected.Count -gt 0){throw ('Unexpected staging files; refusing to package: '+($unexpected.Name -join ', '))}
& $MakeAppx pack /d $staging /p $package /o
if($LASTEXITCODE -ne 0){throw 'MSIX manifest validation/packaging failed.'}
(Get-FileHash -LiteralPath $package).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($package) | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Built UNSIGNED Store candidate: $package"
Write-Output 'Not yet installed, runtime-verified, uploaded, submitted or certified.'
