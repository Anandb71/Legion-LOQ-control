namespace LegionLoqControl.Domain.Capabilities;

public enum HardwareCapability
{
    BatteryConservationMode = 0,
    BatteryRapidCharge = 1,
    ThermalMode = 2,
    FanControl = 3,
    WhiteKeyboardBacklight = 4,
    FourZoneRgbKeyboard = 5,
    SpectrumKeyboard = 6,
    DisplayOverdrive = 7,
    HybridGraphicsMode = 8,
    GpuWorkingMode = 9,
}

public enum CapabilitySupport
{
    Unknown = 0,
    Unsupported = 1,
    Supported = 2,
    Degraded = 3,
}

public sealed record CapabilityEvidence
{
    public CapabilityEvidence(
        HardwareCapability capability,
        CapabilitySupport support,
        string source,
        DateTimeOffset observedAt,
        string evidenceCode,
        string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceCode);

        Capability = capability;
        Support = support;
        Source = source.Trim();
        ObservedAt = observedAt;
        EvidenceCode = evidenceCode.Trim();
        Detail = detail?.Trim();
    }

    public HardwareCapability Capability { get; }

    public CapabilitySupport Support { get; }

    public string Source { get; }

    public DateTimeOffset ObservedAt { get; }

    public string EvidenceCode { get; }

    public string? Detail { get; }
}
