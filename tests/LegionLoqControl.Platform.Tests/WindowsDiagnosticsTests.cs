using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Hid;
using LegionLoqControl.Infrastructure.Windows.Management;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class WindowsDiagnosticsTests
{
    [Fact]
    public async Task Identity_capture_uses_machine_fields_without_requesting_serial_numbers()
    {
        var reader = new StubManagementReader();
        reader.Instances["Win32_ComputerSystemProduct"] = new Dictionary<string, string?>
        {
            ["Vendor"] = "LENOVO",
            ["Name"] = "83DV",
            ["Version"] = "LOQ 15IRX9",
        };
        reader.Instances["Win32_ComputerSystem"] = new Dictionary<string, string?>
        {
            ["Manufacturer"] = "LENOVO",
            ["Model"] = "83DV",
            ["SystemFamily"] = "LOQ 15IRX9",
        };
        reader.Instances["Win32_BIOS"] = new Dictionary<string, string?>
        {
            ["SMBIOSBIOSVersion"] = "NECN50WW",
        };

        var source = new WindowsMachineIdentitySource(reader);
        MachineIdentity identity = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("LOQ 15IRX9", identity.Model.Value);
        Assert.Equal("83DV", identity.MachineType.Value);
        Assert.Equal("NECN50WW", identity.BiosVersion.Value);
        Assert.DoesNotContain(
            reader.RequestedProperties.SelectMany(static properties => properties),
            static property => property.Contains("Serial", StringComparison.OrdinalIgnoreCase) ||
                property.Equals("IdentifyingNumber", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Capability_scan_reports_candidates_without_claiming_verified_support()
    {
        DateTimeOffset now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
        var reader = new StubManagementReader();
        reader.Metadata["LENOVO_GAMEZONE_DATA"] = Metadata(
            "GetPowerChargeMode",
            "GetSmartFanMode", "SetSmartFanMode",
            "GetKeyboardLight", "SetKeyboardLight",
            "IsSupportOD", "GetODStatus", "SetODStatus",
            "IsSupportIGPUMode", "GetIGPUModeStatus", "SetIGPUModeStatus",
            "GetGpuGpsState", "SetGpuGpsState");
        reader.Metadata["LENOVO_FAN_METHOD"] = Metadata("Fan_Get_Table", "Fan_Set_Table");

        var probe = new WindowsCapabilityProbe(
            reader,
            new StubHidInventory(0xC935),
            new FixedTimeProvider(now));

        IReadOnlyCollection<CapabilityEvidence> evidence = await probe.ProbeAsync(
            TestIdentity(),
            TestContext.Current.CancellationToken);

        Assert.All(evidence, item => Assert.NotEqual(CapabilitySupport.Supported, item.Support));
        AssertEvidence(evidence, HardwareCapability.ThermalMode, CapabilitySupport.Unknown, "wmi_interface_present_unverified");
        AssertEvidence(evidence, HardwareCapability.FanControl, CapabilitySupport.Unknown, "wmi_interface_present_unverified");
        AssertEvidence(evidence, HardwareCapability.FourZoneRgbKeyboard, CapabilitySupport.Unknown, "hid_interface_present_unverified");
        AssertEvidence(evidence, HardwareCapability.SpectrumKeyboard, CapabilitySupport.Unsupported, "hid_interface_not_found");
        Assert.Equal(1, reader.MetadataRequests.Count(static name => name == "LENOVO_GAMEZONE_DATA"));
        Assert.Equal(1, reader.MetadataRequests.Count(static name => name == "LENOVO_FAN_METHOD"));
    }

    private static WmiClassMetadata Metadata(params string[] methods) =>
        new(true, methods.ToHashSet(StringComparer.OrdinalIgnoreCase));

    private static MachineIdentity TestIdentity()
    {
        Observation value = Observation.FromValue("Test");
        return new MachineIdentity(value, value, value, value, value);
    }

    private static void AssertEvidence(
        IEnumerable<CapabilityEvidence> evidence,
        HardwareCapability capability,
        CapabilitySupport support,
        string evidenceCode)
    {
        CapabilityEvidence item = Assert.Single(evidence, item => item.Capability == capability);
        Assert.Equal(support, item.Support);
        Assert.Equal(evidenceCode, item.EvidenceCode);
    }

    private sealed class StubManagementReader : IWindowsManagementReader
    {
        public Dictionary<string, IReadOnlyDictionary<string, string?>> Instances { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, WmiClassMetadata> Metadata { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<IReadOnlyCollection<string>> RequestedProperties { get; } = [];

        public List<string> MetadataRequests { get; } = [];

        public IReadOnlyDictionary<string, string?> ReadFirstInstance(
            string namespacePath,
            string className,
            IReadOnlyCollection<string> properties)
        {
            RequestedProperties.Add(properties.ToArray());
            return Instances.TryGetValue(className, out IReadOnlyDictionary<string, string?>? values)
                ? values
                : new Dictionary<string, string?>();
        }

        public WmiClassMetadata ReadClassMetadata(string namespacePath, string className)
        {
            MetadataRequests.Add(className);
            return Metadata.TryGetValue(className, out WmiClassMetadata? metadata)
                ? metadata
                : new WmiClassMetadata(false, new HashSet<string>());
        }
    }

    private sealed class StubHidInventory(params int[] presentProductIds) : IHidDeviceInventory
    {
        private readonly HashSet<int> _presentProductIds = [.. presentProductIds];

        public IReadOnlySet<int> GetProductIds(int vendorId) =>
            vendorId == 0x048D ? _presentProductIds : new HashSet<int>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
