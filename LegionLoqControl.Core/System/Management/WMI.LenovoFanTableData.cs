using global::System;
using global::System.Collections.Generic;
using global::System.Threading.Tasks;

namespace LegionLoqControl.Core.System.Management
{
    public static partial class WMI
    {
        public static class LenovoFanTableData
        {
            public static Task<IEnumerable<(int mode, byte fanId, byte sensorId, ushort[] fanTableData, ushort[] sensorTableData)>> ReadAsync() => WMI.ReadAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_TABLE_DATA",
                pdc =>
                {
                    // Safe parsing with fallbacks similar to LLT (though LLT uses explicit checks)
                    int mode = -1;
                    try { mode = Convert.ToInt32(pdc["Mode"].Value); } catch { }

                    var fanId = Convert.ToByte(pdc["Fan_Id"].Value);
                    var sensorId = Convert.ToByte(pdc["Sensor_ID"].Value);
                    
                    var fanTableData = (ushort[]?)pdc["FanTable_Data"].Value ?? new ushort[0];
                    var sensorTableData = (ushort[]?)pdc["SensorTable_Data"].Value ?? new ushort[0];
                    
                    return (mode, fanId, sensorId, fanTableData, sensorTableData);
                });
        }
    }
}
