param([Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run this read-only diagnostic with the normal Windows administrator approval.'}
$processes=@(Get-CimInstance Win32_Process -Filter "name='winws.exe' OR name='TgWsProxy_windows.exe'" | ForEach-Object {
 $path=$_.ExecutablePath
 [pscustomobject]@{Pid=$_.ProcessId;ParentPid=$_.ParentProcessId;Created=$_.CreationDate;Path=$path;SHA256=if($path -and (Test-Path -LiteralPath $path)){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}else{$null}}
})
$drivers=@(Get-CimInstance Win32_SystemDriver | Where-Object {$_.Name -like '*WinDivert*'} | Select-Object Name,State,PathName)
$pids=@($processes.Pid)
$services=@(Get-CimInstance Win32_Service | Where-Object {$_.ProcessId -ne 0 -and $_.ProcessId -in $pids} | Select-Object Name,State,ProcessId,PathName)
$result=@{ReadOnly=$true;CapturedUtc=[DateTime]::UtcNow;Processes=$processes;Drivers=$drivers;Services=$services;DriverHandleOwners='Not determined by SCM; running state does not prove which processes hold WinDivert handles.'}
$target=[IO.Path]::GetFullPath($OutputPath)
if(Test-Path -LiteralPath $target){throw 'Choose a new output file; existing reports are preserved.'}
$result|ConvertTo-Json -Depth 6|Out-File -LiteralPath $target -Encoding utf8 -NoClobber
