using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Application.Automation;

public interface IPowerSourceReader
{
    ValueTask<HardwareReadResult<PowerSourceKind>> ReadAsync(
        CancellationToken cancellationToken);
}

public interface ISystemPowerTelemetryReader
{
    ValueTask<SystemPowerTelemetry> ReadTelemetryAsync(
        CancellationToken cancellationToken);
}

public interface ISystemResourceTelemetryReader
{
    ValueTask<SystemResourceTelemetry> ReadAsync(CancellationToken cancellationToken);
}

public sealed class PowerSourceService
{
    private readonly IPowerSourceReader _reader;
    private readonly TimeProvider _timeProvider;

    public PowerSourceService(
        IPowerSourceReader reader,
        TimeProvider? timeProvider = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<PowerSourceSnapshot> CaptureAsync(
        CancellationToken cancellationToken = default)
    {
        HardwareReadResult<PowerSourceKind> result = await _reader
            .ReadAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PowerSourceSnapshot(_timeProvider.GetUtcNow(), result);
    }
}

