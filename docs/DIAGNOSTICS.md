# Read-only Diagnostics

The diagnostics collector creates a redacted capability snapshot without invoking Lenovo
WMI methods, opening HID devices, or sending hardware writes.

## Run

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release
```

Run it as a normal user. JSON is written to standard output; stable failures are written to
standard error. Press Ctrl+C to cancel.

## Output

The snapshot contains:

- manufacturer, product name, model, machine type, and BIOS version observations;
- capability, support state, source, timestamp, and stable evidence code;
- redacted exception type only when an inventory source fails.

It does not query or serialize serial numbers, usernames, device paths, or account data.
Evidence fixture tests reject those fields.

## Interpreting results

- `Unknown` plus `*_present_unverified`: a candidate interface exists; do not infer support.
- `Unsupported` plus `*_not_found` or `*_missing`: the expected interface was absent in the
  current inventory.
- `Supported`: reserved for future exact model/BIOS validation; current probes never emit it.
- `Failed` observations: the source failed and no substitute value was invented.

## Design limits

WMI metadata can vary by BIOS and Lenovo driver package. HID product IDs do not identify
all keyboard implementations. A successful scan proves only that inventory was readable.
It does not prove that Lenovo Vantage behavior can be reproduced or that writes are safe.

## Submitting a fixture

1. Run the collector unelevated.
2. Review every field for personal information.
3. Record the exact machine type, model, and BIOS.
4. Keep candidate states as `Unknown`; do not promote them manually.
5. Add the redacted JSON under `hardware-evidence/<machine-type>/<bios>.json`.
6. Run the full build and test commands from [DEVELOPMENT.md](DEVELOPMENT.md).
