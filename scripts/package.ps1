param([string]$OutputDirectory = '', [string]$ExePath = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts' }
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (!$ExePath) { $ExePath = Join-Path $root 'publish/Open Net 0.4.0-alpha.1/Open Net.exe' }
$exe = Get-Item -LiteralPath $ExePath
if ($exe.VersionInfo.FileVersion -ne '0.4.0.0') { throw 'Expected Open Net 0.4.0.0' }
$names = @('Open-Net-0.4.0-alpha.1-Windows-x64.exe','Open-Net-0.4.0-alpha.1-Windows-x64-Portable.zip','LICENSE.txt','SHA256SUMS.txt')
foreach ($name in $names) { if (Test-Path -LiteralPath (Join-Path $destination $name)) { throw "Output already exists: $name" } }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $exe.FullName -Destination (Join-Path $destination $names[0])
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $destination 'LICENSE.txt')
$zip = [IO.Compression.ZipFile]::Open((Join-Path $destination $names[1]), [IO.Compression.ZipArchiveMode]::Create)
try {
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$exe.FullName,'Open Net/Open Net.exe') | Out-Null
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root 'LICENSE'),'Open Net/LICENSE.txt') | Out-Null
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root 'docs/DNS_RECOVERY_RU.txt'),'Open Net/DNS_RECOVERY_RU.txt') | Out-Null
    foreach ($folder in @('Modules','Data','Data/Config','Data/Logs','Data/Cache','Data/Downloads','Data/Backups','Notices')) { [void]$zip.CreateEntry("Open Net/$folder/") }
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'assets/Notices') -File) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,('Open Net/Notices/'+$file.Name)) | Out-Null
    }
    $texts = @{
        'portable.flag' = 'Open Net portable storage marker'
        'Восстановление DNS.cmd' = "@echo off`r`nstart `"`" `"%~dp0Open Net.exe`" --dns-recovery`r`n"
        'README.txt' = @'
Open Net 0.4.0-alpha.1 Experimental / Windows x64
Experimental DNS Manager: Apply/Restore require explicit confirmation and UAC.
Real DNS/UAC behavior has not yet been validated on Windows 10/11.
Supported IPv4 adapter DoH is captured and restored. Unsupported encryption/policies and virtual adapters remain read-only.
Automatic DNS is separate from restoring the first session original. Cache clearing is optional.
Offline recovery: use the adjacent recovery CMD; see DNS_RECOVERY_RU.txt.
Recovery records remain on the original machine even when the portable folder is moved.
Extract the entire Open Net folder into a writable location. Keep portable.flag.
Data and Modules stay beside the EXE. Stop tools and exit before moving the folder.
Zapret and TG WS Proxy are not bundled: install official components in the app.
Recommended startup switches are preselected on a fresh install. First launch asks for approval and UAC to register startup; preferences alone do not start tools. No separate .NET/Python installation is needed.
Native .NET libraries can be extracted to Windows temporary storage.
Zapret needs UAC approval; TG WS Proxy has its own native UI and tray.
Exiting Open Net does not automatically stop network tools.
Reboot, settings, automatic tools and tray were confirmed by the owner on their PC. Physical Windows 10 and multi-monitor/DPI remain unverified.
Original code: MIT, see LICENSE.txt. Third-party notices: Notices/ and About in the app.
Documentation: https://github.com/MaratKraft/Open-Net

Распакуйте всю папку Open Net в доступное для записи место. Сохраните portable.flag.
Экспериментальный DNS Manager: применение и восстановление требуют подтверждения и UAC.
Настоящее изменение DNS/UAC на Windows 10/11 ещё не проверено.
Поддержанные параметры DoH IPv4 сохраняются и восстанавливаются; неизвестные режимы, политики и виртуальные адаптеры доступны только для просмотра.
Автоматический DNS — отдельное действие; восстановление возвращает первый исходник сессии. Очистка кэша необязательна.
Автономное восстановление: соседний CMD и инструкция DNS_RECOVERY_RU.txt.
Резервная запись восстановления остаётся на исходной машине при переносе Portable.
Data и Modules находятся рядом с EXE. Перед переносом остановите инструменты и выйдите.
Zapret и TG WS Proxy устанавливаются отдельно через приложение. В новой установке опции автозапуска выбраны заранее; регистрация требует согласия при первом запуске и UAC.
Устанавливать .NET/Python отдельно не нужно. Нативные библиотеки .NET могут извлекаться во временную папку Windows.
Zapret требует UAC; TG WS Proxy сохраняет собственный интерфейс и трей.
Выход из Open Net не останавливает сетевые инструменты автоматически.
Перезагрузка, настройки, автозапуск инструментов и трей подтверждены владельцем на его ПК. Физическая Windows 10 и разные мониторы/DPI не проверены.
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
