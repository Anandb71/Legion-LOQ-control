using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;
using Xunit;

namespace LegionLoqControl.Application.Tests.Hardware;

public sealed class HardwareStateWriteServiceTests
{
    [Fact]
    public async Task Apply_writes_only_after_the_expected_state_matches()
    {
        var reader = new StubReader(ThermalMode.Balanced, ThermalMode.Performance);
        var writer = new StubWriter();
        var service = new HardwareStateWriteService(() => reader, writer);

        HardwareStateSnapshot snapshot = await service.ApplyAsync(
            HardwareWriteKind.ThermalMode,
            nameof(ThermalMode.Balanced),
            nameof(ThermalMode.Performance),
            TestContext.Current.CancellationToken);

        Assert.Equal(ThermalMode.Performance, snapshot.ThermalMode.Value);
        Assert.Equal(ThermalMode.Performance, writer.LastThermal);
        Assert.Equal(2, reader.CaptureCount);
    }

    [Fact]
    public async Task Apply_refuses_a_stale_expected_value()
    {
        var reader = new StubReader(ThermalMode.Quiet, ThermalMode.Quiet);
        var service = new HardwareStateWriteService(() => reader, new StubWriter());

        HardwareWriteException exception = await Assert.ThrowsAsync<HardwareWriteException>(
            () => service.ApplyAsync(
                HardwareWriteKind.ThermalMode,
                nameof(ThermalMode.Performance),
                nameof(ThermalMode.Quiet),
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("thermal_expected_mismatch", exception.ErrorCode);
        Assert.Equal(HardwareWriteStatus.Conflict, exception.Status);
    }

    [Fact]
    public async Task Apply_does_not_write_custom_thermal_mode()
    {
        var writer = new StubWriter();
        var service = new HardwareStateWriteService(
            () => new StubReader(ThermalMode.Balanced, ThermalMode.Balanced),
            writer);

        HardwareWriteException exception = await Assert.ThrowsAsync<HardwareWriteException>(
            () => service.ApplyAsync(
                HardwareWriteKind.ThermalMode,
                nameof(ThermalMode.Balanced),
                nameof(ThermalMode.Custom),
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("thermal_custom_unsupported", exception.ErrorCode);
        Assert.Null(writer.LastThermal);
    }

    [Fact]
    public async Task Apply_writes_battery_mode_after_the_expected_state_matches()
    {
        var reader = new StubReader(
            [ThermalMode.Balanced, ThermalMode.Balanced],
            [BatteryChargeMode.Normal, BatteryChargeMode.Conservation]);
        var writer = new StubWriter();
        var service = new HardwareStateWriteService(() => reader, writer);

        HardwareStateSnapshot snapshot = await service.ApplyAsync(
            HardwareWriteKind.BatteryChargeMode,
            nameof(BatteryChargeMode.Normal),
            nameof(BatteryChargeMode.Conservation),
            TestContext.Current.CancellationToken);

        Assert.Equal(BatteryChargeMode.Conservation, snapshot.BatteryChargeMode.Value);
        Assert.Equal(BatteryChargeMode.Conservation, writer.LastBattery);
        Assert.Equal(BatteryChargeMode.Normal, writer.LastBatteryExpected);
    }

    [Fact]
    public async Task Apply_does_not_write_when_battery_mode_is_already_desired()
    {
        var writer = new StubWriter();
        var service = new HardwareStateWriteService(
            () => new StubReader(
                [ThermalMode.Balanced],
                [BatteryChargeMode.Conservation]),
            writer);

        HardwareStateSnapshot snapshot = await service.ApplyAsync(
            HardwareWriteKind.BatteryChargeMode,
            nameof(BatteryChargeMode.Conservation),
            nameof(BatteryChargeMode.Conservation),
            TestContext.Current.CancellationToken);

        Assert.Equal(BatteryChargeMode.Conservation, snapshot.BatteryChargeMode.Value);
        Assert.Null(writer.LastBattery);
    }

    [Fact]
    public async Task Apply_writes_four_zone_brightness_when_the_controller_is_present()
    {
        var writer = new StubWriter();
        var service = new HardwareStateWriteService(
            () => new StubReader(ThermalMode.Balanced, ThermalMode.Balanced),
            writer);

        HardwareStateSnapshot snapshot = await service.ApplyAsync(
            HardwareWriteKind.FourZoneKeyboard,
            nameof(FourZoneKeyboardMode.Unknown),
            nameof(FourZoneKeyboardMode.High),
            TestContext.Current.CancellationToken);

        Assert.Equal(FourZoneKeyboardMode.Unknown, snapshot.FourZoneKeyboard.Value);
        Assert.Equal(FourZoneKeyboardMode.High, writer.LastKeyboard);
    }

    private sealed class StubReader : IHardwareStateReader
    {
        private readonly Queue<ThermalMode> _thermal;
        private readonly Queue<BatteryChargeMode> _battery;

        public StubReader(params ThermalMode[] thermal)
            : this(thermal, [BatteryChargeMode.Normal, BatteryChargeMode.Normal])
        {
        }

        public StubReader(ThermalMode[] thermal, BatteryChargeMode[] battery)
        {
            _thermal = new Queue<ThermalMode>(thermal);
            _battery = new Queue<BatteryChargeMode>(battery);
        }

        public int CaptureCount { get; private set; }

        public ValueTask<HardwareReadResult<BatteryChargeMode>> ReadBatteryChargeModeAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(HardwareReadResult<BatteryChargeMode>.Success(_battery.Dequeue()));

        public ValueTask<HardwareReadResult<ThermalMode>> ReadThermalModeAsync(
            CancellationToken cancellationToken)
        {
            CaptureCount++;
            return ValueTask.FromResult(HardwareReadResult<ThermalMode>.Success(_thermal.Dequeue()));
        }

        public ValueTask<HardwareReadResult<ToggleState>> ReadDisplayOverdriveAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(HardwareReadResult<ToggleState>.Success(ToggleState.Disabled));

        public ValueTask<HardwareReadResult<IntegratedGpuMode>> ReadIntegratedGpuModeAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                HardwareReadResult<IntegratedGpuMode>.Success(IntegratedGpuMode.Default));

        public ValueTask<HardwareReadResult<FourZoneKeyboardMode>> ReadFourZoneKeyboardAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                HardwareReadResult<FourZoneKeyboardMode>.Success(FourZoneKeyboardMode.Unknown));

        public ValueTask<HardwareReadResult<FanTableSnapshot>> ReadFanTableAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Unavailable,
                "fan_table_not_opened"));
    }

    private sealed class StubWriter : IHardwareStateWriter
    {
        public ThermalMode? LastThermal { get; private set; }

        public BatteryChargeMode? LastBattery { get; private set; }

        public BatteryChargeMode? LastBatteryExpected { get; private set; }

        public ValueTask WriteThermalModeAsync(
            ThermalMode desired,
            CancellationToken cancellationToken)
        {
            LastThermal = desired;
            return ValueTask.CompletedTask;
        }

        public ValueTask WriteDisplayOverdriveAsync(
            ToggleState desired,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask WriteIntegratedGpuModeAsync(
            IntegratedGpuMode desired,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public FourZoneKeyboardMode? LastKeyboard { get; private set; }

        public ValueTask WriteBatteryChargeModeAsync(
            BatteryChargeMode expected,
            BatteryChargeMode desired,
            CancellationToken cancellationToken)
        {
            LastBatteryExpected = expected;
            LastBattery = desired;
            return ValueTask.CompletedTask;
        }

        public ValueTask WriteFourZoneKeyboardAsync(
            FourZoneKeyboardMode desired,
            CancellationToken cancellationToken)
        {
            LastKeyboard = desired;
            return ValueTask.CompletedTask;
        }
    }
}
