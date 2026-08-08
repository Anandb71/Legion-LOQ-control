using LegionLoqControl.Domain.Capabilities;

namespace LegionLoqControl.Domain.Diagnostics;

public sealed record MachineSnapshot
{
    public MachineSnapshot(
        MachineIdentity identity,
        DateTimeOffset observedAt,
        IEnumerable<CapabilityEvidence> capabilities)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        ArgumentNullException.ThrowIfNull(capabilities);

        ObservedAt = observedAt;
        Capabilities = Array.AsReadOnly(capabilities.ToArray());
    }

    public MachineIdentity Identity { get; }

    public DateTimeOffset ObservedAt { get; }

    public IReadOnlyList<CapabilityEvidence> Capabilities { get; }
}
