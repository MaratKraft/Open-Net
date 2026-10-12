# v0.3.2 approved compact variant A

The owner chose variant A after three WPF proposals, requested a straight-bar Download comparison, and explicitly approved the refined A before integration. This final record supersedes the sizes and Zapret SVG in the preceding optical validation record. Branch remains `feature/ui-theme-0.3.2`, draft PR #1.

## Implementation

Download and Refresh use original minimal stroked paths in a fixed 18 DIP viewport, 1.4 DIP contours and rounded caps/joins. Download retains A's shaft and chevron; its tray sidewalls are replaced by a short straight bar. Settings retains the Fluent outline in a 15.8 DIP box with a small themed contour adjustment. Select uses a 16 DIP circle with the approved checked/unselected appearances. All three controls retain 28 DIP hit areas and 6 DIP hover corners in the unchanged 92 DIP action group. The gear remains centered inside its hover area when Download changes to Refresh or progress; it no longer follows asymmetric glyph bounds.

Shared ActionForeground colors remain dark #CFCFCF / light #4A4A4A; selected fill/check, hover, pressed, disabled and keyboard focus retain their distinct resources. The shared 12 DIP module marker-to-text gap, row heights, main window dimensions, typography, primary/power controls and caption behavior remain preserved.

Zapret uses the visible compound path from the owner's second SVG, already softened at the silhouette details and Play corners. Its separate fill:none path is ignored. Telegram's vector plane remains unchanged. Both use 18 DIP visible width, their natural proportions, a common 22 DIP container and the same theme brush. Original-source and third-party notices are retained; no heavy dependency or raster replacement is added.

## Executed validation

- Final production UI source: full safe Windows 11 suite **87/87 passed**; test host SHA-256 `F4C81F936345DF0070F62F73C30FFF799DB48D1B1D86996CA343913DD0C32224`.
- Headless checks **12/12 passed**, including System palette resolution, fixed hit areas, contour colors, marker spacing, compact drawing bounds and stable loading geometry.
- Native dark/light Mica and Acrylic captures visually reviewed. Actual-template state matrices checked at rendered 100/125/150/200% for Normal/Hover/Pressed/keyboard focus/Disabled, Download/Refresh and Select on/off. Synthetic WPF rendering is not physical multi-monitor or hardware-input validation.
- Safe tests use only owned stub processes. Installation validation downloads upstream files without launching the real tools. Real network modules, WinDivert, DNS/routes/firewall and foreign-process termination are not exercised.
- Production compiled with SDK 10.0.100 and self-contained runtime 10.0.12. Restricted restores report NU1900 for unavailable advisory data; hosted CI retains auditing.
- Final package probes **4/4 passed**: Standard EXE outside the repository, extracted Portable, hidden startup entrypoint without registry registration, and moved Portable. The first Standard attempt activated an already running copy; after the owner exited that copy, a fresh full verification passed. Zero tool launches in all final probes; identical EXE bytes in both packages, no network payload or user profiles bundled.
- Public-file and whitespace checks passed. Hosted CI is verified for the resulting PR commit; raw run details are saved locally alongside reports.

## Artifacts and scope

Separate local directory: `test-builds-0.3.2-approved-a` outside Git. EXE SHA-256 `307B137B65F947EB6A71C269AEE29F00B8DC21674780C161FA45BA5F4C569981`. Portable ZIP SHA-256 `D8B85EA497B169D7AEF1008CC638B0170925D568EA65391AB4811AEB45B5C3BA`. Raw captures/JSON are under `verification`, excluded from the repository. Preceding artifacts are preserved.

Windows 10, physical DPI/multi-monitor/hardware input and live network behavior remain manual checks. No merge, tag, Release, branch-protection change or modification of published v0.3.1 is performed.
