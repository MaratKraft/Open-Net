# Validation scope — 0.3.1 Beta

Final local checks: 12 October 2026, Windows 11 build 26200. These results describe the prepared release files; they do not guarantee network effectiveness on every provider.

## Release binary and source

- All 50 source/project/localization/manifest files match the supplied 0.3.1 source archive byte for byte. Namespaces and application behavior were retained.
- The publication copy built with SDK 10.0.100 and runtime 10.0.12 to an EXE byte-identical to the supplied 75,574,364-byte release candidate: SHA-256 `0d070b9eec3b7c2d5ed457a954e247b72d4d12f26d4601b88c5fa3d965c1f5df`.
- EXE file and desktop assembly version: 0.3.1.0; product: 0.3.1. The internal shared assembly retains version 1.0.0.0. Production excludes known test harness/stub classes and debug symbols.
- The single-file bundle contains .NET and Windows Desktop runtime 10.0.12. Its 457 decoded entries were checked for selected sensitive-data patterns, including UTF-16 strings. No matching personal Windows profile paths, GitHub tokens, private-key headers or literal proxy secrets were found. Pattern scanning is not a complete security audit.
- All six original third-party notice resources remain embedded. Portable adds readable notices, Open Net's MIT license, instructions, the marker and empty data/module directories. Neither edition contains upstream network tools or profiles.
- Final local NuGet restore with advisory auditing enabled completed without warnings after network access was restored. The earlier restricted restore's `NU1900` warning was resolved. This does not certify every bundled native/runtime dependency.

## Standard — actual release EXE

The prepared Standard EXE was copied to a clean temporary folder outside the source and release directories. With other ordinary shell instances closed, its main window opened, general settings opened and saved successfully, and data was shown under `%LOCALAPPDATA%\Open Net`. The folder beside the EXE remained free of `Data` and `Modules`.

The shell was closed and launched again. Two additional cold-start probes completed successfully: visible main window, tray present, exit code 0, zero singleton activations and zero network-tool launches. User settings were restored after the temporary exit-setting test. Existing user network processes were left running and were not used to claim connectivity testing.

An earlier restricted-context attempt produced an error dialog. Successful actual cold starts in the normal user session and the probes above followed; the incomplete earlier attempt is not counted as a pass.

## Portable — actual release EXE

The final ZIP was extracted into a separate clean temporary folder. The shell opened without installed components and displayed **Install** for both tools. Because user-owned network tools already existed on the machine, their status correctly said **External instance**; the clean copy left them unchanged.

General settings opened, switching to Russian persisted under portable `Data/Config`, and Save worked. After normal exit, the entire folder was moved and reopened with its saved settings retained. A probe of the moved copy confirmed portable storage, a visible window, tray, .NET 10.0.12, no network launches and no change to ordinary-mode settings in LocalAppData. `Modules` remained empty before installation testing.

## Installation, failure handling and safe tests

The existing verified 0.3.1 test executable was rerun with official-file installation checks: **74 tests, 0 failures**. Its test-only SHA-256 was `9c8bb59fdf06f6425242856ff14f29d2ef83e2d31f3688f49359aabec159576c`, distinct from the public EXE. The corresponding source is included for contributors; the test executable and outputs are not distributed.

Coverage includes clean missing-module install controls, official release discovery and installation, compatible future artifact formats, SHA-256 validation, release import and rollback, download/network error handling, settings persistence, ownership, localization, tray and safe stub start/stop. Real upstream EXEs, BAT scripts and WinDivert drivers were not executed by these checks.

Existing development reports also record 8/8 storage probes and 3/3 startup probes for the same public EXE. Private raw reports and verification output contain local paths/process information and are intentionally excluded from the public repository.

## Remaining limitations

- Real Windows 10, the previously affected Windows 11 laptop, physical multi-monitor/DPI configurations and pinned icon behavior remain manual checks.
- Real Zapret UAC/WinDivert operation, live TG connectivity and provider-specific effectiveness were not tested in release preparation.
- Startup after actual Windows sign-in and portable startup-entry repair need target-machine validation.
- The EXE is not Authenticode-signed. Mica/Acrylic depends on Windows configuration; Windows 10 uses a solid fallback.
- Fully offline import of unknown releases without a verified official catalog is blocked. Future upstream format changes may require an adapter update.
- VPN and DNS management are not implemented.

## Reproducibility and CI

Local byte identity was verified with the documented SDK/runtime. Different SDK patches, package contents or environments may change future builds. The Portable ZIP hash differs from the development candidate because its license and usage documents were completed; its EXE is unchanged. ZIP timestamps may also affect archive hashes.

GitHub Actions builds and packages the shell without executing network tools or drivers. Its hosted result must be checked before replacing the reviewed EXE. Release checksums describe the specific attached files.

The [initial GitHub Actions run](https://github.com/MaratKraft/Open-Net/actions/runs/38216211000) passed the public-file guard, Windows build, clean packaging and artifact upload. Its EXE differed from the reviewed release by 128 bytes. A bundle/assembly comparison found identical runtime payloads and compiled method bodies; four desktop resources differed only in line endings, and informational-version metadata included the commit ID. See [BUILD.md](BUILD.md) for both hashes. The release uses the reviewed local EXE.
