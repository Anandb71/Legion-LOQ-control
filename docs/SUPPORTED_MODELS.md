# Model Support and Evidence

## Current release gate

No model is approved for hardware writes. The rebuild is read-only, and every write
control remains disabled.

Capability evidence terms:

- **Observed**: a non-sensitive identity value was read.
- **Candidate**: expected interface metadata exists, but behavior and safety are unverified.
- **Unsupported**: the expected interface was not found in that inventory scan.
- **Supported**: protocol behavior and recovery were verified on the exact model/BIOS with
  approved tests. No feature has reached this level yet.

Typed state-read statuses are separate from capability evidence. For example,
`AccessDenied` means the provider rejected that execution context; it does not mean the
feature is unsupported.

## Recorded machines

### LOQ 15IRX9 — machine type 83DV — BIOS NECN50WW

Inventory captured read-only on 2026-08-08:

- manufacturer, model, machine type, and BIOS were observed;
- battery mode's Energy driver transport remains unverified;
- thermal mode, fan table, white keyboard, display overdrive, hybrid graphics, and GPU mode
  WMI method sets were present but remain unverified candidates;
- the fan interface exposes table methods, not the legacy prototype's full-speed methods;
- known 4-zone and Spectrum HID product IDs were not found;
- no Lenovo WMI method was invoked and no HID device was opened during inventory;
- no write behavior was tested.

A separate unelevated typed-state probe on the same machine found:

- the WMI instance query needed by `GetSmartFanMode`, `GetODStatus`, and
  `GetIGPUModeStatus` failed with access denied before method invocation;
- battery charge mode stayed `Unavailable` because no Energy driver read adapter exists;
- no setter, IOCTL, or HID report was sent.

An explicit UAC-assisted broker validation observed Boolean-success/UInt32-data responses
for Performance thermal mode (raw `3`), disabled overdrive (raw `0`), and integrated-only
GPU mode (raw `1`) through the retained built-in Windows PowerShell/CIM adapter. This
single observation does not promote the capabilities to `Supported`.

The redacted inventory record is
[`hardware-evidence/83DV/NECN50WW.json`](../hardware-evidence/83DV/NECN50WW.json).
The redacted state-read record is
[`hardware-evidence/83DV/NECN50WW-state-unelevated.json`](../hardware-evidence/83DV/NECN50WW-state-unelevated.json).
The redacted elevated validation record is
[`hardware-evidence/83DV/NECN50WW-state-elevated.json`](../hardware-evidence/83DV/NECN50WW-state-elevated.json).
The access-denied result is evidence about privilege behavior, not a successful feature
validation.

## All other models

Unverified. Older documentation that listed broad Legion, LOQ, or IdeaPad support was not
backed by repository evidence and has been withdrawn.

## Contributing evidence

Run:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- inventory
```

Before sharing output, confirm it contains no serial number, username, device path, or
account data. Submit the model, machine type, BIOS version, output, and whether the scan
completed unelevated. A metadata scan does not authorize hardware-write testing.
