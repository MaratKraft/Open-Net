param([string]$OutputDirectory = '', [string]$ExePath = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts' }
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (!$ExePath) { $ExePath = Join-Path $root 'publish/Open Net 0.4.0/Open Net.exe' }
$exe = Get-Item -LiteralPath $ExePath
if ($exe.VersionInfo.FileVersion -ne '0.4.0.0' -or $exe.VersionInfo.ProductVersion -notmatch '^0\.4\.0(?:\+[0-9a-f]+)?$') { throw 'Expected stable Open Net 0.4.0 metadata' }
$documents = @{
 'LICENSE.txt'='LICENSE';'README_EN.md'='docs/README_DOWNLOAD_EN.md';'README_RU.md'='docs/README_DOWNLOAD_RU.md';
 'RELEASE_NOTES_EN.md'='docs/RELEASE_NOTES_v0.4.0_EN.md';'RELEASE_NOTES_RU.md'='docs/RELEASE_NOTES_v0.4.0_RU.md';
 'UPGRADE_0.3.3.md'='docs/UPGRADE_0.3.3.md';'DNS_RECOVERY_RU.txt'='docs/DNS_RECOVERY_RU.txt'
}
$names = @('Open-Net-0.4.0-Windows-x64.exe','Open-Net-0.4.0-Windows-x64-Portable.zip','SHA256SUMS.txt','THIRD_PARTY_NOTICES.md','Notices','DNS-Recovery.cmd') + @($documents.Keys)
foreach ($name in $names) { if (Test-Path -LiteralPath (Join-Path $destination $name)) { throw "Output already exists: $name" } }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $exe.FullName -Destination (Join-Path $destination $names[0])
foreach($name in $documents.Keys){Copy-Item -LiteralPath (Join-Path $root $documents[$name]) -Destination (Join-Path $destination $name)}
$notices=[IO.File]::ReadAllText((Join-Path $root 'THIRD_PARTY_NOTICES.md')).Replace('assets/Notices/','Notices/')
[IO.File]::WriteAllText((Join-Path $destination 'THIRD_PARTY_NOTICES.md'),$notices,[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $root 'assets/Notices') -Destination (Join-Path $destination 'Notices') -Recurse
[IO.File]::WriteAllText((Join-Path $destination 'DNS-Recovery.cmd'),"@echo off`r`nstart `"`" `"%~dp0Open-Net-0.4.0-Windows-x64.exe`" --dns-recovery`r`n",[Text.UTF8Encoding]::new($false))
$zip = [IO.Compression.ZipFile]::Open((Join-Path $destination $names[1]), [IO.Compression.ZipArchiveMode]::Create)
try {
 [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$exe.FullName,'Open Net/Open Net.exe') | Out-Null
 foreach($name in @($documents.Keys)+@('THIRD_PARTY_NOTICES.md')){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $destination $name),('Open Net/'+$name)) | Out-Null}
 foreach ($folder in @('Modules','Data','Data/Config','Data/Logs','Data/Cache','Data/Downloads','Data/Backups','Notices')) { [void]$zip.CreateEntry("Open Net/$folder/") }
 foreach ($file in Get-ChildItem -LiteralPath (Join-Path $destination 'Notices') -File) { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,('Open Net/Notices/'+$file.Name)) | Out-Null }
 $texts = @{'portable.flag'='Open Net portable storage marker';'Восстановление DNS.cmd'="@echo off`r`nstart `"`" `"%~dp0Open Net.exe`" --dns-recovery`r`n"}
 foreach ($name in $texts.Keys) {$writer=[IO.StreamWriter]::new($zip.CreateEntry("Open Net/$name").Open(),[Text.UTF8Encoding]::new($false));try{$writer.Write($texts[$name])}finally{$writer.Dispose()}}
} finally { $zip.Dispose() }
$lines=Get-ChildItem -LiteralPath $destination -File -Recurse | Where-Object Name -ne SHA256SUMS.txt | Sort-Object FullName | ForEach-Object {(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetRelativePath($destination,$_.FullName).Replace('\','/')}
[IO.File]::WriteAllLines((Join-Path $destination 'SHA256SUMS.txt'),$lines,[Text.UTF8Encoding]::new($false))
Get-ChildItem -LiteralPath $destination -File | Select-Object Name,Length