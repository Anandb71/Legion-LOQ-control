using LegionLoqControl.Application.Automation;
using LegionLoqControl.Application.Broker;
using LegionLoqControl.Domain.Automation;
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
    public async Task Initialization_shows_live_power_glance_from_unelevated_telemetry()
    {
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), CreateHardwareSnapshot());
        var telemetry = new StubPowerTelemetry(
            PowerSourceKind.Ac,
            BatteryPercent: 87,
            Charging: true);
        var viewModel = new MainWindowViewModel(
            source,
            powerTelemetry: telemetry);

        await viewModel.InitializeAsync();

        Assert.Equal("AC · charging", viewModel.PowerSourceLabel);
        Assert.Equal("87%", viewModel.ChargeLabel);
        Assert.Equal("Conservation", viewModel.ChargingModeLabel);
        Assert.Equal(1, source.HardwareReadCount);
        Assert.Equal(1, telemetry.ReadCount);
    }

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
        Assert.Equal(1, source.HardwareReadCount);
        Assert.Equal(1, source.BrokerInstallAssessCount);
        Assert.Equal("Hardware session ready", viewModel.BannerTitle);
        Assert.True(viewModel.Battery.CanApply);
        Assert.True(viewModel.Thermal.CanApply);
    }

    [Fact]
    public async Task Applying_the_current_value_does_not_start_a_broker_write()
    {
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), CreateHardwareSnapshot());
        var viewModel = new MainWindowViewModel(source);
        await viewModel.InitializeAsync();

        await viewModel.Battery.ApplyOptionCommand.ExecuteAsync(
            nameof(BatteryChargeMode.Conservation));

        Assert.Equal(0, source.WriteCount);
        Assert.Null(source.LastWriteOperations);
        Assert.Equal("Hardware session ready", viewModel.BannerTitle);
        Assert.False(viewModel.IsBusy);
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
        Assert.Equal(2, source.HardwareReadCount);
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
        Assert.Equal(3, source.HardwareReadCount);
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
    public async Task Applying_keeps_the_last_verified_fan_table()
    {
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), CreateHardwareSnapshot())
        {
            WriteSnapshot = CreateHardwareSnapshot() with
            {
                ThermalMode = HardwareReadResult<ThermalMode>.Success(ThermalMode.Quiet),
                FanTable = HardwareReadResult<FanTableSnapshot>.Failure(
                    HardwareReadStatus.Unavailable,
                    "fan_table_not_requested"),
            },
        };
        var viewModel = new MainWindowViewModel(source);
        await viewModel.InitializeAsync();

        await viewModel.Thermal.ApplyOptionCommand.ExecuteAsync(nameof(ThermalMode.Quiet));

        Assert.Equal("Quiet", viewModel.Thermal.Value);
        Assert.Contains("2 OEM points", viewModel.Fans.Value, StringComparison.Ordinal);
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
    public async Task Applying_keyboard_brightness_uses_the_verified_expected_state()
    {
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), CreateHardwareSnapshot());
        var viewModel = new MainWindowViewModel(source);
        await viewModel.InitializeAsync();
        await viewModel.RefreshHardwareStateCommand.ExecuteAsync(null);

        await viewModel.Keyboard.ApplyOptionCommand.ExecuteAsync(
            nameof(FourZoneKeyboardMode.High));

        Assert.Equal("Hardware change verified", viewModel.BannerTitle);
        Assert.Equal(HardwareWriteTarget.FourZoneKeyboard, source.LastWriteTarget);
        Assert.Equal(nameof(FourZoneKeyboardMode.Unknown), source.LastWriteExpected);
        Assert.Equal(nameof(FourZoneKeyboardMode.High), source.LastWriteDesired);
        Assert.True(viewModel.Keyboard.CanApply);
    }

    [Fact]
    public async Task Applying_a_profile_sends_would_change_targets_in_one_batch()
    {
        HardwareStateSnapshot hardware = CreateHardwareSnapshot(DateTimeOffset.UtcNow);
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), hardware);
        var session = new MachineSessionViewModel();
        var viewModel = new MainWindowViewModel(source, session);
        await viewModel.InitializeAsync();
        session.UpdateHardwareStateSnapshot(hardware);
        viewModel.ProfileWorkspace.IncludeBattery = false;
        viewModel.ProfileWorkspace.IncludeThermal = true;
        viewModel.ProfileWorkspace.SelectedThermalMode = ThermalMode.Quiet;
        viewModel.ProfileWorkspace.PreviewDraftCommand.Execute(null);

        await viewModel.ProfileWorkspace.ApplyProfileCommand.ExecuteAsync(null);

        HardwareWriteOperation operation = Assert.Single(source.LastWriteOperations!);
        Assert.Equal(HardwareWriteTarget.ThermalMode, operation.Target);
        Assert.Equal(nameof(ThermalMode.Performance), operation.Expected);
        Assert.Equal(nameof(ThermalMode.Quiet), operation.Desired);
        Assert.Equal("Hardware change verified", viewModel.BannerTitle);
        Assert.Equal("Profile applied", viewModel.ProfileWorkspace.WorkspaceTitle);
    }

    [Fact]
    public async Task Fan_table_card_stays_read_only_after_a_verified_refresh()
    {
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), CreateHardwareSnapshot());
        var viewModel = new MainWindowViewModel(source);
        await viewModel.InitializeAsync();
        await viewModel.RefreshHardwareStateCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Fans.Options);
        Assert.False(viewModel.Fans.CanApply);
        Assert.Contains("2 OEM points", viewModel.Fans.Value, StringComparison.Ordinal);
        Assert.Contains("Edit speeds on POWER", viewModel.Fans.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Overnight_and_spectrum_are_omitted_until_a_typed_read_succeeds()
    {
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), CreateHardwareSnapshot());
        var viewModel = new MainWindowViewModel(source);
        await viewModel.InitializeAsync();

        Assert.False(viewModel.OvernightCharge.IsAvailable);
        Assert.False(viewModel.FnLock.IsAvailable);
        Assert.False(viewModel.Spectrum.IsAvailable);
        Assert.False(viewModel.HasInputControls);
        Assert.True(viewModel.HasLightingSurface);
        Assert.True(viewModel.LightingWorkspace.IsVisible);
        Assert.True(viewModel.FanCurveWorkspace.IsVisible);
        Assert.Equal("LENOVO", viewModel.ManufacturerLabel);
        Assert.Equal("83DV", viewModel.MachineTypeLabel);
    }

    [Fact]
    public async Task Applying_overnight_charge_uses_the_typed_broker_target()
    {
        HardwareStateSnapshot snapshot = CreateHardwareSnapshot() with
        {
            OvernightCharge = HardwareReadResult<ToggleState>.Success(ToggleState.Disabled),
        };
        var source = new StubDashboardDataSource(CreateMachineSnapshot(), snapshot)
        {
            WriteSnapshot = snapshot with
            {
                OvernightCharge = HardwareReadResult<ToggleState>.Success(ToggleState.Enabled),
            },
        };
        var viewModel = new MainWindowViewModel(source);
        await viewModel.InitializeAsync();

        await viewModel.OvernightCharge.ApplyOptionCommand.ExecuteAsync(nameof(ToggleState.Enabled));

        Assert.Equal(1, source.WriteCount);
        Assert.Equal(HardwareWriteTarget.OvernightCharge, source.LastWriteTarget);
        Assert.Equal(nameof(ToggleState.Disabled), source.LastWriteExpected);
        Assert.Equal(nameof(ToggleState.Enabled), source.LastWriteDesired);
        Assert.True(viewModel.OvernightCharge.IsAvailable);
        Assert.Equal("Enabled", viewModel.OvernightCharge.Value);
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

    private static HardwareStateSnapshot CreateHardwareSnapshot(
        DateTimeOffset? observedAt = null) =>
        new(
            observedAt ?? Now,
            HardwareReadResult<BatteryChargeMode>.Success(
                BatteryChargeMode.Conservation),
            HardwareReadResult<ThermalMode>.Success(ThermalMode.Performance),
            HardwareReadResult<ToggleState>.Success(ToggleState.Disabled),
            HardwareReadResult<IntegratedGpuMode>.Success(
                IntegratedGpuMode.IntegratedOnly),
            HardwareReadResult<FourZoneKeyboardMode>.Success(
                FourZoneKeyboardMode.Unknown),
            HardwareReadResult<FanTableSnapshot>.Success(
                new FanTableSnapshot(0, 0, [new FanTablePoint(0, 40), new FanTablePoint(80, 90)])));

    private sealed class StubPowerTelemetry(
        PowerSourceKind source,
        int? BatteryPercent,
        bool Charging) : ISystemPowerTelemetryReader
    {
        public int ReadCount { get; private set; }

        public ValueTask<SystemPowerTelemetry> ReadTelemetryAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return ValueTask.FromResult(new SystemPowerTelemetry(
                HardwareReadResult<PowerSourceKind>.Success(source),
                BatteryPercent,
                Charging));
        }
    }

    private sealed class StubDashboardDataSource(
        MachineSnapshot machineSnapshot,
        HardwareStateSnapshot hardwareStateSnapshot) : IDashboardDataSource
    {
        public int InventoryReadCount { get; private set; }

        public int HardwareReadCount { get; private set; }

        public int BrokerInstallAssessCount { get; private set; }

        public int WriteCount { get; private set; }

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
            CancellationToken cancellationToken,
            bool includeFanTable = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = includeFanTable;
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

        public IReadOnlyList<HardwareWriteOperation>? LastWriteOperations { get; private set; }

        public HardwareStateSnapshot? WriteSnapshot { get; set; }

        public ValueTask<HardwareStateSnapshot> ApplyHardwareWriteAsync(
            HardwareWriteTarget target,
            string expected,
            string desired,
            CancellationToken cancellationToken) =>
            ApplyHardwareWriteBatchAsync(
                [new HardwareWriteOperation(target, expected, desired)],
                cancellationToken);

        public ValueTask<HardwareStateSnapshot> ApplyHardwareWriteBatchAsync(
            IReadOnlyList<HardwareWriteOperation> operations,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            LastWriteOperations = operations;
            if (operations.Count == 1)
            {
                LastWriteTarget = operations[0].Target;
                LastWriteExpected = operations[0].Expected;
                LastWriteDesired = operations[0].Desired;
            }

            if (ReadException is not null)
                return ValueTask.FromException<HardwareStateSnapshot>(ReadException);

            return ValueTask.FromResult(WriteSnapshot ?? hardwareStateSnapshot);
        }

        public void Dispose()
        {
        }
    }
}

