using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;
using Xunit;

namespace LegionLoqControl.Application.Tests.Diagnostics;

public sealed class DiagnosticsExportServiceTests
{
    private static readonly DateTimeOffset ExportedAt =
        new(2026, 8, 11, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Export_is_versioned_ordered_and_normalized_to_utc()
    {
        DateTimeOffset observedAt =
            new(2026, 8, 11, 23, 30, 0, TimeSpan.FromHours(5.5));
        MachineSnapshot machine = CreateMachineSnapshot(
            observedAt,
            [
                Evidence(
                    HardwareCapability.ThermalMode,
                    "thermal_present",
                    observedAt),
                Evidence(
                    HardwareCapability.BatteryConservationMode,
                    "battery_present",
                    observedAt),
            ]);
        var service = new DiagnosticsExportService(new FixedTimeProvider(ExportedAt));

        DiagnosticsExportDocument document = service.Create(
            machine,
            retainedHardwareState: null,
            productVersion: " 1.2.3-preview ");

        Assert.Equal(DiagnosticsExportService.CurrentDocumentType, document.DocumentType);
        Assert.Equal(DiagnosticsExportService.CurrentSchemaVersion, document.SchemaVersion);
        Assert.Equal("1.2.3-preview", document.ProductVersion);
        Assert.Equal(ExportedAt, document.ExportedAtUtc);
        Assert.Equal(observedAt.ToUniversalTime(), document.Machine.ObservedAtUtc);
        Assert.Equal(
            [
                HardwareCapability.BatteryConservationMode,
                HardwareCapability.ThermalMode,
            ],
            document.Machine.Capabilities.Select(static item => item.Capability));
        Assert.True(document.Boundary.SerialFree);
        Assert.False(document.Boundary.HardwareReadTriggeredByExport);
        Assert.False(document.Boundary.HardwareWriteTriggeredByExport);
        Assert.False(document.Boundary.LocalDraftsIncluded);
        Assert.False(document.Boundary.RawDevicePathsIncluded);
    }

    [Fact]
    public void Missing_retained_hardware_state_is_explicit()
    {
        var service = new DiagnosticsExportService(new FixedTimeProvider(ExportedAt));

        DiagnosticsExportDocument document = service.Create(
            CreateMachineSnapshot(ExportedAt, []),
            retainedHardwareState: null,
            productVersion: "1.0.0");

        Assert.Equal(
            DiagnosticsHardwareCaptureStatus.NotCaptured,
            document.HardwareState.CaptureStatus);
        Assert.Null(document.HardwareState.ObservedAtUtc);
        Assert.Null(document.HardwareState.BatteryChargeMode);
        Assert.Null(document.HardwareState.ThermalMode);
        Assert.Null(document.HardwareState.DisplayOverdrive);
        Assert.Null(document.HardwareState.IntegratedGpuMode);
    }

    [Fact]
    public void Retained_hardware_state_maps_typed_values_and_failures()
    {
        var hardware = new HardwareStateSnapshot(
            ExportedAt,
            HardwareReadResult<BatteryChargeMode>.Success(
                BatteryChargeMode.Conservation),
            HardwareReadResult<ThermalMode>.Failure(
                HardwareReadStatus.AccessDenied,
                "wmi_access_denied"),
            HardwareReadResult<ToggleState>.Success(ToggleState.Disabled),
            HardwareReadResult<IntegratedGpuMode>.Success(
                IntegratedGpuMode.IntegratedOnly));
        var service = new DiagnosticsExportService(new FixedTimeProvider(ExportedAt));

        DiagnosticsExportDocument document = service.Create(
            CreateMachineSnapshot(ExportedAt, []),
            hardware,
            "1.0.0");

        Assert.Equal(
            DiagnosticsHardwareCaptureStatus.Captured,
            document.HardwareState.CaptureStatus);
        Assert.Equal("conservation", document.HardwareState.BatteryChargeMode!.Value);
        Assert.Equal(
            HardwareReadStatus.AccessDenied,
            document.HardwareState.ThermalMode!.Status);
        Assert.Null(document.HardwareState.ThermalMode.Value);
        Assert.Equal(
            "wmi_access_denied",
            document.HardwareState.ThermalMode.ErrorCode);
        Assert.Equal("disabled", document.HardwareState.DisplayOverdrive!.Value);
        Assert.Equal("integratedOnly", document.HardwareState.IntegratedGpuMode!.Value);
    }

    [Fact]
    public void Unsafe_dynamic_tokens_fail_closed()
    {
        MachineSnapshot machine = CreateMachineSnapshot(
            ExportedAt,
            [
                new CapabilityEvidence(
                    HardwareCapability.ThermalMode,
                    CapabilitySupport.Unknown,
                    "source-is-not-exported",
                    ExportedAt,
                    "secret value must not escape",
                    "detail-is-not-exported"),
            ]);
        var service = new DiagnosticsExportService(new FixedTimeProvider(ExportedAt));

        DiagnosticsExportException exception = Assert.Throws<DiagnosticsExportException>(
            () => service.Create(machine, null, "1.0.0"));

        Assert.Equal("diagnostics_export_invalid_snapshot", exception.ErrorCode);
    }

    private static MachineSnapshot CreateMachineSnapshot(
        DateTimeOffset observedAt,
        IEnumerable<CapabilityEvidence> capabilities) =>
        new(
            new MachineIdentity(
                Observation.FromValue("LENOVO"),
                Observation.FromValue("LOQ 15IRX9"),
                Observation.FromValue("LOQ 15IRX9"),
                Observation.FromValue("83DV"),
                Observation.FromValue("NECN50WW")),
            observedAt,
            capabilities);

    private static CapabilityEvidence Evidence(
        HardwareCapability capability,
        string evidenceCode,
        DateTimeOffset observedAt) =>
        new(
            capability,
            CapabilitySupport.Unknown,
            "test",
            observedAt,
            evidenceCode,
            "detail-is-deliberately-not-exported");

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
