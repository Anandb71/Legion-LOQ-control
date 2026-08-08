# Architecture

## Current dependency graph

```mermaid
flowchart TD
    Wpf[Unelevated WPF shell] --> Application
    Wpf --> WindowsInfrastructure
    DiagnosticsCli[Redacted diagnostics CLI] --> Application
    DiagnosticsCli --> WindowsInfrastructure
    Application[Application use cases] --> Domain
    WindowsInfrastructure[Windows read adapters] --> Application
    WindowsInfrastructure --> Domain
    Contracts[Typed broker contracts] --> Domain
    LegacyCore[Quarantined legacy Core] --> LegacyTests[Safety tests]
```

The WPF project does not reference `LegionLoqControl.Core`. That assembly contains the
pre-rebuild WMI, IOCTL, and HID writers and remains locked by `HardwareWritePolicy` while
its behavior is replaced feature by feature.

## Projects

- `src/LegionLoqControl.Domain`: platform-neutral observations, capability evidence,
  hardware read results, hardware states, and bounded value objects.
- `src/LegionLoqControl.Application`: read-only use cases and ports such as
  `IMachineIdentitySource`, `ICapabilityProbe`, and `IHardwareStateReader`.
- `src/LegionLoqControl.Infrastructure.Windows`: Windows WMI inventory and strict read
  adapters. Inventory does not invoke methods; the state adapter invokes only fixed Lenovo
  getters and never exposes WMI names to callers.
- `src/LegionLoqControl.Contracts`: versioned, typed compare-and-set commands for the
  future broker. Raw IOCTL values, WMI names, and HID packets are not IPC contracts.
- `src/LegionLoqControl.Diagnostics`: serial-free JSON inventory and typed state collector.
- `LegionLoqControl`: unelevated WPF composition root and read-only status UI.
- `LegionLoqControl.Core`: quarantined migration prototype; never reference it from new
  product projects.

## Inventory flow

1. The composition root creates `MachineDiagnosticsService`.
2. `WindowsMachineIdentitySource` reads an allowlist of non-sensitive CIM properties.
3. `WindowsCapabilityProbe` inventories class method names and matching HID product IDs.
4. Every probe emits evidence. Interface presence remains `Unknown`, not `Supported`.
5. Probe failures become typed unknown evidence with stable error codes.
6. The CLI serializes the snapshot without serial numbers, device paths, usernames, or
   exception messages.

Metadata is cached per scan so each WMI class and HID vendor inventory is queried once.

## Hardware state read flow

```mermaid
flowchart LR
    StateCli[State diagnostics command] --> StateService[HardwareStateService]
    StateService --> StatePort[IHardwareStateReader]
    StatePort --> WmiReader[WindowsHardwareStateReader]
    WmiReader --> FixedGetters[Fixed Lenovo getters]
    FixedGetters --> TypedResults[Typed results and stable errors]
```

Reads are serialized. The adapter validates the Boolean WMI return status and UInt32 data, rejects unknown enum values,
and applies a five-second provider timeout. Access denial, unsupported transport, malformed
output, and timeout remain distinct from real hardware values.

On the recorded 83DV machine, the Lenovo WMI getters require elevation. The current CLI
can expose this behavior for manual validation, but the WPF shell does not call them. The
final product will route privileged reads through the same short-lived broker boundary
needed by writes. Battery state remains unavailable until the Energy driver read path is
implemented behind that boundary.

## Future privileged flow

```mermaid
flowchart LR
    Client[Unelevated UI or CLI] --> Request[Typed read or write request]
    Request --> Broker[Short-lived elevated broker]
    Broker --> Validate[Identity and capability validation]
    Validate --> Compare[Fresh read and expected-state compare]
    Compare --> Operation[Bounded hardware operation]
    Operation --> Verify[Readback verification for writes]
    Verify --> Result[Typed result and redacted journal]
```

The broker does not exist yet. Hardware writes remain disabled until this entire path,
including executable trust, transport ACLs, serialization, timeouts, machine-wide locking,
and crash reconciliation, is implemented and reviewed.

## Architectural rules

- Domain and Application projects stay platform-neutral.
- Infrastructure may depend inward; Domain never depends on Windows or transport code.
- UI and CLI compose interfaces but do not contain hardware protocols.
- Unknown and failure are first-class states, never `false`, zero, or an arbitrary mode.
- Read adapters use fixed identifiers; user-controlled WMI queries are forbidden.
- New dependencies use central package versions and committed lock files.
- A capability becomes `Supported` only with provenance, fixtures, and exact model/BIOS
  hardware evidence.
