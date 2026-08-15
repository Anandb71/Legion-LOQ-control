using System.Reflection;
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
    DiagnosticsCliParseResult parsed = DiagnosticsCliParser.Parse(args);
    if (!parsed.IsValid)
    {
        Console.Error.WriteLine(DiagnosticsCliParser.Usage);
        return 64;
    }

    if (parsed.Verb == DiagnosticsCliVerb.Help)
    {
        Console.WriteLine(DiagnosticsCliParser.Usage);
        Console.WriteLine("  inventory  Collect serial-free identity and interface evidence (default).");
        Console.WriteLine("  state      Invoke allowlisted Lenovo getters and return typed read results.");
        Console.WriteLine("  state-elevated  Request the same reads through the elevated UAC broker.");
        Console.WriteLine("  --output   Write inventory through the atomic export writer. Inventory only.");
        return 0;
    }

    if (parsed.Verb == DiagnosticsCliVerb.StateElevated)
    {
        using var broker = new ElevatedHardwareStateBrokerClient();
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

    if (parsed.Verb == DiagnosticsCliVerb.State)
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
    DiagnosticsExportDocument document = new DiagnosticsExportService().Create(
        snapshot,
        retainedHardwareState: null,
        GetProductVersion());
    if (parsed.OutputPath is null)
    {
        Console.WriteLine(DiagnosticsJsonSerializer.SerializeToString(document));
        return 0;
    }

    await new JsonDiagnosticsExportWriter()
        .WriteAsync(
            document,
            parsed.OutputPath,
            DiagnosticsExportWriteMode.CreateNew,
            cancellation.Token)
        .ConfigureAwait(false);
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("{\"error\":\"diagnostics_cancelled\"}");
    return 2;
}
catch (DiagnosticsExportException exception)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(
        new { error = exception.ErrorCode },
        serializerOptions));
    return 1;
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

static string GetProductVersion()
{
    Assembly assembly = Assembly.GetExecutingAssembly();
    return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
               ?.InformationalVersion
           ?? assembly.GetName().Version?.ToString()
           ?? "unknown";
}
