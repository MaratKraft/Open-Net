# IPv4 DNS and adapter DoH — experimental validation

The initial backend detected adapter DoH but did not serialize its properties, so it correctly refused writes that it could not restore. It also treated any registered system DoH template as active adapter encryption, and replaced specific capability failures with a generic baseline error. The updated backend separates these cases and preserves the specific reason.

## Capture and plan

On supported Windows, `GetInterfaceDnsSettings` version 3 is called with input Flags zero as documented. Its indexed server properties are copied before `FreeInterfaceDnsSettings`: server address, property version/type, template or automatic-template null, and 64-bit flags. IPv4 assignment/order is independently checked against the interface's static override; IPv6 assignment/order is captured separately. Adapter-specific DoH data is compared with the same interface's registry values, including DWORD/QWORD flags. Unknown fields, unsupported properties and disagreement are read-only failures.

The global catalog remains an immutable dependency, not evidence of an active adapter rule. Profile overrides, unverified IPv6 DoH, policies and unsupported global encryption modes remain blocked. The module does not edit the global catalog or registry.

Preset and saved custom profiles can explicitly select HTTPS DoH and plaintext fallback. The confirmation shows both original and planned addresses, templates and policy. Windows without native DoH support rejects the encrypted plan instead of silently downgrading it; plain DNS remains an explicit option. IPv6 DoH is not independently writable in this alpha. Catalog registration alone still does not establish active adapter encryption.

## Protected operation and restoration

After confirmation and UAC, the helper re-reads the adapter and verifies the approved fingerprint, ownership, request hash and capability. It saves the original modes, ordered addresses and encryption properties in the protected machine-local journal, then reads the record back and checks its identity, baseline, expected/target state and selected families before any write.

Manual encrypted IPv4 is written with `SetInterfaceDnsSettings` version 3 and only `DNS_SETTING_NAMESERVER | DNS_SETTING_DOH` (0x1002). ServerIndex maps each DoH property to the ordered address list. DNS and its encryption properties are submitted in the same scoped API call; unrelated fields are zero. No global template registration, firewall, routes or IPv6 reset is performed. Optional cache clearing runs only after verified DNS completion and reports failure separately.

Readback verifies modes, address order, templates and flags. Controlled DoH is part of the snapshot fingerprint, not an immutable dependency, so an intended template change does not invalidate its own recovery record. A third-party template or fallback change still blocks restoration.

Restore uses the protected original record, including Xbox settings. Operator availability metadata produces a warning, not a replacement for measured connectivity. Manual source is restored as manual with its exact DoH properties. Only an originally automatic source uses automatic restoration; unknown source is never replaced with DHCP. All families and dependencies are rechecked after each step; recovery intent survives incomplete operations.

## Read-only diagnostics and scope

The explicit Standard command `Open Net.exe --dns-diagnostic <adapter-GUID>` reads local configuration and validates the Comss IPv4 plan. It writes a private report under `test-results/dns-diagnostic` beside that EXE. It does not construct a writer, run probes, request UAC or create a protected apply journal. Reports contain private adapter/machine information and must not be committed or copied to public packages. Safe Preview rejects this command before initialization and retains fake readers/writers/probes.

Automated coverage includes encrypted Xbox → Comss → exact restoration after restart; native property marshalling/indexes/flags; backup readback failure before writes; rejection of downgrades and substituted templates; external template changes; and real UI event simulation. These tests do not execute the real setter. A separately approved real Windows test is still required; API success is not proof of service reachability or a working account.

Sources: [GetInterfaceDnsSettings](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-getinterfacednssettings), [SetInterfaceDnsSettings](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-setinterfacednssettings), [DNS_INTERFACE_SETTINGS3](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/ns-netioapi-dns_interface_settings3), [DNS_SERVER_PROPERTY](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/ns-netioapi-dns_server_property), [DNS_DOH_SERVER_SETTINGS](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/ns-netioapi-dns_doh_server_settings), [Comss Windows instructions](https://www.comss.ru/page.php?id=11212).

## Managed sessions and automatic assignment

The first original survives successive profiles and Automatic DNS. Each operation persists its current checkpoint, selected families and pending intent before writing. A failed apply compensates to the preceding managed state only when current configuration matches recorded intent; foreign changes prevent overwriting. Restore always returns the first original, even after process restart.

Automatic DNS independently switches IPv4 DNS assignment to DHCP using a fixed System-directory netsh command for the GUID-resolved interface index. It does not switch IP assignment, renew a lease, remove global templates or change IPv6. Original manual ordered DNS and adapter DoH remain recoverable. The actual Windows behavior must still be validated in a separately approved trial. Stale encryption rules for addresses no longer assigned are not considered an active binding.

Offline recovery uses the same protected journal and helper through a separate entry point without constructing the main Shell or requiring internet. A read-only snapshot with a checksum documents the pretrial configuration; it is not accepted as arbitrary elevated write instructions. The authoritative journal is created and read back under UAC before the first actual write.
