# Open Net v0.3.3

Windows startup, automatic selected tools and a more compact Settings window.

## Download

- **[Standard EXE](https://github.com/MaratKraft/Open-Net/releases/download/v0.3.3/Open-Net-0.3.3-Windows-x64.exe)** — standalone application; data in LocalAppData.
- **[Portable ZIP](https://github.com/MaratKraft/Open-Net/releases/download/v0.3.3/Open-Net-0.3.3-Windows-x64-Portable.zip)** — extract the whole folder; data stays beside the EXE.
- [SHA-256](https://github.com/MaratKraft/Open-Net/releases/download/v0.3.3/SHA256SUMS.txt)

## What's New

- Start Open Net after Windows sign-in, optionally in the tray.
- Separately approve automatic startup of installed, selected Zapret/TG tools.
- Compact startup controls, clearer registration status and pinned Import/Save actions.
- Rounded scrollbars keep their minimum thumb size in long logs and license text.

## Fixes

Windows-normalized task XML is read correctly, saved startup choices survive failed registration, and explicit repair restores this installation's startup entry. The owner confirmed the R8 reboot/startup, settings, selected tools, tray and interface on their PC.

## Requirements and Notes

Windows 10/11 x64; physical Windows 10 and multi-monitor/DPI setups remain unverified. No separate .NET/Python installation is needed. The EXE is unsigned.

Zapret and TG WS Proxy are **not bundled**; install them separately through Open Net. Fresh installations preselect startup preferences, but registration requires explicit approval and UAC. Automatic tools use a visible per-user scheduled task; Open Net and its tools run with administrator privileges. No password is stored. Existing settings are preserved.

For Portable, keep `portable.flag` and move the entire folder after stopping tools and exiting. Disable startup before moving, then save/re-register it at the new location.

[English README](https://github.com/MaratKraft/Open-Net/blob/v0.3.3/README.md) · [Русский](https://github.com/MaratKraft/Open-Net/blob/v0.3.3/docs/README_RU.md) · [Validation](https://github.com/MaratKraft/Open-Net/blob/v0.3.3/docs/VALIDATION_v0.3.3_RELEASE.md)
