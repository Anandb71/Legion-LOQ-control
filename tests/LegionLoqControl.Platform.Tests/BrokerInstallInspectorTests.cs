using LegionLoqControl.Application.Broker;
using LegionLoqControl.Infrastructure.Windows.Broker;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class BrokerInstallInspectorTests
{
    [Fact]
    public void Missing_sibling_is_reported_without_launching_anything()
    {
        using var temporary = new TemporaryDirectory();
        string brokerPath = Path.Combine(
            temporary.DirectoryPath,
            ElevatedHardwareStateBrokerClient.BrokerExecutableName);

        BrokerInstallAssessment assessment = WindowsBrokerInstallInspector.Assess(
            brokerPath,
            temporary.DirectoryPath);

        Assert.Equal(BrokerInstallPlacement.Missing, assessment.Placement);
        Assert.Equal("broker_not_found", assessment.StatusCode);
        Assert.False(assessment.AllowsDevelopmentRead);
        Assert.False(assessment.AllowsProductionRelease);
    }

    [Fact]
    public void Unsigned_file_in_a_user_directory_is_development_only()
    {
        using var temporary = new TemporaryDirectory();
        string brokerPath = Path.Combine(
            temporary.DirectoryPath,
            ElevatedHardwareStateBrokerClient.BrokerExecutableName);
        File.WriteAllText(brokerPath, "not-a-signed-broker");

        BrokerInstallAssessment assessment = WindowsBrokerInstallInspector.Assess(
            brokerPath,
            temporary.DirectoryPath);

        Assert.Equal(BrokerSignatureStatus.Unsigned, assessment.Signature);
        Assert.False(assessment.DirectoryProtected);
        Assert.Equal(BrokerInstallPlacement.SiblingDevelopment, assessment.Placement);
        Assert.Equal("broker_install_development", assessment.StatusCode);
        Assert.True(assessment.AllowsDevelopmentRead);
        Assert.False(assessment.AllowsProductionRelease);
    }

    [Fact]
    public async Task Production_mode_refuses_an_unsigned_sibling_before_uac()
    {
        string brokerPath = Path.Combine(
            AppContext.BaseDirectory,
            ElevatedHardwareStateBrokerClient.BrokerExecutableName);
        var client = new ElevatedHardwareStateBrokerClient(
            brokerPath,
            BrokerInstallMode.Production,
            (_, _) => new BrokerInstallAssessment(
                BrokerInstallPlacement.SiblingDevelopment,
                BrokerSignatureStatus.Unsigned,
                DirectoryProtected: false,
                AllowsDevelopmentRead: true,
                AllowsProductionRelease: false,
                "broker_install_development"));

        BrokerTransportException exception = await Assert.ThrowsAsync<BrokerTransportException>(
            () => client.ReadAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("broker_install_unprotected", exception.ErrorCode);
    }

    [Fact]
    public async Task Missing_sibling_fails_on_read_not_construction()
    {
        string brokerPath = Path.Combine(
            AppContext.BaseDirectory,
            ElevatedHardwareStateBrokerClient.BrokerExecutableName);
        using var client = new ElevatedHardwareStateBrokerClient(
            brokerPath,
            BrokerInstallMode.Development,
            (_, _) => new BrokerInstallAssessment(
                BrokerInstallPlacement.Missing,
                BrokerSignatureStatus.Missing,
                DirectoryProtected: false,
                AllowsDevelopmentRead: false,
                AllowsProductionRelease: false,
                "broker_not_found"));

        BrokerTransportException exception = await Assert.ThrowsAsync<BrokerTransportException>(
            () => client.ReadAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("broker_not_found", exception.ErrorCode);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "LegionLoqControl.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }
}
