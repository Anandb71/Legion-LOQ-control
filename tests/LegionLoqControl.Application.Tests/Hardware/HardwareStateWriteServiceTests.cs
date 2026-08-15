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
        Assert.Equal(0, reader.FanReadCount);
        Assert.Equal(HardwareReadStatus.Unavailable, snapshot.FanTable.Status);
        Assert.Equal("fan_table_not_requested", snapshot.FanTable.ErrorCode);
    }

    [Fact]
    public async Task Apply_rebases_onto_live_firmware_when_the_dashboard_is_stale()
    {
        var reader = new StubReader(ThermalMode.Quiet, ThermalMode.Balanced);
        var writer = new StubWriter();
        var service = new HardwareStateWriteService(() => reader, writer);

        HardwareStateSnapshot snapshot = await service.ApplyAsync(
            HardwareWriteKind.ThermalMode,
            nameof(ThermalMode.Performance),
            nameof(ThermalMode.Balanced),
            TestContext.Current.CancellationToken);

        Assert.Equal(ThermalMode.Balanced, snapshot.ThermalMode.Value);
        Assert.Equal(ThermalMode.Balanced, writer.LastThermal);
    }

    [Fact]
    public async Task Apply_skips_the_setter_when_live_firmware_already_matches()
    {
        var writer = new StubWriter();
        var service = new HardwareStateWriteService(
            () => new StubReader(ThermalMode.Quiet),
            writer);

        HardwareStateSnapshot snapshot = await service.ApplyAsync(
            HardwareWriteKind.ThermalMode,
            nameof(ThermalMode.Performance),
            nameof(ThermalMode.Quiet),
            TestContext.Current.CancellationToken);

        Assert.Equal(ThermalMode.Quiet, snapshot.ThermalMode.Value);
        Assert.Null(writer.LastThermal);
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

    [Fact]
    public async Task Apply_many_writes_battery_then_thermal_and_journals_both()
    {
        var reader = new StubReader(
            [ThermalMode.Balanced, ThermalMode.Balanced, ThermalMode.Balanced, ThermalMode.Quiet],
            [
                BatteryChargeMode.Normal,
                BatteryChargeMode.Conservation,
                BatteryChargeMode.Conservation,
                BatteryChargeMode.Conservation,
            ]);
        var writer = new StubWriter();
        var journal = new HardwareWriteJournal();
        var service = new HardwareStateWriteService(() => reader, writer, journal: journal);

        HardwareStateSnapshot snapshot = await service.ApplyManyAsync(
            [
                (HardwareWriteKind.BatteryChargeMode,
                    nameof(BatteryChargeMode.Normal),
                    nameof(BatteryChargeMode.Conservation)),
                (HardwareWriteKind.ThermalMode,
                    nameof(ThermalMode.Balanced),
                    nameof(ThermalMode.Quiet)),
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(BatteryChargeMode.Conservation, snapshot.BatteryChargeMode.Value);
        Assert.Equal(ThermalMode.Quiet, snapshot.ThermalMode.Value);
        Assert.Equal(BatteryChargeMode.Conservation, writer.LastBattery);
        Assert.Equal(ThermalMode.Quiet, writer.LastThermal);
        Assert.Equal(2, journal.Snapshot().Count);
        Assert.All(
            journal.Snapshot(),
            entry => Assert.Equal(HardwareWriteStatus.Succeeded, entry.Status));
    }

    [Fact]
    public async Task Apply_writes_overnight_charge_after_a_live_match()
    {
        var reader = new StubReader(ThermalMode.Balanced, ThermalMode.Balanced)
        {
            Overnight = ToggleState.Disabled,
            OvernightAfter = ToggleState.Enabled,
        };
        var writer = new StubWriter();
        var service = new HardwareStateWriteService(() => reader, writer);

        HardwareStateSnapshot snapshot = await service.ApplyAsync(
            HardwareWriteKind.OvernightCharge,
            nameof(ToggleState.Disabled),
            nameof(ToggleState.Enabled),
            TestContext.Current.CancellationToken);

        Assert.Equal(ToggleState.Enabled, snapshot.OvernightCharge.Value);
        Assert.Equal(ToggleState.Enabled, writer.LastOvernight);
    }

    [Fact]
    public async Task Apply_writes_a_bounded_fan_table_and_reads_it_back()
    {
        var table = new FanTableSnapshot(0, 0, [new FanTablePoint(10, 40), new FanTablePoint(80, 90)]);
        var desired = new FanTableSnapshot(0, 0, [new FanTablePoint(20, 40), new FanTablePoint(90, 90)]);
        var reader = new StubReader(
            [ThermalMode.Balanced, ThermalMode.Balanced, ThermalMode.Balanced],
            [BatteryChargeMode.Normal, BatteryChargeMode.Normal, BatteryChargeMode.Normal])
        {
            Fan = table,
            FanAfter = desired,
        };
        var writer = new StubWriter();
        var service = new HardwareStateWriteService(() => reader, writer);

        HardwareStateSnapshot snapshot = await service.ApplyAsync(
            HardwareWriteKind.FanTable,
            HardwareStateTokens.FormatFanTable(table),
            HardwareStateTokens.FormatFanTable(desired),
            TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.Success, snapshot.FanTable.Status);
        Assert.Equal(desired.PointCount, snapshot.FanTable.Value!.Value.PointCount);
        Assert.Equal(desired.Points[0], snapshot.FanTable.Value.Value.Points[0]);
        Assert.Equal(desired.Points[1], snapshot.FanTable.Value.Value.Points[1]);
        Assert.NotNull(writer.LastFan);
        Assert.Equal(20, writer.LastFan.Value.Points[0].Speed);
        Assert.Equal(90, writer.LastFan.Value.Points[1].Speed);
        Assert.True(reader.FanReadCount >= 1);
    }

    [Fact]
    public void Lighting_and_fan_tokens_round_trip()
    {
        var lighting = new FourZoneLightingState(
            FourZoneEffect.Wave,
            FourZoneKeyboardMode.Low,
            2,
            true,
            new RgbColor(1, 2, 3),
            new RgbColor(4, 5, 6),
            new RgbColor(7, 8, 9),
            new RgbColor(10, 11, 12));
        var fan = new FanTableSnapshot(0, 0, [new FanTablePoint(1, 2), new FanTablePoint(3, 4)]);

        Assert.Equal(lighting, HardwareStateTokens.ParseLighting(HardwareStateTokens.FormatLighting(lighting)));
        Assert.Equal(fan.PointCount, HardwareStateTokens.ParseFanTable(HardwareStateTokens.FormatFanTable(fan)).PointCount);
        Assert.Equal(fan.Points[1], HardwareStateTokens.ParseFanTable(HardwareStateTokens.FormatFanTable(fan)).Points[1]);
    }

    [Fact]
    public async Task Apply_rejects_a_second_write_while_one_is_running()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new StubReader(
            [ThermalMode.Balanced, ThermalMode.Performance],
            [BatteryChargeMode.Normal, BatteryChargeMode.Normal]);
        var service = new HardwareStateWriteService(
            () => reader,
            new HoldingWriter(entered, release));

        Task<HardwareStateSnapshot> first = service.ApplyAsync(
            HardwareWriteKind.ThermalMode,
            nameof(ThermalMode.Balanced),
            nameof(ThermalMode.Performance),
            TestContext.Current.CancellationToken).AsTask();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        HardwareWriteException exception = await Assert.ThrowsAsync<HardwareWriteException>(
            () => service.ApplyAsync(
                HardwareWriteKind.ThermalMode,
                nameof(ThermalMode.Balanced),
                nameof(ThermalMode.Quiet),
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("write_in_progress", exception.ErrorCode);
        Assert.Equal(HardwareWriteStatus.Busy, exception.Status);
        release.SetResult();
        await first;
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

        public int FanReadCount { get; private set; }

        public ToggleState? Overnight { get; init; }

        public ToggleState? OvernightAfter { get; init; }

        public FanTableSnapshot? Fan { get; init; }

        public FanTableSnapshot? FanAfter { get; init; }

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
            CancellationToken cancellationToken)
        {
            FanReadCount++;
            FanTableSnapshot? value = FanReadCount > 1 ? FanAfter ?? Fan : Fan;
            return ValueTask.FromResult(
                value is { } table
                    ? HardwareReadResult<FanTableSnapshot>.Success(table)
                    : HardwareReadResult<FanTableSnapshot>.Failure(
                        HardwareReadStatus.Unavailable,
                        "fan_table_not_opened"));
        }

        public ValueTask<HardwareReadResult<ToggleState>> ReadOvernightChargeAsync(
            CancellationToken cancellationToken)
        {
            ToggleState? value = CaptureCount > 1 ? OvernightAfter ?? Overnight : Overnight;
            return ValueTask.FromResult(
                value is { } overnight
                    ? HardwareReadResult<ToggleState>.Success(overnight)
                    : HardwareReadResult<ToggleState>.Failure(
                        HardwareReadStatus.Unavailable,
                        "overnight_not_implemented"));
        }
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

        public ToggleState? LastOvernight { get; private set; }

        public FanTableSnapshot? LastFan { get; private set; }

        public ValueTask WriteOvernightChargeAsync(
            ToggleState desired,
            CancellationToken cancellationToken)
        {
            LastOvernight = desired;
            return ValueTask.CompletedTask;
        }

        public ValueTask WriteFanTableAsync(
            FanTableSnapshot desired,
            CancellationToken cancellationToken)
        {
            LastFan = desired;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class HoldingWriter(
        TaskCompletionSource entered,
        TaskCompletionSource release) : IHardwareStateWriter
    {
        public async ValueTask WriteThermalModeAsync(
            ThermalMode desired,
            CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public ValueTask WriteDisplayOverdriveAsync(
            ToggleState desired,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask WriteIntegratedGpuModeAsync(
            IntegratedGpuMode desired,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask WriteBatteryChargeModeAsync(
            BatteryChargeMode expected,
            BatteryChargeMode desired,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask WriteFourZoneKeyboardAsync(
            FourZoneKeyboardMode desired,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
