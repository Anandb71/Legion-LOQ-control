using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal static class FanTableParser
{
    internal const int MaximumPoints = FanTableSnapshot.MaximumPoints;
    internal const uint MaximumValue = 255;

    internal static HardwareReadResult<FanTableSnapshot> Parse(
        byte fanId,
        byte sensorId,
        IReadOnlyList<uint> speeds,
        IReadOnlyList<uint> sensors)
    {
        ArgumentNullException.ThrowIfNull(speeds);
        ArgumentNullException.ThrowIfNull(sensors);

        if (speeds.Count == 0 || sensors.Count == 0)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.InvalidData,
                "fan_table_empty");
        }

        if (speeds.Count != sensors.Count)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.InvalidData,
                "fan_table_length_mismatch");
        }

        if (speeds.Count > MaximumPoints)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.InvalidData,
                "fan_table_too_long");
        }

        var points = new FanTablePoint[speeds.Count];
        for (int index = 0; index < speeds.Count; index++)
        {
            uint speed = speeds[index];
            uint sensor = sensors[index];
            if (speed > MaximumValue || sensor > MaximumValue)
            {
                return HardwareReadResult<FanTableSnapshot>.Failure(
                    HardwareReadStatus.InvalidData,
                    "fan_table_value_invalid");
            }

            points[index] = new FanTablePoint((byte)speed, (byte)sensor);
        }

        return HardwareReadResult<FanTableSnapshot>.Success(
            new FanTableSnapshot(fanId, sensorId, points));
    }
}
