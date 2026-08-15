using LegionLoqControl.Domain.Controls;

namespace LegionLoqControl.Application.Hardware;

public static class HardwareStateTokens
{
    public static string FormatLighting(FourZoneLightingState value) =>
        string.Join(
            ':',
            value.Effect,
            value.Brightness,
            value.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.DivideArea ? "1" : "0",
            value.Zone1.ToHex(),
            value.Zone2.ToHex(),
            value.Zone3.ToHex(),
            value.Zone4.ToHex());

    public static FourZoneLightingState ParseLighting(string value)
    {
        string[] parts = value.Split(':');
        if (parts.Length != 8 ||
            !Enum.TryParse(parts[0], ignoreCase: true, out FourZoneEffect effect) ||
            !Enum.IsDefined(effect) ||
            !Enum.TryParse(parts[1], ignoreCase: true, out FourZoneKeyboardMode brightness) ||
            !Enum.IsDefined(brightness) ||
            brightness == FourZoneKeyboardMode.Unknown ||
            !byte.TryParse(parts[2], out byte speed) ||
            speed > FourZoneLightingState.MaximumSpeed ||
            parts[3] is not ("0" or "1") ||
            !RgbColor.TryParseHex(parts[4], out RgbColor zone1) ||
            !RgbColor.TryParseHex(parts[5], out RgbColor zone2) ||
            !RgbColor.TryParseHex(parts[6], out RgbColor zone3) ||
            !RgbColor.TryParseHex(parts[7], out RgbColor zone4))
        {
            throw new HardwareWriteException("lighting_value_invalid", HardwareWriteStatus.Failed);
        }

        return new FourZoneLightingState(
            effect,
            brightness,
            speed,
            parts[3] == "1",
            zone1,
            zone2,
            zone3,
            zone4);
    }

    public static string FormatFanTable(FanTableSnapshot value)
    {
        string points = string.Join(
            ';',
            value.Points.Select(static point =>
                $"{point.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture)},{point.Sensor.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        return $"{value.FanId.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{value.SensorId.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{points}";
    }

    public static FanTableSnapshot ParseFanTable(string value)
    {
        string[] parts = value.Split(':');
        if (parts.Length != 3 ||
            !byte.TryParse(parts[0], out byte fanId) ||
            !byte.TryParse(parts[1], out byte sensorId) ||
            string.IsNullOrWhiteSpace(parts[2]))
        {
            throw new HardwareWriteException("fan_table_value_invalid", HardwareWriteStatus.Failed);
        }

        string[] pointParts = parts[2].Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (pointParts.Length is < 1 or > FanTableSnapshot.MaximumPoints)
            throw new HardwareWriteException("fan_table_value_invalid", HardwareWriteStatus.Failed);

        var points = new FanTablePoint[pointParts.Length];
        for (int index = 0; index < pointParts.Length; index++)
        {
            string[] pair = pointParts[index].Split(',');
            if (pair.Length != 2 ||
                !byte.TryParse(pair[0], out byte speed) ||
                !byte.TryParse(pair[1], out byte sensor))
            {
                throw new HardwareWriteException("fan_table_value_invalid", HardwareWriteStatus.Failed);
            }

            points[index] = new FanTablePoint(speed, sensor);
        }

        return new FanTableSnapshot(fanId, sensorId, points);
    }
}
