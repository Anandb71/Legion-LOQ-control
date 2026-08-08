namespace LegionLoqControl.Infrastructure.Windows.Platform;

public static class WindowsPlatform
{
    public static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763);

    public static void EnsureSupported()
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException("Legion + LOQ Control requires Windows 10 version 1809 or later.");
    }
}
