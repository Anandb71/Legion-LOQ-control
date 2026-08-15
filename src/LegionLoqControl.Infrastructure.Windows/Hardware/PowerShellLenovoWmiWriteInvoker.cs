using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal enum LenovoWmiWriteOperation
{
    ThermalMode = 0,
    DisplayOverdrive = 1,
    IntegratedGpuMode = 2,
    LightControlOwner = 3,
}

internal interface ILenovoWmiWriteInvoker
{
    ValueTask WriteAsync(
        LenovoWmiWriteOperation operation,
        uint data,
        CancellationToken cancellationToken);
}

internal sealed class PowerShellLenovoWmiWriteInvoker : ILenovoWmiWriteInvoker
{
    private const int MaximumOutputBytes = 64 * 1024;
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(12);
    private static readonly string EncodedCommand = Convert.ToBase64String(
        Encoding.Unicode.GetBytes(PowerShellScript));
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
    };

    public async ValueTask WriteAsync(
        LenovoWmiWriteOperation operation,
        uint data,
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation));

        string output = await ExecutePowerShellAsync(
                Opcode(operation),
                data,
                cancellationToken)
            .ConfigureAwait(false);
        ParseResponse(output);
    }

    internal static string ScriptForValidation => PowerShellScript;

    internal static void ParseResponse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        PowerShellWriteResponse response;
        try
        {
            response = JsonSerializer.Deserialize<PowerShellWriteResponse>(
                json.TrimStart('\uFEFF'),
                SerializerOptions)
                ?? throw new InvalidDataException("The PowerShell setter response was null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The PowerShell setter response was invalid JSON.",
                exception);
        }

        if (string.Equals(response.Status, "success", StringComparison.Ordinal))
        {
            if (response.ReturnStatus != true)
                throw new LenovoWmiMethodRejectedException();
            return;
        }

        throw CreateFailure(response.Status);
    }

    private static int Opcode(LenovoWmiWriteOperation operation) =>
        operation switch
        {
            LenovoWmiWriteOperation.ThermalMode => 0,
            LenovoWmiWriteOperation.DisplayOverdrive => 1,
            LenovoWmiWriteOperation.IntegratedGpuMode => 2,
            LenovoWmiWriteOperation.LightControlOwner => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    private static async Task<string> ExecutePowerShellAsync(
        int opcode,
        uint data,
        CancellationToken cancellationToken)
    {
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string executable = Path.Combine(
            systemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        if (!File.Exists(executable))
        {
            throw new LenovoWmiReadFailureException(
                HardwareReadStatus.Unavailable,
                "windows_powershell_not_available");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = systemDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(EncodedCommand);
        startInfo.ArgumentList.Add("-");
        startInfo.ArgumentList.Add(opcode.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(data.ToString(CultureInfo.InvariantCulture));
        startInfo.Environment["PSModulePath"] = Path.Combine(
            systemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "Modules");
        startInfo.Environment["POWERSHELL_TELEMETRY_OPTOUT"] = "1";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new LenovoWmiReadFailureException(
                    HardwareReadStatus.Failed,
                    "powershell_launch_failed");
            }
        }
        catch (LenovoWmiReadFailureException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new LenovoWmiReadFailureException(
                HardwareReadStatus.Failed,
                "powershell_launch_failed",
                exception);
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProcessTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryTerminate(process);
            throw;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            TryTerminate(process);
            throw new LenovoWmiReadFailureException(
                HardwareReadStatus.TimedOut,
                "powershell_setter_timed_out");
        }

        string output = await standardOutput.ConfigureAwait(false);
        _ = await standardError.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new LenovoWmiReadFailureException(
                HardwareReadStatus.Failed,
                "powershell_setter_failed");
        }

        if (Encoding.UTF8.GetByteCount(output) is <= 0 or > MaximumOutputBytes)
            throw new InvalidDataException("The PowerShell setter output size was invalid.");

        return output;
    }

    private static LenovoWmiReadFailureException CreateFailure(string? status) =>
        status switch
        {
            "access_denied" => new(HardwareReadStatus.AccessDenied, "wmi_access_denied"),
            "not_found" => new(HardwareReadStatus.Unavailable, "wmi_provider_object_not_found"),
            "not_supported" => new(HardwareReadStatus.Unsupported, "wmi_setter_not_available"),
            "timed_out" => new(HardwareReadStatus.TimedOut, "wmi_setter_timed_out"),
            "invalid_data" => new(HardwareReadStatus.InvalidData, "wmi_output_invalid"),
            "invalid_opcode" => new(HardwareReadStatus.Failed, "wmi_setter_opcode_invalid"),
            _ => new(HardwareReadStatus.Failed, "wmi_setter_failed"),
        };

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
        }
    }

    private sealed record PowerShellWriteResponse(string Status, bool? ReturnStatus);

    private const string PowerShellScript =
        """
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        $WarningPreference = 'SilentlyContinue'
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

        function Get-FailureStatus {
            param([System.Management.Automation.ErrorRecord] $Record)

            $exception = $Record.Exception
            while ($null -ne $exception.InnerException) {
                $exception = $exception.InnerException
            }

            if ($exception.HResult -eq -2147024891 -or
                $exception.HResult -eq -2147217405) {
                return 'access_denied'
            }

            if ($Record.FullyQualifiedErrorId -like '*0x80041003*') {
                return 'access_denied'
            }

            return 'failed'
        }

        if ($args.Count -ne 2) {
            [ordered]@{ status = 'invalid_opcode'; returnStatus = $null } |
                ConvertTo-Json -Compress -Depth 3
            exit 0
        }

        $opcode = 0
        $data = [uint32]0
        if (-not [int]::TryParse([string]$args[0], [ref]$opcode) -or
            -not [uint32]::TryParse([string]$args[1], [ref]$data)) {
            [ordered]@{ status = 'invalid_opcode'; returnStatus = $null } |
                ConvertTo-Json -Compress -Depth 3
            exit 0
        }

        $method = switch ($opcode) {
            0 { 'SetSmartFanMode' }
            1 { 'SetODStatus' }
            2 { 'SetIGPUModeStatus' }
            3 { 'SetLightControlOwner' }
            default { $null }
        }
        if ($null -eq $method) {
            [ordered]@{ status = 'invalid_opcode'; returnStatus = $null } |
                ConvertTo-Json -Compress -Depth 3
            exit 0
        }

        try {
            $instances = @(CimCmdlets\Get-CimInstance `
                -Namespace 'root/WMI' `
                -ClassName 'LENOVO_GAMEZONE_DATA' `
                -ErrorAction Stop)
            if ($instances.Count -eq 0) {
                [ordered]@{ status = 'not_found'; returnStatus = $null } |
                    ConvertTo-Json -Compress -Depth 3
                exit 0
            }

            $instance = $instances |
                Where-Object { $_.Active -eq $true } |
                Select-Object -First 1
            if ($null -eq $instance) {
                $instance = $instances[0]
            }

            $output = CimCmdlets\Invoke-CimMethod `
                -InputObject $instance `
                -MethodName $method `
                -Arguments @{ Data = $data } `
                -ErrorAction Stop
            if ($output.ReturnValue -isnot [bool]) {
                [ordered]@{ status = 'invalid_data'; returnStatus = $null } |
                    ConvertTo-Json -Compress -Depth 3
                exit 0
            }

            [ordered]@{
                status = 'success'
                returnStatus = [bool]$output.ReturnValue
            } | ConvertTo-Json -Compress -Depth 3
        }
        catch {
            [ordered]@{
                status = Get-FailureStatus $_
                returnStatus = $null
            } | ConvertTo-Json -Compress -Depth 3
        }
        """;
}

internal sealed class WindowsHardwareStateWriter : IHardwareStateWriter
{
    private readonly ILenovoWmiWriteInvoker _invoker;
    private readonly IEnergyDriverBatteryWriter _batteryWriter;
    private readonly IFourZoneKeyboardHid _keyboard;

    public WindowsHardwareStateWriter()
        : this(
            new SystemLenovoWmiWriteInvoker(),
            new EnergyDriverBatteryWriter(),
            new FourZoneKeyboardHid())
    {
    }

    internal WindowsHardwareStateWriter(ILenovoWmiWriteInvoker invoker)
        : this(invoker, new EnergyDriverBatteryWriter(), new FourZoneKeyboardHid())
    {
    }

    internal WindowsHardwareStateWriter(
        ILenovoWmiWriteInvoker invoker,
        IEnergyDriverBatteryWriter batteryWriter)
        : this(invoker, batteryWriter, new FourZoneKeyboardHid())
    {
    }

    internal WindowsHardwareStateWriter(
        ILenovoWmiWriteInvoker invoker,
        IEnergyDriverBatteryWriter batteryWriter,
        IFourZoneKeyboardHid keyboard)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _batteryWriter = batteryWriter ?? throw new ArgumentNullException(nameof(batteryWriter));
        _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
    }

    public ValueTask WriteThermalModeAsync(
        ThermalMode desired,
        CancellationToken cancellationToken)
    {
        uint data = desired switch
        {
            ThermalMode.Quiet => 1,
            ThermalMode.Balanced => 2,
            ThermalMode.Performance => 3,
            ThermalMode.Extreme => 224,
            _ => throw new HardwareWriteException(
                "thermal_custom_unsupported",
                HardwareWriteStatus.Unsupported),
        };
        return WriteAsync(LenovoWmiWriteOperation.ThermalMode, data, cancellationToken);
    }

    public ValueTask WriteDisplayOverdriveAsync(
        ToggleState desired,
        CancellationToken cancellationToken) =>
        WriteAsync(
            LenovoWmiWriteOperation.DisplayOverdrive,
            desired == ToggleState.Enabled ? 1u : 0u,
            cancellationToken);

    public ValueTask WriteIntegratedGpuModeAsync(
        IntegratedGpuMode desired,
        CancellationToken cancellationToken)
    {
        uint data = desired switch
        {
            IntegratedGpuMode.Default => 0,
            IntegratedGpuMode.IntegratedOnly => 1,
            IntegratedGpuMode.Automatic => 2,
            _ => throw new HardwareWriteException(
                "integrated_gpu_value_invalid",
                HardwareWriteStatus.Failed),
        };
        return WriteAsync(LenovoWmiWriteOperation.IntegratedGpuMode, data, cancellationToken);
    }

    public ValueTask WriteBatteryChargeModeAsync(
        BatteryChargeMode expected,
        BatteryChargeMode desired,
        CancellationToken cancellationToken) =>
        _batteryWriter.WriteAsync(expected, desired, cancellationToken);

    public async ValueTask WriteFourZoneKeyboardAsync(
        FourZoneKeyboardMode desired,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteAsync(LenovoWmiWriteOperation.LightControlOwner, 1, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HardwareWriteException)
        {
        }

        await _keyboard.WriteAsync(desired, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteAsync(
        LenovoWmiWriteOperation operation,
        uint data,
        CancellationToken cancellationToken)
    {
        try
        {
            await _invoker.WriteAsync(operation, data, cancellationToken).ConfigureAwait(false);
        }
        catch (HardwareWriteException)
        {
            throw;
        }
        catch (LenovoWmiReadFailureException exception)
        {
            throw new HardwareWriteException(
                exception.ErrorCode,
                exception.Status == HardwareReadStatus.Unsupported
                    ? HardwareWriteStatus.Unsupported
                    : HardwareWriteStatus.Failed);
        }
        catch (LenovoWmiMethodRejectedException)
        {
            throw new HardwareWriteException("wmi_setter_rejected", HardwareWriteStatus.Failed);
        }
        catch (Exception)
        {
            throw new HardwareWriteException("wmi_setter_failed", HardwareWriteStatus.Failed);
        }
    }
}
