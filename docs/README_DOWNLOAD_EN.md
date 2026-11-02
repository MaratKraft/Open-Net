# Open Net 0.4.0

Open Net manages Zapret, TG WS Proxy and IPv4 DNS in one compact Windows application. Tools are installed separately from their official GitHub releases. Open Net does not include a VPN and does not guarantee access to individual websites.

Standard: run Open-Net-0.4.0-Windows-x64.exe. It is the application, not an installer. Settings and installed modules use %LOCALAPPDATA%\Open Net.

Portable: extract the entire ZIP to a writable folder and run Open Net.exe. Keep portable.flag, Data and Modules together. Do not run the EXE inside the ZIP. Native runtime libraries may extract to Windows temporary storage.

Use the download icon to install a missing module. Zapret defaults to ALT13 when supported. Configure tools with their gear, then use an individual switch or the assigned main button. A running process does not prove website access. External instances require confirmation and verified identity; ambiguous or service-managed instances are left unchanged. The shared WinDivert driver is not automatically unloaded.

DNS Manager changes IPv4 DNS on one connected physical adapter. Off returns automatic IPv4 DNS, not the previous manual addresses. Protected originals are retained for the separate offline recovery action. IPv6 is preserved. Unsupported encryption or policies block changes. Clear DNS cache is a separate action. Cookies, browser profiles and VPN are not modified.

Closing the window normally leaves Open Net in the tray. Tray > Exit closes the shell and leaves tools running. Configure Windows startup explicitly in Settings; tool startup uses a visible Task Scheduler entry with normal Windows approval. If the EXE is moved or renamed, use Settings > Repair after reviewing the new location.

Settings > About and licenses offers a manual update check and optional pre-release channel. Automatic discovery runs about weekly after successful checks. Settings shows Open release only for a compatible newer release; it opens the official GitHub page. Automatic download/installation is not implemented. Module updates use their existing settings.

Windows x64. .NET 10.0.12 is included; no separate .NET or Python installation is needed. Native DNS changes require Windows 10 build 19041 or newer; per-adapter DoH requires supported Windows 11 APIs. Windows 10 support also depends on Microsoft's .NET/OS lifecycle; not every edition is supported by Microsoft. Physical Windows 10 validation by the developer is unavailable. Mica falls back to a supported material.

The EXE is not Authenticode signed. Do not disable Windows security. SHA-256 detects file differences; a checksum obtained from the same release is not independent publisher authentication. Obtain files only from https://github.com/MaratKraft/Open-Net/releases.

Upgrade from 0.3.3: exit the old shell through the tray, back up the data folder, replace only the EXE in the same location and retain its filename where possible. Do not delete Modules or Data. A legacy settings backup is created before conversion. Check startup registration if the path/name changed. Existing tools are not reinstalled merely because the shell was updated. See UPGRADE_0.3.3.md for rollback.

Known limits: machine-local DNS recovery, online provenance required for external TG control, conservative refusal for unknown trees/services, and comparatively high WPF shell memory on some systems. Auto-installation of Open Net updates and old-module cleanup are not included.

Original Open Net code: MIT, LICENSE.txt. Third-party terms remain separate; see THIRD_PARTY_NOTICES.md, Notices/ and Settings > About and licenses. Repository: https://github.com/MaratKraft/Open-Net. Report bugs without private logs or credentials.