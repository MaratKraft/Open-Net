# Open Net v0.4.0

Open Net 0.4.0 adds a configurable modular workspace and a compact DNS Manager while preserving the approved dark/light interface.

## Highlights

- A configurable workspace for Zapret, TG WS Proxy and DNS Manager.
- Improved verified process control and ALT13 startup.
- Official release discovery and preserved v0.3.3 settings.

## What's New

- Show, hide and reorder modules independently of running state, group control and Windows startup.
- Assign the main button to a group or a module; quick profiles appear alongside status.
- Install compatible official Zapret/TG releases. ALT13 launch selection and asynchronous controls are corrected. Verified TG parent/child processes are treated as one instance; external instances use the existing switch with confirmation.
- Configure IPv4 DNS on one physical adapter, use custom DNS, inspect hosts conflicts and clear DNS cache separately. Off returns automatic IPv4 DNS. Protected original settings remain available through explicit offline recovery.
- Discover newer official releases with Stable as default and optional Pre-release. Open release opens GitHub; automatic self-installation is not included.
- Keep v0.3.3 settings and installed modules in the existing official data directory, with a backup before legacy settings conversion. The first-run language follows Windows UI language (Russian or English); saved choices take precedence.

## Improvements & Fixes

The owner reports successful pre.3 tests on the main PC and several previously affected laptops, including YouTube, TG, external instances and switches. Stable adds narrow audit fixes for damaged update caches and asynchronous/bounded helper waits, plus official metadata and documentation; it therefore receives a fresh isolated test pass.

## Known Limitations

 no Authenticode publisher signature; checksums provide integrity only. Physical Windows 10 and every OS/DPI/network combination have not been certified. Recovery is machine-local; unusual encrypted DNS/policies and ambiguous/service-managed processes are refused. After shell restart, surviving tools may require external-instance confirmation. External TG validation needs GitHub. Memory usage is not reduced to a promised 100–120 MB. Automatic application installation and old-version cleanup remain deferred. Network tools are not bundled.

Internal pre.1/pre.2/pre.3 candidates were not public Releases. v0.3.3 remains unchanged. This prepared file does not itself publish v0.4.0.
## Download / Скачать

- [Standard EXE](https://github.com/MaratKraft/Open-Net/releases/download/v0.4.0/Open-Net-0.4.0-Windows-x64.exe) — data / данные: `%LOCALAPPDATA%\Open Net`.
- [Portable ZIP](https://github.com/MaratKraft/Open-Net/releases/download/v0.4.0/Open-Net-0.4.0-Windows-x64-Portable.zip) — extract the whole folder / распакуйте всю папку; keep `portable.flag`, `Data`, `Modules`.
- [SHA-256](https://github.com/MaratKraft/Open-Net/releases/download/v0.4.0/SHA256SUMS.txt)

## Upgrading from v0.3.3

Exit via the tray, back up data and replace only the EXE at the same path/name. Keep installed modules. Repair startup explicitly if the path changes. [Upgrade and rollback](https://github.com/MaratKraft/Open-Net/blob/v0.4.0/docs/UPGRADE_0.3.3.md).

## Third-Party Credits

Thanks to Flowseal, HB AI Smart DNS, .NET/WPF and Fluent contributors. [Full notices, including HB AI copyright and both license texts](https://github.com/MaratKraft/Open-Net/blob/v0.4.0/THIRD_PARTY_NOTICES.md). Original Open Net code is MIT.

---

# Open Net v0.4.0

Open Net 0.4.0 добавляет настраиваемую модульную рабочую область и компактный DNS Manager, сохраняя утверждённый интерфейс и темы.

- Видимость и порядок модулей настраиваются независимо от работы, участия в группе и автозапуска.
- Главная кнопка управляет выбранной группой или модулем; быстрый профиль расположен в строке состояния.
- Установка совместимых официальных Zapret/TG. Исправлены выбор ALT13 и асинхронное управление. Родительский/дочерний TG определяются как один экземпляр; внешний экземпляр управляется тем же переключателем после подтверждения.
- DNS IPv4 одного физического адаптера, свои адреса, проверка конфликтов hosts и отдельная очистка DNS-кэша. Выключение возвращает автоматический IPv4 DNS. Исходная конфигурация сохраняется для явного автономного восстановления.
- Проверка новых официальных выпусков: Stable по умолчанию, дополнительный Pre-release. Открыть выпуск ведёт на GitHub; автоматической установки оболочки пока нет.
- Сохранение данных v0.3.3 в прежней официальной папке, резервная копия до преобразования старых настроек. При первом запуске выбирается русский для русской Windows, иначе английский; сохранённый выбор важнее системного языка.

Владелец подтвердил успешные проверки pre.3 на основном ПК и нескольких ранее проблемных ноутбуках: YouTube, TG, внешние экземпляры и переключатели. После этого аудит добавил узкие исправления повреждённого кэша обновлений и ожиданий помощников, официальные метаданные и документацию; стабильная сборка проверяется повторно изолированно.

## Известные ограничения

 Authenticode-подписи издателя нет; SHA-256 проверяет только целостность. Физическая Windows 10 и все сочетания DPI/ОС/сетей не сертифицированы. Восстановление DNS привязано к компьютеру; неподдерживаемое шифрование/политики и неоднозначные процессы/службы не изменяются. После перезапуска оболочки оставшиеся инструменты могут требовать подтверждения внешнего экземпляра. Для внешнего TG нужен GitHub. Расход памяти не снижен до обещанных 100–120 MB. Автоустановка Open Net и очистка старых версий отложены. Сетевые инструменты в комплект не входят.

Внутренние pre.1/pre.2/pre.3 не публиковались как Releases. v0.3.3 сохранена. Подготовка этого файла сама по себе не публикует v0.4.0.
## Download / Скачать

- [Standard EXE](https://github.com/MaratKraft/Open-Net/releases/download/v0.4.0/Open-Net-0.4.0-Windows-x64.exe) — data / данные: `%LOCALAPPDATA%\Open Net`.
- [Portable ZIP](https://github.com/MaratKraft/Open-Net/releases/download/v0.4.0/Open-Net-0.4.0-Windows-x64-Portable.zip) — extract the whole folder / распакуйте всю папку; keep `portable.flag`, `Data`, `Modules`.
- [SHA-256](https://github.com/MaratKraft/Open-Net/releases/download/v0.4.0/SHA256SUMS.txt)

## Обновление с v0.3.3

Выйдите через трей, сохраните резервную копию данных и замените только EXE в прежнем месте, сохранив имя. Модули остаются на месте. После изменения пути явно исправьте автозапуск. [Обновление и откат](https://github.com/MaratKraft/Open-Net/blob/v0.4.0/docs/UPGRADE_0.3.3.md).

## Благодарности

Flowseal, HB AI Smart DNS, .NET/WPF и Fluent. [Все уведомления, включая авторство HB AI и оба лицензионных текста](https://github.com/MaratKraft/Open-Net/blob/v0.4.0/THIRD_PARTY_NOTICES.md). Собственный код Open Net — MIT.

[Full Changelog / Все изменения](https://github.com/MaratKraft/Open-Net/compare/v0.3.3...v0.4.0)
