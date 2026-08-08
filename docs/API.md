# Public API

The rebuild API is deliberately small. Legacy types in `LegionLoqControl.Core` are
quarantined implementation history and are not supported public API.

## Domain observations

`Observation` preserves whether a field was observed, unavailable, or failed:

```csharp
Observation model = Observation.FromValue("LOQ 15IRX9");
Observation missing = Observation.Unavailable("wmi_value_missing");
```

`MachineIdentity` contains manufacturer, product name, model, machine type, and BIOS
version. It intentionally contains no serial number.

## Capability evidence

`CapabilityEvidence` combines:

- `HardwareCapability`
- `CapabilitySupport` (`Unknown`, `Unsupported`, `Supported`, or `Degraded`)
- a stable source and evidence code
- observation time
- an optional redacted detail

An interface match is emitted as `Unknown` with
`wmi_interface_present_unverified` or `hid_interface_present_unverified`. Only validated
hardware work may emit `Supported`.

## Hardware read results

`HardwareReadResult<T>` separates a real value from failure states. Its status is one of
`Success`, `Unsupported`, `AccessDenied`, `Unavailable`, `InvalidData`, `Failed`, or
`TimedOut`. Only `Success` has a value; failures carry a stable, non-sensitive error code.

`HardwareStateSnapshot` currently groups battery charge mode, thermal mode, display
overdrive, and integrated-GPU mode results under one observation timestamp.

## Application ports

```csharp
public interface IMachineIdentitySource
{
    string SourceName { get; }
    ValueTask<MachineIdentity> ReadAsync(CancellationToken cancellationToken);
}

public interface ICapabilityProbe
{
    string SourceName { get; }
    IReadOnlySet<HardwareCapability> Capabilities { get; }
    ValueTask<IReadOnlyCollection<CapabilityEvidence>> ProbeAsync(
        MachineIdentity identity,
        CancellationToken cancellationToken);
}
```

`MachineDiagnosticsService` captures identity, runs probes, and converts probe exceptions
into unknown evidence. Caller cancellation is propagated.

`IHardwareStateReader` exposes one typed method per state. `HardwareStateService` invokes
those methods sequentially to avoid concurrent access to Lenovo providers and retains each
individual outcome in the snapshot.

## Windows diagnostics

- `WindowsMachineIdentitySource` reads an allowlist of CIM properties.
- `WindowsCapabilityProbe` reads WMI class metadata and enumerates Lenovo HID product IDs.

Those inventory types do not invoke Lenovo WMI methods, open HID devices, or perform
writes.

`WindowsHardwareStateReader` is a separate adapter. It invokes only the fixed
`GetSmartFanMode`, `GetODStatus`, and `GetIGPUModeStatus` methods, imposes a five-second WMI
timeout, validates the Boolean return status and UInt32 data, and maps access denial
explicitly. Its battery result is intentionally `Unavailable` until the Energy driver read
transport exists.

## Broker contracts

Commands require a non-empty `CommandId`, an expected state, and a desired state. Current
contracts cover battery charge mode, thermal mode, fan mode, and keyboard brightness.
They are definitions only; no broker executes them yet.

`BrokerCommandStatus` includes `Succeeded`, `Unsupported`, `InvalidRequest`, `Conflict`,
`Busy`, `Unverified`, and `Failed`.

The read-only broker protocol uses `HardwareStateReadRequest` and
`HardwareStateReadResponse`. Every request carries the protocol major version, a non-empty
request ID, a one-time nonce, and the initiating process ID. Messages use a four-byte
little-endian length prefix, a 64 KiB maximum payload, strict JSON members, and string-only
enum values. Transport peers must still validate every semantic field before performing a
read.
