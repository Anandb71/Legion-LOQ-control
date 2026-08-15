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

## Diagnostics export contract

`DiagnosticsExportService` maps a `MachineSnapshot` and optional retained
`HardwareStateSnapshot` into schema version 1 of `DiagnosticsExportDocument`. The mapping
is an explicit allowlist: it omits observation details, dynamic source strings, local
drafts, transport identifiers, and future domain fields unless the contract is deliberately
versioned.

`IDiagnosticsExportWriter` accepts `CreateNew` or `ReplaceExisting` mode. The Windows JSON
writer uses string-only enums, a 256 KiB cap, an exclusive same-directory temporary file,
write-through flush, and atomic rename. Export failure codes are stable and do not include
the selected path. The diagnostics CLI `inventory --output` path uses `CreateNew` and the
same writer; stdout remains the default.

## Windows diagnostics

- `WindowsMachineIdentitySource` reads an allowlist of CIM properties.
- `WindowsCapabilityProbe` reads WMI class metadata and enumerates Lenovo HID product IDs.

Those inventory types do not invoke Lenovo WMI methods, open HID devices, or perform
writes.

`WindowsHardwareStateReader` is a separate adapter. It invokes only the fixed
`GetSmartFanMode`, `GetODStatus`, and `GetIGPUModeStatus` methods. Its default transport
invokes them in-process through System.Management with those fixed names, validates
Boolean return status and UInt32 data, and applies a 5-second bound. It fails closed when
the provider does not
expose that complete output. The default unelevated reader reports battery mode as
`Unavailable`; the broker-only factory adds the fixed, parameterless EnergyDrv reader with
zero requested device access. The same privileged factory may invoke `Fan_Get_Table` on
`LENOVO_FAN_METHOD` with FanID `0` and SensorID `0`, then accept only matching UInt32
speed/sensor arrays of 1–10 values in `0–255`. The unelevated reader does not open that
class.

## Broker contracts

Commands require a non-empty `CommandId`, an expected state, and a desired state. Current
contracts cover battery charge mode, thermal mode, fan mode, and keyboard brightness.
They are definitions only; the current broker has no command dispatcher and cannot execute
them.

`BrokerCommandStatus` includes `Succeeded`, `Unsupported`, `InvalidRequest`, `Conflict`,
`Busy`, `Unverified`, and `Failed`.

The read-only broker protocol uses `HardwareStateReadRequest` and
`HardwareStateReadResponse`. Every request carries the protocol major version, a non-empty
request ID, a one-time nonce, and the initiating process ID. Messages use a four-byte
little-endian length prefix, a 64 KiB maximum payload, strict JSON members, and string-only
enum values. Transport peers must still validate every semantic field before performing a
read.

`HardwareStateReadPayload` and `HardwareReadValue<T>` are explicit wire DTOs. They convert
to domain snapshots only after validating success/failure shape, enum values, fan-table
point bounds, and stable error-code syntax; domain constructors are not exposed for
deserialization.

`ElevatedHardwareStateBrokerClient` launches only a sibling executable named
`LegionLoqControl.Broker.exe` with `--session`, keeps that elevated process for later
reads and writes, validates each response, and maps cancellation, timeout, elevation
rejection, peer mismatch, and malformed response to stable transport error codes.
Windows prompts only when the session is created or has to be recreated.

`ElevatedHardwareStateBrokerClient.WriteAsync` sends one `HardwareStateWriteRequest`
through that session for thermal mode, display overdrive, integrated-GPU mode, battery
charge mode, or 4-zone keyboard brightness. The broker rereads, compares the expected
value, invokes one allowlisted setter, and returns a readback snapshot. One-shot
`--write` remains available for diagnostics.

`BrokerInstallPolicy` classifies that sibling as missing, development, protected, or
unprotected from owner/DACL evidence and Authenticode state. Production mode refuses
unsigned or user-writable installs before UAC with `broker_install_unprotected`,
`broker_unsigned`, or `broker_signature_invalid`. The dashboard shows the assessment
without launching the broker.
