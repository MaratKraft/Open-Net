# Build, test and package

Use Windows x64, PowerShell 7 and .NET SDK **10.0.100** (`global.json` permits SDK patch roll-forward). Package sources are restricted by `NuGet.Config` to official NuGet. The runtime is pinned to **10.0.12**. Internet access or a populated local package cache is required.

```powershell
./scripts/publish.ps1
# Optional explicit SDK and pre-populated cache:
./scripts/publish.ps1 -SdkPath 'C:/SDK/dotnet.exe' -NugetCache 'C:/NuGetCache'
```

Current development output: `publish/Open Net 0.4.0-alpha.1/Open Net.exe`; safe host: `publish/Open Net 0.4.0-alpha.1 Test/Open Net.exe`. Production compilation excludes the test harness and safe stub classes. Options: Release, win-x64, self-contained, single-file compression, native extraction, no trimming, no debug symbols. No network component is bundled or launched by the build. Published Stable v0.3.2 remains unchanged. See [DNS Manager alpha](DNS_MANAGER_ALPHA.md) for capability restrictions and manual-test gates.

## Repository layout

- `src/NetworkHub.Desktop`: WPF window, tray, module actions, storage selection and Windows integration.
- `src/NetworkHub.Reused`: adapters, process ownership checks, installation, validation and localization. This is current shared application code; namespaces and files are retained from the verified source release.
- `assets`: original application icon/logo, Fluent vector, license texts and reviewed screenshots.
- `scripts`: build, packaging, public-file checks and the existing safe test entry point.
- `docs`: Russian README, release notes and validation scope.

Some shared types retain names/comments from earlier development stages. They are kept to preserve the verified source; old application directories and prototype projects are not included.

## Safe development tests

The existing harness is an interactive Windows test executable, not a `dotnet test` project. It opens its own windows and launches only its own stub processes. Obtain the official Zapret 1.10.3 reference files `general.bat`, `general (ALT13).bat` and `service.bat` separately. The tests read these fixtures, never execute them. Do not commit private fixtures or outputs.

```powershell
./scripts/publish.ps1 -TestBuild
./scripts/test.ps1 -FixtureDirectory 'C:/Fixtures/zapret-discord-youtube-1.10.3'
```

`-InstallFiles` additionally downloads upstream release binaries for installation validation; it does not launch them. It is optional and is not enabled in CI. CI runs the separate twelve-check `test-headless.ps1` suite without fixture downloads or screenshots. Existing historical installation test results are summarized in [VALIDATION.md](VALIDATION.md). Do not mistake skipped network-installation checks for new live network testing.

## Packaging

```powershell
./scripts/publish.ps1
./scripts/package.ps1
```

The packaging script copies the production EXE, constructs a clean Portable ZIP with `portable.flag`, empty data/module directories, notices and Open Net's license, and writes SHA-256 sums under `artifacts/`. It refuses to overwrite an existing artifact set. No runtime profiles or developer reports are copied. Publish the output as release assets, not source files. The standalone EXE keeps the bundled third-party notices; attach `LICENSE.txt` alongside it for Open Net's MIT terms.

## CI and reproducibility

The GitHub Actions workflow checks tracked public files, builds the production application and a separate test executable, runs twelve safe headless checks, and uploads a Windows artifact. It has read-only repository permissions. It does not launch the production application, network utilities, WinDivert or system-changing tests. Interactive tests are left to a Windows desktop session; no formatter configuration existed to enforce in CI. The public-file guard checks selected secret patterns and forbidden outputs; it is not a full credential scanner.

The prepared source rebuilt locally with SDK 10.0.100 and runtime 10.0.12 to an EXE **byte-identical to the supplied release candidate**. This is a measured result for that environment, not a universal reproducible-build guarantee. SDK patches, package contents or build paths may change results. CI artifacts must be independently compared before replacing the reviewed release EXE.

The first restricted restore used an existing official-package cache and could not reach vulnerability data (`NU1900`). The final online restore with advisory auditing enabled completed without warnings after network access was restored. This does not certify every bundled runtime/native dependency. Check the GitHub Actions run associated with the release commit for hosted build results.

The [first hosted build](https://github.com/MaratKraft/Open-Net/actions/runs/38216211000) passed. Its EXE was 75,574,492 bytes, SHA-256 `47a3b0abbcbee7149b97cad636bd070e51de7ec936ca987ba4572d9514ec268e`, versus the reviewed release EXE's 75,574,364 bytes. Decoding both bundles showed identical runtime entries and identical compiled method bodies in both application assemblies (481 desktop and 639 shared methods). Four desktop text resources differed only in line endings (XAML and three notice files), and CI included the Git commit in informational-version metadata. These packaging/metadata differences affect checksums. Later documentation commits also change that commit metadata. Release assets use the locally tested EXE, not the CI artifact.

## Current development checks

The direct-launch user preview is a separate build: `./scripts/publish.ps1 -SafePreview` writes `publish/Open Net Safe Preview`. Verify it with `./scripts/verify-safe-preview.ps1 -ExePath 'publish/Open Net Safe Preview/Open Net.exe'`. This check launches without `--test-mode`, verifies isolated data and zero tool launches, rejects privileged/self-test entry points, and compares observed system DNS before/after using read-only enumeration. CI also builds and checks this preview. The developer harness remains separate: its legacy startup-registration fixture uses only an owned temporary HKCU key; that fixture cannot be run in the user preview.

The compact DNS update has 51 always-run headless checks (plus the conditional administrator ACL fixture) and 81 interactive checks. New checks cover profile ownership across restart, inspection failures, actual Configure/Apply/Restore events, minimized/hidden reopening, and simulated Xbox application. Rendered dark/light images and scale checks do not substitute for physical DPI or real DNS/UAC validation.

After building with -TestBuild, run ./scripts/test-headless.ps1 for palette/layout/configuration and isolated DNS transaction checks. The alpha currently has 51 always-run checks; an additional protected-file ACL fixture runs only under an elevated administrator token, entirely in the owned temporary data directory. A skipped ACL fixture is not evidence of production UAC validation. The full interactive harness still requires the private official BAT fixtures and a Windows desktop; it tests native materials, only owned stub processes, and fake DNS apply/restore. Its graphical checks must not be represented as real network tests. Safe DNS mode never constructs the production writer or probe runner, and the safe executable rejects the DNS elevated entry point.

R5 adds thirteen headless session/profile/cache checks and three interactive scenarios for automatic assignment, profile switches, custom-profile persistence and simulated UAC/write/verification/restore/cache failures. `--dns-recovery --verify-only` opens the independent emergency window and produces a local read-only probe without applying DNS. Safe Preview rejects that entry point. A real Windows DNS trial is still separately gated; fake tests and inspection cannot certify real setters or physical Windows 10.
