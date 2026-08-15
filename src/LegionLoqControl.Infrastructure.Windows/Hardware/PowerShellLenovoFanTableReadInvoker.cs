using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal interface IFanTableReader
{
    ValueTask<HardwareReadResult<FanTableSnapshot>> ReadAsync(
        CancellationToken cancellationToken);
}

internal sealed class PowerShellLenovoFanTableReadInvoker : IFanTableReader
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

    internal static string ScriptForValidation => PowerShellScript;

    public async ValueTask<HardwareReadResult<FanTableSnapshot>> ReadAsync(
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string output = await ExecutePowerShellAsync(cancellationToken).ConfigureAwait(false);
            return Parse(output);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (LenovoWmiReadFailureException exception)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                exception.Status,
                exception.ErrorCode);
        }
        catch (InvalidDataException)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.InvalidData,
                "wmi_output_invalid");
        }
        catch (Exception)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Failed,
                "wmi_getter_failed");
        }
    }

    internal static HardwareReadResult<FanTableSnapshot> Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        PowerShellFanTableResponse response;
        try
        {
            response = JsonSerializer.Deserialize<PowerShellFanTableResponse>(
                json.TrimStart('\uFEFF'),
                SerializerOptions)
                ?? throw new InvalidDataException("The PowerShell fan-table response was null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The PowerShell fan-table response was invalid JSON.",
                exception);
        }

        if (!string.Equals(response.Status, "success", StringComparison.Ordinal))
            return FailureFromStatus(response.Status);
        if (response.FanTable is null || response.SensorTable is null)
            throw new InvalidDataException("The PowerShell fan-table response was malformed.");

        return FanTableParser.Parse(
            response.FanId,
            response.SensorId,
            response.FanTable,
            response.SensorTable);
    }

    private static HardwareReadResult<FanTableSnapshot> FailureFromStatus(string? status) =>
        status switch
        {
            "access_denied" => HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.AccessDenied,
                "wmi_access_denied"),
            "not_found" => HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Unavailable,
                "wmi_provider_object_not_found"),
            "not_supported" => HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Unsupported,
                "wmi_getter_not_available"),
            "timed_out" => HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.TimedOut,
                "wmi_getter_timed_out"),
            "invalid_data" => HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.InvalidData,
                "wmi_output_invalid"),
            _ => HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Failed,
                "wmi_getter_failed"),
        };

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
            throw new InvalidDataException("The PowerShell fan-table output size was invalid.");

        return output;
    }

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

    private sealed record PowerShellFanTableResponse(
        string Status,
        byte FanId,
        byte SensorId,
        uint[]? FanTable,
        uint[]? SensorTable);

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

        function Convert-UInt32Array {
            param($Value)

            if ($null -eq $Value) {
                return $null
            }

            $items = @($Value)
            $result = [uint32[]]::new($items.Length)
            for ($index = 0; $index -lt $items.Length; $index++) {
                if ($items[$index] -isnot [uint32] -and $items[$index] -isnot [byte] -and $items[$index] -isnot [uint16] -and $items[$index] -isnot [int]) {
                    return $null
                }

                $result[$index] = [uint32]$items[$index]
            }

            return $result
        }

        try {
            $instances = @(CimCmdlets\Get-CimInstance `
                -Namespace 'root/WMI' `
                -ClassName 'LENOVO_FAN_METHOD' `
                -ErrorAction Stop)
            if ($instances.Count -eq 0) {
                [ordered]@{ status = 'not_found' } | ConvertTo-Json -Compress -Depth 5
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
                -MethodName 'Fan_Get_Table' `
                -Arguments @{
                    FanID = [byte]0
                    SensorID = [byte]0
                } `
                -ErrorAction Stop

            $fanTable = Convert-UInt32Array $output.FanTable
            $sensorTable = Convert-UInt32Array $output.SensorTable
            if ($null -eq $fanTable -or $null -eq $sensorTable) {
                [ordered]@{ status = 'invalid_data' } | ConvertTo-Json -Compress -Depth 5
                exit 0
            }

            if ($null -ne $output.FanTableSize -and [uint32]$output.FanTableSize -ne $fanTable.Length) {
                [ordered]@{ status = 'invalid_data' } | ConvertTo-Json -Compress -Depth 5
                exit 0
            }

            if ($null -ne $output.SensorTableSize -and [uint32]$output.SensorTableSize -ne $sensorTable.Length) {
                [ordered]@{ status = 'invalid_data' } | ConvertTo-Json -Compress -Depth 5
                exit 0
            }

            $fanValues = [System.Collections.Generic.List[uint32]]::new()
            $sensorValues = [System.Collections.Generic.List[uint32]]::new()
            foreach ($item in $fanTable) { $fanValues.Add([uint32]$item) }
            foreach ($item in $sensorTable) { $sensorValues.Add([uint32]$item) }

            [ordered]@{
                status = 'success'
                fanId = [byte]0
                sensorId = [byte]0
                fanTable = $fanValues
                sensorTable = $sensorValues
            } | ConvertTo-Json -Compress -Depth 5
        }
        catch {
            [ordered]@{ status = Get-FailureStatus $_ } | ConvertTo-Json -Compress -Depth 5
        }
        """;
}
