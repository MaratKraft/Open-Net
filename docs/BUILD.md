# Build, test and package Open Net 0.4.0

Use Windows x64, PowerShell 7 and .NET SDK 10.0.100 (global.json permits patch roll-forward). Runtime 10.0.12 is pinned; NuGet.Config uses official NuGet. Builds are Release, self-contained win-x64, compressed single-file, native extraction, no trimming or debug symbols. No network tool is bundled or started by a build.

```powershell
./scripts/publish.ps1
./scripts/publish.ps1 -TestBuild
./scripts/test-headless.ps1
./scripts/test.ps1 -FixtureDirectory 'C:/Fixtures/zapret-discord-youtube-1.10.3'
./scripts/publish.ps1 -SafePreview
./scripts/verify-safe-preview.ps1 -ExePath 'publish/Open Net Safe Preview/Open Net.exe'
./scripts/verify-dns-recovery.ps1 -ExePath 'publish/Open Net 0.4.0/Open Net.exe'
./scripts/package.ps1 -OutputDirectory 'C:/Candidates/Open-Net-0.4.0'
```

Production: publish/Open Net 0.4.0/Open Net.exe. Test host: publish/Open Net 0.4.0 Test/Open Net.exe. Safe Preview is separate and is not a public Stable asset. Production excludes Tests*.cs, SelfTests, SafeStub, StubSupervisor, the unused DriverControl and the old DnsManagerView. The current DNS transaction/restore engine remains shared; excluding the former window does not remove protected recovery.

The harness is not a dotnet test project. Headless tests cover pure contracts and isolated integration; WPF tests require an unlocked interactive Windows desktop. Fixtures are official BAT/archive files used as data and never executed. OPENNET_ZAPRET_FIXTURE and OPENNET_ZAPRET_ARCHIVES enable extra compatible-format checks. OPENNET_033_FIXTURE can point to an isolated copy of v0.3.3 data to exercise module manifests and settings conversion. Never commit these fixtures, runtime data or results. Optional -InstallFiles downloads official files for installation checks; it never starts them.

Tests start only owned stub processes. DNS uses fake readers/writers. The startup fixture uses an owned temporary HKCU key, not the real autostart entry. Native materials and focus tests must run in a normal host desktop session; restricted process/registry access is not equivalent to a product defect or a passing test. Physical Windows 10, real network outcomes and a real logon are separate evidence.

The production --startup-probe path is a diagnostic that opens/closes the shell without starting tools when used without --autostart. Use an isolated extracted Portable or a separate Windows user profile, never the owner's live data directory for automated audit. --dns-recovery --verify-only opens the independent recovery window without restoring DNS.

Packaging refuses to overwrite an existing artifact set. It checks stable metadata and creates Standard EXE and Portable ZIP plus bilingual README/release notes, license, notices and upgrade/recovery instructions. SHA256SUMS covers all packaged files except itself. Portable includes empty Data/Modules and the marker; no developer profiles, private logs or downloaded utilities are copied. Do not copy Safe Preview or test output into public Stable assets.

GitHub Windows CI checks public files, builds/runs the safe harness, verifies Preview and the read-only recovery window, and packages artifacts. It does not publish a Release or tag. Hosted runs normally omit private optional archive/legacy fixtures, so counts can differ from local runs. CI uses read-only repository permissions.

Build from a clean committed tree. The EXE's ProductVersion contains 0.4.0 plus the source revision; compare it to the intended commit and record the full SHA in the handoff. A merge commit or different SDK/line endings may change binaries and checksums. Never silently substitute a hosted binary for the locally tested artifact. Checksums prove integrity only, not independent publisher identity; no Authenticode signing identity is configured.

The final audit and actual check results are recorded in STABLE_040_AUDIT_RU.md and the local candidate handoff. Historical alpha/pre-release documents remain development history and are not current user instructions. main, published v0.3.3, tags and release assets must remain unchanged until separately approved.