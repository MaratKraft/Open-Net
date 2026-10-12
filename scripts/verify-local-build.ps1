param([string]$ExePath,[string]$PortableZip,[string]$OutputDirectory,[switch]$SkipNormal)
$ErrorActionPreference='Stop'
if(!$ExePath -or !$PortableZip -or !$OutputDirectory){throw 'Provide EXE, portable ZIP and verification output paths'}
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$root=Join-Path ([IO.Path]::GetTempPath()) ('Open-Net-package-check-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $root|Out-Null
$results=[Collections.Generic.List[object]]::new()
function Probe([string]$exe,[string]$data,[string]$case,[bool]$hidden){$info=[Diagnostics.ProcessStartInfo]::new($exe);$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WorkingDirectory=$env:WINDIR;$info.ArgumentList.Add('--startup-probe');if($hidden){$info.ArgumentList.Add('--autostart')};$started=[DateTime]::UtcNow;$p=[Diagnostics.Process]::Start($info);if(-not $p.WaitForExit(15000)-or $p.ExitCode -ne 0){throw "Probe failed: $case"};$file=Join-Path $data 'Logs/startup-probe.json';if(-not (Test-Path $file)-or (Get-Item $file).LastWriteTimeUtc -lt $started.AddMilliseconds(-100)){throw "No fresh own probe: $case (another running normal instance may have activated instead)"};$result=Get-Content $file -Raw|ConvertFrom-Json;if($result.LaunchCount -ne 0 -or $result.Visible -eq $hidden -or !$result.Tray){throw "Startup contract mismatch: $case"};$results.Add(@{Case=$case;Passed=$true;Probe=$result})}
if(!$SkipNormal){$normal=Join-Path $root 'normal';New-Item -ItemType Directory $normal|Out-Null;Copy-Item $ExePath (Join-Path $normal 'Open Net.exe');Probe (Join-Path $normal 'Open Net.exe') (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Open Net') 'Normal EXE outside repository' $false}
$zip=[IO.Compression.ZipFile]::OpenRead($PortableZip);try{$exeEntries=@($zip.Entries|Where-Object {$_.FullName -match '\.exe$'});if($exeEntries.Count -ne 1){throw 'Portable must contain only one EXE'};if(@($zip.Entries|Where-Object {$_.FullName -match '\.(dll|sys|bat|pdb|log|json)$'}).Count -ne 0){throw 'Unwanted payload in portable'};$entries=@($zip.Entries|ForEach-Object {$_.FullName})}finally{$zip.Dispose()}
Expand-Archive -LiteralPath $PortableZip -DestinationPath (Join-Path $root 'extracted')
$portable=Join-Path $root 'extracted/Open Net';$exe=Join-Path $portable 'Open Net.exe';if((Get-FileHash $exe).Hash -ne (Get-FileHash $ExePath).Hash){throw 'Normal and portable EXE mismatch'}
Probe $exe (Join-Path $portable 'Data') 'Extracted portable ZIP' $false
Probe $exe (Join-Path $portable 'Data') 'Autostart entrypoint without registry registration' $true
$move=Join-Path $root 'extracted/Перенос с пробелами';$source=[IO.Path]::GetFullPath($portable);$destination=[IO.Path]::GetFullPath($move);$boundary=[IO.Path]::GetFullPath($root)+'\';if(!$source.StartsWith($boundary)-or !$destination.StartsWith($boundary)){throw 'Move escaped own verification root'};Move-Item -LiteralPath $source -Destination $destination
Probe (Join-Path $move 'Open Net.exe') (Join-Path $move 'Data') 'Moved portable' $false
@{Results=$results;NormalPortableIdentical=$true;NormalSkipped=[bool]$SkipNormal;PortableEntries=$entries;EXESHA256=(Get-FileHash $ExePath).Hash;PortableSHA256=(Get-FileHash $PortableZip).Hash;Root=$root}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $OutputDirectory 'package-smoke.json') -Encoding utf8
$results|ForEach-Object {"$($_.Case): $($_.Passed)"}


