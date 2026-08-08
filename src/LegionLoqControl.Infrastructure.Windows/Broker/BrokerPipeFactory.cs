using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using LegionLoqControl.Contracts.Broker;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Broker;

internal static class BrokerPipeFactory
{
    public static NamedPipeServerStream CreateServer(string pipeName)
    {
        WindowsPlatform.EnsureSupported();
        if (!BrokerProtocol.IsValidPipeName(pipeName))
            throw new ArgumentException("The broker pipe name is invalid.", nameof(pipeName));

        using WindowsIdentity identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
        SecurityIdentifier user = identity.User
            ?? throw new InvalidOperationException("The current Windows user SID is unavailable.");

        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(user);
        security.AddAccessRule(new PipeAccessRule(
            user,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance,
            inBufferSize: 4096,
            outBufferSize: 4096,
            security);
    }
}
