param([switch]$InstallFiles,[string]$SavedCatalogDirectory="",[string]$FixtureDirectory="")
$ErrorActionPreference='Stop'
$appRoot=Split-Path $PSScriptRoot -Parent
$root=Join-Path $env:TEMP ('Open-Net-tests-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$run=Join-Path $root 'test-results/ui'
New-Item -ItemType Directory -Path $run -Force|Out-Null
if($SavedCatalogDirectory -ne ''){
 $cache=Join-Path $run 'Cache';New-Item -ItemType Directory -Path $cache -Force|Out-Null
 foreach($id in @('zapret','tg')){
  $source=Join-Path $SavedCatalogDirectory ('live-'+$id+'-asset.json')
  $asset=Get-Content -LiteralPath $source -Raw|ConvertFrom-Json
  @{RetrievedUtc=(Get-Item -LiteralPath $source).LastWriteTimeUtc.ToString('o');Asset=$asset}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $cache ('releases-'+$id+'.json')) -Encoding utf8
 }
}
$fixtureTarget=Join-Path $root 'test-results/Fixtures';New-Item -ItemType Directory -Path $fixtureTarget -Force|Out-Null
if($FixtureDirectory -eq ''){throw 'Specify FixtureDirectory with separately obtained official Zapret 1.10.3 BAT fixtures.'}
if(-not(Test-Path -LiteralPath (Join-Path $FixtureDirectory 'general.bat'))){throw 'Provide FixtureDirectory with official private 1.10.3 reference BATs; these are deliberately not bundled in the release EXE.'}
foreach($fixtureName in @('general.bat','general (ALT13).bat','service.bat')){Copy-Item -LiteralPath (Join-Path $FixtureDirectory $fixtureName) -Destination (Join-Path $fixtureTarget $(if($fixtureName -eq 'general (ALT13).bat'){'ALT13.bat'}else{$fixtureName}))}
Set-Content -LiteralPath (Join-Path $root 'portable.flag') -Value 'Open Net portable' -Encoding utf8
$exe=Join-Path $root 'Open Net.exe'
Copy-Item -LiteralPath (Join-Path $appRoot 'publish/Open Net 0.4.0-alpha.1 Test/Open Net.exe') -Destination $exe
$info=[Diagnostics.ProcessStartInfo]::new($exe);$info.UseShellExecute=$false;$info.WorkingDirectory=$env:WINDIR;$info.CreateNoWindow=$true
foreach($arg in @('--test-mode','--self-test','--data-dir',$run)){$info.ArgumentList.Add($arg)}
if($InstallFiles){$info.ArgumentList.Add('--installer-test')}
$process=[Diagnostics.Process]::Start($info)
while(-not $process.WaitForExit(30000)){Write-Output ('Own safe tests running, PID '+$process.Id)}
$result=Get-Content -LiteralPath (Join-Path $run 'ui-tests.json') -Raw|ConvertFrom-Json
$result.Results|Format-Table Name,Passed,Error -AutoSize
Write-Output "Total $($result.Total), failed $($result.Failed); $run"
if($process.ExitCode -ne 0 -or $result.Failed -ne 0){throw 'Safe tests failed'}



