# Third-party notices

Scope: Open Net 0.3.3 release preparation, reviewed 21 October 2026. The MIT license for Open Net's original code does not replace any third-party license. This inventory distinguishes included dependencies from separately downloaded tools.

## Included in the application

| Component | Developer | Version / provenance | License and retained texts |
| --- | --- | --- | --- |
| .NET Runtime | .NET Foundation and contributors / Microsoft | 10.0.12, self-contained win-x64 runtime | MIT plus dependency-specific notices: [license](assets/Notices/dotnet-runtime-LICENSE.TXT), [third-party notices](assets/Notices/dotnet-runtime-THIRD-PARTY-NOTICES.TXT); [source](https://github.com/dotnet/runtime/tree/v10.0.12) |
| Windows Desktop runtime: WPF and Windows Forms | .NET Foundation and contributors / Microsoft | 10.0.12 | MIT plus dependency-specific notices: [license](assets/Notices/windowsdesktop-LICENSE.txt), [WPF notices](assets/Notices/dotnet-wpf-THIRD-PARTY-NOTICES.txt), [WinForms notices](assets/Notices/dotnet-winforms-THIRD-PARTY-NOTICES.txt); [WPF source](https://github.com/dotnet/wpf/tree/v10.0.12), [WinForms source](https://github.com/dotnet/winforms/tree/v10.0.12) |
| Fluent UI System Icons: settings gear | Microsoft Corporation | Existing vector supplied with this project; exact upstream commit was not recorded | [MIT text](assets/Notices/Fluent-LICENSE.txt); [source](https://github.com/microsoft/fluentui-system-icons), [upstream license](https://github.com/microsoft/fluentui-system-icons/blob/main/LICENSE) |
| HB AI Smart DNS | HB AI (vk.com/neiroseti_ai) | DNS experiment based on reviewed source c3a85a5e05aae3d866271770fe7cf240d742a8d5 | [MIT copyright and permission notice](assets/Notices/HB-AI-LICENSE.txt), [additional Russian license](assets/Notices/HB-AI-LICENSE.ru.txt); [source](https://github.com/neiroseti-ai/hb-ai-xbox-dns) |

Retain the applicable copyright, permission and dependency notices when redistributing. The six original .NET/Fluent notices and two module-symbol notices are embedded in the v0.3.3 EXE and displayed by **Settings → About and licenses**; Portable also includes readable copies under `Notices/`. .NET dependency notices list additional licenses, including native/runtime dependencies; these must not be replaced by a blanket MIT statement. Windows system DLLs are referenced through operating-system APIs and are not copied into this repository as third-party network tools.

### Module symbols

The Telegram plane follows separate [Telegram brand terms](assets/Notices/Telegram-BRAND.txt), not MIT. The owner-supplied Zapret composite is described in [Zapret-ICON.txt](assets/Notices/Zapret-ICON.txt): separate permission for the derivative has not been supplied, and [Discord's brand guidelines](https://discord.com/branding) restrict modification of its logo. This question remains open for the supplied derivative symbol. Neither symbol implies endorsement or affiliation; the symbols do not relicense the corresponding brands.

The original Open Net logo is owner-supplied artwork, preserved without redesign. It is not a Fluent icon. This release assigns MIT to original code and documentation; it does not assign a separate artwork/trademark license.

## Installed separately — not included in either download

| Component | Developer | Version status | License, source and distribution considerations |
| --- | --- | --- | --- |
| zapret-discord-youtube | Flowseal; includes work by bol-van and others | No version bundled. 1.10.3 used in existing development checks. | [Project](https://github.com/Flowseal/zapret-discord-youtube), [license](https://github.com/Flowseal/zapret-discord-youtube/blob/main/LICENSE.txt): MIT for covered project code; the bundle's WinDivert and other components have separate terms. Preserve notices and audit every actual binary if creating a bundled edition. |
| TG WS Proxy | Flowseal | No version bundled. v1.11.1 is a reviewed baseline; existing installation tests also cover v1.11.2. | [Project/source](https://github.com/Flowseal/tg-ws-proxy), [MIT license](https://github.com/Flowseal/tg-ws-proxy/blob/main/LICENSE). Retain MIT notices; independently inventory any runtime and transitive libraries in the specific packaged EXE before redistributing it. |
| zapret / winws | bol-van and contributors | No version bundled; selected by the upstream Zapret package | [Source](https://github.com/bol-van/zapret), [MIT license](https://github.com/bol-van/zapret/blob/master/docs/LICENSE.txt). Retain copyright/permission notices; license of the code does not override the licenses of linked/bundled dependencies. |
| WinDivert | Basil Projects and contributors | No version bundled | [Source](https://github.com/basil00/WinDivert), [license](https://github.com/basil00/WinDivert/blob/master/LICENSE): choice of LGPLv3 or GPLv2. A binary redistribution must satisfy the chosen license, including applicable corresponding-source and notice obligations. A generic project link alone is not a completed compliance plan. |
| Cygwin API library and associated tools | Cygwin contributors | No version bundled | [Source and project](https://cygwin.com/), [licensing terms](https://cygwin.com/licensing.html): current API library is LGPLv3-or-later with the stated linking exception; tools have individual licenses, often GPL. Identify exact versions and provide required corresponding source and license texts when conveying covered binaries. |

The two Open Net downloads contain **no winws, WinDivert, Cygwin or TG WS Proxy binaries, profiles, proxy secrets or upstream BAT scripts**. They fetch user-selected components from upstream releases. License conclusions for current upstream source do not certify every historical or future binary package. Full redistribution of a preinstalled network-tool bundle remains outside this release and requires a separate version-specific audit, source provision and transitive dependency inventory.
