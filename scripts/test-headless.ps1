$ErrorActionPreference='Stop'
$appRoot=Split-Path $PSScriptRoot -Parent
$root=Join-Path ([IO.Path]::GetTempPath()) ('Open-Net-headless-'+[guid]::NewGuid().ToString('N'))
$run=Join-Path $root 'test-results/ci'
New-Item -ItemType Directory $run -Force|Out-Null
Set-Content (Join-Path $root 'portable.flag') 'Open Net portable'
$exe=Join-Path $root 'Open Net.exe';Copy-Item -LiteralPath (Join-Path $appRoot 'publish/Open Net 0.4.0-alpha.1 Test/Open Net.exe') -Destination $exe
$info=[Diagnostics.ProcessStartInfo]::new($exe);$info.UseShellExecute=$false;$info.CreateNoWindow=$true
foreach($arg in @('--test-mode','--headless-tests','--data-dir',$run)){$info.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($info)
if(-not $process.WaitForExit(45000)){throw 'Own headless checks timed out'}
$result=Get-Content (Join-Path $run 'headless-tests.json') -Raw|ConvertFrom-Json
$result.Results|Format-Table -AutoSize
if($process.ExitCode -ne 0 -or $result.Failed -ne 0){throw "Headless checks failed: $($result.Failed)/$($result.Total)"}
Write-Output "Headless safe checks: $($result.Total) passed; no network tools."
