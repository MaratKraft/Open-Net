# Disabled DoH readback and interrupted application

On Windows 11, applying plain DNS after encrypted DNS can leave adapter entries
with `DohFlags=0` and no template while `GetInterfaceDnsSettings` returns no server
properties. R6 compared these representations literally, blocked inspection, and
could leave a durable write intent awaiting readback.

R7 compares an empty, disabled property with omission. Raw properties remain in
backups. Active encryption, stored templates, unknown flags, adapter identity,
policies, DNS ordering and IPv6 still require exact agreement.

Explicit repeat may reconcile a Writing record only when its entire saved target
and its pending-family destination match a fresh, changeable snapshot. It verifies
the saved checkpoint before another write, preserves the first baseline/session,
and records both the recovered readback and the repeat. Restoring, rolling-back,
partial, foreign, uninspectable or wrong-owner records remain blocked.

Validation: 62 isolated headless checks and 83 interactive checks passed. New checks
cover disabled-property equivalence, meaningful encryption mismatches, interrupted
readback followed by repeat/restoration, foreign owner and partial destinations.

An owner-authorized real repeat of the currently assigned plain ComssDNS on one
physical Wi-Fi adapter produced one accepted and verified Windows write. Independent
before/after DNS, native API, adapter registry, interface encryption and global
catalog snapshots agreed. The original Google DNS baseline and session identity
were preserved. No cache clearing, IPv6 write or network-tool restart was requested.
The independent offline recovery window opened, found the protected record and
enabled restoration. No real restore was performed in this trial. HTTPS/service
availability and a network-state refresh are separate, unproven outcomes.
