using System.Text.Json;
using System.Text.Json.Serialization;
using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Diagnostics;

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
    var service = new MachineDiagnosticsService(
        new WindowsMachineIdentitySource(),
        [new WindowsCapabilityProbe()]);

    MachineSnapshot snapshot = await service.CaptureAsync(cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(snapshot, serializerOptions));
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("{\"error\":\"diagnostics_cancelled\"}");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(
        new { error = "diagnostics_failed", detail = exception.GetType().Name },
        serializerOptions));
    return 1;
}
