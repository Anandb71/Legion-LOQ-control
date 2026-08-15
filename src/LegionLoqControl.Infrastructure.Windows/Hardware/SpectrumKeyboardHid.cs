using HidSharp;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal interface ISpectrumKeyboardHid
{
    ValueTask<HardwareReadResult<SpectrumBrightness>> ReadAsync(
        CancellationToken cancellationToken);

    ValueTask WriteAsync(SpectrumBrightness desired, CancellationToken cancellationToken);
}

internal sealed class SpectrumKeyboardHid : ISpectrumKeyboardHid
{
    internal const int VendorId = 0x048D;
    internal const int FeatureReportLength = 960;
    internal const byte Head = 7;
    internal const byte GetBrightness = 0xCD;
    internal const byte SetBrightness = 0xCE;
    internal const byte Size = 0xC0;
    internal const byte Tail = 3;
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(5);
    private static readonly HashSet<int> FourZoneProductIds = [.. FourZoneKeyboardPacket.RecognizedProductIds];

    public async ValueTask<HardwareReadResult<SpectrumBrightness>> ReadAsync(
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
            return HardwareReadResult<SpectrumBrightness>.Failure(
                HardwareReadStatus.TimedOut,
                "spectrum_hid_timed_out");
        }
        catch (Exception)
        {
            return HardwareReadResult<SpectrumBrightness>.Failure(
                HardwareReadStatus.Failed,
                "spectrum_hid_read_failed");
        }
    }

    public async ValueTask WriteAsync(
        SpectrumBrightness desired,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(desired))
        {
            throw new HardwareWriteException(
                "spectrum_value_invalid",
                HardwareWriteStatus.Failed);
        }

        byte[] packet = new byte[FeatureReportLength];
        packet[0] = Head;
        packet[1] = SetBrightness;
        packet[2] = Size;
        packet[3] = Tail;
        packet[4] = desired switch
        {
            SpectrumBrightness.Off => (byte)0,
            SpectrumBrightness.Low => (byte)1,
            SpectrumBrightness.Medium => (byte)2,
            _ => (byte)3,
        };

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
                "spectrum_hid_timed_out",
                HardwareWriteStatus.Failed);
        }
        catch (Exception)
        {
            throw new HardwareWriteException(
                "spectrum_hid_write_failed",
                HardwareWriteStatus.Failed);
        }
    }

    internal static bool IsSpectrumProduct(int productId, int featureLength) =>
        (productId & 0xFF00) == 0xC900 &&
        !FourZoneProductIds.Contains(productId) &&
        featureLength == FeatureReportLength;

    private static HardwareReadResult<SpectrumBrightness> ReadCore()
    {
        HidDevice? device = FindDevice();
        if (device is null)
        {
            return HardwareReadResult<SpectrumBrightness>.Failure(
                HardwareReadStatus.Unsupported,
                "spectrum_hid_not_found");
        }

        if (!device.TryOpen(out HidStream? stream))
        {
            return HardwareReadResult<SpectrumBrightness>.Failure(
                HardwareReadStatus.AccessDenied,
                "spectrum_hid_access_denied");
        }

        using (stream)
        {
            byte[] buffer = new byte[FeatureReportLength];
            buffer[0] = Head;
            buffer[1] = GetBrightness;
            buffer[2] = Size;
            buffer[3] = Tail;
            stream.GetFeature(buffer);
            return MapBrightness(buffer[4]);
        }
    }

    private static void WriteCore(byte[] packet)
    {
        HidDevice device = FindDevice()
            ?? throw new HardwareWriteException(
                "spectrum_hid_not_found",
                HardwareWriteStatus.Unsupported);
        if (!device.TryOpen(out HidStream? stream))
        {
            throw new HardwareWriteException(
                "spectrum_hid_access_denied",
                HardwareWriteStatus.Failed);
        }

        using (stream)
            stream.SetFeature(packet);
    }

    private static HardwareReadResult<SpectrumBrightness> MapBrightness(byte raw) =>
        raw switch
        {
            0 => HardwareReadResult<SpectrumBrightness>.Success(SpectrumBrightness.Off),
            1 => HardwareReadResult<SpectrumBrightness>.Success(SpectrumBrightness.Low),
            2 => HardwareReadResult<SpectrumBrightness>.Success(SpectrumBrightness.Medium),
            >= 3 and <= 9 => HardwareReadResult<SpectrumBrightness>.Success(SpectrumBrightness.High),
            _ => HardwareReadResult<SpectrumBrightness>.Failure(
                HardwareReadStatus.InvalidData,
                "spectrum_brightness_invalid"),
        };

    private static HidDevice? FindDevice() =>
        DeviceList.Local
            .GetHidDevices(VendorId)
            .FirstOrDefault(static device =>
                IsSpectrumProduct(device.ProductID, device.GetMaxFeatureReportLength()));
}
