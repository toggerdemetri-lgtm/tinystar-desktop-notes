param([string]$ApplicationPath='dist\1.1.6\ZhuomianNotes.exe',[string]$ExpectedVersion='1.1.6.0')
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$exe=[IO.Path]::GetFullPath((Join-Path $project $ApplicationPath))
$relativeData='tests\bin\startup-read\upgrade-fixture\data'
$expectedFile=[IO.Path]::GetFullPath((Join-Path $project ($relativeData+'\notes.json')))
if(!(Test-Path -LiteralPath $expectedFile)){throw 'Independent latest-data fixture must be prepared first'}
$started=[DateTime]::UtcNow
$info=[Diagnostics.ProcessStartInfo]::new($exe,('--test-mode --data-dir "'+$relativeData+'"'))
$info.WorkingDirectory=$project;$info.UseShellExecute=$false
$original=[Diagnostics.Process]::Start($info)
if(!$original.WaitForExit(5000)){throw 'Initial launch did not hand off to the desktop shell'}
if($original.ExitCode -ne 0){throw 'Initial launch failed'}
$traceRoot=Join-Path $env:TEMP 'TinyStar-Startup';$render=$null;$traceFile=$null
for($attempt=0;$attempt -lt 60 -and !$render;$attempt++){
    foreach($file in @(Get-ChildItem -LiteralPath $traceRoot -Filter '*.jsonl' -File | Where-Object {$_.LastWriteTimeUtc -ge $started.AddSeconds(-1)})){
        foreach($record in @(Get-Content -LiteralPath $file.FullName | ForEach-Object {try{$_|ConvertFrom-Json}catch{}})){
            if($record.phase -eq 'first-render' -and $record.version -eq $ExpectedVersion -and $record.executable -eq $exe){$render=$record;$traceFile=$file.FullName;break}
        }
        if($render){break}
    }
    if(!$render){Start-Sleep -Milliseconds 100}
}
if(!$render){throw 'No first-render result from the actual application entry point'}
try {
    $render | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $project ('docs\verification-'+([Version]$ExpectedVersion).ToString(3)+'\actual-entry-first-render.json')) -Encoding UTF8
    if($render.dataFile -ne $expectedFile){throw ('Relative data-dir changed across desktop handoff: '+$render.dataFile)}
    'PASS: actual EXE preserves the explicitly requested relative independent data folder'
    if($render.modelNoteCount -ne 2 -or !$render.contentMatchesDisk){throw 'First real application window does not contain latest fixture notes'}
    'PASS: actual EXE handoff renders the latest independent saved data on its first opening'
    if($render.packageFamily -or $render.physicalDataFile -match '\\Packages\\'){throw 'Actual EXE still uses the calling package runtime'}
    'PASS: actual EXE first launch is outside the caller package filesystem view'
}finally{
    $child=Get-Process -Id $render.pid -ErrorAction SilentlyContinue
    if($child -and [string]::Equals($child.MainModule.FileName,$exe,[StringComparison]::OrdinalIgnoreCase)){
        if(!$child.CloseMainWindow() -or !$child.WaitForExit(5000)){throw 'Own independent test window did not close normally'}
    }
}

