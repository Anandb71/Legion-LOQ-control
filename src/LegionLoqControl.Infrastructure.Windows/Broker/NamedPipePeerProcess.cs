using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LegionLoqControl.Infrastructure.Windows.Broker;

internal static class NamedPipePeerProcess
{
    public static int GetClientProcessId(NamedPipeServerStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.IsConnected)
            throw new InvalidOperationException("The broker pipe is not connected.");

        if (!GetNamedPipeClientProcessId(stream.SafePipeHandle, out uint processId))
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        return checked((int)processId);
    }

    public static int GetServerProcessId(NamedPipeClientStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.IsConnected)
            throw new InvalidOperationException("The broker pipe is not connected.");

        if (!GetNamedPipeServerProcessId(stream.SafePipeHandle, out uint processId))
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        return checked((int)processId);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(
        SafePipeHandle pipe,
        out uint serverProcessId);
}
