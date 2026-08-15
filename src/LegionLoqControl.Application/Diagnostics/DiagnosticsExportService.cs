using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Application.Diagnostics;

public sealed class DiagnosticsExportService
{
    public const int CurrentSchemaVersion = 1;
    public const string CurrentDocumentType = "legion-loq-control-diagnostics";

    private const int MaximumProductVersionLength = 128;
    private const int MaximumStableTokenLength = 96;

    private readonly TimeProvider _timeProvider;

    public DiagnosticsExportService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public DiagnosticsExportDocument Create(
        MachineSnapshot machine,
        HardwareStateSnapshot? retainedHardwareState,
        string productVersion)
    {
        ArgumentNullException.ThrowIfNull(machine);

        string normalizedProductVersion = ValidateProductVersion(productVersion);
        DiagnosticsCapabilityExport[] capabilities = machine.Capabilities
            .Select(MapCapability)
            .OrderBy(static item => item.Capability)
            .ThenBy(static item => item.EvidenceCode, StringComparer.Ordinal)
            .ThenBy(static item => item.ObservedAtUtc)
            .ToArray();

        var identity = new DiagnosticsMachineIdentityExport(
            MapObservation(machine.Identity.Manufacturer),
            MapObservation(machine.Identity.ProductName),
            MapObservation(machine.Identity.Model),
            MapObservation(machine.Identity.MachineType),
            MapObservation(machine.Identity.BiosVersion));
        var machineExport = new DiagnosticsMachineExport(
            machine.ObservedAt.ToUniversalTime(),
            identity,
            Array.AsReadOnly(capabilities));

        return new DiagnosticsExportDocument(
            CurrentDocumentType,
            CurrentSchemaVersion,
            normalizedProductVersion,
            _timeProvider.GetUtcNow().ToUniversalTime(),
            new DiagnosticsExportBoundary(
                SerialFree: true,
                HardwareReadTriggeredByExport: false,
                HardwareWriteTriggeredByExport: false,
                LocalDraftsIncluded: false,
                RawDevicePathsIncluded: false),
            machineExport,
            MapHardwareState(retainedHardwareState));
    }

    private static DiagnosticsObservationExport MapObservation(Observation observation)
    {
        if (observation is null || !Enum.IsDefined(observation.State))
            throw InvalidSnapshot();

        return observation.State switch
        {
            ObservationState.Observed when !string.IsNullOrWhiteSpace(observation.Value) =>
                new DiagnosticsObservationExport(
                    observation.State,
                    observation.Value.Trim(),
                    ErrorCode: null),
            ObservationState.Unavailable or ObservationState.Failed =>
                new DiagnosticsObservationExport(
                    observation.State,
                    Value: null,
                    ValidateStableToken(observation.ErrorCode)),
            _ => throw InvalidSnapshot(),
        };
    }

    private static DiagnosticsCapabilityExport MapCapability(CapabilityEvidence evidence)
    {
        if (evidence is null ||
            !Enum.IsDefined(evidence.Capability) ||
            !Enum.IsDefined(evidence.Support))
        {
            throw InvalidSnapshot();
        }

        return new DiagnosticsCapabilityExport(
            evidence.Capability,
            evidence.Support,
            evidence.ObservedAt.ToUniversalTime(),
            ValidateStableToken(evidence.EvidenceCode));
    }

    private static DiagnosticsHardwareStateExport MapHardwareState(
        HardwareStateSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new DiagnosticsHardwareStateExport(
                DiagnosticsHardwareCaptureStatus.NotCaptured,
                ObservedAtUtc: null,
                BatteryChargeMode: null,
                ThermalMode: null,
                DisplayOverdrive: null,
                IntegratedGpuMode: null,
                FourZoneKeyboard: null,
                FanTable: null);
        }

        return new DiagnosticsHardwareStateExport(
            DiagnosticsHardwareCaptureStatus.Captured,
            snapshot.ObservedAt.ToUniversalTime(),
            MapHardwareRead(snapshot.BatteryChargeMode),
            MapHardwareRead(snapshot.ThermalMode),
            MapHardwareRead(snapshot.DisplayOverdrive),
            MapHardwareRead(snapshot.IntegratedGpuMode),
            MapHardwareRead(snapshot.FourZoneKeyboard),
            MapFanTable(snapshot.FanTable));
    }

    private static DiagnosticsHardwareReadExport MapFanTable(
        HardwareReadResult<FanTableSnapshot> result)
    {
        if (result is null || !Enum.IsDefined(result.Status))
            throw InvalidSnapshot();

        if (result.Status == HardwareReadStatus.Success)
        {
            if (!result.Value.HasValue ||
                result.Value.Value.PointCount is < 1 or > FanTableSnapshot.MaximumPoints)
            {
                throw InvalidSnapshot();
            }

            return new DiagnosticsHardwareReadExport(
                result.Status,
                result.Value.Value.PointCount + "-point",
                ErrorCode: null);
        }

        if (result.Value.HasValue)
            throw InvalidSnapshot();

        return new DiagnosticsHardwareReadExport(
            result.Status,
            Value: null,
            ValidateStableToken(result.ErrorCode));
    }

    private static DiagnosticsHardwareReadExport MapHardwareRead<T>(
        HardwareReadResult<T> result)
        where T : struct, Enum
    {
        if (result is null || !Enum.IsDefined(result.Status))
            throw InvalidSnapshot();

        if (result.Status == HardwareReadStatus.Success)
        {
            if (!result.Value.HasValue || !Enum.IsDefined(result.Value.Value))
                throw InvalidSnapshot();

            return new DiagnosticsHardwareReadExport(
                result.Status,
                FormatEnum(result.Value.Value),
                ErrorCode: null);
        }

        if (result.Value.HasValue)
            throw InvalidSnapshot();

        return new DiagnosticsHardwareReadExport(
            result.Status,
            Value: null,
            ValidateStableToken(result.ErrorCode));
    }

    private static string FormatEnum<T>(T value)
        where T : struct, Enum
    {
        string? name = Enum.GetName(value);
        if (string.IsNullOrEmpty(name))
            throw InvalidSnapshot();

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string ValidateProductVersion(string productVersion)
    {
        if (string.IsNullOrWhiteSpace(productVersion))
        {
            throw new DiagnosticsExportException(
                "diagnostics_export_invalid_product_version");
        }

        string normalized = productVersion.Trim();
        if (normalized.Length > MaximumProductVersionLength)
        {
            throw new DiagnosticsExportException(
                "diagnostics_export_invalid_product_version");
        }

        return normalized;
    }

    private static string ValidateStableToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw InvalidSnapshot();

        string normalized = token.Trim();
        if (normalized.Length > MaximumStableTokenLength ||
            normalized.Any(static character =>
                character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '_' and
                not '-' and
                not '.'))
        {
            throw InvalidSnapshot();
        }

        return normalized;
    }

    private static DiagnosticsExportException InvalidSnapshot() =>
        new("diagnostics_export_invalid_snapshot");
}
