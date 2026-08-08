# Source and Protocol Provenance

This document tracks where hardware behavior, protocol constants, and source code came
from. It is part of the release gate: a hardware feature cannot become supported until
its provenance and validation evidence are recorded.

## Baseline audit: 2026-08-08

The pre-rebuild C# prototype was published under MIT while several files explicitly
described themselves as matching or deriving from Lenovo Legion Toolkit (LLT), which is
GPL-3.0. The project is being relicensed to GPL-3.0 to correct that mismatch.

The original worktree, including unfinished white-keyboard support, is preserved on
`archive/pre-rebuild` at commit `f6a7d28`.

### Legacy files requiring replacement or explicit adaptation notices

| Local file | Existing provenance statement | Rebuild disposition |
|---|---|---|
| `LegionLoqControl.Core/Hardware/BatteryController.cs` | LLT battery command logic and IOCTL behavior | Quarantine; replace behind a typed battery state machine and record any adapted source |
| `LegionLoqControl.Core/Native/NativeMethods.cs` | LLT P/Invoke extension pattern | Replace with reviewed interop and contract tests |
| `LegionLoqControl.Core/Hardware/LightingController.cs` | LLT 4-zone structure and controller behavior | Rebuild behind a HID adapter with packet fixtures |
| `LegionLoqControl.Core/Hardware/SpectrumKeyboardController.cs` | LLT Spectrum protocol operations | Rebuild behind a HID adapter with per-device capability evidence |
| `LegionLoqControl.Core/Hardware/WhiteKeyboardController.cs` | LLT white-keyboard feature logic | Preserved only on the archive branch; reintroduce after state/error modeling |
| `LegionLoqControl.Core/System/Management/WMI.LenovoOtherMethod.cs` | LLT capability identifiers | Verify every identifier against independent observations or retain an explicit GPL adaptation notice |
| `LegionLoqControl.Core/System/Management/WMI.LenovoFanTableData.cs` | LLT-inspired parsing | Replace with typed validation and malformed-response fixtures |

No source from the active LLT team fork was copied into product code while repairing the
`LLT_Reference` submodule. The submodule update from `63c1730` to `6f19ef4` is
reference-only.

## Reference repositories

### Lenovo Legion Toolkit

- Historical repository:
  <https://github.com/BartoszCichecki/LenovoLegionToolkit>
- Active repository:
  <https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit>
- Local fork:
  <https://github.com/Anandb71/LenovoLegionToolkit>
- License: GPL-3.0 with an LLT-specific plugin exception.
- Current reference commit: `6f19ef48095a32afe439474a65e5b95cf8fa1b24`.
- Use: protocol comparison, behavioral regression research, compatibility evidence, and
  identification of model-specific edge cases.

The LLT-specific plugin exception applies to LLT. It is not automatically claimed for
Legion + LOQ Control.

## Implementation record: read-only machine diagnostics

- **Feature:** Machine identity and capability-interface inventory
- **Local files:** `src/LegionLoqControl.Infrastructure.Windows/Diagnostics/`,
  `src/LegionLoqControl.Infrastructure.Windows/Management/`,
  `src/LegionLoqControl.Infrastructure.Windows/Hid/`
- **Classification:** Original implementation with protocol cross-check
- **External project and URL:** Lenovo Legion Toolkit,
  <https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit>
- **Source commit:** `6f19ef48095a32afe439474a65e5b95cf8fa1b24`
- **External files examined:** `LenovoLegionToolkit.Lib/System/Management/WMI.Win32.cs`
  and `LenovoLegionToolkit.Lib/Utils/Compatibility.cs`
- **License:** GPL-3.0 with LLT-specific plugin exception
- **Local implementation:** Independently written allowlisted WMI property reads, WMI
  class metadata inventory, HID product-ID inventory, typed evidence, and redacted JSON
  output. No LLT source expression was copied.
- **Protocol facts cross-checked:** `Win32_ComputerSystemProduct` identity mapping and
  Lenovo interface naming. Legacy HID VID/PID facts remain GPL-governed protocol
  cross-checks pending independent device captures.
- **Independent evidence:** Read-only metadata capture on Lenovo LOQ 15IRX9, machine type
  `83DV`, BIOS `NECN50WW`, on 2026-08-08
- **Test fixtures:** `hardware-evidence/83DV/NECN50WW.json`
- **Hardware validation:** Identity and interface presence only. No Lenovo WMI method was
  invoked, no HID device was opened, and no write behavior was tested.
- **Reviewer:** Pending
- **Date:** 2026-08-08

## Implementation record: typed Lenovo WMI state getters

- **Feature:** Read-only thermal mode, display overdrive, and integrated-GPU mode state
- **Local files:** `src/LegionLoqControl.Domain/Results/HardwareReadResult.cs`,
  `src/LegionLoqControl.Domain/Diagnostics/HardwareStateSnapshot.cs`,
  `src/LegionLoqControl.Application/Hardware/`,
  `src/LegionLoqControl.Infrastructure.Windows/Hardware/`
- **Classification:** Original implementation with protocol cross-check
- **External project and URL:** Lenovo Legion Toolkit,
  <https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit>
- **Source commit:** `6f19ef48095a32afe439474a65e5b95cf8fa1b24`
- **External files examined:**
  `LenovoLegionToolkit.Lib/System/Management/WMI.LenovoGameZoneData.cs`,
  `LenovoLegionToolkit.Lib/Features/AbstractWmiFeature.cs`,
  `LenovoLegionToolkit.Lib/Enums.cs`,
  `LenovoLegionToolkit.Lib/Features/BatteryFeature.cs`, and
  `LenovoLegionToolkit.Lib/System/Power.cs`
- **License:** GPL-3.0 with LLT-specific plugin exception
- **Local implementation:** Independently written typed result contracts, sequential state
  service, fixed getter allowlist, bounded WMI options, Boolean-status/UInt32-data validation, and
  stable error mapping. No LLT source expression was copied.
- **Protocol facts cross-checked:** `GetSmartFanMode` uses a one-value offset for quiet,
  balanced, performance, extreme, and custom modes; overdrive and integrated-GPU getters
  use direct enum values. `GetPowerChargeMode` was confirmed to be unrelated to battery
  conservation/rapid-charge state and was deliberately excluded.
- **Independent evidence:** Local CIM metadata confirms a Boolean method return plus one
  UInt32 `Data` output; the state-read pipeline produced unelevated `AccessDenied` during
  the prerequisite instance query on LOQ 15IRX9, machine type `83DV`, BIOS `NECN50WW`, on
  2026-08-08
- **Test fixtures:** Stubbed raw-value mappings, malformed values, access denial, false
  battery-mapping guard, and serialized read ordering in
  `tests/LegionLoqControl.Platform.Tests/HardwareStateReaderTests.cs`; redacted privilege
  evidence in `hardware-evidence/83DV/NECN50WW-state-unelevated.json`
- **Hardware validation:** No successful state value has been captured yet. No setter,
  Energy driver IOCTL, or HID report was sent.
- **Reviewer:** Pending
- **Date:** 2026-08-08
## Classification

Every implementation record must use one classification:

- **Original**: implemented from vendor documentation, observed device behavior, or
  independently designed interfaces without copying source expression.
- **Adapted**: source expression or structure was modified from an external project.
  Preserve its copyright/license notices and identify the exact source commit and file.
- **Protocol cross-check**: only factual behavior, packet captures, IDs, or public
  protocol observations were compared. Record all references and independent validation.
- **Dependency**: an external package is consumed through its public API. Record its
  package version and notices in `OSS-SOURCES.md`.

## Required record for each hardware feature

Add a section before merging the feature:

```text
Feature:
Local files:
Classification:
External project and URL:
Source commit/tag:
External files examined or adapted:
License:
Local modifications:
Independent evidence:
Test fixtures:
Hardware validation:
Reviewer:
Date:
```

## Attribution policy

- Give accurate, visible credit wherever code or protocol research materially enabled a
  feature.
- Do not imply that Lenovo or another open-source project endorses this application.
- Do not remove upstream copyright notices.
- Mark modified upstream files and modification dates as required by GPL-3.0.
- Keep the About/Credits view and distributed third-party notices in sync with this file.
