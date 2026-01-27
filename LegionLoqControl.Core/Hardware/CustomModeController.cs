using global::System;
using global::System.Threading.Tasks;
using LegionLoqControl.Core.System.Management;

namespace LegionLoqControl.Core.Hardware
{
    public class CustomModeController
    {
        public bool IsSupported { get; private set; } = true; // Assume supported on newer devices or check WMI existence

        public async Task<bool> SetFanFullSpeedAsync(bool enabled)
        {
            try
            {
                // Call WMI to set fan full speed (1 = On, 0 = Off)
                await WMI.LenovoFanMethod.FanSetFullSpeedAsync(enabled ? 1 : 0).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"SetFanFullSpeed failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> GetFanFullSpeedAsync()
        {
            try
            {
                return await WMI.LenovoFanMethod.FanGetFullSpeedAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"GetFanFullSpeed failed: {ex.Message}");
                return false;
            }
        }
        
        // Placeholder for future power limit controls
        // public async Task SetPl1(int value) { ... }
    }
}
