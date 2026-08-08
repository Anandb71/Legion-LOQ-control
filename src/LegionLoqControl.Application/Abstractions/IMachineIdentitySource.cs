using LegionLoqControl.Domain.Diagnostics;

namespace LegionLoqControl.Application.Abstractions;

public interface IMachineIdentitySource
{
    string SourceName { get; }

    ValueTask<MachineIdentity> ReadAsync(CancellationToken cancellationToken);
}
