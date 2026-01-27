using global::System;
using global::System.Threading.Tasks;

namespace LegionLoqControl.Core.System.Management
{
    // Extracted from LLT CapabilityID.cs
    public enum CapabilityID : uint
    {
        // CPU
        CPULongTermPowerLimit = 0x00E40001,
        CPUShortTermPowerLimit = 0x00E40002,
        CPUPeakPowerLimit = 0x00E40003,
        CPUCrossLoadingPowerLimit = 0x00E40004,
        CPUPL1Tau = 0x00E40005,
        
        // GPU
        GPUPowerBoost = 0x00E40008,
        GPUConfigurableTGP = 0x00E40009,
        GPUTemperatureLimit = 0x00E4000A,
        GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = 0x00E4000B,
        GPUToCPUDynamicBoost = 0x00E4000C,

        // Common
        CPUTemperatureLimit = 0x00E40007,
        APUsPPTPowerLimit = 0x00E40006,

        // Fan
        FanFullSpeed = 0x00E40000, // Derived assumption or check implementation
    }

    public static partial class WMI
    {
        public static class LenovoOtherMethod
        {
            public static Task<int> GetSupportThermalModeAsync() => CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_OTHER_METHOD",
                "GetSupportThermalMode",
                new Dictionary<string, object>(),
                pdc => Convert.ToInt32(pdc["mode"].Value));

            public static Task<int> GetFeatureValueAsync(CapabilityID id) => CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_OTHER_METHOD",
                "GetFeatureValue",
                new Dictionary<string, object> { { "IDs", (int)id } },
                pdc => Convert.ToInt32(pdc["Value"].Value));

            public static Task SetFeatureValueAsync(CapabilityID id, int value) => CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_OTHER_METHOD",
                "SetFeatureValue",
                new Dictionary<string, object>
                {
                    { "IDs", (int)id },
                    { "value", value }
                });
        }
    }
}
