using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal static class FourZoneKeyboardPacket
{
    internal const int VendorId = 0x048D;
    internal const int FeatureReportLength = 33;
    internal const byte ReportId = 0xCC;
    internal const byte LightingCommand = 0x16;
    internal const byte IdentityCommand = 0x05;
    internal const byte StaticEffect = 0x01;

    internal static readonly int[] RecognizedProductIds = [0xC935, 0xC955, 0xC993];

    internal static byte[] Build(FourZoneKeyboardMode mode)
    {
        if (mode is FourZoneKeyboardMode.Unknown || !Enum.IsDefined(mode))
        {
            throw new HardwareWriteException(
                "keyboard_value_invalid",
                HardwareWriteStatus.Failed);
        }

        byte[] packet = new byte[FeatureReportLength];
        packet[0] = ReportId;
        packet[1] = LightingCommand;
        packet[3] = 0x01;
        if (mode == FourZoneKeyboardMode.Off)
        {
            packet[2] = 0x00;
            packet[4] = 0x00;
            return packet;
        }

        packet[2] = StaticEffect;
        packet[4] = mode == FourZoneKeyboardMode.Low ? (byte)0x01 : (byte)0x02;
        for (int offset = 5; offset <= 14; offset += 3)
        {
            packet[offset] = 0xFF;
            packet[offset + 1] = 0xFF;
            packet[offset + 2] = 0xFF;
        }

        return packet;
    }

    internal static HardwareReadResult<FourZoneKeyboardMode> Parse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != FeatureReportLength || packet[0] != ReportId)
        {
            return HardwareReadResult<FourZoneKeyboardMode>.Failure(
                HardwareReadStatus.InvalidData,
                "keyboard_report_invalid");
        }

        if (packet[1] == IdentityCommand || packet[1] != LightingCommand)
            return HardwareReadResult<FourZoneKeyboardMode>.Success(FourZoneKeyboardMode.Unknown);

        byte effect = packet[2];
        byte brightness = packet[4];
        if (effect == 0 || brightness == 0 || IsBlackStatic(packet, effect))
            return HardwareReadResult<FourZoneKeyboardMode>.Success(FourZoneKeyboardMode.Off);
        if (brightness == 1)
            return HardwareReadResult<FourZoneKeyboardMode>.Success(FourZoneKeyboardMode.Low);
        if (brightness == 2)
            return HardwareReadResult<FourZoneKeyboardMode>.Success(FourZoneKeyboardMode.High);

        return HardwareReadResult<FourZoneKeyboardMode>.Success(FourZoneKeyboardMode.Unknown);
    }

    private static bool IsBlackStatic(ReadOnlySpan<byte> packet, byte effect)
    {
        if (effect != StaticEffect)
            return false;

        for (int offset = 5; offset <= 14; offset++)
        {
            if (packet[offset] != 0)
                return false;
        }

        return true;
    }
}
