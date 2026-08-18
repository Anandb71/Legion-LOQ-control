# Rebuild Roadmap

The rebuild advances in small, testable slices. Safety gates are release criteria, not
optional follow-up work.

## Completed foundation

- .NET 10 layered solution with locked dependencies
- serial-free identity and capability evidence
- typed battery, thermal, display-overdrive, and integrated-GPU reads
- short-lived authenticated read-only elevation broker
- exact zero-access EnergyDrv battery-mode read
- unelevated precision dashboard with an explicit brokered refresh
- strict local battery and thermal drafts with typed, fail-closed previews
- deterministic AC/battery automation previews with strict local rule storage
- versioned, allowlisted diagnostics reports with atomic export of retained snapshots
- protocol provenance, redacted hardware fixtures, and fail-closed tests

## Completed: profile apply

Profiles remain local drafts until the user applies a would-change preview. The profile
workspace:

1. models bounded battery and thermal targets;
2. compares a draft with the latest typed hardware snapshot;
3. treats a fresh typed read as enough evidence to plan a change when the capability is
   not `Unsupported`;
4. persists versioned local drafts with strict validation; and
5. applies only would-change battery and thermal targets in one session-broker batch.

Custom thermal mode stays non-writable. Empty plans do not launch the broker.

## Completed: opt-in automation

Automation evaluates deterministic AC/battery rules, then can watch in this app session:

1. observes the current power source through `GetSystemPowerStatus` without elevation;
2. persists bounded, versioned rules with strict JSON validation;
3. selects only one unique highest-priority rule for the observed source;
4. applies the winning profile through the same session-broker batch used by profile Apply;
5. cools down after an apply or cancelled elevation; and
6. suspends after a failed readback until the user resumes.

There is no SYSTEM service, scheduled task, or write on launch. Starting the watcher is
an explicit click.

## Current milestone: Vantage-class pages, no ads

The dashboard remains the live instrument. POWER, INPUT, LIGHTING, DISPLAY, and DEVICE
are first-class tabs. Clicks rebase onto a fresh privileged capture. Unsupported
hardware is omitted. This LOQ's ITE controller is `048D:C993` (4-zone, not Spectrum).
The BIOS exposes `Fan_Set_Table` and no full-speed methods. Overnight charge, Fn lock,
Always-on USB, touchpad lock, and Win-key lock are capability-gated. The first
successful fan-table read persists a local OEM snapshot so Restore OEM exists before
the first curve write.

## Vantage replacement map

This app replaces Vantage controls that have typed protocol evidence. It does not copy
Vantage chrome, ads, Lena, warranty upsell, or serial numbers.

| Vantage surface | Here now | Next evidenced slice |
| --- | --- | --- |
| Home device identity | DEVICE tab: manufacturer, model, type, BIOS | No serial, product number, or Device ID |
| Home / Power battery | POWER: live %, AC, Normal / Conservation / Rapid, overnight, Always-on USB | No invented 75/80% conservation graph |
| Performance thermal / GPU | Quiet–Extreme, GPU working mode, overdrive, live CPU/RAM/disk | GPU gauges only after a typed GPU reader exists |
| Keyboard lighting | LIGHTING: 4-zone effect, speed, zone colors | Spectrum section only on 960-byte HID |
| Fans | Bounded `Fan_Set_Table` plus Restore OEM | No invented full-speed on 83DV |
| Input | INPUT: Fn lock, touchpad lock, Win key | Gestures and widgets stay out |
| Sound / Nahimic / scan | Not present | No invented audio or diagnostic writers |
| Profiles / automation | Local drafts and AC/battery watcher | Process and hotkey sources after the rule model is proven |

## Previous milestone: release-grade read-only foundation

CI, broker-free preview packaging, accessibility contracts, diagnostics export, and release
documentation are now in place. Cross-process local-store integrity uses exclusive file
locks so two app instances cannot silently overwrite drafts. Broker install policy now
separates unsigned sibling development launches from administrator-protected, signed
production installs. Remaining release-foundation work covers obtaining those signatures,
shipping a protected installer, and supported Windows/scaling validation.
These gates must create a useful read-only release without weakening the write-path
boundary.

Verified 2026-08-18: broker-free CI preview starts the unelevated window. Verified
2026-08-19: current-option chips match either the visible label or the firmware token.

Process, temperature, time, resume, and hotkey sources follow only after the rule model,
precedence, cooldown, stale-data handling, and audit output are tested.

## Write-path gate

A hardware control becomes eligible for implementation only when all requirements in
[`SAFETY.md`](../SAFETY.md) are satisfied, including:

- a signed broker installed under administrator-only ACLs;
- a versioned typed command with no raw protocol fields;
- exact model/BIOS capability evidence;
- fresh read and expected-state comparison;
- machine-wide serialization;
- bounded execution and readback verification;
- redacted in-memory intent/result journaling; and
- crash reconciliation that never blindly replays a command.

Allowlisted writes are already in the session broker. No feature is authorized merely
because a legacy implementation exists.

## Expansion and release

0.3.0 is a public development preview: Windows zip with an unsigned session broker, plus
Linux portable libraries. Authenticode signing, a protected installer, and `Supported`
model/BIOS declarations remain later gates. ITS / custom thermal power limits,
firmware flashing, and unattended privileged services remain out of scope until they have
their own stronger recovery and validation designs.

