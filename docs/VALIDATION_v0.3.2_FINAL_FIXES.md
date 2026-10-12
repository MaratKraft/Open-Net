# v0.3.2 final UI fixes — local test build

These are three fixes within v0.3.2 on `feature/ui-theme-0.3.2`, updating PR #1. No v0.3.3 branch, merge, tag or Release is created. The earlier v0.3.2 artifacts remain available; new test files are stored separately.

The owner subsequently identified unequal visible gaps despite equal hit areas. The [spacing correction](VALIDATION_v0.3.2_SPACING.md) records the follow-up and its newer artifacts; hashes below identify the preceding build.

## Changes and causes

- Module actions now share equal 28 × 28 hit areas in a three-cell, 92 px group. Previously the selection template aligned its 24 px circle differently from the two buttons. The circle is centered inside a transparent full-size hit surface, so its edge areas also receive input. Both buttons share one style; both rows use the same layout. Download and Refresh paths stretch uniformly within a centered 16 px box. Main size, row height and text sizes are unchanged.
- All option switches share a 42 × 22 track and 14 px circular thumb, with balanced 4 px insets. The thumb is a separate centered element rather than a child of the track border. Focus is a separate overlay, so border thickness cannot displace the thumb. Hover/pressed thumb sizes retain balanced insets; disabled switches do not react to those states. The off-state light-theme thumb is darker for contrast. No animation or new decoration was introduced; settings handlers are unchanged.
- The original 26 px logo is wrapped in a standalone button with a hand cursor, localized tooltip/accessibility name and keyboard support. Only the logo is clickable; the name/background still return native caption hit tests. A fixed HTTPS address uses Windows shell execution to open the default browser. Failures are logged and shown as a localized nonmodal message. Test builds inject a launch action without opening browsers; that hook is absent from production.

## Executed checks

- **85/85 full safe tests passed** on Windows 11 build 26200. Final test-host SHA-256: `CA0ECEAFDB6A5BAD93C2E423F29A10C11C3D218B04395345B81E6A3A9BA72F97`.
- **11/11 headless safe checks passed** locally. Added checks cover equal spacing and hit areas, selection edge hit tests, centered thumb positions in normal/hover/pressed/disabled states, control contrast and the fixed default-browser launch plan.
- Logo tests exercised click events, accessibility invocation, routed Enter/Space activation, RU/EN tooltips and an injected launcher failure. Existing native hit-test assertions now explicitly require logo HTCLIENT and title HTCAPTION. Existing settings round trips, tray, installation, themes and owned-stub checks still pass.
- Control images were generated and visually reviewed in both themes at rendered 100/125/150/200% scales, including Download/Refresh, selected/unselected, hover, pressed and disabled states. Focused settings and main images were reviewed. Synthetic hover/pressed states use actual templates; this is not physical mouse/monitor testing.
- The native material suite still verifies Mica/Acrylic using screen captures above its own colored window. One intermediate run failed to show the expected backdrop color difference; its setup did not explicitly establish adjacency in the window order. The test now explicitly brings its own backdrop forward before restoring its own shell. The assertions were retained and final full runs passed. Production material code was not changed.
- Production build succeeded using SDK 10.0.100 / runtime 10.0.12. Metadata inspection of the compiled application inputs checked **209 types**, with no test harness, stub or test supervisor types.
- **4/4 final package probes passed:** standard EXE outside the repository, extracted portable ZIP, hidden startup entrypoint without registry registration, and portable moved to a Cyrillic path with spaces. Each reported zero tool launches, expected visibility/tray and storage mode. Normal/portable executable bytes match. The ZIP contains no third-party network executables, drivers, profiles, logs or test payload.

## Local artifacts

New output directory: `test-builds-0.3.2-ui-fixes` in the development workspace, outside the Git repository. Previous supplied test builds were not overwritten. Production EXE SHA-256: `7BEAC77739622856701E6E66AB8B131CB0274D1ED7981D03AE947186F5778E17`. Portable ZIP SHA-256: `6AC97511759844B8C03070064CC6C886563C274334D9D4604DB73DAF45B1BEED`.

Raw reports and generated images accompany the local builds under `verification/`; user paths and runtime observations are deliberately not committed. Hosted CI status is available on PR #1 for the associated commit.

## Limits

Windows 10, physical multi-monitor DPI, hardware keyboard/mouse interaction and real default-browser association failures remain manual checks. Browser launch was intercepted in automated tests; no browser was repeatedly opened. Existing network tools were observed only; no real Zapret/TG start/stop, driver load or DNS/routes/firewall changes occurred. The restricted local restore could not reach NuGet vulnerability data (NU1900); compilation succeeded but this does not represent a completed vulnerability audit. Hosted CI retains its auditing configuration.

Main, published v0.3.1 and branch protection remain unchanged. PR #1 remains draft for owner testing.
