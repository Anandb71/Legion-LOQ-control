using LegionLoqControl.Application.Abstractions;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Diagnostics;

namespace LegionLoqControl.Application.Diagnostics;

public sealed class MachineDiagnosticsService
{
    private readonly IMachineIdentitySource _identitySource;
    private readonly IReadOnlyList<ICapabilityProbe> _probes;
    private readonly TimeProvider _timeProvider;

    public MachineDiagnosticsService(
        IMachineIdentitySource identitySource,
        IEnumerable<ICapabilityProbe> probes,
        TimeProvider? timeProvider = null)
    {
        _identitySource = identitySource ?? throw new ArgumentNullException(nameof(identitySource));
        ArgumentNullException.ThrowIfNull(probes);

        _probes = Array.AsReadOnly(probes.ToArray());
        if (_probes.Any(static probe => probe is null))
            throw new ArgumentException("Capability probes cannot contain null entries.", nameof(probes));

        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MachineSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        MachineIdentity identity = await _identitySource.ReadAsync(cancellationToken).ConfigureAwait(false);
        Task<IReadOnlyCollection<CapabilityEvidence>>[] tasks = _probes
            .Select(probe => ProbeSafelyAsync(probe, identity, cancellationToken))
            .ToArray();

        IReadOnlyCollection<CapabilityEvidence>[] probeResults = await Task
            .WhenAll(tasks)
            .ConfigureAwait(false);

        return new MachineSnapshot(
            identity,
            _timeProvider.GetUtcNow(),
            probeResults.SelectMany(static result => result));
    }

    private async Task<IReadOnlyCollection<CapabilityEvidence>> ProbeSafelyAsync(
        ICapabilityProbe probe,
        MachineIdentity identity,
        CancellationToken cancellationToken)
    {
        try
        {
            return await probe.ProbeAsync(identity, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            DateTimeOffset observedAt = _timeProvider.GetUtcNow();
            return probe.Capabilities
                .Select(capability => new CapabilityEvidence(
                    capability,
                    CapabilitySupport.Unknown,
                    probe.SourceName,
                    observedAt,
                    "probe_failed",
                    exception.GetType().Name))
                .ToArray();
        }
    }
}
