# Safety Policy

Hardware integrity takes priority over feature availability. Unknown, stale, malformed, or
failed state is never treated as a usable default.

## Current rebuild status

The rebuild is write-gated. Inventory, refresh, preview, and export stay read-only:

- the GUI runs as the current user (`asInvoker`), not as administrator;
- the dashboard can apply thermal mode, display overdrive, integrated-GPU mode,
  battery charge mode, 4-zone brightness and lighting, overnight charge, Fn lock,
  Always-on USB, touchpad lock, Win-key lock, a bounded fan table, and Spectrum
  brightness only after an explicit click through the session broker;
- unsupported hardware is omitted, not shown as a dead switch;
- each apply is one typed broker write, or one battery-then-thermal batch; the broker
  captures live firmware first and uses that as the expected state, then reads back;
  those write captures skip `Fan_Get_Table` unless the target is the fan table;
  a stale dashboard paint is not a conflict gate;
- the elevated broker may read `LENOVO_FAN_METHOD.Fan_Get_Table` with FanID `0` and
  SensorID `0`, persist the first successful table as a local OEM snapshot, and apply
  a bounded `Fan_Set_Table` plus Restore OEM; full-speed methods stay omitted when
  absent;
- custom thermal mode and arbitrary power-limit sliders remain disabled;
- `HardwareWritePolicy` in the quarantined Core assembly still has no unlock path;
- legacy or write-capable EnergyDrv, WMI mutation, and HID feature-write entry points
  reject commands before opening a driver or selecting a device;
- default inventory invokes no Lenovo methods and opens no HID devices;
- the optional state diagnostic invokes only `GetSmartFanMode`, `GetODStatus`, and
  `GetIGPUModeStatus`, with fixed identifiers, bounded execution, and typed failures;
- those getters run in-process through CIM (`Microsoft.Management.Infrastructure` on
  the inbox Windows MI stack) with fixed class and method names, Boolean status,
  UInt32 data, and a 5-second bound;
- the optional elevated state path uses a session-lived broker: Windows asks once at
  connect, later reads and writes reuse the same pipe, each request is bounded (45
  seconds for reads, 90 seconds for writes), and the broker exits when the parent
  process exits; it is not a SYSTEM service or scheduled task;
- one-shot `--write` and default read launches remain available for diagnostics; the
  dashboard always launches `--session`;
- the session uses strict framing, a current-user ACL, mutual process-ID checks, a
  connect-time nonce, and anonymous client impersonation;
- the elevated broker has one fixed EnergyDrv battery read (`0x831020F8`, selector `0xFF`,
  zero requested device access) and one typed EnergyDrv battery write using only
  selectors `0x03`, `0x05`, `0x07`, and `0x08`;
- the elevated broker may also use EnergyDrv settings (`0x831020E8`) for Fn lock and
  Always-on USB, and night-charge (`0x83102150`) for overnight charge, with fixed
  selectors only;
- the elevated broker may run one or two allowlisted setters on a session write or a
  one-shot `--write` launch (battery then thermal in one request, or a single dashboard
  target);
- the elevated broker may open one allowlisted ITE HID collection (`048D` + `C935` /
  `C955` / `C993`, 33-byte feature report) for 4-zone effect, speed, brightness, and
  zone colors; a separate Spectrum path opens only a 960-byte C9xx collection that is
  not a 4-zone PID; it still has no legacy Core reference, generic IOCTL entry point,
  or caller-supplied WMI names;
- the elevated path may invoke `Fan_Get_Table` and a bounded `Fan_Set_Table` on
  `LENOVO_FAN_METHOD` with fixed zero identifiers; it never invents a full-speed
  method when the BIOS does not expose one;
- profile drafts use a bounded, strict, versioned local JSON store with a
  cross-process file lock and compare only against retained typed snapshots and
  capability evidence;
- the profile workspace can apply a would-change preview through one session-broker
  batch; preview, save, and delete still do not start a write;
- AC/battery automation observes one typed `GetSystemPowerStatus` result, then performs
  deterministic in-memory rule evaluation;
- the dashboard may also poll unelevated CPU (`GetSystemTimes`), RAM
  (`GlobalMemoryStatusEx`), and system-disk space (`GetDiskFreeSpaceEx`); those
  reads never open Lenovo WMI, EnergyDrv, or HID;
- an opt-in in-process watcher may apply the winning profile through that same session
  broker, cools down after an attempt, and suspends after a failed readback;
- automation rules use a separate bounded, strict, versioned local JSON store; there is
  no SYSTEM service, scheduled task, or write on launch;
- diagnostics export uses an explicit versioned allowlist, omits local drafts and dynamic
  detail fields, and writes only the typed snapshots already retained by the session;
- exporting diagnostics never starts a hardware read, launches the broker, requests
  elevation, or performs a hardware write;
- unit tests verify that battery, thermal, fan, and keyboard commands fail closed.

The broker may apply the allowlisted WMI setters, typed EnergyDrv features, the bounded
fan table, and the typed 4-zone or Spectrum HID packets after the session is elevated.
That is not authorization for unsigned production installs. The public 0.3.0 zip includes
the unsigned sibling broker and stays in development mode. Development mode may launch an
unsigned sibling broker after an
explicit UAC prompt. Production mode refuses that launch unless the sibling directory is
administrator-protected and the broker is Authenticode-signed. See
[`docs/BROKER_INSTALL.md`](docs/BROKER_INSTALL.md).
Current device detection is only a candidate-device heuristic. It does
**not** authorize hardware writes or prove feature compatibility. A getter name is not
assumed harmless without allowlisting and provenance.

## Required privileged architecture

Hardware writes may return only after all of these controls exist and pass review:

1. An unelevated UI and CLI with no direct privileged WMI, EnergyDrv, or HID access.
2. A session-lived elevated broker installed under administrator-only filesystem ACLs for
   privileged reads and validated writes. It exits with the unelevated parent.
3. A versioned, local-only, typed command protocol. Raw WMI names, IOCTL values, HID
   packets, paths, and plugin-defined commands are forbidden at the IPC boundary.
4. Independent broker-side device and per-feature capability validation.
5. Machine-wide serialization for each hardware domain.
6. Fresh privileged read as the expected state, bounded write, and readback verification.
7. Typed `Unsupported`, `Unknown`, `Busy`, `Conflict`, `Unverified`, and `Failed` results.
8. A redacted intent/result journal and crash reconciliation that never blindly replays a
   command.
9. Fake/replay tests and exact model/BIOS hardware evidence.

## Explicit intent

- No hardware write runs during startup, refresh, state hydration, shutdown, or migration.
- UI binding changes are not user intent.
- Creating, editing, saving, deleting, or previewing a profile never launches the broker.
  Apply is a separate explicit action.
- Creating, editing, saving, deleting, refreshing, or previewing an automation rule never
  launches the broker or applies its target profile. Starting the session watcher is a
  separate explicit action; each later apply still requires Windows approval.
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
