using LegionLoqControl.Application.Abstractions;
using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Broker;
using LegionLoqControl.Services;
using Xunit;

namespace LegionLoqControl.Presentation.Tests;

public sealed class DashboardDataSourceTests
{
    [Fact]
    public async Task Broker_creation_is_deferred_until_an_explicit_hardware_read()
    {
        int brokerReadCount = 0;
        var source = new DashboardDataSource(
            new MachineDiagnosticsService(new StubIdentitySource(), []),
            _ =>
            {
                brokerReadCount++;
                throw new BrokerTransportException("broker_not_found");
            });

        MachineSnapshot snapshot = await source.CaptureMachineAsync(CancellationToken.None);

        Assert.Equal("LOQ 15IRX9", snapshot.Identity.Model.Value);
        Assert.Equal(0, brokerReadCount);

        DashboardDataSourceException exception =
            await Assert.ThrowsAsync<DashboardDataSourceException>(async () =>
            {
                await source.ReadHardwareStateAsync(CancellationToken.None);
            });

        Assert.Equal("broker_not_found", exception.ErrorCode);
        Assert.Equal(1, brokerReadCount);

        _ = source.AssessBrokerInstall();
        Assert.Equal(1, brokerReadCount);
    }

    private sealed class StubIdentitySource : IMachineIdentitySource
    {
        public string SourceName => "test";

        public ValueTask<MachineIdentity> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new MachineIdentity(
                Observation.FromValue("LENOVO"),
                Observation.FromValue("LOQ 15IRX9"),
                Observation.FromValue("LOQ 15IRX9"),
                Observation.FromValue("83DV"),
                Observation.FromValue("NECN50WW")));
        }
    }
}
