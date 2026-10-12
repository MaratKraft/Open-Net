# v0.3.2 release preparation

This document supplements [approved variant A](VALIDATION_v0.3.2_APPROVED_A.md). The application design and code are unchanged during release-page preparation.

## Provenance and checks

- Approved application source: PR #1, `feature/ui-theme-0.3.2`, commit `0ce2604839da66d59b9349e7a7671b7d3787ce79`. The remote/local HEAD agreed at the start of release preparation; existing CI was successful.
- Current local production EXE and approved-a Standard are byte-identical: SHA-256 `307b137b65f947eb6a71c269aee29f00b8dc21674780c161fa45ba5f4c569981`.
- Approved-a Portable: SHA-256 `d8b85ea497b169d7aef1008cc638b0170925d568ea65391ab4811aeb45b5c3ba`. Its existing verification records identify the same EXE and confirm no preinstalled network payload.
- Final Portable preparation changes only `Beta` to `Stable` in its README. Every other decompressed entry was compared byte-for-byte with approved-a; the EXE is unchanged. Final ZIP: 70,037,144 bytes, SHA-256 `1fa67ebaab6e8728845ea97d389bb6442505282004a13441e79e13475ebad4f3`. The approved original archive is preserved.
- Existing, verifiable results for these builds: 87/87 safe UI checks, 12/12 headless checks and 4/4 package probes. They include Standard outside the repository, settings/tray, EN/RU, Light/Dark/System, extracted Portable, hidden shell startup and moved Portable. These suites were not rerun merely to produce the release layout.
- New production-window inspection used an isolated portable profile with Dark/Solid and opened general settings. The actual Solid control was verified. That temporary shell was exited normally; real user settings and network processes were left unchanged.
- Review of PR changes found that `ModuleInstaller` changes its user-agent version only; shared settings add theme persistence/migration. Process adapters and network launch/ownership contracts were not unexpectedly rewritten.

## Screenshots

The two main-window pictures come from approved-a's verified native Solid captures of the actual application in a safe test state. The settings picture was captured from the approved production EXE with a separate profile and Solid selected. The neutral presentation composition pastes these screenshots without enlarging them or redrawing controls. Only the native Windows title bar was cropped from settings, and a capture-pointer halo was removed from a blank area using its measured Solid background. No text/control was altered.

The screenshots show sample component states, not a claim that components are bundled or that connectivity was tested. No personal path, account name, secret or diagnostic log appears. Older screenshots remain available; this gallery uses only v0.3.2 images.

## Release gates and limitations

The owner approved publication and merge on 2026-10-12, with a dark presentation background and no separate Dark/Light comparison in the README. The user-supplied Zapret symbol is a derivative brand asset: [its retained notice](../assets/Notices/Zapret-ICON.txt) records that separate permission has not been supplied. Discord's published brand guidelines restrict changes to its logo. The owner was informed of this unresolved permission question before authorizing publication; that authorization is not evidence of third-party permission. MIT for the shell does not clear third-party marks. No icon redesign is made in this release-preparation task.

Real Windows 10, physical multi-monitor/DPI/hardware input, live network/UAC behavior and startup after a real Windows sign-in remain target-machine checks. Safe tests launch only owned stubs; upstream executables, BATs and WinDivert drivers are not executed. EXEs are unsigned. Initial GitHub access and offline import trust requirements still apply.

The approved local build used SDK 10.0.100 and runtime 10.0.12. Earlier restricted restores reported unavailable advisory data; hosted CI retains auditing. Different build environments and Git informational-version metadata can change EXE checksums. Attach the approved, verified local files; do not silently substitute a fresh CI binary.
