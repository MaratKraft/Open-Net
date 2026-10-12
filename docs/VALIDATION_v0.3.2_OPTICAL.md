# v0.3.2 optical refinement

Development branch `feature/ui-theme-0.3.2`, draft PR #1. This record supersedes the icon sizes/markers in the preceding Fluent validation record; earlier artifacts remain available separately.

## Changes

Refresh's full drawing envelope and the filled Select circle looked heavier than Settings. Download/Refresh now render at 17 DIP inside their unchanged 18 DIP layout viewport. Select uses the same one-DIP optical reduction around its center, avoiding the half-pixel layout shift from arranging a 17 DIP box with integer layout rounding. Hit areas remain 28 by 28, surface corners 6, action cells and gaps remain 28 / 4 / 28 / 4 / 28 (92 DIP overall). Settings remains 18 DIP and centered between visible edges. Progress replaces the first glyph without moving its neighbors.

`ActionForeground` supplies normal enabled Download/Refresh, Settings and the unselected Select contour: dark #CFCFCF, light #4A4A4A, high-contrast WindowText. Selected fill/check, hover, pressed, disabled and keyboard-only focus retain their distinct theme resources.

Both module rows share a 32 DIP marker column instead of 28. With the visible 18 DIP marker centered in its existing 22 DIP container, text begins 12 DIP after its right edge (previously 8). Titles and statuses move together by 4 DIP; right actions, row heights, main dimensions and all command handlers remain unchanged.

The supplied Zapret SVG's path is used verbatim as nonzero WPF geometry, with uniform scaling and the same SecondaryText brush as the Telegram plane. Both markers have 18 DIP visible width, are centered within 22 DIP containers, and preserve their natural aspect ratio. There is no bitmap replacement or runtime dependency. The owner's explicit choice was to retain their supplied SVG after reviewing an independent-controller proposal. The proposal is not distributed.

## Source and brand limitation

The supplied silhouette visually resembles Discord's Clyde with a Play cutout. [Discord brand guidelines](https://discord.com/branding) restrict logo modification and use of its marks/assets. Separate permission has not been supplied; owner approval is not Discord permission. The [YouTube brand resource site](https://brand.youtube/) supplies its official assets; a generic Play triangle alone in the supplied SVG does not establish brand clearance. Open Net claims no affiliation or endorsement. These third-party rights are not licensed by the project's MIT license. Provenance is in [vector sources](../assets/Icons/SOURCES.md); `Zapret-ICON.txt` is embedded in the EXE and included in Portable notices. Stable public distribution still needs a brand-rights decision.

## Executed checks

- Full safe Windows 11 suite: **87/87 passed**, including real owned focus, command/event checks, selection isolation, installation failure/retry, native dark/light Mica and Acrylic, active/inactive appearance and tray restoration. Test-host SHA-256: `EC1D1B65E68A0195114B402257CE971507799A8A9A64B6D09C63CBEB07DCB5A2`.
- Headless suite: **12/12 passed**, including injected System light/dark, one-DIP optical bounds, shared contour colors, both marker gaps, fixed action group and loading without gear shift.
- Native dark/light Mica/Acrylic captures reviewed. Actual-template state matrices reviewed at rendered 100/125/150/200%: Download/Refresh, selected/unselected, Normal/Hover/Pressed/Keyboard Focus/Disabled. Whole-window renders at these scales were also inspected for the marker/text layout. RenderTargetBitmap scaling and synthetic input states are not physical monitor or hardware-input tests.
- Production builds use SDK 10.0.100 and self-contained runtime 10.0.12. Restricted local restore reports NU1900 because advisory data could not be reached; advisory auditing remains enabled in hosted CI.
- **4/4 package checks passed**: Standard EXE outside the repository, extracted Portable, hidden startup entrypoint without registry registration, and Portable moved to a Cyrillic path containing spaces. EXE bytes match in both packages; each startup probe recorded zero tool launches. Portable contains no network binaries, drivers, profiles or test payload.
- Public-file guard and whitespace checks run before the commit. Hosted CI is checked for the resulting PR head; its exact run is recorded with the local reports.

## Local artifacts and scope

Separate directory `test-builds-0.3.2-optical`, outside Git. Standard EXE SHA-256: `3976AC926219D898C1DFB193AC8C5AB7C10EA8AC0DFD8491DC6CBF29B02EF4C5`. Portable ZIP SHA-256: `85C60D258DBC15BE0E3FF090DB485C2859BC4DC7210276D4A0B8E89892B146C6`. Raw reports and images are beside the builds in `verification`, excluded from Git.

Windows 10, physical multi-monitor/DPI/hardware input and real network behavior remain manual checks. Real Zapret/TG, WinDivert, system network configuration and foreign-process termination were not exercised. Main, published v0.3.1, branch protection and release assets remain unchanged. No merge, v0.3.2 tag or Release is performed.
