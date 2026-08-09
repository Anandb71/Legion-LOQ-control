# Safety Policy

Hardware integrity takes priority over feature availability. Unknown, stale, malformed, or
failed state is never treated as a usable default.

## Current rebuild status

The C# migration prototype is deliberately **read-only**:

- the GUI runs as the current user (`asInvoker`), not as administrator;
- write controls are disabled and have no event handlers;
- `HardwareWritePolicy` has no unlock path;
- legacy or write-capable EnergyDrv, WMI mutation, and HID feature-write entry points
  reject commands before opening a driver or selecting a device;
- default inventory invokes no Lenovo methods and opens no HID devices;
- the optional state diagnostic invokes only `GetSmartFanMode`, `GetODStatus`, and
  `GetIGPUModeStatus`, with fixed identifiers, bounded execution, and typed failures;
- those getters are batched through the exact system Windows PowerShell executable with
  profiles disabled, a system-only module path, static script text, capped output, and a
  12-second process timeout;
- the optional elevated state path uses a one-request, 30-second broker with strict framing,
  current-user ACL, mutual process-ID checks, a one-time nonce, and anonymous client
  impersonation;
- the elevated broker has one fixed EnergyDrv battery read (`0x831020F8`, selector `0xFF`,
  zero requested device access) and no caller-controlled or generic driver operation;
- the elevated broker has no write message type, write dispatcher, legacy Core reference,
  writable EnergyDrv handle, or HID access;
- unit tests verify that battery, thermal, fan, and keyboard commands fail closed.

The read-only broker is a validation milestone, not authorization for writes. It is not
production-ready until the executable is signed and installed under administrator-only
filesystem ACLs. Current device detection is only a candidate-device heuristic. It does
**not** authorize hardware writes or prove feature compatibility. A getter name is not
assumed harmless without allowlisting and provenance.

## Required privileged architecture

Hardware writes may return only after all of these controls exist and pass review:

1. An unelevated UI and CLI with no direct privileged WMI, EnergyDrv, or HID access.
2. A short-lived elevated broker installed under administrator-only filesystem ACLs for
   privileged reads and validated writes.
3. A versioned, local-only, typed command protocol. Raw WMI names, IOCTL values, HID
   packets, paths, and plugin-defined commands are forbidden at the IPC boundary.
4. Independent broker-side device and per-feature capability validation.
5. Machine-wide serialization for each hardware domain.
6. Fresh read, expected-state comparison, bounded write, and readback verification.
7. Typed `Unsupported`, `Unknown`, `Busy`, `Conflict`, `Unverified`, and `Failed` results.
8. A redacted intent/result journal and crash reconciliation that never blindly replays a
   command.
9. Fake/replay tests and exact model/BIOS hardware evidence.

## Explicit intent

- No hardware write runs during startup, refresh, state hydration, shutdown, or migration.
- UI binding changes are not user intent.
- Profiles show a dry-run preview before their first application.
- Ambiguous timeout or readback failure suspends automation; it is not retried blindly.
- Real-hardware tests require an exact machine/BIOS record and explicit confirmation for
  each feature.

## Prohibited until validated

- arbitrary fan tables or power-limit values;
- persistent firmware tuning without a proven OEM-safe reset path;
- background privileged services;
- broker-loaded plugins or scripts;
- unattended BIOS flashing;
- writes on unsupported or unknown models and BIOS revisions.

## Protocol scope

The project uses Windows WMI, vendor-installed Lenovo drivers, and HID interfaces. These
interfaces can still trigger firmware behavior and are not safe merely because they are
accessible through standard Windows APIs. Every protocol implementation must have
provenance, bounded inputs, fixtures, and hardware evidence as described in
[`docs/PROVENANCE.md`](docs/PROVENANCE.md).
