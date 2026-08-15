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
- **Platform documentation:**
  [Get-CimInstance](https://learn.microsoft.com/powershell/module/cimcmdlets/get-ciminstance)
  and
  [Invoke-CimMethod](https://learn.microsoft.com/powershell/module/cimcmdlets/invoke-cimmethod)
- **Local implementation:** Independently written typed result contracts, sequential state
  service, fixed getter allowlist, a single bounded built-in Windows PowerShell/CIM batch,
  Boolean-status/UInt32-data validation, and stable error mapping. The script and all
  identifiers are static, profiles are disabled, and no MI runtime is redistributed. No
  LLT source expression was copied.
- **Protocol facts cross-checked:** `GetSmartFanMode` uses a one-value offset for quiet,
  balanced, performance, extreme, and custom modes; overdrive and integrated-GPU getters
  use direct enum values. `GetPowerChargeMode` was confirmed to be unrelated to battery
  conservation/rapid-charge state and was deliberately excluded.
- **Independent evidence:** Local CIM metadata confirms a Boolean method return plus one
  UInt32 `Data` output; the state-read pipeline produced unelevated `AccessDenied` during
  the prerequisite instance query and validated three elevated getter responses on LOQ
  15IRX9, machine type `83DV`, BIOS `NECN50WW`, on 2026-08-08
- **Test fixtures:** Stubbed raw-value mappings, malformed values, access denial, false
  battery-mapping guard, and serialized read ordering in
  `tests/LegionLoqControl.Platform.Tests/HardwareStateReaderTests.cs`; redacted privilege
  evidence in `hardware-evidence/83DV/NECN50WW-state-unelevated.json` and
  `hardware-evidence/83DV/NECN50WW-state-elevated.json`
- **Hardware validation:** The broker observed Performance thermal mode (raw `3`), disabled
  overdrive (raw `0`), and integrated-only GPU mode (raw `1`), each with Boolean `true`
  status and UInt32 data through the retained built-in PowerShell/CIM adapter. No setter or
  HID report was sent.
- **Reviewer:** Pending
- **Date:** 2026-08-08

## Implementation record: EnergyDrv battery-mode read

- **Feature:** Read-only Normal, Conservation, and Rapid Charge state
- **Local files:**
  `src/LegionLoqControl.Infrastructure.Windows/Hardware/EnergyDriverBatteryReader.cs`
  and `src/LegionLoqControl.Broker/BrokerHost.cs`
- **Classification:** Original implementation with protocol cross-check
- **External project and URL:** Lenovo Legion Toolkit,
  <https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit>
- **Source commit:** `6f19ef48095a32afe439474a65e5b95cf8fa1b24`
- **External files examined:** `LenovoLegionToolkit.Lib/System/Drivers.cs`,
  `LenovoLegionToolkit.Lib/Features/AbstractDriverFeature.cs`, and
  `LenovoLegionToolkit.Lib/Features/BatteryFeature.cs`
- **License:** GPL-3.0 with LLT-specific plugin exception
- **Local implementation:** Independently written, parameterless adapter for one fixed
  buffered IOCTL. It requests zero device access, requires exactly four output bytes,
  applies a five-second caller wait, maps stable Win32 failures, and is instantiated only
  by the elevated read-only broker.
- **Protocol facts cross-checked:** Control code `0x831020F8`, read selector `0xFF`,
  Conservation bit `0x20`, and Rapid Charge bit `0x04`. Conflicting mode bits are rejected.
- **Independent evidence:** One explicitly approved broker run on LOQ 15IRX9, machine type
  `83DV`, BIOS `NECN50WW`, returned Normal mode on 2026-08-09.
- **Test fixtures:** Mapping, conflicting-bit, broker delegation, and exact
  control-code/selector/access invariants in
  `tests/LegionLoqControl.Platform.Tests/HardwareStateReaderTests.cs`; redacted result in
  `hardware-evidence/83DV/NECN50WW-state-elevated.json`
- **Hardware validation:** One read-only IOCTL was sent with selector `0xFF` through a handle
  opened with zero requested access. It returned a payload whose charge-mode bits mapped to
  Normal. No setter code, generic IOCTL entry point, or caller-controlled value exists.
- **Reviewer:** Pending
- **Date:** 2026-08-09

## Implementation record: EnergyDrv battery-mode write

- **Feature:** Apply Normal, Conservation, and Rapid Charge
- **Local files:**
  `src/LegionLoqControl.Infrastructure.Windows/Hardware/EnergyDriverBatteryWriter.cs`
  and `src/LegionLoqControl.Application/Hardware/HardwareStateWriteService.cs`
- **Classification:** Original implementation with protocol cross-check
- **External project and URL:** Lenovo Legion Toolkit,
  <https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit>
- **Source commit:** `6f19ef48095a32afe439474a65e5b95cf8fa1b24`
- **External files examined:** `LenovoLegionToolkit.Lib/Features/BatteryFeature.cs`
- **License:** GPL-3.0 with LLT-specific plugin exception
- **Local implementation:** Independently written typed writer for the same IOCTL as the
  reader. Selectors are fixed: Conservation `0x03`, leave-conservation `0x05`, Rapid
  `0x07`, leave-rapid `0x08`. The handle requests `GENERIC_READ | GENERIC_WRITE` only on
  this path. There is no generic IOCTL entry point and no caller-controlled control code.
- **Protocol facts cross-checked:** Same control code `0x831020F8`; write selectors are
  distinct from the `0xFF` read selector.
- **Independent evidence:** Pending an explicit elevated apply on LOQ 15IRX9, machine type
  `83DV`, BIOS `NECN50WW`.
- **Test fixtures:** Selector-mapping and access-mask invariants in
  `tests/LegionLoqControl.Platform.Tests/HardwareStateReaderTests.cs`
- **Hardware validation:** Not yet recorded. Do not treat the protocol cross-check as a
  successful on-device write.
- **Reviewer:** Pending
- **Date:** 2026-08-15

## Implementation record: ITE 4-zone keyboard brightness

- **Feature:** Detect `048D:C993` and apply Off/Low/High brightness
- **Local files:**
  `src/LegionLoqControl.Infrastructure.Windows/Hardware/FourZoneKeyboardPacket.cs`,
  `src/LegionLoqControl.Infrastructure.Windows/Hardware/FourZoneKeyboardHid.cs`,
  and `src/LegionLoqControl.Infrastructure.Windows/Diagnostics/WindowsCapabilityProbe.cs`
- **Classification:** Original implementation with protocol cross-check
- **External projects and URLs:** LegionAura,
  <https://github.com/Nivedck/LegionAura>; 4JX/lenovo-legion-hid,
  <https://github.com/4JX/lenovo-legion-hid>
- **External files examined:** `lib/legionaura.cpp`, `lib/legionaura.h`,
  `devices/devices.json`, and `src/lib.rs`
- **License:** Check each upstream project before copying code. No source was copied.
- **Local implementation:** Independently written 33-byte feature-report codec. Write
  packets are `CC 16` plus a fixed Off/Low/High layout. The HID opener accepts only
  vendor `048D`, product IDs `C935`/`C955`/`C993`, and feature length 33. `C996` is
  excluded. GetFeature of report `CC` on this LOQ returned an identity report
  (`CC 05` plus VID/PID), not lighting state.
- **Protocol facts cross-checked:** LegionAura lists LOQ PID `C993` as its default
  4-zone device and uses the same `CC 16` command, effect/speed/brightness offsets,
  and 32-byte payload padded to the 33-byte Windows feature report.
- **Independent evidence:** Unelevated HID inventory on LOQ 15IRX9 `83DV` / `NECN50WW`
  observed `048D:C993` collections with feature lengths 17, 33, 65, and 192, and
  `048D:C996` with no 33-byte collection. One GetFeature of the 33-byte collection
  returned `CC-05-FF-0A-00-8D-04-93-C9-00-03` plus zeros.
- **Test fixtures:** Packet encode/decode and PID allowlist in
  `tests/LegionLoqControl.Platform.Tests/HardwareStateReaderTests.cs`
- **Hardware validation:** No SetFeature apply has been recorded yet.
- **Reviewer:** Pending
- **Date:** 2026-08-15

## Implementation record: read-only elevated broker

- **Feature:** One-request privileged hardware-state read boundary
- **Local files:** `src/LegionLoqControl.Broker/`,
  `src/LegionLoqControl.Contracts/Broker/`,
  `src/LegionLoqControl.Infrastructure.Windows/Broker/`
- **Classification:** Original implementation from Windows/.NET platform documentation
- **External documentation:**
  [PipeOptions](https://learn.microsoft.com/dotnet/api/system.io.pipes.pipeoptions),
  [NamedPipeServerStreamAcl](https://learn.microsoft.com/dotnet/api/system.io.pipes.namedpipeserverstreamacl),
  [named-pipe security](https://learn.microsoft.com/windows/win32/ipc/named-pipe-security-and-access-rights),
  [GetNamedPipeClientProcessId](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-getnamedpipeclientprocessid),
  [GetNamedPipeServerProcessId](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-getnamedpipeserverprocessid),
  and [mandatory integrity control](https://learn.microsoft.com/windows/win32/secauthz/mandatory-integrity-control)
- **Local implementation:** Explicit current-user pipe DACL, random first-instance pipe,
  mutual peer-process checks, 256-bit one-time nonce, anonymous impersonation level,
  strict 64 KiB length-prefixed JSON, explicit wire DTO validation, UAC launch, and bounded
  broker lifetime
- **Security boundary:** The original 2026-08-08 record accepted only one hardware-state
  read request. A later `--write` path adds one allowlisted WMI setter or the typed
  EnergyDrv battery write documented above. The broker still has no legacy Core reference,
  generic IOCTL entry point, HID access, or caller-supplied WMI names.
- **Independent evidence:** Same-process named-pipe integration tests verify ACL creation,
  server/client process-ID APIs, framing, request binding, and response validation on
  Windows 10. An explicit UAC-assisted run verified the cross-integrity pipe and one-shot
  process lifecycle on the recorded 83DV machine.
- **Test fixtures:** `tests/LegionLoqControl.Platform.Tests/BrokerWireProtocolTests.cs` and
  `tests/LegionLoqControl.Platform.Tests/BrokerSecurityTests.cs`
- **Hardware validation:** The elevated broker returned the successful typed state values
  recorded in `hardware-evidence/83DV/NECN50WW-state-elevated.json` through the retained
  built-in PowerShell/CIM adapter. No write behavior was implemented or tested.
- **Release gate:** Authenticode signing and administrator-protected installation ACLs are
  required before production broker use.
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
