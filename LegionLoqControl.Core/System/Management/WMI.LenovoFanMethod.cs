using global::System;
using global::System.Linq;
using global::System.Collections.Generic;
using global::System.Threading.Tasks;

namespace LegionLoqControl.Core.System.Management
{
    public static partial class WMI
    {
        public static class LenovoFanMethod
        {
            public static Task FanSetTableAsync(byte[] fanTable) => CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_METHOD",
                "Fan_Set_Table",
                new Dictionary<string, object> { { "FanTable", fanTable } });

            public static Task<bool> FanGetFullSpeedAsync() => CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_METHOD",
                "Fan_Get_FullSpeed",
                new Dictionary<string, object>(),
                pdc => (bool)pdc["Status"].Value);

            public static Task FanSetFullSpeedAsync(int status) => CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_METHOD",
                "Fan_Set_FullSpeed",
                new Dictionary<string, object> { { "Status", status } });

            public static Task<int> FanGetCurrentSensorTemperatureAsync(int sensorId) => CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_METHOD",
                "Fan_GetCurrentSensorTemperature",
                new Dictionary<string, object> { { "SensorID", sensorId } },
                pdc => Convert.ToInt32(pdc["CurrentSensorTemperature"].Value));

            public static Task<int> FanGetCurrentFanSpeedAsync(int fanId) => CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_METHOD",
                "Fan_GetCurrentFanSpeed",
                new Dictionary<string, object> { { "FanID", fanId } },
                pdc => Convert.ToInt32(pdc["CurrentFanSpeed"].Value));
        }
    }
}
