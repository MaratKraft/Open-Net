# v0.3.2 visible icon spacing correction

The preceding fix made button centers and hit areas uniform. It did not make the **visible gaps** uniform: the 24 px selection circle is wider than the 16 px gear. The owner's comparison correctly exposed the remaining visual offset.

The shared alignment helper now measures the rendered Download/Refresh path's right edge and the selection circle's left edge. It places the unchanged gear halfway between those edges. This is optical centering of the visible gap; the three equal hit areas, their non-overlap, row dimensions, main size and other UI behavior remain unchanged. The helper responds to layout and glyph changes and bases its offset on the fixed button center to avoid accumulated movement.

Checks cover both first-icon states and both rows, asserting equal visible gaps within 0.1 DIP in addition to the existing hit-area checks. The dark/light visual matrices include the actual installation content structure and alignment helper at rendered 100/125/150/200% scales. This is not a test on physical monitors. The native main-window capture was also reviewed against the owner's reference. Other v0.3.2 changes remain as documented in [final fixes](VALIDATION_v0.3.2_FINAL_FIXES.md).

New local test artifacts are stored separately in `test-builds-0.3.2-spacing`; previous files are not overwritten. Standard EXE SHA-256: `71548B3AA8DE4A1218FC1B74DAF6653143DCBCB10E2FFDA074DD193CDA23719E`. Portable ZIP SHA-256: `82A26C9D3D42287CAFE50754C03AE9AA66481EF3CE27A6ABB61CD77463001F9B`.

Final checks passed: **85/85** full safe tests, **11/11** headless checks and **4/4** production-package probes. Final test-host SHA-256: `E9FD72F137EE7E2CFFF815DA8A848413D2C1CEB7349B718DAF5577D3DEA50CB0`. The standard probe ran independently after the owner closed the preceding shell; normal and portable executable bytes match. Raw results accompany the local artifacts under `verification/`.

No real network tools are started/stopped. Windows 10, physical DPI and real browser/network behavior retain their prior manual-check limits. The restricted local build still reports NU1900 for unavailable vulnerability data; compilation succeeds and hosted CI auditing remains enabled. GitHub CI is associated with the correction commit in PR #1. Main, published v0.3.1, branch protection and release publication remain untouched.
