# Open Net v0.3.1 Beta — First Public Release

Welcome to the first public beta of Open Net: a free Windows interface for managing Zapret and TG WS Proxy from one small window.

## Highlights

- Install compatible official components from GitHub and update them independently of the shell.
- Select tools, see their state and manage verified owned processes.
- Separate general, Zapret and TG settings; verified release import and component rollback.
- English and Russian interface, system tray support, optional shell-only startup with Windows, and Windows visual materials with fallback.
- Normal and portable storage options, with the .NET runtime included.

## Downloads

| Asset | Choose this when... |
| --- | --- |
| `Open-Net-0.3.1-Windows-x64.exe` | You want a standalone application EXE with data under `%LOCALAPPDATA%\Open Net`. It is not an installer. Download `LICENSE.txt` alongside it. |
| `Open-Net-0.3.1-Windows-x64-Portable.zip` | You want `Data` and `Modules` beside the EXE. Extract the whole folder and keep `portable.flag`. |
| `SHA256SUMS.txt` | You want to verify the downloaded assets with SHA-256. |
| `LICENSE.txt` | MIT terms for original Open Net code. Bundled dependencies retain their separate notices. |

The same application binary is used in both editions. The public Portable wrapper adds the Open Net MIT license and a revised usage note to the prior release candidate; the EXE is unchanged. Neither edition includes Zapret, TG WS Proxy, WinDivert or Cygwin binaries. Install network tools separately through the application. No separate .NET or Python installation is required.

## Requirements and behavior

Target: **Windows 10/11 x64**. Existing development validation ran on Windows 11 build 26200; real Windows 10 validation is still pending. GitHub access is needed for component discovery/downloads. Zapret requests UAC approval for elevated operations. TG WS Proxy keeps its own interface and tray. Confirmation before forced TG termination protects against losing unsaved settings.

Closing the main window hides it in the tray by default. Exiting Open Net does not automatically stop running network tools. Stop them explicitly before moving Portable or replacing files. Windows startup is disabled by default and, when enabled, starts only the shell.

## Beta limitations

- Live network effectiveness, real UAC/driver operations, physical multi-monitor DPI and startup after a real Windows sign-in still need manual validation. No universal provider compatibility is promised.
- Mica/Acrylic appearance depends on Windows settings and DWM. Windows 10 uses a solid background fallback.
- Future upstream file/strategy formats can require a shell adapter update. Fully offline import of an unknown release without a verified catalog is blocked.
- No VPN or DNS module is implemented.
- The EXE is unsigned; verify the checksum and source before running it.
- The EXE passed actual cold-start, settings and tray checks; Portable retained settings after a folder move. All 74 safe checks passed, including official-file installation checks. These do not establish live network effectiveness: actual results depend on Windows configuration, required permissions and your network environment.

## Getting started

Open this release’s **Assets** list. Standard is the usual choice: download the Windows x64 EXE and `LICENSE.txt`, then run the EXE. For Portable, download the ZIP, extract the entire `Open Net` folder and run `Open Net.exe` from there; never run it inside the ZIP. On first launch, use **Install** beside each desired component, wait for installation, review its settings, then start it explicitly. The [README](../README.md) includes step-by-step instructions and troubleshooting for GitHub access and unsigned-app warnings.

See [validation details](VALIDATION.md), [English documentation](../README.md), [Russian documentation](README_RU.md) and [third-party notices](../THIRD_PARTY_NOTICES.md). Thanks to Flowseal, bol-van, the WinDivert and Cygwin contributors, and the .NET and Fluent teams.
