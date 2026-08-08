using global::System;
using global::System.Threading.Tasks;
using LegionLoqControl.Core.Native;
using LegionLoqControl.Core.System;

namespace LegionLoqControl.Core.Hardware
{
    /// <summary>
    /// White keyboard backlight states (for LOQ and some Legion models).
    /// </summary>
    public enum WhiteKeyboardState
    {
        Off = 0,
        Low = 1,
        High = 2
    }

    /// <summary>
    /// Controller for white-only keyboard backlight using EnergyDrv driver IOCTL.
    /// Based on LLT's WhiteKeyboardDriverBacklightFeature.
    /// Supports: LOQ series, some Legion models without RGB.
    /// </summary>
    public class WhiteKeyboardController
    {
        /// <summary>
        /// Check if white keyboard backlight is supported.
        /// </summary>
        public bool IsSupported
        {
            get
            {
                try
                {
                    var handle = Drivers.GetEnergy();
                    uint inBuffer = 0x1; // Query
                    bool success = NativeMethods.DeviceIoControl<uint, uint>(
                        handle,
                        Drivers.IOCTL_ENERGY_KEYBOARD,
                        inBuffer,
                        out uint outBuffer);

                    if (!success) return false;

                    // Per LLT: outBuffer >> 1 should equal 0x2 if supported
                    return (outBuffer >> 1) == 0x2;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Set keyboard backlight state (Off/Low/High).
        /// </summary>
        public bool SetState(WhiteKeyboardState state)
        {
            try
            {
                var handle = Drivers.GetEnergy();

                // Control codes per LLT WhiteKeyboardDriverBacklightFeature:
                // Off  = 0x00023
                // Low  = 0x10023
                // High = 0x20023
                uint inBuffer = state switch
                {
                    WhiteKeyboardState.Off => 0x00023,
                    WhiteKeyboardState.Low => 0x10023,
                    WhiteKeyboardState.High => 0x20023,
                    _ => throw new ArgumentException("Invalid keyboard state")
                };

                bool success = NativeMethods.DeviceIoControl<uint, uint>(
                    handle,
                    Drivers.IOCTL_ENERGY_KEYBOARD,
                    inBuffer,
                    out uint outBuffer);

                global::System.Diagnostics.Debug.WriteLine($"WhiteKeyboard SetState({state}): inBuffer=0x{inBuffer:X}, success={success}");
                return success;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"WhiteKeyboard SetState failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get current keyboard backlight state.
        /// </summary>
        public WhiteKeyboardState GetState()
        {
            try
            {
                var handle = Drivers.GetEnergy();

                // Send query with 0x22 per LLT GetInBufferValue()
                uint inBuffer = 0x22;
                
                bool success = NativeMethods.DeviceIoControl<uint, uint>(
                    handle,
                    Drivers.IOCTL_ENERGY_KEYBOARD,
                    inBuffer,
                    out uint outBuffer);

                if (!success) return WhiteKeyboardState.Off;

                // Per LLT FromInternalAsync:
                // 0x1 = Off, 0x3 = Low, 0x5 = High
                return outBuffer switch
                {
                    0x1 => WhiteKeyboardState.Off,
                    0x3 => WhiteKeyboardState.Low,
                    0x5 => WhiteKeyboardState.High,
                    _ => WhiteKeyboardState.Off
                };
            }
            catch
            {
                return WhiteKeyboardState.Off;
            }
        }
    }
}
