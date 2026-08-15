# Safety Policy

Hardware integrity takes priority over feature availability. Unknown, stale, malformed, or
failed state is never treated as a usable default.

## Current rebuild status

The rebuild is write-gated. Inventory, refresh, preview, and export stay read-only:

- the GUI runs as the current user (`asInvoker`), not as administrator;
- the dashboard can apply thermal mode, display overdrive, integrated-GPU mode,
  battery charge mode, and 4-zone keyboard brightness only after an explicit click and a
  UAC prompt;
- each apply is one typed broker write with a fresh expected-state check and readback;
- custom thermal mode, fan tables, and per-zone RGB color writes remain disabled;
- `HardwareWritePolicy` in the quarantined Core assembly still has no unlock path;
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
  zero requested device access) and one typed EnergyDrv battery write using only
  selectors `0x03`, `0x05`, `0x07`, and `0x08`;
- the elevated broker may run one allowlisted WMI setter (`SetSmartFanMode`,
  `SetODStatus`, or `SetIGPUModeStatus`) or the typed battery write when launched with
  `--write`;
- the elevated broker may open one allowlisted ITE HID collection (`048D` + `C935` /
  `C955` / `C993`, 33-byte feature report) for 4-zone brightness; it still has no legacy
  Core reference, generic IOCTL entry point, or caller-supplied WMI names;
- profile drafts use a bounded, strict, versioned local JSON store with a
  cross-process file lock and compare only against retained typed snapshots and
  capability evidence;
- the profile workspace exposes new, save, delete, and preview commands but no Apply
  command, broker call, or automation runner;
- AC/battery automation observes one typed `GetSystemPowerStatus` result, then performs
  deterministic in-memory rule evaluation;
- automation rules use a separate bounded, strict, versioned local JSON store, and the
  workspace has no watcher, scheduler, execute command, broker call, or profile Apply path;
- diagnostics export uses an explicit versioned allowlist, omits local drafts and dynamic
  detail fields, and writes only the typed snapshots already retained by the session;
- exporting diagnostics never starts a hardware read, launches the broker, requests
  elevation, or performs a hardware write;
- unit tests verify that battery, thermal, fan, and keyboard commands fail closed.

The broker may apply the allowlisted WMI setters, the typed battery-mode write, and the
typed 4-zone brightness packet after an explicit UAC prompt. That is not authorization
for fan tables, per-zone color editors, or unsigned production installs. Development mode may launch an unsigned sibling broker after an
explicit UAC prompt. Production mode refuses that launch unless the sibling directory is
administrator-protected and the broker is Authenticode-signed. See
[`docs/BROKER_INSTALL.md`](docs/BROKER_INSTALL.md).
Current device detection is only a candidate-device heuristic. It does
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
- Creating, editing, saving, deleting, or previewing a profile never launches the broker.
- Creating, editing, saving, deleting, refreshing, or previewing an automation rule never
  launches the broker or applies its target profile.
- Exporting diagnostics never refreshes inventory or hardware state and never includes
  local profile or automation-rule data.
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

The delivery order for profile preview, automation preview, and the write-path gate is
defined in [`docs/ROADMAP.md`](docs/ROADMAP.md).
