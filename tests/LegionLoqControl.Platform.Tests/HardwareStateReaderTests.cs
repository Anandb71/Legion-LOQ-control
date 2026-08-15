using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Hardware;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class HardwareStateReaderTests
{
    [Theory]
    [InlineData(1u, ThermalMode.Quiet)]
    [InlineData(2u, ThermalMode.Balanced)]
    [InlineData(3u, ThermalMode.Performance)]
    [InlineData(224u, ThermalMode.Extreme)]
    [InlineData(255u, ThermalMode.Custom)]
    public async Task Thermal_values_map_to_typed_modes(uint rawValue, ThermalMode expected)
    {
        var invoker = new StubInvoker();
        invoker.Values[LenovoWmiReadOperation.ThermalMode] = rawValue;
        var reader = new WindowsHardwareStateReader(invoker);

        HardwareReadResult<ThermalMode> result = await reader.ReadThermalModeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.Success, result.Status);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Battery_mode_is_not_inferred_from_an_unrelated_WMI_getter()
    {
        var invoker = new StubInvoker();
        var reader = new WindowsHardwareStateReader(invoker);

        HardwareReadResult<BatteryChargeMode> result = await reader.ReadBatteryChargeModeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.Unavailable, result.Status);
        Assert.False(result.HasValue);
        Assert.Equal("battery_read_transport_not_implemented", result.ErrorCode);
        Assert.Empty(invoker.Operations);
    }

    [Fact]
    public async Task Privileged_reader_delegates_battery_state_to_the_Energy_adapter()
    {
        var invoker = new StubInvoker();
        var batteryReader = new StubBatteryReader(
            HardwareReadResult<BatteryChargeMode>.Success(
                BatteryChargeMode.Conservation));
        var reader = new WindowsHardwareStateReader(invoker, batteryReader);

        HardwareReadResult<BatteryChargeMode> result =
            await reader.ReadBatteryChargeModeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.Success, result.Status);
        Assert.Equal(BatteryChargeMode.Conservation, result.Value);
        Assert.Equal(1, batteryReader.CallCount);
        Assert.Empty(invoker.Operations);
    }

    [Theory]
    [InlineData(0x00u, BatteryChargeMode.Normal)]
    [InlineData(0x80u, BatteryChargeMode.Normal)]
    [InlineData(0x20u, BatteryChargeMode.Conservation)]
    [InlineData(0x04u, BatteryChargeMode.RapidCharge)]
    public void Energy_driver_bits_map_to_battery_modes(
        uint rawValue,
        BatteryChargeMode expected)
    {
        HardwareReadResult<BatteryChargeMode> result =
            EnergyDriverBatteryReader.MapRawValue(rawValue);

        Assert.Equal(HardwareReadStatus.Success, result.Status);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Conflicting_energy_driver_bits_are_rejected()
    {
        HardwareReadResult<BatteryChargeMode> result =
            EnergyDriverBatteryReader.MapRawValue(0x24);

        Assert.Equal(HardwareReadStatus.InvalidData, result.Status);
        Assert.False(result.HasValue);
        Assert.Equal("energy_battery_mode_conflict", result.ErrorCode);
    }

    [Fact]
    public void Energy_driver_reader_is_locked_to_the_read_contract()
    {
        Assert.Equal(0x831020F8u, EnergyDriverBatteryReader.ControlCodeForValidation);
        Assert.Equal(0xFFu, EnergyDriverBatteryReader.ReadSelectorForValidation);
        Assert.Equal(0u, EnergyDriverBatteryReader.DesiredAccessForValidation);
    }

    [Theory]
    [InlineData(BatteryChargeMode.Normal, BatteryChargeMode.Conservation, 0x03u)]
    [InlineData(BatteryChargeMode.Conservation, BatteryChargeMode.Normal, 0x05u)]
    [InlineData(BatteryChargeMode.Normal, BatteryChargeMode.RapidCharge, 0x07u)]
    [InlineData(BatteryChargeMode.RapidCharge, BatteryChargeMode.Normal, 0x08u)]
    [InlineData(BatteryChargeMode.Conservation, BatteryChargeMode.RapidCharge, 0x07u)]
    public void Energy_driver_write_selectors_are_fixed(
        BatteryChargeMode expected,
        BatteryChargeMode desired,
        uint selector)
    {
        Assert.Equal(selector, EnergyDriverBatteryWriter.ResolveSelector(expected, desired));
    }

    [Fact]
    public void Energy_driver_writer_is_locked_to_the_write_contract()
    {
        Assert.Equal(0x831020F8u, EnergyDriverBatteryWriter.ControlCodeForValidation);
        Assert.Equal(0xC0000000u, EnergyDriverBatteryWriter.DesiredAccessForValidation);
        Assert.Throws<HardwareWriteException>(
            () => EnergyDriverBatteryWriter.ResolveSelector(
                BatteryChargeMode.Normal,
                BatteryChargeMode.Normal));
    }

    [Fact]
    public void Getter_output_contract_requires_true_status_and_UInt32_data()
    {
        SystemLenovoWmiReadInvoker.ValidateReturnValue(true);
        Assert.Equal(42u, SystemLenovoWmiReadInvoker.ConvertDataToUInt32(42u));

        Assert.Throws<LenovoWmiMethodRejectedException>(
            () => SystemLenovoWmiReadInvoker.ValidateReturnValue(false));
        Assert.Throws<InvalidDataException>(
            () => SystemLenovoWmiReadInvoker.ValidateReturnValue(0u));
        Assert.Throws<InvalidDataException>(
            () => SystemLenovoWmiReadInvoker.ConvertDataToUInt32("42"));
    }

    [Fact]
    public void PowerShell_batch_requires_typed_success_for_every_getter()
    {
        const string json =
            """
            {
              "status": "success",
              "results": {
                "thermalMode": {
                  "status": "success",
                  "returnStatus": true,
                  "data": 3
                },
                "displayOverdrive": {
                  "status": "success",
                  "returnStatus": true,
                  "data": 0
                },
                "integratedGpuMode": {
                  "status": "success",
                  "returnStatus": true,
                  "data": 1
                }
              }
            }
            """;

        IReadOnlyDictionary<LenovoWmiReadOperation, GetterOutcome> outcomes =
            PowerShellLenovoWmiReadInvoker.ParseBatch(json);

        Assert.Equal(3u, outcomes[LenovoWmiReadOperation.ThermalMode].GetValue());
        Assert.Equal(0u, outcomes[LenovoWmiReadOperation.DisplayOverdrive].GetValue());
        Assert.Equal(1u, outcomes[LenovoWmiReadOperation.IntegratedGpuMode].GetValue());
    }

    [Fact]
    public void PowerShell_batch_preserves_rejection_and_access_denial()
    {
        const string rejectedJson =
            """
            {
              "status": "success",
              "results": {
                "thermalMode": {
                  "status": "success",
                  "returnStatus": false,
                  "data": 3
                },
                "displayOverdrive": { "status": "access_denied" },
                "integratedGpuMode": { "status": "not_supported" }
              }
            }
            """;

        IReadOnlyDictionary<LenovoWmiReadOperation, GetterOutcome> outcomes =
            PowerShellLenovoWmiReadInvoker.ParseBatch(rejectedJson);

        Assert.Throws<LenovoWmiMethodRejectedException>(
            () => outcomes[LenovoWmiReadOperation.ThermalMode].GetValue());
        LenovoWmiReadFailureException denied = Assert.Throws<LenovoWmiReadFailureException>(
            () => outcomes[LenovoWmiReadOperation.DisplayOverdrive].GetValue());
        Assert.Equal(HardwareReadStatus.AccessDenied, denied.Status);
        Assert.Equal("wmi_access_denied", denied.ErrorCode);
    }

    [Fact]
    public void PowerShell_batch_rejects_unknown_json_members()
    {
        const string json =
            """
            {
              "status": "access_denied",
              "results": null,
              "unexpected": true
            }
            """;

        Assert.Throws<InvalidDataException>(
            () => PowerShellLenovoWmiReadInvoker.ParseBatch(json));
    }

    [Fact]
    public void PowerShell_script_is_static_and_getter_only()
    {
        string script = PowerShellLenovoWmiReadInvoker.ScriptForValidation;

        Assert.Contains("'GetSmartFanMode'", script, StringComparison.Ordinal);
        Assert.Contains("'GetODStatus'", script, StringComparison.Ordinal);
        Assert.Contains("'GetIGPUModeStatus'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSmartFanMode", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SetODStatus", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SetIGPUModeStatus", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Add-Type", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Start-Process", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PowerShell_write_script_is_static_and_allowlisted()
    {
        string script = PowerShellLenovoWmiWriteInvoker.ScriptForValidation;

        Assert.Contains("'SetSmartFanMode'", script, StringComparison.Ordinal);
        Assert.Contains("'SetODStatus'", script, StringComparison.Ordinal);
        Assert.Contains("'SetIGPUModeStatus'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSmartFanMode", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Add-Type", script, StringComparison.OrdinalIgnoreCase);
        PowerShellLenovoWmiWriteInvoker.ParseResponse(
            """{"status":"success","returnStatus":true}""");
        Assert.Throws<LenovoWmiMethodRejectedException>(
            () => PowerShellLenovoWmiWriteInvoker.ParseResponse(
                """{"status":"success","returnStatus":false}"""));
    }

    [Fact]
    public async Task Unexpected_firmware_values_fail_as_invalid_data()
    {
        var invoker = new StubInvoker();
        invoker.Values[LenovoWmiReadOperation.DisplayOverdrive] = 7;
        var reader = new WindowsHardwareStateReader(invoker);

        HardwareReadResult<ToggleState> result = await reader.ReadDisplayOverdriveAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.InvalidData, result.Status);
        Assert.False(result.HasValue);
        Assert.Equal("unexpected_overdrive_value", result.ErrorCode);
    }

    [Fact]
    public async Task Access_denied_is_distinct_from_unsupported_and_off()
    {
        var invoker = new StubInvoker
        {
            Exception = new UnauthorizedAccessException(),
        };
        var reader = new WindowsHardwareStateReader(invoker);

        HardwareReadResult<IntegratedGpuMode> result = await reader.ReadIntegratedGpuModeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.AccessDenied, result.Status);
        Assert.False(result.HasValue);
        Assert.Equal("wmi_access_denied", result.ErrorCode);
    }

    [Fact]
    public async Task State_service_serializes_reads_in_a_stable_order()
    {
        DateTimeOffset now = new(2026, 8, 8, 12, 30, 0, TimeSpan.Zero);
        var reader = new OrderedStateReader();
        var service = new HardwareStateService(reader, new FixedTimeProvider(now));

        HardwareStateSnapshot snapshot = await service.CaptureAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ["battery", "thermal", "overdrive", "igpu"],
            reader.Operations);
        Assert.Equal(now, snapshot.ObservedAt);
        Assert.Equal(BatteryChargeMode.Normal, snapshot.BatteryChargeMode.Value);
        Assert.Equal(ThermalMode.Balanced, snapshot.ThermalMode.Value);
    }

    private sealed class StubInvoker : ILenovoWmiReadInvoker
    {
        public Dictionary<LenovoWmiReadOperation, uint> Values { get; } = [];

        public List<LenovoWmiReadOperation> Operations { get; } = [];

        public Exception? Exception { get; init; }

        public ValueTask<uint> ReadAsync(
            LenovoWmiReadOperation operation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Operations.Add(operation);
            if (Exception is not null)
                return ValueTask.FromException<uint>(Exception);

            return ValueTask.FromResult(Values[operation]);
        }
    }

    private sealed class StubBatteryReader(HardwareReadResult<BatteryChargeMode> result)
        : IEnergyDriverBatteryReader
    {
        public int CallCount { get; private set; }

        public ValueTask<HardwareReadResult<BatteryChargeMode>> ReadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class OrderedStateReader : IHardwareStateReader
    {
        public List<string> Operations { get; } = [];

        public ValueTask<HardwareReadResult<BatteryChargeMode>> ReadBatteryChargeModeAsync(
            CancellationToken cancellationToken)
        {
            Operations.Add("battery");
            return ValueTask.FromResult(HardwareReadResult<BatteryChargeMode>.Success(BatteryChargeMode.Normal));
        }

        public ValueTask<HardwareReadResult<ThermalMode>> ReadThermalModeAsync(
            CancellationToken cancellationToken)
        {
            Operations.Add("thermal");
            return ValueTask.FromResult(HardwareReadResult<ThermalMode>.Success(ThermalMode.Balanced));
        }

        public ValueTask<HardwareReadResult<ToggleState>> ReadDisplayOverdriveAsync(
            CancellationToken cancellationToken)
        {
            Operations.Add("overdrive");
            return ValueTask.FromResult(HardwareReadResult<ToggleState>.Success(ToggleState.Disabled));
        }

        public ValueTask<HardwareReadResult<IntegratedGpuMode>> ReadIntegratedGpuModeAsync(
            CancellationToken cancellationToken)
        {
            Operations.Add("igpu");
            return ValueTask.FromResult(HardwareReadResult<IntegratedGpuMode>.Success(IntegratedGpuMode.Default));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
