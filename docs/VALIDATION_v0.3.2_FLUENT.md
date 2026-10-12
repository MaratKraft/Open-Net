# v0.3.2 Fluent icon controls and TG module marker

Development branch: `feature/ui-theme-0.3.2`, PR #1. This is another local test build within v0.3.2, not a release or redesign. Main, published v0.3.1 and the network adapters remain unchanged.

## Causes and changes

The preceding controls mixed a caption-button hover template with an independent selection-circle template. Download/Refresh used hand-stroked geometry, Settings used a filled Fluent outline, and module markers/dropdown arrows used font symbols. Different geometry bounds and fractional UniformGrid cells made sizes and spacing inconsistent.

`IconButton`, `IconSelection` and `IconGlyph` now define a common compact system: 28 × 28 DIP hit areas, 18 DIP vector viewports, 6 DIP surface corners, transparent normal state, theme-resource hover/pressed colors and muted disabled state. The 28 DIP size preserves the existing row height; the suggested 32 DIP was not required. The selected circle retains its distinct filled/ticked state and now matches the 18 DIP optical size of its neighbors. Three fixed cells with 4 DIP gaps keep the complete group 92 DIP wide. Download/Refresh have the same drawing envelope, so neighboring controls stay fixed. Layout rounding and device-pixel snapping are confined to the new components, not enabled globally.

Keyboard focus uses one explicit indicator controlled by the shared input-mode helper. Mouse mode clears it without removing focus or disabling keyboard activation. Option switches use the same input-mode gate while retaining their existing track/thumb geometry and settings handlers.

Converted elements: both module Download/Refresh buttons, both module settings gears, both Select controls, and the compact Zapret/TG path browse buttons. Download, Refresh and Folder/Open use Fluent regular vectors; the existing Fluent Settings vector is reused. The dropdown chevron and Zapret diamond are vector geometry instead of font symbols. Primary Start/Stop, power, save/large text actions, caption controls and the Open Net logo/link retain their distinct components and commands.

TG WS Proxy now has the official Telegram plane silhouette as a theme-colored vector, with no raster, circular background or fixed white color. Its row height, name, launcher and own tray icon are unchanged. The icon uses the same module-marker palette and alignment container as Zapret.

## Sources and distribution

[Microsoft Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons), revision `a347af95478f989038cccbf085978865264c048a`: MIT. The complete Microsoft license already included with Open Net is retained in both packages. Only small SVG path resources are added; there is no icon-font or runtime package dependency.

The plane path comes from the official logo archive linked on [Telegram Press Info](https://telegram.org/press). Telegram permits illustrative/button logo use provided the project does not represent Telegram officially. This is brand-use permission, not an MIT license or a public-domain claim for the logo. `Telegram-BRAND.txt` accompanies both distributions and the in-app notices: Open Net is independent, is not an official Telegram client and is not endorsed by Telegram. The user-provided raster is not redistributed. Exact source links and transformations are recorded in [assets/Icons/SOURCES.md](../assets/Icons/SOURCES.md).

## Validation

- Full safe Windows 11 suite: **87/87 passed**. Test-host SHA-256: `60F4BBA5927E88210ACD524EB7AC01E8818B6E38D4B3D129D59B2FAC5785869C`. One intermediate run returned HTTP 504 while fetching the official ZIP for payload comparison; the unchanged check passed on retry. No UI failure was converted into Passed.
- Final headless suite: **12/12 passed**, including shared surface geometry, vector/theme resources, System light/dark through an injected provider, and the additional loading/no-neighbor-movement assertion. That assertion was added after the full run; it changes test source only, not the tested production UI.
- New interactive checks exercise real focus on owned controls, mouse-mode clearing, select isolation, both palettes and folder-button accessibility. Synthetic hover/pressed/disabled/focus matrices use the actual templates. Existing tests still cover command handlers, settings persistence, duplicate installs, controlled failure/retry, file readiness, tray, storage, localisation, native Mica/Acrylic and fallback policies.
- Images were visually reviewed in both themes at rendered 100/125/150/200% scales, alongside native Mica captures and both module settings windows. Rendering scale and synthetic input states are not physical multi-monitor/hardware tests. The test harness opened its own Windows UI; no foreign app inspection was required.
- Both production builds compile with SDK 10.0.100 and runtime 10.0.12. The restricted local restore still reports NU1900 because vulnerability data is unavailable; this is not a completed advisory audit. CI auditing remains enabled.
- **4/4** final package probes passed: normal EXE outside the repository, extracted portable, startup-to-tray entrypoint without registry registration, and moved portable in a Cyrillic path with spaces. Normal/portable EXE bytes match; LaunchCount=0 in each probe. The ZIP is clean, with no third-party network executables/drivers, test payload or user profiles.

## Current local artifacts and limits

Directory: `test-builds-0.3.2-fluent-final`, outside the Git working copy. Preceding supplied artifacts are not overwritten. EXE SHA-256: `430F4A810CDCF407047299563B92320059665D24080C2F917726FBA3E684A18F`. Portable ZIP SHA-256: `134E3FB48517396422A49894679FEA6399D9321AFB23E3B8E0133A4A0C5ACAEA`.

Raw JSON and images accompany the local builds under `verification/`; private paths/runtime observations are not committed. Hosted CI is attached to the corresponding PR commit. Windows 10, physical DPI/multi-monitor configurations, hardware input and real network behavior remain owner checks. No real Zapret/TG start/stop, driver load, DNS/routes/firewall changes, merge, tag, Release or branch-protection change is performed.
