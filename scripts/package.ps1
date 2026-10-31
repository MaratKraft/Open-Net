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
Open Net 0.4.0-alpha.1 — HB DNS Experiment / Windows x64
This is an independent experiment based on checked R5. Do not copy the original Open Net Data here.
On assigns the selected IPv4 DNS to one selected physical adapter. Off returns automatic IPv4 DNS.
Off does NOT restore earlier manual addresses. Emergency recovery is a separate explicit operation.
Active DoH is preserved where supported; a plaintext downgrade requires consent. IPv6 is unchanged.
The system DNS cache is cleared only by the separate cache action. Browser caches are unchanged.
Standard data: LocalAppData/Open Net HB Experiment. Portable data: adjacent Data/Modules.
No automatic startup registration is offered on first launch. Real network trials require separate approval.
Offline emergency recovery: adjacent recovery CMD and DNS_RECOVERY_RU.txt. Snapshots are machine-local.
No real DNS/DoH/cache, VPN or physical reboot was tested during development. Readback does not prove Gemini works.
No network components, .NET or Python installation are bundled/required to run the manager.
Original code: MIT, LICENSE.txt. HB AI attribution and both upstream license texts: Notices/ and About.

Это отдельный эксперимент на базе проверенной R5. Не копируйте сюда Data основной версии.
Включение назначает DNS IPv4 выбранному физическому адаптеру. Выключение возвращает автоматический DNS.
Прежние ручные адреса при выключении НЕ возвращаются. Для этого есть отдельное аварийное восстановление.
DoH сохраняется, если это поддержано; переход к незашифрованному DNS требует согласия. IPv6 не меняется.
Кэш очищается только отдельной кнопкой. Данные и кэш браузера не сбрасываются.
Standard: LocalAppData/Open Net HB Experiment. Portable: соседние Data/Modules.
Регистрация автозапуска при первом запуске не предлагается. Реальный тест требует отдельного согласования.
Автономное восстановление: соседний CMD и DNS_RECOVERY_RU.txt. Снимки остаются на исходной машине.
Настоящие DNS/DoH/кэш, VPN и перезагрузка при разработке не испытывались. Запись DNS не доказывает доступность Gemini.
Zapret и TG устанавливаются отдельно. Собственный код: MIT. Лицензии HB AI включены в Notices/ и EXE.

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
