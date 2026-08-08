using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Diagnostics;

namespace LegionLoqControl.Application.Abstractions;

public interface ICapabilityProbe
{
    string SourceName { get; }

    IReadOnlySet<HardwareCapability> Capabilities { get; }

    ValueTask<IReadOnlyCollection<CapabilityEvidence>> ProbeAsync(
        MachineIdentity identity,
        CancellationToken cancellationToken);
}
