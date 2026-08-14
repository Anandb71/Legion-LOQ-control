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

## Completed: profile preview

Profiles now begin as local, read-only plans. The profile workspace:

1. model bounded battery and thermal targets;
2. compare a draft with the latest typed hardware snapshot;
3. distinguish matching, would-change, unavailable, stale, and unverified states;
4. persist versioned local drafts with strict validation; and
5. expose save, delete, and preview actions only.

There is no Apply action, profile-triggered elevation, hardware write, or background
automation.

## Completed: automation preview

Automation now starts as deterministic rule evaluation against read-only AC/battery
observations. The automation workspace:

1. observes the current power source through `GetSystemPowerStatus` without elevation;
2. persists bounded, versioned rules with strict JSON validation;
3. selects only one unique highest-priority rule for the observed source;
4. distinguishes unavailable, stale, unmatched, ambiguous, disabled, and missing-profile
   outcomes; and
5. exposes local new, save, delete, refresh, and preview actions only.

There is no watcher, scheduler, background runner, profile application, broker call, or
hardware write.

## Current milestone: release-grade read-only foundation

CI, broker-free preview packaging, accessibility contracts, diagnostics export, and release
documentation are now in place. Cross-process local-store integrity uses exclusive file
locks so two app instances cannot silently overwrite drafts. Remaining release-foundation
work covers broker signing, install-ACL design, and supported Windows/scaling validation.
These gates must create a useful read-only release without weakening the write-path
boundary.

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
- redacted intent/result journaling; and
- crash reconciliation that never blindly replays a command.

The first writable feature will be selected from the best-evidenced bounded protocols. No
feature is authorized merely because a legacy implementation exists.

## Expansion and release

After a write slice passes its gate, later milestones can add additional validated controls,
profile application, opt-in automation execution, packaging, updates, accessibility tests,
and model support. Fan curves, power limits, firmware flashing, and unattended privileged
services remain out of scope until they have their own stronger recovery and validation
designs.

