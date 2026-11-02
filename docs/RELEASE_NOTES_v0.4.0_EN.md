# Open Net v0.4.0

Open Net 0.4.0 adds a configurable modular workspace and a compact DNS Manager while preserving the approved dark/light interface.

- Show, hide and reorder modules independently of running state, group control and Windows startup.
- Assign the main button to a group or a module; quick profiles appear alongside status.
- Install compatible official Zapret/TG releases. ALT13 launch selection and asynchronous controls are corrected. Verified TG parent/child processes are treated as one instance; external instances use the existing switch with confirmation.
- Configure IPv4 DNS on one physical adapter, use custom DNS, inspect hosts conflicts and clear DNS cache separately. Off returns automatic IPv4 DNS. Protected original settings remain available through explicit offline recovery.
- Discover newer official releases with Stable as default and optional Pre-release. Open release opens GitHub; automatic self-installation is not included.
- Keep v0.3.3 settings and installed modules in the existing official data directory, with a backup before legacy settings conversion. The first-run language follows Windows UI language (Russian or English); saved choices take precedence.

The owner reports successful pre.3 tests on the main PC and several previously affected laptops, including YouTube, TG, external instances and switches. Stable adds narrow audit fixes for damaged update caches and asynchronous/bounded helper waits, plus official metadata and documentation; it therefore receives a fresh isolated test pass.

Known limits: no Authenticode publisher signature; checksums provide integrity only. Physical Windows 10 and every OS/DPI/network combination have not been certified. Recovery is machine-local; unusual encrypted DNS/policies and ambiguous/service-managed processes are refused. After shell restart, surviving tools may require external-instance confirmation. External TG validation needs GitHub. Memory usage is not reduced to a promised 100–120 MB. Automatic application installation and old-version cleanup remain deferred. Network tools are not bundled.

Internal pre.1/pre.2/pre.3 candidates were not public Releases. v0.3.3 remains unchanged. This prepared file does not itself publish v0.4.0.