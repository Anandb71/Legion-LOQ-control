using System.Text.Json;
using System.Text.Json.Serialization;
using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Contracts.Broker;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Broker;
using LegionLoqControl.Infrastructure.Windows.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Hardware;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var serializerOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() },
};

try
{
    string command = args.Length == 0 ? "inventory" : args[0].ToLowerInvariant();
    if (args.Length > 1 ||
        command is not ("inventory" or "state" or "state-elevated" or "--help" or "-h"))
    {
        Console.Error.WriteLine(
            "Usage: LegionLoqControl.Diagnostics [inventory|state|state-elevated]");
        return 64;
    }

    if (command is "--help" or "-h")
    {
        Console.WriteLine(
            "Usage: LegionLoqControl.Diagnostics [inventory|state|state-elevated]");
        Console.WriteLine("  inventory  Collect serial-free identity and interface evidence (default).");
        Console.WriteLine("  state      Invoke allowlisted Lenovo getters and return typed read results.");
        Console.WriteLine("  state-elevated  Request the same reads through the short-lived UAC broker.");
        return 0;
    }

    if (command == "state-elevated")
    {
        var broker = new ElevatedHardwareStateBrokerClient();
        HardwareStateReadResponse response = await broker.ReadAsync(cancellation.Token);
        if (response.Status != BrokerReadStatus.Succeeded)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(
                new { error = response.ErrorCode, status = response.Status },
                serializerOptions));
            return 3;
        }

        HardwareStateSnapshot state = response.Snapshot!.ToSnapshot();
        Console.WriteLine(JsonSerializer.Serialize(state, serializerOptions));
        return 0;
    }

    if (command == "state")
    {
        var stateService = new HardwareStateService(new WindowsHardwareStateReader());
        HardwareStateSnapshot state = await stateService.CaptureAsync(cancellation.Token);
        Console.WriteLine(JsonSerializer.Serialize(state, serializerOptions));
        return 0;
    }

    var diagnosticsService = new MachineDiagnosticsService(
        new WindowsMachineIdentitySource(),
        [new WindowsCapabilityProbe()]);
    MachineSnapshot snapshot = await diagnosticsService.CaptureAsync(cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(snapshot, serializerOptions));
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("{\"error\":\"diagnostics_cancelled\"}");
    return 2;
}
catch (BrokerTransportException exception)
{
    int? nativeErrorCode = exception.InnerException is System.ComponentModel.Win32Exception win32
        ? win32.NativeErrorCode
        : null;
    Console.Error.WriteLine(JsonSerializer.Serialize(
        new
        {
            error = exception.ErrorCode,
            detail = exception.InnerException?.GetType().Name,
            nativeErrorCode,
        },
        serializerOptions));
    return 3;
}
catch (Exception exception)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(
        new { error = "diagnostics_failed", detail = exception.GetType().Name },
        serializerOptions));
    return 1;
}
