# Read-only Diagnostics

The diagnostics CLI has two deliberately separate read-only paths:

- `inventory` (default) creates a redacted capability snapshot without invoking Lenovo WMI
  methods, opening HID devices, or sending hardware writes;
- `state` invokes only three fixed Lenovo `Get*` methods and returns typed results. It has no
  setter names, caller-provided WMI identifiers, Energy driver access, or HID access.

## Run inventory

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- inventory
```

Run inventory as a normal user. Omitting the command selects `inventory`. JSON is written
to standard output; stable failures are written to standard error. Press Ctrl+C to cancel.

The inventory contains:

- manufacturer, product name, model, machine type, and BIOS version observations;
- capability, support state, source, timestamp, and stable evidence code;
- redacted exception type only when an inventory source fails.

It does not query or serialize serial numbers, usernames, device paths, or account data.
Evidence fixture tests reject those fields.

## Probe typed state

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- state
```

The state probe reads, sequentially:

- battery charge mode: currently `Unavailable` because the Energy driver read adapter has
  not been implemented;
- thermal mode: `GetSmartFanMode`;
- display overdrive: `GetODStatus`;
- integrated-GPU mode: `GetIGPUModeStatus`.

Each result is `Success`, `Unsupported`, `AccessDenied`, `Unavailable`, `InvalidData`,
`Failed`, or `TimedOut`. A failed read never becomes `Off`, zero, or a default mode.
Unexpected firmware values are rejected instead of cast into an enum.

On LOQ 15IRX9 machine type `83DV`, BIOS `NECN50WW`, the Lenovo WMI provider rejects
the instance query needed by these getters in a normal-user process, before method
invocation. This is emitted as `AccessDenied`. An elevated run may be used as an explicit
manual read-only validation step. The production UI remains unelevated; this command is
not a substitute for the planned short-lived broker.

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

## Submitting an inventory fixture

1. Run the `inventory` command unelevated.
2. Review every field for personal information.
3. Record the exact machine type, model, and BIOS.
4. Keep candidate states as `Unknown`; do not promote them manually.
5. Add the redacted JSON under `hardware-evidence/<machine-type>/<bios>.json`.
6. Run the full build and test commands from [DEVELOPMENT.md](DEVELOPMENT.md).
