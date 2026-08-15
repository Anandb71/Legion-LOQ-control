using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Application.Diagnostics;

public sealed record DiagnosticsExportDocument(
    string DocumentType,
    int SchemaVersion,
    string ProductVersion,
    DateTimeOffset ExportedAtUtc,
    DiagnosticsExportBoundary Boundary,
    DiagnosticsMachineExport Machine,
    DiagnosticsHardwareStateExport HardwareState);

public sealed record DiagnosticsExportBoundary(
    bool SerialFree,
    bool HardwareReadTriggeredByExport,
    bool HardwareWriteTriggeredByExport,
    bool LocalDraftsIncluded,
    bool RawDevicePathsIncluded);

public sealed record DiagnosticsMachineExport(
    DateTimeOffset ObservedAtUtc,
    DiagnosticsMachineIdentityExport Identity,
    IReadOnlyList<DiagnosticsCapabilityExport> Capabilities);

public sealed record DiagnosticsMachineIdentityExport(
    DiagnosticsObservationExport Manufacturer,
    DiagnosticsObservationExport ProductName,
    DiagnosticsObservationExport Model,
    DiagnosticsObservationExport MachineType,
    DiagnosticsObservationExport BiosVersion);

public sealed record DiagnosticsObservationExport(
    ObservationState State,
    string? Value,
    string? ErrorCode);

public sealed record DiagnosticsCapabilityExport(
    HardwareCapability Capability,
    CapabilitySupport Support,
    DateTimeOffset ObservedAtUtc,
    string EvidenceCode);

public enum DiagnosticsHardwareCaptureStatus
{
    NotCaptured = 0,
    Captured = 1,
}

public sealed record DiagnosticsHardwareStateExport(
    DiagnosticsHardwareCaptureStatus CaptureStatus,
    DateTimeOffset? ObservedAtUtc,
    DiagnosticsHardwareReadExport? BatteryChargeMode,
    DiagnosticsHardwareReadExport? ThermalMode,
    DiagnosticsHardwareReadExport? DisplayOverdrive,
    DiagnosticsHardwareReadExport? IntegratedGpuMode,
    DiagnosticsHardwareReadExport? FourZoneKeyboard,
    DiagnosticsHardwareReadExport? FanTable);

public sealed record DiagnosticsHardwareReadExport(
    HardwareReadStatus Status,
    string? Value,
    string? ErrorCode);
