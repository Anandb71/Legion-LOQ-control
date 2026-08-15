using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Xml.Linq;
using LegionLoqControl.Broker;
using LegionLoqControl.Contracts.Broker;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Broker;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class BrokerSecurityTests
{
    [Fact]
    public void Broker_manifest_requires_administrator_without_ui_access()
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "broker-app.manifest");
        XDocument manifest = XDocument.Load(manifestPath);
        XElement executionLevel = Assert.Single(
            manifest.Descendants(),
            element => element.Name.LocalName == "requestedExecutionLevel");

        Assert.Equal("requireAdministrator", executionLevel.Attribute("level")?.Value);
        Assert.Equal("false", executionLevel.Attribute("uiAccess")?.Value);
    }

    [Fact]
    public void Broker_runtime_graph_excludes_the_legacy_core()
    {
        string dependencies = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "LegionLoqControl.Broker.deps.json"));

        Assert.DoesNotContain(
            "LegionLoqControl.Core",
            dependencies,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Broker_arguments_accept_only_the_exact_transport_shape()
    {
        string pipeName = BrokerProtocol.CreatePipeName();
        string nonce = BrokerProtocol.CreateNonce();
        string[] valid =
        [
            "--pipe", pipeName,
            "--nonce", nonce,
            "--parent-pid", "1234",
        ];

        Assert.True(BrokerArguments.TryParse(valid, out BrokerArguments? result));
        Assert.Equal(new BrokerArguments(pipeName, nonce, 1234), result);
        Assert.True(BrokerArguments.TryParse([.. valid, "--write"], out BrokerArguments? write));
        Assert.True(write!.Write);
        Assert.False(BrokerArguments.TryParse([.. valid, "--extra"], out _));
        Assert.False(BrokerArguments.TryParse(
            ["--pipe", "known", "--nonce", nonce, "--parent-pid", "1234"],
            out _));
        Assert.False(BrokerArguments.TryParse(
            ["--pipe", pipeName, "--nonce", nonce, "--parent-pid", "0"],
            out _));
    }

    [Fact]
    public void Broker_client_accepts_only_the_expected_sibling_executable()
    {
        _ = new ElevatedHardwareStateBrokerClient();

        Assert.Throws<ArgumentException>(
            () => new ElevatedHardwareStateBrokerClient(
                Path.Combine(AppContext.BaseDirectory, "other.exe")));
        Assert.Throws<ArgumentException>(
            () => new ElevatedHardwareStateBrokerClient(
                Path.Combine(
                    Path.GetTempPath(),
                    ElevatedHardwareStateBrokerClient.BrokerExecutableName)));
    }

    [Fact]
    public void Request_validation_binds_nonce_and_process_identity()
    {
        string nonce = BrokerProtocol.CreateNonce();
        var request = new HardwareStateReadRequest(
            BrokerProtocol.MajorVersion,
            Guid.NewGuid(),
            nonce,
            1234);

        Assert.True(BrokerMessageValidator.ValidateRequest(request, nonce, 1234).IsValid);
        Assert.Equal(
            BrokerReadStatus.Unauthorized,
            BrokerMessageValidator.ValidateRequest(request, nonce, 5678).Status);
        Assert.Equal(
            BrokerReadStatus.Unauthorized,
            BrokerMessageValidator.ValidateRequest(
                request,
                BrokerProtocol.CreateNonce(),
                1234).Status);
    }

    [Fact]
    public void Wire_values_cannot_bypass_domain_result_invariants()
    {
        var malformed = new HardwareReadValue<ThermalMode>(
            HardwareReadStatus.Success,
            null,
            null);

        Assert.Throws<InvalidDataException>(() => malformed.ToResult());
    }

    [Fact]
    public void Pipe_acl_is_owned_by_and_grants_only_the_current_user()
    {
        using NamedPipeServerStream server = BrokerPipeFactory.CreateServer(
            BrokerProtocol.CreatePipeName());
        PipeSecurity security = server.GetAccessControl();
        SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current user SID is unavailable.");
        PipeAccessRule[] rules = security
            .GetAccessRules(
                includeExplicit: true,
                includeInherited: false,
                typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToArray();

        PipeAccessRule rule = Assert.Single(rules);
        Assert.Equal(currentUser, security.GetOwner(typeof(SecurityIdentifier)));
        Assert.Equal(currentUser, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            rule.PipeAccessRights);
    }

    [Fact]
    public async Task Pipe_exchange_verifies_local_peer_and_round_trips_one_response()
    {
        string pipeName = BrokerProtocol.CreatePipeName();
        string nonce = BrokerProtocol.CreateNonce();
        Guid requestId = Guid.NewGuid();
        var request = new HardwareStateReadRequest(
            BrokerProtocol.MajorVersion,
            requestId,
            nonce,
            Environment.ProcessId);
        using NamedPipeServerStream server = BrokerPipeFactory.CreateServer(pipeName);

        Task broker = Task.Run(async () =>
        {
            using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Anonymous);
            await client.ConnectAsync(TestContext.Current.CancellationToken);
            Assert.Equal(Environment.ProcessId, NamedPipePeerProcess.GetServerProcessId(client));
            HardwareStateReadRequest received = await BrokerWireProtocol
                .ReadAsync<HardwareStateReadRequest>(
                    client,
                    TestContext.Current.CancellationToken);
            Assert.Equal(request, received);

            var response = new HardwareStateReadResponse(
                BrokerProtocol.MajorVersion,
                requestId,
                BrokerReadStatus.Succeeded,
                HardwareStateReadPayload.FromSnapshot(Snapshot()),
                null);
            await BrokerWireProtocol.WriteAsync(
                client,
                response,
                TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        HardwareStateReadResponse actual = await BrokerPipeExchange.ExchangeAsync(
            server,
            Environment.ProcessId,
            request,
            TestContext.Current.CancellationToken);
        await broker;

        Assert.Equal(BrokerReadStatus.Succeeded, actual.Status);
        Assert.NotNull(actual.Snapshot);
    }

    private static HardwareStateSnapshot Snapshot() =>
        new(
            DateTimeOffset.UnixEpoch,
            HardwareReadResult<BatteryChargeMode>.Failure(
                HardwareReadStatus.Unavailable,
                "not_available"),
            HardwareReadResult<ThermalMode>.Success(ThermalMode.Balanced),
            HardwareReadResult<ToggleState>.Success(ToggleState.Disabled),
            HardwareReadResult<IntegratedGpuMode>.Success(IntegratedGpuMode.Default));
}
