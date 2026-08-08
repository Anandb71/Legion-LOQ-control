using System.Collections.Frozen;
using LegionLoqControl.Application.Abstractions;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Hid;
using LegionLoqControl.Infrastructure.Windows.Management;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Diagnostics;

public sealed class WindowsCapabilityProbe : ICapabilityProbe
{
    private const string LenovoWmiNamespace = @"root\WMI";
    private const int LenovoLightingVendorId = 0x048D;

    private static readonly FrozenSet<int> FourZoneProductIds =
        new[] { 0xC935, 0xC955 }.ToFrozenSet();

    private static readonly FrozenSet<int> SpectrumProductIds =
        new[] { 0xC965 }.ToFrozenSet();

    private static readonly FrozenSet<HardwareCapability> ProbedCapabilities =
        Enum.GetValues<HardwareCapability>().ToFrozenSet();

    private readonly IWindowsManagementReader _managementReader;
    private readonly IHidDeviceInventory _hidInventory;
    private readonly TimeProvider _timeProvider;

    public WindowsCapabilityProbe()
        : this(new SystemManagementReader(), new SystemHidDeviceInventory(), TimeProvider.System)
    {
    }

    internal WindowsCapabilityProbe(
        IWindowsManagementReader managementReader,
        IHidDeviceInventory hidInventory,
        TimeProvider timeProvider)
    {
        _managementReader = managementReader ?? throw new ArgumentNullException(nameof(managementReader));
        _hidInventory = hidInventory ?? throw new ArgumentNullException(nameof(hidInventory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public string SourceName => "windows.interface.inventory";

    public IReadOnlySet<HardwareCapability> Capabilities => ProbedCapabilities;

    public ValueTask<IReadOnlyCollection<CapabilityEvidence>> ProbeAsync(
        MachineIdentity identity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        return new ValueTask<IReadOnlyCollection<CapabilityEvidence>>(
            Task.Run(() => Probe(cancellationToken), cancellationToken));
    }

    private IReadOnlyCollection<CapabilityEvidence> Probe(CancellationToken cancellationToken)
    {
        DateTimeOffset observedAt = _timeProvider.GetUtcNow();
        var metadata = new Dictionary<string, MetadataAttempt>(StringComparer.OrdinalIgnoreCase);
        var evidence = new List<CapabilityEvidence>(ProbedCapabilities.Count)
        {
            ProbeWmi(
                metadata,
                HardwareCapability.BatteryConservationMode,
                "LENOVO_GAMEZONE_DATA",
                observedAt,
                ["GetPowerChargeMode"]),
            ProbeWmi(
                metadata,
                HardwareCapability.BatteryRapidCharge,
                "LENOVO_GAMEZONE_DATA",
                observedAt,
                ["GetPowerChargeMode"]),
            ProbeWmi(
                metadata,
                HardwareCapability.ThermalMode,
                "LENOVO_GAMEZONE_DATA",
                observedAt,
                ["GetSmartFanMode", "SetSmartFanMode"]),
            ProbeWmi(
                metadata,
                HardwareCapability.FanControl,
                "LENOVO_FAN_METHOD",
                observedAt,
                ["Fan_Get_Table", "Fan_Set_Table"],
                ["Fan_Get_FullSpeed", "Fan_Set_FullSpeed"]),
            ProbeWmi(
                metadata,
                HardwareCapability.WhiteKeyboardBacklight,
                "LENOVO_GAMEZONE_DATA",
                observedAt,
                ["GetKeyboardLight", "SetKeyboardLight"]),
        };

        cancellationToken.ThrowIfCancellationRequested();
        HidInventoryAttempt hidInventory = ReadHidInventory();
        evidence.Add(ProbeHid(
            hidInventory,
            HardwareCapability.FourZoneRgbKeyboard,
            FourZoneProductIds,
            observedAt));
        evidence.Add(ProbeHid(
            hidInventory,
            HardwareCapability.SpectrumKeyboard,
            SpectrumProductIds,
            observedAt));

        cancellationToken.ThrowIfCancellationRequested();
        evidence.Add(ProbeWmi(
            metadata,
            HardwareCapability.DisplayOverdrive,
            "LENOVO_GAMEZONE_DATA",
            observedAt,
            ["IsSupportOD", "GetODStatus", "SetODStatus"]));
        evidence.Add(ProbeWmi(
            metadata,
            HardwareCapability.HybridGraphicsMode,
            "LENOVO_GAMEZONE_DATA",
            observedAt,
            ["IsSupportIGPUMode", "GetIGPUModeStatus", "SetIGPUModeStatus"]));
        evidence.Add(ProbeWmi(
            metadata,
            HardwareCapability.GpuWorkingMode,
            "LENOVO_GAMEZONE_DATA",
            observedAt,
            ["GetGpuGpsState", "SetGpuGpsState"]));

        if (evidence.Count != ProbedCapabilities.Count)
            throw new InvalidOperationException("Every declared hardware capability must produce evidence.");

        return evidence.AsReadOnly();
    }

    private CapabilityEvidence ProbeWmi(
        IDictionary<string, MetadataAttempt> cache,
        HardwareCapability capability,
        string className,
        DateTimeOffset observedAt,
        params string[][] acceptedMethodSets)
    {
        MetadataAttempt attempt = GetMetadata(cache, className);
        if (attempt.ErrorDetail is not null)
        {
            return Evidence(
                capability,
                CapabilitySupport.Unknown,
                "windows.wmi.metadata",
                observedAt,
                "wmi_metadata_query_failed",
                attempt.ErrorDetail);
        }

        WmiClassMetadata metadata = attempt.Metadata!;
        if (!metadata.Exists)
            return Evidence(capability, CapabilitySupport.Unsupported, "windows.wmi.metadata", observedAt, "wmi_class_missing");

        bool hasAcceptedMethodSet = acceptedMethodSets.Any(
            methodSet => methodSet.All(metadata.Methods.Contains));

        return hasAcceptedMethodSet
            ? Evidence(capability, CapabilitySupport.Unknown, "windows.wmi.metadata", observedAt, "wmi_interface_present_unverified")
            : Evidence(capability, CapabilitySupport.Unsupported, "windows.wmi.metadata", observedAt, "wmi_method_set_missing");
    }

    private MetadataAttempt GetMetadata(
        IDictionary<string, MetadataAttempt> cache,
        string className)
    {
        if (cache.TryGetValue(className, out MetadataAttempt? cached))
            return cached;

        MetadataAttempt attempt;
        try
        {
            attempt = new MetadataAttempt(
                _managementReader.ReadClassMetadata(LenovoWmiNamespace, className),
                null);
        }
        catch (Exception exception)
        {
            attempt = new MetadataAttempt(null, exception.GetType().Name);
        }

        cache[className] = attempt;
        return attempt;
    }

    private HidInventoryAttempt ReadHidInventory()
    {
        try
        {
            return new HidInventoryAttempt(
                _hidInventory.GetProductIds(LenovoLightingVendorId),
                null);
        }
        catch (Exception exception)
        {
            return new HidInventoryAttempt(null, exception.GetType().Name);
        }
    }

    private static CapabilityEvidence ProbeHid(
        HidInventoryAttempt inventory,
        HardwareCapability capability,
        IReadOnlySet<int> productIds,
        DateTimeOffset observedAt)
    {
        if (inventory.ErrorDetail is not null)
        {
            return Evidence(
                capability,
                CapabilitySupport.Unknown,
                "windows.hid.inventory",
                observedAt,
                "hid_inventory_failed",
                inventory.ErrorDetail);
        }

        bool present = productIds.Overlaps(inventory.ProductIds!);
        return present
            ? Evidence(capability, CapabilitySupport.Unknown, "windows.hid.inventory", observedAt, "hid_interface_present_unverified")
            : Evidence(capability, CapabilitySupport.Unsupported, "windows.hid.inventory", observedAt, "hid_interface_not_found");
    }

    private static CapabilityEvidence Evidence(
        HardwareCapability capability,
        CapabilitySupport support,
        string source,
        DateTimeOffset observedAt,
        string evidenceCode,
        string? detail = null) =>
        new(capability, support, source, observedAt, evidenceCode, detail);

    private sealed record MetadataAttempt(WmiClassMetadata? Metadata, string? ErrorDetail);

    private sealed record HidInventoryAttempt(IReadOnlySet<int>? ProductIds, string? ErrorDetail);
}
