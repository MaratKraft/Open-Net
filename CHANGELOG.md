# Changelog

## v0.3.2 — Unreleased

- Compact Zapret/TG rows with version/strategy metadata, full tooltips and separate install/settings/select icon actions.
- Equal module-action hit areas and spacing; centered Download/Refresh and selection icons without changing main dimensions or row heights.
- Follow-up: center the gear between the visible edges of the smaller Download/Refresh glyph and larger selection circle, preserving the fixed hit areas.
- Shared Windows-style toggle geometry with balanced insets, stable focus, hover/pressed/disabled states and a contrasting off-state thumb in the light theme.
- The unchanged header logo opens the official GitHub repository in the default browser, with RU/EN tooltips, keyboard activation and a nonfatal failure message; the remaining title area still drags the window.
- File-validated Download/Refresh presentation and small progress indicator; installation failures preserve the previous module and restore retry controls.
- Shared System/Light/Dark palettes for all owned windows, dialogs, controls and tray menus; independent Mica/Acrylic/solid material selection.
- Published v0.3.1 settings retain dark appearance until a new theme is chosen; new installs default to System, reacting to Windows app-theme changes.
- Only the module list scrolls; main dimensions and existing network/storage/tray/autostart contracts are preserved.
- Permanent Git/PR workflow documentation and safe headless CI checks. No future placeholder modules, release publication or main merge.


## 0.3.1 Beta — 2026-10-12 — first public release

- A Windows interface for managing Zapret and TG WS Proxy, with English and Russian localization.
- Separate general and module settings; independent component installation, update checks, release import and rollback.
- Normal storage under LocalAppData and portable storage next to the executable.
- System tray integration, optional shell-only startup with Windows, and multi-size application icons.
- Clean portable distribution with .NET included and network tools installed separately.

This is the first public release. Earlier versions were development milestones. See [release notes](docs/RELEASE_NOTES_v0.3.1.md) and [validation limits](docs/VALIDATION.md).
