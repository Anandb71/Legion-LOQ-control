using HidSharp;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal interface IFourZoneKeyboardHid
{
    ValueTask<HardwareReadResult<FourZoneKeyboardMode>> ReadAsync(
        CancellationToken cancellationToken);

    ValueTask WriteAsync(FourZoneKeyboardMode desired, CancellationToken cancellationToken);

    ValueTask<HardwareReadResult<FourZoneLightingState>> ReadLightingAsync(
        CancellationToken cancellationToken);

    ValueTask WriteLightingAsync(
        FourZoneLightingState desired,
        CancellationToken cancellationToken);
}

internal sealed class FourZoneKeyboardHid : IFourZoneKeyboardHid
{
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(5);
    private static readonly HashSet<int> ProductIds = [.. FourZoneKeyboardPacket.RecognizedProductIds];

    public async ValueTask<HardwareReadResult<FourZoneKeyboardMode>> ReadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await Task
                .Run(ReadCore, CancellationToken.None)
                .WaitAsync(IoTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return HardwareReadResult<FourZoneKeyboardMode>.Failure(
                HardwareReadStatus.TimedOut,
                "keyboard_hid_timed_out");
        }
        catch (UnauthorizedAccessException)
        {
            return HardwareReadResult<FourZoneKeyboardMode>.Failure(
                HardwareReadStatus.AccessDenied,
                "keyboard_hid_access_denied");
        }
        catch (Exception)
        {
            return HardwareReadResult<FourZoneKeyboardMode>.Failure(
                HardwareReadStatus.Failed,
                "keyboard_hid_read_failed");
        }
    }

    public async ValueTask WriteAsync(
        FourZoneKeyboardMode desired,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] packet = FourZoneKeyboardPacket.Build(desired);
        try
        {
            await Task
                .Run(() => WriteCore(packet), CancellationToken.None)
                .WaitAsync(IoTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HardwareWriteException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            throw new HardwareWriteException(
                "keyboard_hid_timed_out",
                HardwareWriteStatus.Failed);
        }
        catch (UnauthorizedAccessException)
        {
            throw new HardwareWriteException(
                "keyboard_hid_access_denied",
                HardwareWriteStatus.Failed);
        }
        catch (Exception)
        {
            throw new HardwareWriteException(
                "keyboard_hid_write_failed",
                HardwareWriteStatus.Failed);
        }
    }

    public async ValueTask<HardwareReadResult<FourZoneLightingState>> ReadLightingAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await Task
                .Run(ReadLightingCore, CancellationToken.None)
                .WaitAsync(IoTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return HardwareReadResult<FourZoneLightingState>.Failure(
                HardwareReadStatus.TimedOut,
                "keyboard_hid_timed_out");
        }
        catch (Exception)
        {
            return HardwareReadResult<FourZoneLightingState>.Failure(
                HardwareReadStatus.Failed,
                "keyboard_hid_read_failed");
        }
    }

    public async ValueTask WriteLightingAsync(
        FourZoneLightingState desired,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] packet = FourZoneKeyboardPacket.BuildLighting(desired);
        try
        {
            await Task
                .Run(() => WriteCore(packet), CancellationToken.None)
                .WaitAsync(IoTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HardwareWriteException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            throw new HardwareWriteException(
                "keyboard_hid_timed_out",
                HardwareWriteStatus.Failed);
        }
        catch (Exception)
        {
            throw new HardwareWriteException(
                "keyboard_hid_write_failed",
                HardwareWriteStatus.Failed);
        }
    }

    private static HardwareReadResult<FourZoneLightingState> ReadLightingCore()
    {
        HidDevice? device = FindDevice();
        if (device is null)
        {
            return HardwareReadResult<FourZoneLightingState>.Failure(
                HardwareReadStatus.Unavailable,
                "keyboard_hid_not_found");
        }

        if (!device.TryOpen(out HidStream? stream))
        {
            return HardwareReadResult<FourZoneLightingState>.Failure(
                HardwareReadStatus.AccessDenied,
                "keyboard_hid_access_denied");
        }

        using (stream)
        {
            byte[] buffer = new byte[FourZoneKeyboardPacket.FeatureReportLength];
            buffer[0] = FourZoneKeyboardPacket.ReportId;
            stream.GetFeature(buffer);
            return FourZoneKeyboardPacket.ParseLighting(buffer);
        }
    }

    private static HardwareReadResult<FourZoneKeyboardMode> ReadCore()
    {
        HidDevice? device = FindDevice();
        if (device is null)
        {
            return HardwareReadResult<FourZoneKeyboardMode>.Failure(
                HardwareReadStatus.Unavailable,
                "keyboard_hid_not_found");
        }

        if (!device.TryOpen(out HidStream? stream))
        {
            return HardwareReadResult<FourZoneKeyboardMode>.Failure(
                HardwareReadStatus.AccessDenied,
                "keyboard_hid_access_denied");
        }

        using (stream)
        {
            byte[] buffer = new byte[FourZoneKeyboardPacket.FeatureReportLength];
            buffer[0] = FourZoneKeyboardPacket.ReportId;
            stream.GetFeature(buffer);
            return FourZoneKeyboardPacket.Parse(buffer);
        }
    }

    private static void WriteCore(byte[] packet)
    {
        HidDevice device = FindDevice()
            ?? throw new HardwareWriteException(
                "keyboard_hid_not_found",
                HardwareWriteStatus.Unsupported);
        if (!device.TryOpen(out HidStream? stream))
        {
            throw new HardwareWriteException(
                "keyboard_hid_access_denied",
                HardwareWriteStatus.Failed);
        }

        using (stream)
            stream.SetFeature(packet);
    }

    private static HidDevice? FindDevice() =>
        DeviceList.Local
            .GetHidDevices(FourZoneKeyboardPacket.VendorId)
            .FirstOrDefault(static device =>
                ProductIds.Contains(device.ProductID) &&
                device.GetMaxFeatureReportLength() == FourZoneKeyboardPacket.FeatureReportLength);
}
