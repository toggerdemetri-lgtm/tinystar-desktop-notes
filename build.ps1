param([switch]$Test,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Push-Location -LiteralPath $root
try {
$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fx 'csc.exe'
New-Item -ItemType Directory -Force -Path (Join-Path $root 'dist'),(Join-Path $root 'tests\bin') | Out-Null
$refs = @('System.dll','System.Core.dll','Microsoft.CSharp.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','System.Xaml.dll') | ForEach-Object { '/r:' + (Join-Path $fx $_) }
if ($Test) {
    & $csc /nologo /target:exe /utf8output /out:tests\bin\CoreTests.exe $refs src\Model.cs src\Storage.cs src\ScreenPlacement.cs src\WindowGeometry.cs src\ShortcutGesture.cs tests\CoreTests.cs
    if ($LASTEXITCODE -ne 0) { throw '测试编译失败' }
    & (Join-Path $root 'tests\bin\CoreTests.exe')
    if ($LASTEXITCODE -ne 0) { throw '测试失败' }
} else {
    & $csc /nologo /target:exe /utf8output /out:tests\bin\IconBuilder.exe $refs tools\IconBuilder.cs
    if ($LASTEXITCODE -ne 0) { throw '图标生成工具编译失败' }
    & (Join-Path $root 'tests\bin\IconBuilder.exe') (Join-Path $root 'assets')
    if ($LASTEXITCODE -ne 0) { throw '图标生成失败' }
    & (Join-Path $root 'tests\bin\IconBuilder.exe') (Join-Path $root 'assets\shell-icon-preview')
    if ($LASTEXITCODE -ne 0) { throw '深色图标生成失败' }
    $releaseVersion=([Version]([regex]::Match((Get-Content -LiteralPath 'src\AssemblyInfo.cs' -Raw),'AssemblyVersion\("([0-9.]+)"\)').Groups[1].Value)).ToString(3)
    $outputDir = if($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $root ('dist\'+$releaseVersion) }
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
    & $csc /nologo /target:winexe /optimize+ /utf8output /win32manifest:app.manifest /win32icon:assets\shell-icon-preview\app.ico /resource:assets\app.ico,App.ico /resource:assets\shell-icon-preview\app.ico,Shell.ico ('/out:'+(Join-Path $outputDir 'ZhuomianNotes.exe')) $refs $sources
    if ($LASTEXITCODE -ne 0) { throw '应用编译失败' }
    if (Test-Path installer\Installer.cs) {
        & $csc /nologo /target:winexe /optimize+ /utf8output /win32manifest:app.manifest /win32icon:assets\shell-icon-preview\app.ico ('/out:'+(Join-Path $outputDir 'TinyStar桌面便签安装.exe')) /r:System.dll /r:System.Core.dll /r:Microsoft.CSharp.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ('/resource:'+(Join-Path $outputDir 'ZhuomianNotes.exe')+',App.exe') /resource:assets\shell-icon-preview\app.ico,Shell.ico installer\Installer.cs src\DesktopLaunchContext.cs
        if ($LASTEXITCODE -ne 0) { throw '安装器编译失败' }
    }
    Copy-Item -LiteralPath (Join-Path $root ('docs\使用说明-'+$releaseVersion+'.txt')) -Destination (Join-Path $outputDir '使用说明.txt') -Force
    Copy-Item -LiteralPath (Join-Path $root 'assets\shell-icon-preview\app-icon.png') -Destination (Join-Path $outputDir '图标预览.png') -Force
    Compress-Archive -LiteralPath (Join-Path $outputDir 'ZhuomianNotes.exe'),(Join-Path $outputDir '使用说明.txt') -DestinationPath (Join-Path $outputDir 'TinyStar桌面便签免安装版.zip') -Force
    $releaseNames=@('ZhuomianNotes.exe','TinyStar桌面便签安装.exe','TinyStar桌面便签免安装版.zip')
    $releaseHashes=foreach($name in $releaseNames) { (Get-FileHash -LiteralPath (Join-Path $outputDir $name)).Hash.ToLowerInvariant()+'  '+$name }
    [IO.File]::WriteAllLines((Join-Path $outputDir 'SHA256SUMS.txt'),$releaseHashes,(New-Object Text.UTF8Encoding($false)))
}
} finally { Pop-Location }


