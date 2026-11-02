<p align="center"><img src="../assets/OpenNet.png" width="80" alt="Open Net logo"></p>
<h1 align="center">Open Net</h1>
<p align="center">Открытое Windows-приложение для управления Zapret, TG WS Proxy и DNS Manager из одного компактного окна.</p>
<p align="center"><a href="../README.md">English</a> | <strong>Русский</strong></p>

<p align="center">
<img alt="Windows x64" src="https://img.shields.io/badge/Windows-x64-596579">
<img alt="v0.4.0 Stable" src="https://img.shields.io/badge/version-0.4.0%20Stable-3b6ca8">
<a href="../LICENSE"><img alt="MIT" src="https://img.shields.io/badge/license-MIT-596579"></a>
<a href="https://github.com/MaratKraft/Open-Net/actions/workflows/build.yml"><img alt="Build" src="https://github.com/MaratKraft/Open-Net/actions/workflows/build.yml/badge.svg?branch=main"></a>
</p>
<p align="center">
<a href="https://github.com/MaratKraft/Open-Net/releases/download/v0.4.0/Open-Net-0.4.0-Windows-x64.exe"><img alt="Download Open Net v0.4.0" src="https://img.shields.io/badge/Download_Open_Net-v0.4.0-3b6ca8?style=for-the-badge"></a>
<a href="https://github.com/MaratKraft/Open-Net/releases"><img alt="All Releases" src="https://img.shields.io/badge/All_Releases-596579?style=for-the-badge"></a>
</p>
<p align="center"><a href="https://github.com/MaratKraft/Open-Net/releases/download/v0.4.0/Open-Net-0.4.0-Windows-x64-Portable.zip">Portable ZIP</a> · <a href="https://github.com/MaratKraft/Open-Net/releases/download/v0.4.0/SHA256SUMS.txt">SHA-256</a></p>

<p align="center"><img src="../assets/screenshots/v0.4.0/presentation-dark.png" width="508" alt="Open Net — dark theme"></p>

## Быстрый старт

1. **[Скачайте Open Net](https://github.com/MaratKraft/Open-Net/releases).** Выберите Standard EXE на официальной странице Releases. Это готовая программа, не установщик.
2. **Откройте EXE.** Системные действия могут запросить права администратора: изучите запрос Windows и подтвердите при необходимости. Не отключайте защиту Windows.
3. **Установите инструменты.** Нажмите значок загрузки рядом с Zapret или TG WS Proxy. Open Net получает совместимые версии из официальных источников; инструменты не входят в комплект.
4. **Выберите настройки.** Откройте шестерёнку модуля. Для Zapret выберите поддерживаемую стратегию, например ALT13. DNS Manager настраивается отдельно.
5. **Включите нужное.** Используйте индивидуальный переключатель или назначенную главную кнопку. Запуск процесса не гарантирует доступность сайта: результат зависит от сети и стратегии.

## Возможности

- **Zapret и TG WS Proxy:** установка совместимых официальных версий, настройки и управление подтверждёнными экземплярами. Внешние экземпляры требуют проверки и подтверждения; неоднозначные процессы остаются без изменений.
- **DNS Manager:** профили и свои адреса IPv4 DNS для одного подключённого физического адаптера; отдельная очистка DNS-кэша и защищённое восстановление. IPv6 сохраняется.
- **Ваше рабочее пространство:** показ, скрытие и порядок модулей, индивидуальные переключатели и назначаемая главная кнопка группы или модуля.
- **Трей и автозапуск:** закрытие окна скрывает оболочку; выход через трей оставляет инструменты работающими. Автозапуск Windows и выбранных инструментов настраивается отдельно с разрешением Windows.
- **Интерфейс:** русский и английский, язык Windows при первом запуске и сохранение ручного выбора; светлая, тёмная и системная темы, Mica/Acrylic там, где поддерживаются.
- **Обновления:** автоматическое обнаружение новых выпусков и ручная проверка в «О программе и лицензии». «Открыть выпуск» ведёт на официальный GitHub; автоматическая замена EXE не реализована.

## Ещё один взгляд

<details>
<summary>Светлая тема</summary>
<p align="center"><img src="../assets/screenshots/v0.4.0/presentation-light.png" width="508" alt="Open Net — light theme"></p>
</details>

Реальные утверждённые кадры pre.3: финальная v0.4.0 сохраняет эту компоновку. Показано безопасное тестовое состояние, не результат проверки сети. Скругление, прозрачные поля и тень добавлены только при оформлении; интерфейс не перерисован.

## Standard или Portable

| | Standard EXE | Portable ZIP |
| --- | --- | --- |
| Запуск | Откройте скачанный EXE | Распакуйте всю папку и откройте `Open Net.exe` |
| Данные и модули | `%LOCALAPPDATA%\Open Net` | `Data` и `Modules` рядом с EXE |
| Перенос | Проверьте автозапуск после смены пути | Сохраняйте `portable.flag` и переносите всю папку после остановки инструментов и выхода |

Не запускайте EXE внутри ZIP. Нативные библиотеки runtime могут извлекаться во временную папку Windows.

## Обновление с v0.3.3

Выйдите из старой оболочки через трей, сохраните резервную копию данных и замените только EXE в прежнем месте, по возможности сохранив имя. Не удаляйте модули. Перед преобразованием старых настроек создаётся копия; после изменения пути явно исправьте автозапуск. [Подробности и откат](UPGRADE_0.3.3.md).

## Системные требования

Windows x64. .NET 10.0.12 включён; отдельно устанавливать .NET/Python не нужно. Для изменения DNS требуется Windows 10 build 19041 или новее; DoH зависит от API Windows 11. Поддержка ОС зависит также от жизненного цикла .NET и редакции Windows. Физическая Windows 10 и все сочетания DPI/мониторов не проверены. Для системных действий нужны соответствующие права и UAC.

## Ограничения и безопасность

- EXE не подписан Authenticode. Проверяйте официальный источник и SHA-256; сумма проверяет целостность, но не заменяет подпись издателя. Не отключайте защиту Windows.
- **DNS Off возвращает автоматический IPv4 DNS**, а не прежние ручные адреса. Защищённое восстановление исходника — отдельное действие на том же ПК. Неизвестное шифрование/политики блокируют изменения.
- VPN отсутствует; доступность сайтов не гарантируется. Для подтверждения внешнего TG нужен GitHub. Неоднозначные процессы/службы не останавливаются; общий драйвер автоматически не выгружается.
- Расход памяти WPF остаётся ограничением; цель 100–120 MB и отсутствие длительных утечек не подтверждены. Автоустановка обновлений и очистка старых модулей не включены.

[Проверки и ограничения выпуска](STABLE_040_AUDIT_RU.md) · [Восстановление DNS](DNS_RECOVERY_RU.txt) · [Сборка](BUILD.md)

## Благодарности и лицензия

Спасибо [Flowseal / Zapret](https://github.com/Flowseal/zapret-discord-youtube), [TG WS Proxy](https://github.com/Flowseal/tg-ws-proxy), [HB AI Smart DNS](https://github.com/neiroseti-ai/hb-ai-xbox-dns), [.NET](https://github.com/dotnet/runtime), [WPF](https://github.com/dotnet/wpf) и [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons).

Собственный код Open Net — [MIT](../LICENSE). У сторонних компонентов, кода HB AI и символики отдельные условия; сохранены необходимые авторство и лицензионные тексты. См. [Third-Party Notices](../THIRD_PARTY_NOTICES.md) и «О программе и лицензии». Open Net не заявляет аффилированность с исходными проектами. [Сообщить об ошибке](https://github.com/MaratKraft/Open-Net/issues) — удалите личные данные и секреты из отчёта.
