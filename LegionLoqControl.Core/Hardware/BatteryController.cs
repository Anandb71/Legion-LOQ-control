using global::System;
using global::System.Runtime.InteropServices;
using global::System.Threading.Tasks;
using LegionLoqControl.Core.Native;
using LegionLoqControl.Core.System;

namespace LegionLoqControl.Core.Hardware
{
    public class BatteryController
    {
        public bool SetConservationMode(bool enabled)
        {
            uint code = enabled ? 0x03u : 0x05u;
            return SendCode(Drivers.IOCTL_ENERGY_BATTERY_CHARGE_MODE, code);
        }

        public bool SetRapidCharge(bool enabled)
        {
            uint code = enabled ? 0x07u : 0x08u;
            return SendCode(Drivers.IOCTL_ENERGY_BATTERY_CHARGE_MODE, code);
        }

        private bool SendCode(uint controlCode, uint inBuffer)
        {
            try
            {
                var handle = Drivers.GetEnergy();
                
                var input = BitConverter.GetBytes(inBuffer);
                
                uint bytesReturned;
                return NativeMethods.DeviceIoControl(
                    handle,
                    controlCode,
                    input,
                    (uint)input.Length,
                    null,
                    0,
                    out bytesReturned,
                    IntPtr.Zero);
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"Battery Control Failed: {ex.Message}");
                return false;
            }
        }
    }
}
