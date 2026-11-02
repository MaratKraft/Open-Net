param([Parameter(Mandatory)][string]$ExePath)
$ErrorActionPreference='Stop'
$source=(Get-Item -LiteralPath $ExePath).FullName
$destination=Join-Path ([IO.Path]::GetTempPath()) ('Open-Net-recovery-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $destination | Out-Null
$exe=Join-Path $destination 'Open Net.exe';Copy-Item -LiteralPath $source -Destination $exe
$before=@(Get-DnsClientServerAddress | Sort-Object InterfaceIndex,AddressFamily | Select-Object InterfaceIndex,AddressFamily,ServerAddresses) | ConvertTo-Json -Depth 5 -Compress
$plan=[Diagnostics.ProcessStartInfo]::new($exe);$plan.UseShellExecute=$false;$plan.ArgumentList.Add('--dns-recovery');$plan.ArgumentList.Add('--verify-only')
$process=[Diagnostics.Process]::Start($plan)
if(!$process.WaitForExit(15000)-or $process.ExitCode -ne 0){throw 'The read-only emergency window did not complete its probe'}
$probe=Get-Content -LiteralPath (Join-Path $destination 'test-results/recovery-probe.json') -Raw | ConvertFrom-Json
if(!$probe.Visible -or !$probe.VerifiedOnly -or $probe.NetworkRequired){throw 'Emergency window verification contract failed'}
$after=@(Get-DnsClientServerAddress | Sort-Object InterfaceIndex,AddressFamily | Select-Object InterfaceIndex,AddressFamily,ServerAddresses) | ConvertTo-Json -Depth 5 -Compress
if($before -ne $after){throw 'Observed DNS differed during read-only recovery verification'}
Write-Output 'Independent recovery window opens; observed DNS unchanged; no Restore action performed.'
Write-Output $destination
