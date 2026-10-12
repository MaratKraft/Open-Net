<p align="center"><img src="assets/OpenNet.png" width="112" alt="Open Net logo"></p>

<h1 align="center">Open Net</h1>
<p align="center">A simple Windows home for your network tools.</p>
<p align="center">
  <img alt="Windows 10 / 11 x64" src="https://img.shields.io/badge/Windows-10%20%2F%2011%20x64-0078D4">
  <img alt="0.3.1 Beta" src="https://img.shields.io/badge/version-0.3.1%20Beta-805AD5">
  <a href="LICENSE"><img alt="MIT — Open Net code" src="https://img.shields.io/badge/license-MIT-369B59"></a>
</p>

**English** | [Русский](docs/README_RU.md)

## Overview

Open Net is a free, open-source desktop interface for installing and managing **Zapret** and **TG WS Proxy**. It brings component status, settings and start/stop controls into one small window. The network tools remain independent upstream projects with their own behavior and licenses.

**0.3.1 is a beta.** Network results depend on your environment and upstream tools. Windows 10 is a target platform; testing on a real Windows 10 machine remains pending.

## Screenshots

Real Open Net 0.3.1 windows captured by its safe test harness. Components shown in the screenshots are test state and are **not bundled**. No network tools were started for these captures.

| Main window (Russian) | General settings (English) |
| --- | --- |
| <img src="assets/screenshots/main.png" width="330" alt="Open Net main window"> | <img src="assets/screenshots/settings.png" width="330" alt="General settings"> |

<details>
<summary>Zapret and TG WS Proxy settings</summary>

| Zapret | TG WS Proxy |
| --- | --- |
| <img src="assets/screenshots/zapret.png" width="330" alt="Zapret settings"> | <img src="assets/screenshots/tg-proxy.png" width="330" alt="TG WS Proxy settings"> |

</details>

## Download

**[Open Releases and download Open Net](https://github.com/MaratKraft/Open-Net/releases)** · [v0.3.1 Beta](https://github.com/MaratKraft/Open-Net/releases/tag/v0.3.1)

Choose one of two editions. **Standard is recommended for most users.** Both are for Windows x64 and contain the same application. Neither contains preinstalled Zapret or TG WS Proxy.

### Standard — download and run

1. Open **[Releases](https://github.com/MaratKraft/Open-Net/releases)** and choose the newest Open Net release, including Beta if appropriate.
2. Expand **Assets** if the file list is collapsed.
3. Download **[Open-Net-0.3.1-Windows-x64.exe](https://github.com/MaratKraft/Open-Net/releases/download/v0.3.1/Open-Net-0.3.1-Windows-x64.exe)** and the accompanying `LICENSE.txt`.
4. Run the EXE from a convenient folder. It is the application itself, not an installer.
5. Install the tools you need from inside Open Net, as described below.

Settings, logs and installed tools live under `%LOCALAPPDATA%\Open Net\`. Standard does not create `Data` or `Modules` beside its EXE unless you deliberately add a `portable.flag` marker.

### Portable — extract the whole folder

1. Open **[Releases](https://github.com/MaratKraft/Open-Net/releases)** and expand **Assets**.
2. Download **[Open-Net-0.3.1-Windows-x64-Portable.zip](https://github.com/MaratKraft/Open-Net/releases/download/v0.3.1/Open-Net-0.3.1-Windows-x64-Portable.zip)**.
3. Extract the ZIP to a writable folder. **Do not run the EXE directly inside the ZIP.**
4. Open the extracted `Open Net` folder and run `Open Net.exe`.
5. Install the tools you need through Open Net.
6. To transfer the installation, stop its tools, exit Open Net and move the **whole folder**, not just the EXE. Keep `portable.flag`.

Portable stores settings in `Data` and installed components in `Modules` inside its own folder. Empty directories in the ZIP are intentional. Native .NET libraries may still be extracted to Windows temporary storage.

GitHub also displays automatically generated **Source code** archives: these are for developers, not the ready-to-run Windows application. The Releases page includes beta releases; `/latest` may omit prereleases.

## Installation — add your network tools

No separate .NET or Python installation is required for the shell. **Zapret and TG WS Proxy are installed separately.** Their first download requires access to official GitHub sources or suitable previously downloaded official files.

1. Find **Zapret** or **TG WS Proxy** on the main screen.
2. Click **Install** beside a missing component. If another copy is already running, its status can say **External instance**; Open Net leaves that process alone.
3. Review the official release name, size and source in the confirmation dialog. Confirm file installation and wait for the download and extraction to finish. Installation does not start the tool.
4. Use the gear beside the component for settings. Select a supported Zapret strategy if needed; TG keeps its own interface and tray.
5. Select the tool and use **Start selected** when you want to run it. Zapret requires UAC approval.
6. If automatic download fails, obtain the appropriate file from [official Zapret releases](https://github.com/Flowseal/zapret-discord-youtube/releases) or [official TG WS Proxy releases](https://github.com/Flowseal/tg-ws-proxy/releases). Open the component's gear and choose **Import release**.

**Import has an integrity requirement:** Open Net must obtain official release metadata online, or already have a verified catalog saved from a previous successful check, and the imported file must match its official SHA-256. A manually downloaded file alone is insufficient on a fresh, completely offline installation. Open Net does not guarantee access to GitHub and does not use unofficial mirrors.

## Features

- Install official compatible components from GitHub and check for updates independently of Open Net.
- Select tools and start or stop processes managed by Open Net; external instances are left alone.
- Configure each module separately, import verified release files and roll back installed component versions.
- Choose normal or portable data storage.
- Keep Open Net in the system tray; optionally start the shell in the tray when signing in to Windows.
- Switch between English and Russian.
- Use Windows Mica/Acrylic materials where available, with a solid background fallback.

There is no built-in VPN or DNS manager in this release. See [validation and limitations](docs/VALIDATION.md) for what has actually been tested.

## Usage

1. Open the application and use **Install** for each desired component. Internet access to GitHub is required.
2. Open the gear beside a tool for its settings. For Zapret, select a supported strategy; ALT13 is preferred when present. Configure TG WS Proxy through its own native interface and tray.
3. Select the desired tools on the main screen, then choose **Start selected**. Confirm the action when prompted. Zapret also requires Windows UAC approval.
4. Stop tools through Open Net when you are finished. Open Net controls only processes it can verify as its own. TG may require confirmation before forced termination because unsaved settings could be lost.
5. Closing the window hides it in the tray by default. Use the tray's exit action to close Open Net. Exiting the shell does not automatically stop running network tools.

General settings contain language, appearance, close-to-tray, confirmation and Windows startup options. Windows startup is off by default and starts only Open Net, never the network tools. After moving a portable installation, repair its startup entry explicitly in settings.

## Updates

Use **Check for updates** beside a module or in its settings. Compatible official upstream releases can be installed without updating Open Net. Compatibility depends on the expected release asset and supported executable/strategy format; upstream format changes can require an Open Net adapter update. This is not a promise that every future version will work.

Release import requires a previously verified official catalog and matching SHA-256. An unknown download cannot be imported on a completely offline computer. Check for shell updates on the releases page and replace the shell only after stopping tools and exiting Open Net.

## Requirements

- Windows 10/11 **x64**, a writable user data folder or portable folder, and enough space for the application and selected components.
- Internet access to official GitHub releases for component discovery and installation.
- Administrator approval for Zapret operations involving WinDivert; the shell normally runs without elevation.
- A compatible upstream strategy and working network environment.

The existing development checks ran on Windows 11 build 26200. Real Windows 10, additional physical displays/DPI configurations, actual sign-in startup and live network/UAC behavior still need manual validation. Visual materials depend on Windows, transparency settings, power mode and DWM.

## Build from Source

On Windows x64, install **.NET SDK 10.0.100** and PowerShell 7. From the repository root:

```powershell
./scripts/publish.ps1
```

The output is `publish/Open Net 0.3.1/Open Net.exe`. The build pins runtime **10.0.12**, publishes a self-contained compressed single-file application and excludes test-only classes. See [BUILD.md](docs/BUILD.md) for dependency caches, safe tests, packaging and reproducibility limits.

## Troubleshooting and known limitations

**Windows warns about an unknown publisher.** This beta EXE is not Authenticode-signed. SmartScreen may warn because it cannot establish a trusted publisher/reputation. Check that you obtained it from `github.com/MaratKraft/Open-Net`, compare its SHA-256 with the release's `SHA256SUMS.txt`, and check the matching version and source tag. A matching hash establishes integrity, not a guarantee of safety. Do not disable SmartScreen, Defender or other protections; if the origin or warning is unclear, do not run the file.

```powershell
Get-FileHash .\Open-Net-0.3.1-Windows-x64.exe -Algorithm SHA256
Get-FileHash .\Open-Net-0.3.1-Windows-x64-Portable.zip -Algorithm SHA256
```

**GitHub cannot be reached.** Retry when connectivity is restored. **Import release** requires official metadata and a matching digest, as explained above; it does not bypass the initial trust check. Already installed components remain available.

**A tool says External instance.** It was started outside this copy of Open Net. Manage it through its own interface; Open Net does not take over unrelated processes.

Real Windows 10, physical monitor/DPI combinations and live network behavior remain subject to the [validation limits](docs/VALIDATION.md). There is no built-in VPN or DNS manager. For ordinary bugs, use [Issues](https://github.com/MaratKraft/Open-Net/issues), include your version and reproduction steps, and remove secrets from logs. Follow [SECURITY.md](SECURITY.md) for sensitive reports.

## Credits

- [Flowseal / zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube)
- [Flowseal / tg-ws-proxy](https://github.com/Flowseal/tg-ws-proxy)
- [bol-van / zapret](https://github.com/bol-van/zapret)
- [WinDivert](https://github.com/basil00/WinDivert) and [Cygwin](https://cygwin.com/), used by upstream bundles
- [.NET](https://github.com/dotnet/runtime), [WPF](https://github.com/dotnet/wpf), [Windows Forms](https://github.com/dotnet/winforms) and [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons)

Open Net is an independent project maintained by [MaratKraft](https://github.com/MaratKraft); it does not imply upstream endorsement.

## License

Original Open Net code and documentation: [MIT](LICENSE), copyright 2026 MaratKraft. The supplied Open Net logo is retained as project artwork; this release does not grant a separate logo/trademark license. Third-party code, runtime and icons keep their own licenses. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and `assets/Notices`. License texts for bundled dependencies are also available in **Settings → About and licenses**.

## Development workflow

GitHub is the canonical source. Development uses feature/fix branches, tests and pull requests; main merges and releases require the owner's approval. See [Development Workflow](CONTRIBUTING.md#development-workflow), [v0.3.2 validation](docs/VALIDATION_v0.3.2.md) and [build instructions](docs/BUILD.md). v0.3.2 artifacts are local/CI test builds until separately approved; the download links above still point to published v0.3.1.
