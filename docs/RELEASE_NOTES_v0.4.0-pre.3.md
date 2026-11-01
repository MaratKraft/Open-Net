# Open Net v0.4.0-pre.3 — personal-test candidate

## Русский

- Исправлен устаревший отложенный выбор стратегии Zapret, способный заменить выбранную ALT13 при следующем запуске.
- Запуск, чтение процессов и подготовка вынесены из UI-потока; ожидание Windows и текущий модуль видны во время операции. В журнал добавлены измерения времени.
- Убрана постоянно работающая повышенная копия Open Net для перенаправления вывода Zapret. Ограничены наследуемые дескрипторы; управление процессами и общий WinDivert не переписаны.
- Ожидание завершения подтверждённого дерева ограничено общим бюджетом.
- Внедрены утверждённые точечные исправления шестерёнок. Остальной дизайн сохранён.

Это сборка для личной проверки на проблемных ноутбуках. Одинаковые планы двух запусков ALT13 подтверждены на Windows 11, доступность YouTube через браузер без VPN не подтверждена. Цель 100–120 MB не достигнута. Windows 10 и чистая установка на втором ПК требуют проверки. Автоматическая установка обновлений и очистка старых версий не включены. Подробности: PRE3_VALIDATION_RU.md. Не опубликовано как GitHub Release.

## English

- Fix a stale pending Zapret strategy overriding a newly selected ALT13 on the next launch.
- Move process inspection, preparation and UAC launch off the UI thread; show the current module and Windows-permission wait, and record operation timings.
- Remove the resident elevated Open Net output-pump process. The child inherits only explicit log/input handles. Existing process identity and shared WinDivert protections remain.
- Use one bounded completion budget for the approved process tree.
- Apply the two approved gear corrections, preserving the rest of the interface.

Personal testing on affected laptops remains necessary. Two ALT13 launch plans matched on Windows 11; browser/YouTube behavior without VPN is unverified. The 100–120 MB goal is not met. Windows 10 and a second-machine clean install are not tested. Automatic self-installation and old-version cleanup remain excluded. No merge, tag or public release is performed.
