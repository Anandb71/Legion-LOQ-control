using global::System;
using global::System.Threading.Tasks;
using LegionLoqControl.Core.System.Management;

namespace LegionLoqControl.Core.Hardware
{
    public enum PowerProfile
    {
        Quiet = 1,
        Balanced = 2,
        Performance = 3,
        Unknown = 0
    }

    public class PowerController
    {
        public async Task<bool> SetProfileAsync(PowerProfile profile)
        {
            try
            {
                await WMI.LenovoGameZoneData.SetSmartFanModeAsync((int)profile).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"Power Profile Set Failed: {ex.Message}");
                return false;
            }
        }
    }
}
