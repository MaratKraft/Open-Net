# Open Net 0.4.0 Pre-release 1 — draft

This is a pre-release for testing, not the stable version. No release or tag has been published as part of preparation.

## English

- DNS Manager configures IPv4 DNS on a selected adapter. Turning it off selects automatic IPv4 DNS; it does not restore earlier manual addresses. Emergency snapshots are separate.
- The workspace supports individual module controls, module order and visibility. Hiding a module does not stop it.
- Open Net checks for compatible releases and can open the official GitHub release page. Automatic installation is not available.
- Standard uses `%LOCALAPPDATA%\Open Net`; Portable keeps `Data` and `Modules` beside the EXE. Zapret and TG WS Proxy are installed separately.

To try the Standard pre-release from v0.3.3, back up your data, exit the old Open Net shell from the tray, replace the EXE at the same path and launch the new one. Existing settings are backed up before conversion. Verify startup registration if the EXE path changes. Running external tools are not forcibly stopped or adopted without verification.

Validation uses owned stubs, simulated DNS and WPF tests on Windows. Real DNS/UAC, real logon and Windows 10 are not certified by those tests. Automatic self-update and old-version cleanup are not included. See [third-party notices](../THIRD_PARTY_NOTICES.md) and [DNS emergency recovery](DNS_RECOVERY_RU.txt).

## Русский

- DNS Manager настраивает IPv4 DNS выбранного адаптера. При выключении включается автоматический IPv4 DNS; прежние ручные адреса автоматически не возвращаются. Аварийные снимки существуют отдельно.
- В рабочей области доступны отдельные переключатели, порядок и видимость модулей. Скрытие не останавливает модуль.
- Open Net проверяет доступность совместимых выпусков и открывает официальную страницу GitHub. Автоматическая установка обновления отсутствует.
- Standard использует `%LOCALAPPDATA%\Open Net`, Portable хранит `Data` и `Modules` рядом с EXE. Zapret и TG WS Proxy устанавливаются отдельно.

Для тестирования Standard поверх v0.3.3 сохраните копию данных, выйдите из старой оболочки через трей, замените EXE по тому же пути и запустите новую версию. Перед преобразованием исходные настройки резервируются. При изменении пути EXE проверьте запись автозапуска. Внешние процессы не завершаются и не принимаются под управление без проверки.

Автоматические проверки выполняются на заглушках и имитации DNS в Windows. Они не подтверждают работу настоящего DNS/UAC, входа Windows или Windows 10. Автоматическое самообновление и очистка старых версий не включены. См. [лицензии и уведомления](../THIRD_PARTY_NOTICES.md) и [аварийное восстановление DNS](DNS_RECOVERY_RU.txt).
