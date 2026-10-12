# v0.3.2 development validation

Status: local test build, not a published release. Base: published v0.3.1 (50cf7789fd1fcedb7d104567491a875a43b81393), with current main documentation at 9c60ca1b278a015fb107c6cf06b0006e373c67b8. Development branch: feature/ui-theme-0.3.2.

## Executed checks

On Windows 11 build 26200, the full existing 74-test harness plus eight new v0.3.2 checks passed **82/82**. The test host hash was **002B80A4A5BB616F926F4E09E0376BADE1CBBE0E49C9A09CC23789EF579189C8**. The standalone CI-safe suite passed **9/9** without screenshots, network downloads, fixtures, registry registration or network processes. The full harness downloaded official files only to verify installation, digests, import and rollback; actual winws/WinDivert/TG listeners were not launched.

New coverage includes published-configuration dark preservation/new System defaults, immediate persisted choices and localization, system-change handling through a controlled provider/message, manual-theme precedence even when system preferences cannot be read, compact rows and full strategy tooltip, a private four-item scroll fixture, both palettes/all owned windows/dialogs/logs/tray, contrast, native light/dark Mica/Acrylic and solid fallback, repeated restoration, file-based readiness and deletion/recovery, responsive controls during installation, duplicate-click protection and a controlled failed download preserving the existing module and its ability to run via a safe stub.

The old dark-log test now checks high-contrast near-white text rather than exact white: the shared dark Text brush is #F3F3F3, retaining readable dark appearance. The initial main-install assertion now checks the accessible action name because the action uses an icon rather than a text caption. No actual failure was converted into a fake Passed. Graphical capture helpers verify the foreground belongs to the owned test window.

Local production and test builds succeed with SDK 10.0.100 and runtime 10.0.12. The restricted local environment returned NU1900 because the NuGet vulnerability feed could not be reached; this is not a completed vulnerability audit. CI retains normal auditing. Final production-package and hosted-CI results are recorded separately after they execute.

## Visual review

Reviewed main Light + Mica and both theme settings images. The dropdown arrow/caret now use readable palette resources. Settings use a slightly taller secondary window at normal DPI; scrolling remains available when the work area is smaller. Main dimensions remain 460 × 710 nominal, with the preceding work-area/DPI fit behavior. Module rows are about 73 px. Only the module region scrolls; the power/primary action and footer remain fixed.

## Limits and manual checks

- A real Windows 10 computer and physical multi-monitor 100/125/150/200% DPI configurations remain manual checks. Rendered scale coverage is not a physical-monitor test.
- The Windows theme registry was read only. System-change tests inject a test-only provider and send a message to the owned window; a human should also switch Apps theme in Windows Settings while System is selected, then verify manual Light/Dark remain unchanged.
- Real network behavior/UAC, driver unloading and TG data preservation require the owner's separate safe manual verification. Existing ownership checks, adapters, paths, autostart and start/stop warnings are retained.
- Mica/Acrylic depend on OS/compositor/transparency/power/high-contrast settings. Desktop Accent Acrylic and the preceding custom-caption sentinel have the same compatibility limitations as v0.3.1; supported fallbacks remain.
- A compatible managed package can remain installed while an invalid external path/strategy is selected. Its Refresh icon describes installed files; the separate process/error state still reports configuration problems. Readiness does not silently alter the selected path or strategy.
- No DNS/VPN/Xray or future placeholder modules appear. The four-item layout model exists only in excluded test source.
- ChatGPT project instructions are not remotely saved; see PROJECT_INSTRUCTIONS.md. Repository and local workspace agent instructions independently enforce the workflow.

