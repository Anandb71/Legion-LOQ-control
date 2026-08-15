using LegionLoqControl.Application.Broker;
using LegionLoqControl.Contracts.Broker;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Services;
using LegionLoqControl.ViewModels;
using Xunit;

namespace LegionLoqControl.Presentation.Tests;

public sealed class DashboardViewModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Initialization_retains_the_typed_machine_snapshot()
    {
        MachineSnapshot machine = CreateMachineSnapshot();
        var source = new StubDashboardDataSource(machine, CreateHardwareSnapshot());
        var session = new MachineSessionViewModel();
        var viewModel = new MainWindowViewModel(source, session);

        await viewModel.InitializeAsync();

        Assert.Same(machine, session.MachineSnapshot);
        Assert.Equal("LOQ 15IRX9", viewModel.DeviceName);
        Assert.Equal("Machine type 83DV · BIOS NECN50WW", viewModel.DeviceMetadata);
        Assert.Equal("Inventory complete · 1 candidate interfaces", viewModel.InventoryStatus);
        Assert.Equal(
            "Development broker · unsigned sibling, not a public install",
            viewModel.BrokerInstallStatus);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(1, source.InventoryReadCount);
        Assert.Equal(1, source.BrokerInstallAssessCount);
    }

    [Fact]
    public async Task Explicit_refresh_retains_typed_hardware_state_and_formats_cards()
    {
        HardwareStateSnapshot hardware = CreateHardwareSnapshot();
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), hardware);
        var session = new MachineSessionViewModel();
        var viewModel = new MainWindowViewModel(source, session);
        await viewModel.InitializeAsync();

        await viewModel.RefreshHardwareStateCommand.ExecuteAsync(null);

        Assert.Same(hardware, session.HardwareStateSnapshot);
        Assert.Equal("Conservation", viewModel.Battery.Value);
        Assert.Equal("Performance", viewModel.Thermal.Value);
        Assert.Equal("Disabled", viewModel.DisplayOverdrive.Value);
        Assert.Equal("Integrated only", viewModel.IntegratedGpu.Value);
        Assert.Equal("Hardware state verified", viewModel.BannerTitle);
        Assert.Equal(1, source.HardwareReadCount);
    }

    [Fact]
    public async Task Broker_failure_does_not_replace_the_last_typed_snapshot()
    {
        HardwareStateSnapshot hardware = CreateHardwareSnapshot();
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), hardware);
        var session = new MachineSessionViewModel();
        var viewModel = new MainWindowViewModel(source, session);
        await viewModel.InitializeAsync();
        await viewModel.RefreshHardwareStateCommand.ExecuteAsync(null);
        source.ReadException = new DashboardDataSourceException(
            "broker_elevation_cancelled");

        await viewModel.RefreshHardwareStateCommand.ExecuteAsync(null);

        Assert.Same(hardware, session.HardwareStateSnapshot);
        Assert.Equal("Elevation cancelled", viewModel.BannerTitle);
        Assert.Equal(DashboardStateKind.Warning, viewModel.BannerState);
        Assert.Equal(2, source.HardwareReadCount);
    }

    [Fact]
    public async Task Broker_free_package_reports_the_missing_optional_broker()
    {
        var source = new StubDashboardDataSource(
            CreateMachineSnapshot(),
            CreateHardwareSnapshot())
        {
            ReadException = new DashboardDataSourceException("broker_not_found"),
        };
        var viewModel = new MainWindowViewModel(source);
        await viewModel.InitializeAsync();

        await viewModel.RefreshHardwareStateCommand.ExecuteAsync(null);

        Assert.Equal("Hardware state unavailable", viewModel.BannerTitle);
        Assert.Contains("not included in this package", viewModel.BannerMessage);
        Assert.Equal(DashboardStateKind.Error, viewModel.BannerState);
    }

    [Fact]
    public async Task Applying_a_thermal_option_uses_the_verified_expected_state()
    {
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), CreateHardwareSnapshot());
        var viewModel = new MainWindowViewModel(source);
        await viewModel.InitializeAsync();
        await viewModel.RefreshHardwareStateCommand.ExecuteAsync(null);

        await viewModel.Thermal.ApplyOptionCommand.ExecuteAsync(nameof(ThermalMode.Quiet));

        Assert.Equal("Hardware change verified", viewModel.BannerTitle);
        Assert.Equal(DashboardStateKind.Success, viewModel.BannerState);
        Assert.Equal(HardwareWriteTarget.ThermalMode, source.LastWriteTarget);
        Assert.Equal(nameof(ThermalMode.Performance), source.LastWriteExpected);
        Assert.Equal(nameof(ThermalMode.Quiet), source.LastWriteDesired);
        Assert.True(viewModel.Thermal.CanApply);
    }

    [Fact]
    public async Task Applying_a_battery_option_uses_the_verified_expected_state()
    {
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), CreateHardwareSnapshot());
        var viewModel = new MainWindowViewModel(source);
        await viewModel.InitializeAsync();
        await viewModel.RefreshHardwareStateCommand.ExecuteAsync(null);

        await viewModel.Battery.ApplyOptionCommand.ExecuteAsync(
            nameof(BatteryChargeMode.RapidCharge));

        Assert.Equal("Hardware change verified", viewModel.BannerTitle);
        Assert.Equal(DashboardStateKind.Success, viewModel.BannerState);
        Assert.Equal(HardwareWriteTarget.BatteryChargeMode, source.LastWriteTarget);
        Assert.Equal(nameof(BatteryChargeMode.Conservation), source.LastWriteExpected);
        Assert.Equal(nameof(BatteryChargeMode.RapidCharge), source.LastWriteDesired);
        Assert.True(viewModel.Battery.CanApply);
    }

    [Fact]
    public async Task Production_install_refusal_explains_the_unprotected_path()
    {
        var source = new StubDashboardDataSource(
            CreateMachineSnapshot(),
            CreateHardwareSnapshot())
        {
            ReadException = new DashboardDataSourceException("broker_install_unprotected"),
        };
        var viewModel = new MainWindowViewModel(source);
        await viewModel.InitializeAsync();

        await viewModel.RefreshHardwareStateCommand.ExecuteAsync(null);

        Assert.Equal("Hardware state unavailable", viewModel.BannerTitle);
        Assert.Contains("administrator-protected", viewModel.BannerMessage);
        Assert.Equal(DashboardStateKind.Error, viewModel.BannerState);
    }

    private static MachineSnapshot CreateMachineSnapshot()
    {
        var identity = new MachineIdentity(
            Observation.FromValue("LENOVO"),
            Observation.FromValue("LOQ 15IRX9"),
            Observation.FromValue("LOQ 15IRX9"),
            Observation.FromValue("83DV"),
            Observation.FromValue("NECN50WW"));
        CapabilityEvidence[] capabilities =
        [
            new(
                HardwareCapability.ThermalMode,
                CapabilitySupport.Unknown,
                "test",
                Now,
                "wmi_interface_present_unverified"),
        ];
        return new MachineSnapshot(identity, Now, capabilities);
    }

    private static HardwareStateSnapshot CreateHardwareSnapshot() =>
        new(
            Now,
            HardwareReadResult<BatteryChargeMode>.Success(
                BatteryChargeMode.Conservation),
            HardwareReadResult<ThermalMode>.Success(ThermalMode.Performance),
            HardwareReadResult<ToggleState>.Success(ToggleState.Disabled),
            HardwareReadResult<IntegratedGpuMode>.Success(
                IntegratedGpuMode.IntegratedOnly));

    private sealed class StubDashboardDataSource(
        MachineSnapshot machineSnapshot,
        HardwareStateSnapshot hardwareStateSnapshot) : IDashboardDataSource
    {
        public int InventoryReadCount { get; private set; }

        public int HardwareReadCount { get; private set; }

        public int BrokerInstallAssessCount { get; private set; }

        public Exception? ReadException { get; set; }

        public BrokerInstallAssessment BrokerInstall { get; set; } =
            new(
                BrokerInstallPlacement.SiblingDevelopment,
                BrokerSignatureStatus.Unsigned,
                DirectoryProtected: false,
                AllowsDevelopmentRead: true,
                AllowsProductionRelease: false,
                "broker_install_development");

        public Task<MachineSnapshot> CaptureMachineAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InventoryReadCount++;
            return Task.FromResult(machineSnapshot);
        }

        public ValueTask<HardwareStateSnapshot> ReadHardwareStateAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HardwareReadCount++;
            if (ReadException is not null)
                return ValueTask.FromException<HardwareStateSnapshot>(ReadException);

            return ValueTask.FromResult(hardwareStateSnapshot);
        }

        public BrokerInstallAssessment AssessBrokerInstall()
        {
            BrokerInstallAssessCount++;
            return BrokerInstall;
        }

        public HardwareWriteTarget? LastWriteTarget { get; private set; }

        public string? LastWriteExpected { get; private set; }

        public string? LastWriteDesired { get; private set; }

        public ValueTask<HardwareStateSnapshot> ApplyHardwareWriteAsync(
            HardwareWriteTarget target,
            string expected,
            string desired,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastWriteTarget = target;
            LastWriteExpected = expected;
            LastWriteDesired = desired;
            if (ReadException is not null)
                return ValueTask.FromException<HardwareStateSnapshot>(ReadException);

            return ValueTask.FromResult(hardwareStateSnapshot);
        }
    }
}

