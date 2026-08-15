# Version History

## Current version: 0.3.0

Public **development preview**. Hardware writes go through an unsigned session broker
after one UAC prompt. No model is `Supported` yet. See
[`docs/SUPPORTED_MODELS.md`](docs/SUPPORTED_MODELS.md).

### Requirements

- Windows 10 version 1809 or later, plus the .NET 10 Desktop Runtime
- Lenovo Energy Management / EnergyDrv on machines that expose battery and input IOCTLs
- Administrator approval once per app session for the elevated broker

Linux is not a hardware target. The Linux artifact is portable Domain/Application
libraries. See [`docs/LINUX.md`](docs/LINUX.md).

### Recorded machine

| Machine | Type | BIOS | Lighting | Notes |
| --- | --- | --- | --- | --- |
| LOQ 15IRX9 | 83DV | NECN50WW | 4-zone ITE `048D:C993` | Unelevated Lenovo WMI getters return AccessDenied; privileged reads and writes use the broker. Not Spectrum. |

### Controls on 83DV / NECN50WW

| Control | Status on this BIOS |
| --- | --- |
| Identity (manufacturer, model, type, BIOS) | Observed, serial-free |
| Thermal mode | Brokered apply |
| Display overdrive | Brokered apply |
| Integrated GPU mode | Brokered apply |
| Battery charge mode | Brokered apply |
| Overnight charge, Always-on USB, Fn lock | Brokered apply when the EnergyDrv selectors exist |
| Touchpad lock, Win-key lock | Brokered apply when GameZone `IsSupport*` is true |
| 4-zone lighting | Brokered apply on ITE `C993` |
| Fan table | Bounded `Fan_Set_Table` plus Restore OEM; no full-speed method on this BIOS |
| Spectrum per-key RGB | Hidden; this machine has no 960-byte Spectrum collection |
| Custom thermal / ITS power limits | Not implemented |
| GPU gauges | Not implemented until a typed GPU reader exists |

### All other models

Unverified. Older documents that listed broad Legion, LOQ, or IdeaPad support were not
backed by repository evidence and are withdrawn. A metadata scan is not a support claim.

### Known limits

- Public zips are unsigned. Production mode refuses that layout.
- Keep Vantage and other Lenovo utilities closed if they fight the same WMI/HID/EnergyDrv
  surfaces.
- Clicking Apply uses the live firmware capture; a manual Refresh is not required.
