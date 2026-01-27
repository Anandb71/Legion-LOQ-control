using global::System;
using global::System.Collections.Generic;
using global::System.Threading.Tasks;

namespace LegionLoqControl.Core.System.Management
{
    public record RangeCapability(CapabilityID Id, int DefaultValue, int Min, int Max, int Step);

    public static partial class WMI
    {
        public static class LenovoCapabilityData01
        {
            // Note: WMI.ReadAsync helper needed in WMI.cs
            public static Task<IEnumerable<RangeCapability>> ReadAsync() => WMI.ReadAsync("root\\WMI",
                $"SELECT * FROM LENOVO_CAPABILITY_DATA_01",
                pdc =>
                {
                    var id = Convert.ToInt32(pdc["IDs"].Value);
                    var defaultValue = Convert.ToInt32(pdc["DefaultValue"].Value);
                    var min = Convert.ToInt32(pdc["MinValue"].Value);
                    var max = Convert.ToInt32(pdc["MaxValue"].Value);
                    var step = Convert.ToInt32(pdc["Step"].Value);
                    return new RangeCapability((CapabilityID)id, defaultValue, min, max, step);
                });
        }
    }
}
