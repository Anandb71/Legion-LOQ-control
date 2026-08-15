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

    internal static byte[] BuildLighting(FourZoneLightingState state)
    {
        if (!Enum.IsDefined(state.Effect) ||
            !Enum.IsDefined(state.Brightness) ||
            state.Brightness == FourZoneKeyboardMode.Unknown ||
            state.Speed > FourZoneLightingState.MaximumSpeed)
        {
            throw new HardwareWriteException(
                "lighting_value_invalid",
                HardwareWriteStatus.Failed);
        }

        byte[] packet = new byte[FeatureReportLength];
        packet[0] = ReportId;
        packet[1] = LightingCommand;
        if (state.Brightness == FourZoneKeyboardMode.Off || state.Effect == FourZoneEffect.Off)
        {
            packet[2] = 0x00;
            packet[3] = 0x01;
            packet[4] = 0x00;
            return packet;
        }

        packet[2] = (byte)state.Effect;
        packet[3] = state.Speed == 0 ? (byte)0x01 : state.Speed;
        packet[4] = state.Brightness == FourZoneKeyboardMode.Low ? (byte)0x01 : (byte)0x02;
        RgbColor[] zones = state.DivideArea
            ? [state.Zone1, state.Zone2, state.Zone3, state.Zone4]
            : [state.Zone1, state.Zone1, state.Zone1, state.Zone1];
        for (int index = 0; index < zones.Length; index++)
        {
            int offset = 5 + (index * 3);
            packet[offset] = zones[index].Red;
            packet[offset + 1] = zones[index].Green;
            packet[offset + 2] = zones[index].Blue;
        }

        return packet;
    }

    internal static HardwareReadResult<FourZoneLightingState> ParseLighting(
        ReadOnlySpan<byte> packet)
    {
        HardwareReadResult<FourZoneKeyboardMode> brightness = Parse(packet);
        if (brightness.Status != HardwareReadStatus.Success || !brightness.Value.HasValue)
        {
            return HardwareReadResult<FourZoneLightingState>.Failure(
                brightness.Status,
                brightness.ErrorCode ?? "keyboard_report_invalid");
        }

        if (packet[1] == IdentityCommand)
        {
            return HardwareReadResult<FourZoneLightingState>.Success(
                FourZoneLightingState.Default with { Brightness = FourZoneKeyboardMode.Unknown });
        }

        FourZoneEffect effect = Enum.IsDefined((FourZoneEffect)packet[2])
            ? (FourZoneEffect)packet[2]
            : FourZoneEffect.Static;
        byte speed = packet[3] <= FourZoneLightingState.MaximumSpeed ? packet[3] : (byte)1;
        RgbColor zone1 = new(packet[5], packet[6], packet[7]);
        RgbColor zone2 = new(packet[8], packet[9], packet[10]);
        RgbColor zone3 = new(packet[11], packet[12], packet[13]);
        RgbColor zone4 = new(packet[14], packet[15], packet[16]);
        bool divided = !zone1.Equals(zone2) || !zone1.Equals(zone3) || !zone1.Equals(zone4);
        return HardwareReadResult<FourZoneLightingState>.Success(
            new FourZoneLightingState(
                effect,
                brightness.Value.Value,
                speed,
                divided,
                zone1,
                zone2,
                zone3,
                zone4));
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
