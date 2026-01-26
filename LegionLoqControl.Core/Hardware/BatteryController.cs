using global::System;
using global::System.Runtime.InteropServices;
using LegionLoqControl.Core.Native;
using LegionLoqControl.Core.System;

namespace LegionLoqControl.Core.Hardware
{
    public class BatteryController
    {
        public bool SetConservationMode(bool enabled)
        {
            // LLT BatteryFeature.cs logic:
            // Conservation ON: 0x03
            // Normal (from Conservation): 0x05
            uint code = enabled ? 0x03u : 0x05u;
            return SendCode(Drivers.IOCTL_ENERGY_BATTERY_CHARGE_MODE, code);
        }

        public bool SetRapidCharge(bool enabled)
        {
            // Rapid ON: 0x07
            // Normal (from Rapid): 0x08
            uint code = enabled ? 0x07u : 0x08u;
            return SendCode(Drivers.IOCTL_ENERGY_BATTERY_CHARGE_MODE, code);
        }

        private bool SendCode(uint controlCode, uint inBuffer)
        {
            try
            {
                var handle = Drivers.GetEnergy();
                
                // Use LLT-style generic DeviceIoControl
                bool result = NativeMethods.DeviceIoControl(
                    handle,
                    controlCode,
                    inBuffer,
                    out uint outBuffer);

                global::System.Diagnostics.Debug.WriteLine($"Battery IOCTL: code=0x{inBuffer:X}, result={result}, out=0x{outBuffer:X}");
                return result;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"Battery Control Failed: {ex.Message}");
                return false;
            }
        }
    }
}
