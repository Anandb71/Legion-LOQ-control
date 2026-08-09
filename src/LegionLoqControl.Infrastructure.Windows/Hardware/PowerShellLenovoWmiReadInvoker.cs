using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal sealed class PowerShellLenovoWmiReadInvoker : ILenovoWmiReadInvoker
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

    private readonly object _sync = new();
    private Task<IReadOnlyDictionary<LenovoWmiReadOperation, GetterOutcome>>? _batchTask;

    public async ValueTask<uint> ReadAsync(
        LenovoWmiReadOperation operation,
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        Task<IReadOnlyDictionary<LenovoWmiReadOperation, GetterOutcome>> task;
        lock (_sync)
            task = _batchTask ??= ReadBatchAsync(cancellationToken);

        IReadOnlyDictionary<LenovoWmiReadOperation, GetterOutcome> outcomes =
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!outcomes.TryGetValue(operation, out GetterOutcome? outcome))
            throw new InvalidDataException("The PowerShell getter result was incomplete.");

        return outcome.GetValue();
    }

    internal static IReadOnlyDictionary<LenovoWmiReadOperation, GetterOutcome> ParseBatch(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        PowerShellBatchResponse response;
        try
        {
            response = JsonSerializer.Deserialize<PowerShellBatchResponse>(
                json.TrimStart('\uFEFF'),
                SerializerOptions)
                ?? throw new InvalidDataException("The PowerShell getter response was null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The PowerShell getter response was invalid JSON.",
                exception);
        }

        if (!string.Equals(response.Status, "success", StringComparison.Ordinal))
            throw CreateFailure(response.Status);
        if (response.Results is null)
            throw new InvalidDataException("The PowerShell getter response had no results.");

        return new Dictionary<LenovoWmiReadOperation, GetterOutcome>
        {
            [LenovoWmiReadOperation.ThermalMode] = ParseOutcome(
                response.Results,
                "thermalMode"),
            [LenovoWmiReadOperation.DisplayOverdrive] = ParseOutcome(
                response.Results,
                "displayOverdrive"),
            [LenovoWmiReadOperation.IntegratedGpuMode] = ParseOutcome(
                response.Results,
                "integratedGpuMode"),
        };
    }

    internal static string ScriptForValidation => PowerShellScript;

    private static async Task<IReadOnlyDictionary<LenovoWmiReadOperation, GetterOutcome>>
        ReadBatchAsync(CancellationToken cancellationToken)
    {
        string output = await ExecutePowerShellAsync(cancellationToken).ConfigureAwait(false);
        return ParseBatch(output);
    }

    private static async Task<string> ExecutePowerShellAsync(
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
                "powershell_getter_timed_out");
        }

        string output = await standardOutput.ConfigureAwait(false);
        _ = await standardError.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new LenovoWmiReadFailureException(
                HardwareReadStatus.Failed,
                "powershell_getter_failed");
        }
        if (Encoding.UTF8.GetByteCount(output) is <= 0 or > MaximumOutputBytes)
            throw new InvalidDataException("The PowerShell getter output size was invalid.");

        return output;
    }

    private static GetterOutcome ParseOutcome(
        IReadOnlyDictionary<string, PowerShellGetterResponse> results,
        string key)
    {
        if (!results.TryGetValue(key, out PowerShellGetterResponse? result) ||
            result is null)
        {
            throw new InvalidDataException("The PowerShell getter result was missing.");
        }

        if (!string.Equals(result.Status, "success", StringComparison.Ordinal))
            return GetterOutcome.Failure(CreateFailure(result.Status));
        if (!result.ReturnStatus.HasValue || !result.Data.HasValue)
            throw new InvalidDataException("The PowerShell getter result was malformed.");
        if (!result.ReturnStatus.Value)
        {
            return GetterOutcome.Failure(new LenovoWmiMethodRejectedException());
        }

        return GetterOutcome.Success(result.Data.Value);
    }

    private static LenovoWmiReadFailureException CreateFailure(string? status) =>
        status switch
        {
            "access_denied" => new(
                HardwareReadStatus.AccessDenied,
                "wmi_access_denied"),
            "not_found" => new(
                HardwareReadStatus.Unavailable,
                "wmi_provider_object_not_found"),
            "not_supported" => new(
                HardwareReadStatus.Unsupported,
                "wmi_getter_not_available"),
            "timed_out" => new(
                HardwareReadStatus.TimedOut,
                "wmi_getter_timed_out"),
            "invalid_data" => new(
                HardwareReadStatus.InvalidData,
                "wmi_output_invalid"),
            _ => new(HardwareReadStatus.Failed, "wmi_getter_failed"),
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
            // The process has its own timeout; termination is best effort.
        }
    }

    private sealed record PowerShellBatchResponse(
        string Status,
        IReadOnlyDictionary<string, PowerShellGetterResponse>? Results);

    private sealed record PowerShellGetterResponse(
        string Status,
        bool? ReturnStatus,
        uint? Data);

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

            $nativeErrorCode = $exception.PSObject.Properties['NativeErrorCode']
            if ($null -ne $nativeErrorCode) {
                switch ([int]$nativeErrorCode.Value) {
                    2 { return 'access_denied' }
                    3 { return 'not_supported' }
                    5 { return 'not_supported' }
                    6 { return 'not_found' }
                    7 { return 'not_supported' }
                    16 { return 'not_supported' }
                    17 { return 'not_supported' }
                    20 { return 'timed_out' }
                }
            }

            $statusCode = $exception.PSObject.Properties['StatusCode']
            if ($null -ne $statusCode) {
                switch ($statusCode.Value.ToString()) {
                    'AccessDenied' { return 'access_denied' }
                    'NotFound' { return 'not_found' }
                    'InvalidNamespace' { return 'not_supported' }
                    'InvalidClass' { return 'not_supported' }
                    'MethodNotAvailable' { return 'not_supported' }
                    'MethodNotFound' { return 'not_supported' }
                    'NotSupported' { return 'not_supported' }
                    'InvalidOperationTimeout' { return 'timed_out' }
                }
            }

            return 'failed'
        }

        function Invoke-Getter {
            param(
                [Microsoft.Management.Infrastructure.CimInstance] $Instance,
                [string] $MethodName
            )

            try {
                $output = CimCmdlets\Invoke-CimMethod `
                    -InputObject $Instance `
                    -MethodName $MethodName `
                    -ErrorAction Stop
                if ($output.ReturnValue -isnot [bool] -or
                    $output.Data -isnot [uint32]) {
                    return [ordered]@{ status = 'invalid_data' }
                }

                return [ordered]@{
                    status = 'success'
                    returnStatus = [bool]$output.ReturnValue
                    data = [uint32]$output.Data
                }
            }
            catch {
                return [ordered]@{ status = Get-FailureStatus $_ }
            }
        }

        try {
            $instances = @(CimCmdlets\Get-CimInstance `
                -Namespace 'root/WMI' `
                -ClassName 'LENOVO_GAMEZONE_DATA' `
                -ErrorAction Stop)
            if ($instances.Count -eq 0) {
                [ordered]@{ status = 'not_found'; results = $null } |
                    ConvertTo-Json -Compress -Depth 5
                exit 0
            }

            $instance = $instances |
                Where-Object { $_.Active -eq $true } |
                Select-Object -First 1
            if ($null -eq $instance) {
                $instance = $instances[0]
            }

            $results = [ordered]@{
                thermalMode = Invoke-Getter $instance 'GetSmartFanMode'
                displayOverdrive = Invoke-Getter $instance 'GetODStatus'
                integratedGpuMode = Invoke-Getter $instance 'GetIGPUModeStatus'
            }
            [ordered]@{ status = 'success'; results = $results } |
                ConvertTo-Json -Compress -Depth 5
        }
        catch {
            [ordered]@{
                status = Get-FailureStatus $_
                results = $null
            } | ConvertTo-Json -Compress -Depth 5
        }
        """;
}

internal sealed class GetterOutcome
{
    private readonly uint? _value;
    private readonly Exception? _exception;

    private GetterOutcome(uint? value, Exception? exception)
    {
        _value = value;
        _exception = exception;
    }

    public uint GetValue()
    {
        if (_exception is not null)
            throw _exception;
        return _value
            ?? throw new InvalidDataException("The PowerShell getter value was missing.");
    }

    public static GetterOutcome Success(uint value) => new(value, null);

    public static GetterOutcome Failure(Exception exception) =>
        new(null, exception ?? throw new ArgumentNullException(nameof(exception)));
}

internal sealed class LenovoWmiReadFailureException : Exception
{
    public LenovoWmiReadFailureException(
        HardwareReadStatus status,
        string errorCode)
        : base(errorCode)
    {
        Status = status;
        ErrorCode = errorCode;
    }

    public LenovoWmiReadFailureException(
        HardwareReadStatus status,
        string errorCode,
        Exception innerException)
        : base(errorCode, innerException)
    {
        Status = status;
        ErrorCode = errorCode;
    }

    public HardwareReadStatus Status { get; }

    public string ErrorCode { get; }
}
