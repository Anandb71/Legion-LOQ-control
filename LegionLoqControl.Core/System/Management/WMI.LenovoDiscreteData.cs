using global::System;
using global::System.Collections.Generic;
using global::System.Threading.Tasks;

namespace LegionLoqControl.Core.System.Management
{
    public record DiscreteCapability(CapabilityID Id, int Value);

    public static partial class WMI
    {
        public static class LenovoDiscreteData
        {
            public static Task<IEnumerable<DiscreteCapability>> ReadAsync() => WMI.ReadAsync("root\\WMI",
                $"SELECT * FROM LENOVO_DISCRETE_DATA",
                pdc =>
                {
                    var id = (CapabilityID)Convert.ToInt32(pdc["IDs"].Value);
                    var value = Convert.ToInt32(pdc["Value"].Value);
                    return new DiscreteCapability(id, value);
                });
        }
    }
}
