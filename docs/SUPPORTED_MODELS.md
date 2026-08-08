# Model Support and Evidence

## Current release gate

No model is approved for hardware writes. The rebuild is read-only, and every write
control remains disabled.

Evidence terms:

- **Observed**: a non-sensitive identity value was read.
- **Candidate**: expected interface metadata exists, but behavior and safety are unverified.
- **Unsupported**: the expected interface was not found in that scan.
- **Supported**: protocol behavior and recovery were verified on the exact model/BIOS with
  approved tests. No feature has reached this level yet.

## Recorded machines

### LOQ 15IRX9 — machine type 83DV — BIOS NECN50WW

Captured read-only on 2026-08-08:

- manufacturer, model, machine type, and BIOS were observed;
- battery mode, thermal mode, fan table, white keyboard, display overdrive, hybrid graphics,
  and GPU mode WMI method sets were present but remain unverified candidates;
- the fan interface exposes table methods, not the legacy prototype's full-speed methods;
- known 4-zone and Spectrum HID product IDs were not found;
- no Lenovo WMI method was invoked and no HID device was opened;
- no write behavior was tested.

The redacted machine record is
[`hardware-evidence/83DV/NECN50WW.json`](../hardware-evidence/83DV/NECN50WW.json).

## All other models

Unverified. Older documentation that listed broad Legion, LOQ, or IdeaPad support was not
backed by repository evidence and has been withdrawn.

## Contributing evidence

Run:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release
```

Before sharing output, confirm it contains no serial number, username, device path, or
account data. Submit the model, machine type, BIOS version, output, and whether the scan
completed unelevated. A metadata scan does not authorize hardware-write testing.
