# v0.3.3 release validation

## Approved source and owner acceptance

PR #3, `feature/autostart-tray-0.3.3`, application commit `f2b6c0c2c2f4d00cea82bccc819efd6825e0c72d` is the R8 source. All seven development commits were reviewed; the last fixes shared scrollbar minimum geometry for long text. The public branch and local source agreed. PR comments, inline comments and reviews contained no unresolved requests. Both hosted checks on this application commit succeeded.

On 2026-10-21 the owner explicitly confirmed personally testing R8 on their computer: startup after reboot, settings persistence, automatic selected tools, tray and updated UI. The owner approved PR #3 merge and Stable publication and stated that no further user testing is required. This is owner-reported physical acceptance, distinct from automated/stub tests; it is not a claim of testing every OS or network environment.

## Repeated technical checks

- Rebuilding the R8 source with SDK 10.0.100/runtime 10.0.12 produced an EXE byte-identical to the supplied R8: SHA-256 `bcade0996458420f0575094c13c9d8fe40a5ef0de5bc1c9939a2ae78cae9b1c0`, 75,615,260 bytes. The online restore completed without the earlier advisory-lookup warning.
- 21/21 safe headless checks and 82/82 interactive checks passed again during release preparation. These cover settings, stored choices, approval cancellation, startup/task readback and repair, single-instance/tray behavior, English/Russian, both themes, constrained settings height, and long scrollbars at rendered 100/125/150%. Real task registration and real network tools are not used by these suites.
- A separate production R8 copy with a preseeded isolated Dark/Solid profile was opened. All three startup options, the full Save button and compact layout were visible without standard-state vertical scrolling. Startup was off in that profile; the owner's settings, existing shell and network processes were left unchanged. The temporary shell was closed normally.
- Release preparation changes documentation, pictures and the Portable README's validation wording. Application source is unchanged from R8. The final build is made from the merged main state; assembly informational Git metadata can change EXE checksums even when application code is identical. Final artifact hashes, bundle/assembly comparison and public re-download results are recorded in the release verification report.
- Clean packaging keeps the same EXE in Standard and Portable, the marker, empty data/module folders, MIT text and eight third-party notices. The public-file guard, ZIP/bundle contents, selected credential patterns, image metadata, Markdown links and checksums are checked before upload.

## Startup behavior and compatibility review

Automatic tool startup runs only through the primary `--autostart` shell, after approval and a verified registration. It is a bounded one-shot action without downloads or retry loops. Installed selected TG and Zapret are considered independently; running/external/unknown processes are not duplicated or terminated. Manual launch opens the window and does not run this startup controller. Existing profiles stay opted out unless the owner selected otherwise.

Fresh installations preselect recommended startup preferences; registration still requires explicit approval and UAC. The visible per-user task uses InteractiveToken and HighestAvailable without saving a password, so the shell and tools run elevated. Shell-only startup uses Run. Repair is restricted to the same installation/user, checks the inspected task hash and backs up the old XML. Portable relocation requires explicit path repair/re-registration; another live installation's registration is not silently taken over. See [startup details](AUTOSTART_033_RU.md).

PR #2 and the `feature/dns-manager-0.4.0-alpha` branch are outside this release. DNS Manager source is absent; the stable main must contain only PR #3's approved application changes and release documentation. Previous tags and release assets are preserved and compared before/after publication.

## Screenshots and limits

New v0.3.3 main and settings images show actual application windows in Solid, using an isolated safe state. The settings PNG is an exact capture of the application's WPF content at 100%, including its current controls, rather than a drawn mockup; transparent native-frame bounds are cropped. Startup is shown off so the options remain visible without claiming a live registration. The main PNG combines the clean header and body of two native Solid captures of the same stopped state, omitting transient pointer/tooltip overlays; only a three-pixel capture edge is cropped. A dark neutral composition preserves the source scale; no UI text or control is redrawn. Sample tool state does not imply bundled components or a network connectivity test. Older v0.3.2 pictures are preserved, with no separate Dark/Light comparison added to the README.

Physical Windows 10, multi-monitor/DPI hardware and environments other than the owner's PC remain unverified. Rendered scale checks are not physical monitor tests. The release EXE is unsigned. Selected-pattern scans are not an exhaustive security or legal audit. Third-party notices, including the existing derivative-symbol permission limitation, are retained; MIT covers Open Net's own code.
