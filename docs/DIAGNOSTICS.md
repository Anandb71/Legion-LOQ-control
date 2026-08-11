# Read-only Diagnostics

The diagnostics CLI has three deliberately separate read-only paths:

- `inventory` (default) creates a redacted capability snapshot without invoking Lenovo WMI
  methods, opening HID devices, or sending hardware writes;
- `state` invokes only three fixed Lenovo `Get*` methods and returns typed results. It has no
  setter names, caller-provided WMI identifiers, Energy driver access, or HID access;
- `state-elevated` sends the same typed request to a short-lived UAC broker, which adds one
  fixed EnergyDrv battery read with zero requested device access.

The dashboard can also export the inventory and latest retained hardware snapshot. Export
does not collect fresh data, launch the broker, or request elevation.

## Run inventory

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- inventory
```

Run inventory as a normal user. Omitting the command selects `inventory`. JSON is written
to standard output; stable failures are written to standard error. Press Ctrl+C to cancel.
Inventory output uses the same explicit, versioned allowlist as the dashboard export
instead of serializing domain objects by reflection.

The inventory contains:

- report schema and product version;
- manufacturer, product name, model, machine type, and BIOS version observations;
- capability, support state, timestamp, and stable evidence code;
- an explicit `notCaptured` hardware-state marker; and
- declarations that export triggered no hardware read or write and included no drafts or
  raw device paths.

It does not query or serialize serial numbers, usernames, device paths, account data,
exception details, dynamic source strings, local profiles, or automation rules. Export
tests inspect the serialized bytes so future domain properties do not enter the report
automatically.

## Export from the dashboard

After inventory completes, select **Export diagnostics** in the Capability evidence
section. The standard Windows save dialog writes a bounded JSON document through a
same-directory temporary file and atomic rename.

The export captures only the two typed snapshots already retained by the current session:

- serial-free inventory is always present;
- hardware state is included only after a successful explicit brokered read; and
- otherwise hardware state remains explicitly `notCaptured`.

Cancelling the dialog creates no file. A failed or cancelled write leaves an existing
destination untouched. The UI never displays the destination path in status or error
messages. Model, machine type, BIOS, and timestamps can still fingerprint a device, so
review the JSON before sharing it.

## Probe typed state

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- state
```

The state probe reads, sequentially:

- battery charge mode: `Unavailable` in the unelevated path; the broker uses only IOCTL
  `0x831020F8` with selector `0xFF` and maps bits `0x20`/`0x04`;
- thermal mode: `GetSmartFanMode`;
- display overdrive: `GetODStatus`;
- integrated-GPU mode: `GetIGPUModeStatus`.

Each result is `Success`, `Unsupported`, `AccessDenied`, `Unavailable`, `InvalidData`,
`Failed`, or `TimedOut`. A failed read never becomes `Off`, zero, or a default mode.
Unexpected firmware values are rejected instead of cast into an enum.

On LOQ 15IRX9 machine type `83DV`, BIOS `NECN50WW`, the Lenovo WMI provider rejects
the instance query needed by these getters in a normal-user process, before method
invocation. This is emitted as `AccessDenied`. An elevated run may be used as an explicit
manual read-only validation step.

## Probe through the read-only broker

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- state-elevated
```

This command explicitly displays a UAC prompt. The unelevated CLI creates a random,
single-instance pipe restricted to the current user, launches the sibling broker, verifies
the connected broker process ID, and exchanges one 64 KiB-bounded message. The broker
checks the parent process ID and one-time nonce, connects with anonymous impersonation,
executes only the fixed getters, returns typed wire values, and exits within a bounded
lifetime.

`PipeOptions.CurrentUserOnly` is deliberately not used because .NET also requires equal
elevation levels for that option. A specific current-user ACL permits the same split-token
account across the UAC boundary; mutual PID checks prevent an unrelated local process from
being accepted.

This remains a development validation path. The broker is unsigned and copied from the
local build output; production use is blocked on code signing and administrator-protected
installation ACLs. It has no write request type or write dispatcher.

On LOQ 15IRX9 `83DV`, BIOS `NECN50WW`, the broker validated Boolean-success/UInt32-data
responses for Performance thermal mode (raw `3`), disabled display overdrive (raw `0`),
and integrated-only GPU mode (raw `1`). It also observed Normal battery mode from the two
documented charge-mode bits using the exact EnergyDrv read contract. This is one exact
machine/BIOS observation, not a general support claim. The redacted record is
[`hardware-evidence/83DV/NECN50WW-state-elevated.json`](../hardware-evidence/83DV/NECN50WW-state-elevated.json).
The retained adapter starts the exact system `powershell.exe` with profiles disabled,
restricts module lookup to the system Windows PowerShell modules directory, runs one static
CIM script, validates Boolean status and UInt32 data, caps output at 64 KiB, and terminates
the process after 12 seconds. No script, class name, method name, or argument comes from the
caller.

`GetPowerChargeMode` is intentionally not used for battery mode. Protocol cross-checking
showed that it represents charging/power suitability, while conservation and rapid-charge
state use a separate Energy driver protocol.

## Interpreting inventory results

- `Unknown` plus `*_present_unverified`: a candidate interface exists; do not infer support.
- `Unsupported` plus `*_not_found` or `*_missing`: the expected interface was absent in the
  current inventory.
- `Supported`: reserved for future exact model/BIOS validation; current probes never emit it.
- `Failed` observations: the source failed and no substitute value was invented.

## Design limits

WMI metadata and getter behavior can vary by BIOS and Lenovo driver package. HID product
IDs do not identify all keyboard implementations. A successful inventory proves only that
inventory was readable. A successful state getter proves only that one read returned a
recognized value; it does not authorize a write or establish full model support.

## Preparing an inventory fixture

1. Run the `inventory` command unelevated or use **Export diagnostics**.
2. Review every field for personal information.
3. Record the exact machine type, model, and BIOS.
4. Keep candidate states as `Unknown`; do not promote them manually.
5. Convert the report into the repository's reviewed evidence-fixture schema; do not copy
   a support export into `hardware-evidence` verbatim.
6. Add the fixture under `hardware-evidence/<machine-type>/<bios>.json`.
7. Run the full build and test commands from [DEVELOPMENT.md](DEVELOPMENT.md).
