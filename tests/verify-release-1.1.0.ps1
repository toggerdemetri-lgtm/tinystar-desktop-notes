param([string]$ReleaseDirectory='dist\1.1.0',[string]$ExpectedVersion='1.1.0.0')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$release=Join-Path $root $ReleaseDirectory
$app=Join-Path $release 'ZhuomianNotes.exe'
$setup=Join-Path $release 'TinyStar桌面便签安装.exe'
$hash=[System.Security.Cryptography.SHA256]::Create()
try {
    $appHash=(Get-FileHash -LiteralPath $app).Hash
    $assembly=[System.Reflection.Assembly]::LoadFile($setup)
    $payload=$assembly.GetManifestResourceStream('App.exe')
    if (-not $payload) { throw '安装包缺少应用资源' }
    try { $payloadHash=[BitConverter]::ToString($hash.ComputeHash($payload)).Replace('-','') } finally { $payload.Dispose() }
    if ($appHash -ne $payloadHash) { throw '安装包应用与便携程序不一致' }
    'PASS: installer contains exact application payload'
    $appAssembly=[System.Reflection.Assembly]::LoadFile($app)
    $icon=$appAssembly.GetManifestResourceStream('App.ico')
    if (-not $icon) { throw '应用缺少托盘图标资源' }
    try { $iconHash=[BitConverter]::ToString($hash.ComputeHash($icon)).Replace('-','') } finally { $icon.Dispose() }
    if ($iconHash -ne (Get-FileHash -LiteralPath (Join-Path $root 'assets\app.ico')).Hash) { throw '图标资源不一致' }
    'PASS: embedded quick-add artwork matches source icon'
    $shellStream=$appAssembly.GetManifestResourceStream('Shell.ico')
    if(-not $shellStream){throw 'Application window/taskbar shell icon resource is missing'}
    try {$shellHash=[BitConverter]::ToString($hash.ComputeHash($shellStream)).Replace('-','')}finally{$shellStream.Dispose()}
    if($shellHash -ne (Get-FileHash -LiteralPath (Join-Path $root 'assets\shell-icon-preview\app.ico')).Hash){throw 'Shell icon differs from approved dark icon'}
    'PASS: embedded window/taskbar icon matches approved dark icon'
    $installerIcon=$assembly.GetManifestResourceStream('Shell.ico')
    if(-not $installerIcon){throw 'Installer is missing shortcut icon payload'}
    try {$installerIconHash=[BitConverter]::ToString($hash.ComputeHash($installerIcon)).Replace('-','')}finally{$installerIcon.Dispose()}
    if($installerIconHash -ne $shellHash){throw 'Installer shortcut icon payload mismatch'}
    $temporaryLink=Join-Path $root 'tests\bin\icon-shortcut-check.lnk'
    try {
        $method=$assembly.GetType('Installer').GetMethod('Shortcut',[Reflection.BindingFlags]'Static,NonPublic')
        $null=$method.Invoke($null,[object[]]([string[]]@($temporaryLink,$app,'')))
        $link=(New-Object -ComObject WScript.Shell).CreateShortcut($temporaryLink)
        $expectedIcon='TinyStar-'+([Version]$ExpectedVersion).ToString(3)+'-'+$shellHash.Substring(0,8).ToLowerInvariant()+'.ico,0'
        if(-not $link.IconLocation.EndsWith($expectedIcon)){throw 'Shortcut does not reference the current artwork fingerprint; Windows may reuse old cached artwork'}
    }finally{if(Test-Path -LiteralPath $temporaryLink){Remove-Item -LiteralPath $temporaryLink -Force}}
    'PASS: shortcut references versioned dark icon and installer carries the exact icon'
    $reader=[System.IO.BinaryReader]::new([System.IO.File]::OpenRead((Join-Path $root 'assets\shell-icon-preview\app.ico')))
    try {
        if ($reader.ReadUInt16() -ne 0 -or $reader.ReadUInt16() -ne 1 -or $reader.ReadUInt16() -ne 9) { throw 'ICO 目录格式不正确' }
        $sizes=@()
        for ($index=0;$index -lt 9;$index++) {
            $width=[int]$reader.ReadByte();if ($width -eq 0) { $width=256 };$sizes+=$width
            $null=$reader.ReadBytes(7);$length=$reader.ReadUInt32();$offset=$reader.ReadUInt32()
            if ($offset + $length -gt $reader.BaseStream.Length) { throw 'ICO 帧数据不完整' }
        }
        if (($sizes -join ',') -ne '16,20,24,32,40,48,64,128,256') { throw '图标尺寸不完整' }
    } finally { $reader.Dispose() }
    'PASS: nine complete icon sizes (16 to 256)'
    Add-Type -AssemblyName System.Drawing
    foreach($file in @($app,$setup)) {
        $shellIcon=[System.Drawing.Icon]::ExtractAssociatedIcon($file)
        try {
            $bitmap=$shellIcon.ToBitmap()
            try {
                $dark=0
                for($y=0;$y -lt $bitmap.Height;$y++){for($x=0;$x -lt $bitmap.Width;$x++){$pixel=$bitmap.GetPixel($x,$y);if($pixel.A -gt 200 -and $pixel.R -lt 50 -and $pixel.G -lt 50 -and $pixel.B -lt 50){$dark++}}}
                if($dark -lt $bitmap.Width*$bitmap.Height*.45){throw 'Native shell icon does not have the approved dark background'}
            } finally {$bitmap.Dispose()}
        } finally {$shellIcon.Dispose()}
    }
    'PASS: application and installer use dark-background shell icons'
    foreach ($file in @($app,$setup)) {
        if ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($file).FileVersion -ne $ExpectedVersion) { throw '发布版本号不正确' }
    }
    "PASS: application and installer version $ExpectedVersion"
    foreach ($file in @($app,$setup)) {
        if ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($file).ProductName -ne 'TinyStar桌面便签') { throw '程序显示名称不正确' }
    }
    'PASS: application and installer product name TinyStar'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $release 'TinyStar桌面便签免安装版.zip'))
    try {
        $entry=$archive.GetEntry('ZhuomianNotes.exe')
        if (-not $entry -or -not $archive.GetEntry('使用说明.txt')) { throw '免安装包缺少文件' }
        $portableStream=$entry.Open()
        try { $portableHash=[BitConverter]::ToString($hash.ComputeHash($portableStream)).Replace('-','') } finally { $portableStream.Dispose() }
        if ($portableHash -ne $appHash) { throw '免安装包应用与安装包不一致' }
    } finally { $archive.Dispose() }
    'PASS: portable ZIP contains exact application and instructions'
    foreach ($name in @('ZhuomianNotes.exe','TinyStar桌面便签安装.exe','TinyStar桌面便签免安装版.zip')) {
        $lines=@([IO.File]::ReadAllLines((Join-Path $release 'SHA256SUMS.txt')) | Where-Object { $_.EndsWith('  '+$name) })
        if ($lines.Count -ne 1 -or $lines[0].Substring(0,64) -ne (Get-FileHash -LiteralPath (Join-Path $release $name)).Hash.ToLowerInvariant()) { throw '发布校验和不一致' }
    }
    'PASS: checksums match all three release files'
} finally { $hash.Dispose() }

