using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LegionLoqControl.Core.System.Management
{
    public static partial class WMI
    {
        public static class LenovoGameZoneData
        {
            public static Task<int> GetSmartFanModeAsync() => CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_GAMEZONE_DATA",
                "GetSmartFanMode",
                new Dictionary<string, object>(),
                pdc => Convert.ToInt32(pdc["Data"].Value));

            public static Task SetSmartFanModeAsync(int data) => CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_GAMEZONE_DATA",
                "SetSmartFanMode",
                new Dictionary<string, object> { { "Data", data } });

            public static Task SetLightControlOwnerAsync(int data) => CallAsync("ROOT\\WMI",
                $"SELECT * FROM LENOVO_GAMEZONE_DATA",
                "SetLightControlOwner",
                new Dictionary<string, object> { { "Data", data } });
        }
    }
}
