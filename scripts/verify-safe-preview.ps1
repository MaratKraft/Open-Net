param([Parameter(Mandatory)][string]$ExePath)
$ErrorActionPreference='Stop'
$root=Join-Path $env:TEMP ('Open-Net-preview-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$exe=Join-Path $root 'Open Net Safe Preview.exe'
Copy-Item -LiteralPath $ExePath -Destination $exe
function DnsFingerprint {
    $state=@([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Sort-Object Id | ForEach-Object {
        @{Id=$_.Id;Dns=@($_.GetIPProperties().DnsAddresses | ForEach-Object ToString)}
    }) | ConvertTo-Json -Depth 5 -Compress
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($state)))
}
function RunPreview([string[]]$Arguments) {
    $start=[Diagnostics.ProcessStartInfo]::new($exe);$start.UseShellExecute=$false;$start.WorkingDirectory=$env:WINDIR
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($start)
    if(!$process.WaitForExit(30000)){throw 'Owned preview did not exit in time'}
    $process.ExitCode
}
$before=DnsFingerprint
if((RunPreview @('--startup-probe')) -ne 0){throw 'Direct preview startup failed'}
$probe=Get-Content -LiteralPath (Join-Path $root 'test-results/preview/Logs/startup-probe.json') -Raw | ConvertFrom-Json
if(!$probe.TestMode){throw 'Direct EXE failed simulation mode'}
if($probe.LaunchCount -ne 0){throw 'Direct EXE launched a tool'}
# Hosted Windows can expose TEMP as RUNNER~1 while ProcessPath contains the long name.
# Compare existing directories after expanding 8.3 aliases and normalizing separators.
Add-Type -Namespace OpenNet.Checks -Name PreviewPaths -MemberDefinition '[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode, SetLastError=true)] public static extern uint GetLongPathName(string path, System.Text.StringBuilder buffer, uint length);'
function LongDirectory([string]$Path) {
    $buffer=[Text.StringBuilder]::new(32768)
    $length=[OpenNet.Checks.PreviewPaths]::GetLongPathName([IO.Path]::GetFullPath($Path),$buffer,32768)
    if(!$length -or $length -ge 32768){throw 'Could not resolve preview directory identity'}
    $buffer.ToString().TrimEnd('\','/')
}
if(![StringComparer]::OrdinalIgnoreCase.Equals((LongDirectory $probe.Data),(LongDirectory (Join-Path $root 'test-results/preview')))){throw 'Direct EXE failed preview directory isolation'}
foreach($entry in @('--dns-elevated','--elevated','--autostart','--self-test','--installer-test')) {
    if((RunPreview @($entry)) -ne 2){throw "Preview did not reject $entry"}
}
if((DnsFingerprint) -ne $before){throw 'Observed system DNS changed during the preview check'}
Write-Output 'Direct preview uses isolated simulation; privileged/test entry points rejected; observed system DNS unchanged.'
Write-Output $root
