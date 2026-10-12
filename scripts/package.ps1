param([string]$OutputDirectory = '', [string]$ExePath = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts' }
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (!$ExePath) { $ExePath = Join-Path $root 'publish/Open Net 0.3.2/Open Net.exe' }
$exe = Get-Item -LiteralPath $ExePath
if ($exe.VersionInfo.FileVersion -ne '0.3.2.0') { throw 'Expected Open Net 0.3.2.0' }
$names = @('Open-Net-0.3.2-Windows-x64.exe','Open-Net-0.3.2-Windows-x64-Portable.zip','LICENSE.txt','SHA256SUMS.txt')
foreach ($name in $names) { if (Test-Path -LiteralPath (Join-Path $destination $name)) { throw "Output already exists: $name" } }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $exe.FullName -Destination (Join-Path $destination $names[0])
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $destination 'LICENSE.txt')
$zip = [IO.Compression.ZipFile]::Open((Join-Path $destination $names[1]), [IO.Compression.ZipArchiveMode]::Create)
try {
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$exe.FullName,'Open Net/Open Net.exe') | Out-Null
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root 'LICENSE'),'Open Net/LICENSE.txt') | Out-Null
    foreach ($folder in @('Modules','Data','Data/Config','Data/Logs','Data/Cache','Data/Downloads','Data/Backups','Notices')) { [void]$zip.CreateEntry("Open Net/$folder/") }
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'assets/Notices') -File) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,('Open Net/Notices/'+$file.Name)) | Out-Null
    }
    $texts = @{
        'portable.flag' = 'Open Net portable storage marker'
        'README.txt' = @'
Open Net 0.3.2 Stable / Windows x64
Extract the entire Open Net folder into a writable location. Keep portable.flag.
Data and Modules stay beside the EXE. Stop tools and exit before moving the folder.
Zapret and TG WS Proxy are not bundled: install official components in the app.
No network tool starts automatically. No separate .NET/Python installation is needed.
Native .NET libraries can be extracted to Windows temporary storage.
Zapret needs UAC approval; TG WS Proxy has its own native UI and tray.
Exiting Open Net does not automatically stop network tools.
Real Windows 10, live network/UAC and physical DPI validation remain pending.
Original code: MIT, see LICENSE.txt. Third-party notices: Notices/ and About in the app.
Documentation: https://github.com/MaratKraft/Open-Net

Распакуйте всю папку Open Net в доступное для записи место. Сохраните portable.flag.
Data и Modules находятся рядом с EXE. Перед переносом остановите инструменты и выйдите.
Zapret и TG WS Proxy устанавливаются отдельно через приложение; автоматически не запускаются.
Устанавливать .NET/Python отдельно не нужно. Нативные библиотеки .NET могут извлекаться во временную папку Windows.
Zapret требует UAC; TG WS Proxy сохраняет собственный интерфейс и трей.
Выход из Open Net не останавливает сетевые инструменты автоматически.
Реальная Windows 10, сетевая работа/UAC и физические DPI-конфигурации ещё требуют проверки.
Собственный код: MIT, см. LICENSE.txt. Сторонние лицензии: Notices/ и раздел «О программе».
'@
    }
    foreach ($name in $texts.Keys) {
        $writer = [IO.StreamWriter]::new($zip.CreateEntry("Open Net/$name").Open(),[Text.UTF8Encoding]::new($false))
        try { $writer.Write($texts[$name]) } finally { $writer.Dispose() }
    }
} finally { $zip.Dispose() }
$lines = foreach ($name in $names[0..2]) { (Get-FileHash -LiteralPath (Join-Path $destination $name) -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$name }
[IO.File]::WriteAllLines((Join-Path $destination 'SHA256SUMS.txt'),$lines,[Text.UTF8Encoding]::new($false))
Get-ChildItem -LiteralPath $destination -File | Select-Object Name,Length
