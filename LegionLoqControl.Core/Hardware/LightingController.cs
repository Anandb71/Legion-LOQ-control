using global::System;
using global::System.Linq;
using global::System.Runtime.InteropServices;
using HidSharp;
using LegionLoqControl.Core.System.Management;
using Task = global::System.Threading.Tasks.Task;

namespace LegionLoqControl.Core.Hardware
{
    /// <summary>
    /// LENOVO_RGB_KEYBOARD_STATE struct - matches LLT Native.cs exactly (33 bytes total).
    /// Used for 4-zone RGB keyboards (older Legion models).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LENOVO_RGB_KEYBOARD_STATE
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Header;            // 0xCC, 0x16
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
        public byte[] Unused;            // 13 bytes padding
        public byte Padding;             // 1 byte
        public byte Effect;              // 1=Static, 3=Breath, 4=Wave, 6=Smooth
        public byte WaveLTR;             // Wave left-to-right flag
        public byte WaveRTL;             // Wave right-to-left flag
        public byte Brightness;          // 0=Off, 1=Low, 2=High
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Zone1Rgb;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Zone2Rgb;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Zone3Rgb;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Zone4Rgb;
    }

    /// <summary>
    /// Controller for 4-zone RGB keyboards on Legion models.
    /// Based on LLT's RGBKeyboardBacklightController.
    /// </summary>
    public class LightingController
    {
        private const int VENDOR_ID = 0x048D;
        private const int PRODUCT_ID_MASK = 0xFF00;
        private const int PRODUCT_ID_MATCH = 0xC900;
        private const int FEATURE_REPORT_LENGTH = 0x21; // 33 bytes - critical for 4-zone RGB!

        private HidDevice? _device;
        private HidStream? _stream;

        /// <summary>
        /// Check if a 4-zone RGB keyboard is available.
        /// </summary>
        public bool IsSupported => FindDevice() != null;

        /// <summary>
        /// Take control of keyboard lighting from Vantage.
        /// </summary>
        public async Task<bool> SetLightingOwnerAsync(bool appControl)
        {
            try
            {
                await WMI.LenovoGameZoneData.SetLightControlOwnerAsync(appControl ? 1 : 0);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Set keyboard to all one color with specified brightness.
        /// </summary>
        public bool SetValues(byte brightness, byte r, byte g, byte b)
        {
            var device = FindDevice();
            if (device == null)
            {
                global::System.Diagnostics.Debug.WriteLine("4-zone RGB keyboard not found");
                return false;
            }

            try
            {
                if (_stream == null || !_stream.CanWrite)
                {
                    if (!device.TryOpen(out _stream))
                    {
                        global::System.Diagnostics.Debug.WriteLine("Failed to open HID device");
                        return false;
                    }
                }

                // Build state struct matching LLT exactly
                var state = new LENOVO_RGB_KEYBOARD_STATE
                {
                    Header = new byte[] { 0xCC, 0x16 },
                    Unused = new byte[13],
                    Padding = 0x00,
                    Effect = 1, // Static
                    WaveLTR = 0,
                    WaveRTL = 0,
                    Brightness = brightness, // 0=Off, 1=Low, 2=High
                    Zone1Rgb = new byte[] { r, g, b },
                    Zone2Rgb = new byte[] { r, g, b },
                    Zone3Rgb = new byte[] { r, g, b },
                    Zone4Rgb = new byte[] { r, g, b }
                };

                // Marshal struct to byte array
                int size = Marshal.SizeOf(state);
                byte[] buffer = new byte[size];

                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(state, ptr, false);
                    Marshal.Copy(ptr, buffer, 0, size);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                global::System.Diagnostics.Debug.WriteLine($"Sending RGB packet: {BitConverter.ToString(buffer)}");
                _stream.SetFeature(buffer);
                return true;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"SetValues failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Turn off keyboard backlight.
        /// </summary>
        public bool SetOff()
        {
            var device = FindDevice();
            if (device == null) return false;

            try
            {
                if (_stream == null || !_stream.CanWrite)
                {
                    if (!device.TryOpen(out _stream)) return false;
                }

                // Off state per LLT
                var state = new LENOVO_RGB_KEYBOARD_STATE
                {
                    Header = new byte[] { 0xCC, 0x16 },
                    Unused = new byte[13],
                    Padding = 0,
                    Effect = 0,
                    WaveLTR = 0,
                    WaveRTL = 0,
                    Brightness = 0,
                    Zone1Rgb = new byte[3],
                    Zone2Rgb = new byte[3],
                    Zone3Rgb = new byte[3],
                    Zone4Rgb = new byte[3]
                };

                int size = Marshal.SizeOf(state);
                byte[] buffer = new byte[size];

                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(state, ptr, false);
                    Marshal.Copy(ptr, buffer, 0, size);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                _stream.SetFeature(buffer);
                return true;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"SetOff failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find the 4-zone RGB keyboard HID device.
        /// Must match VendorID, ProductID mask, AND feature report length of 0x21 (33 bytes).
        /// </summary>
        private HidDevice? FindDevice()
        {
            if (_device != null) return _device;

            // Find device matching LLT's exact criteria
            _device = DeviceList.Local.GetHidDevices(VENDOR_ID)
                .FirstOrDefault(d =>
                    (d.ProductID & PRODUCT_ID_MASK) == PRODUCT_ID_MATCH &&
                    d.GetMaxFeatureReportLength() == FEATURE_REPORT_LENGTH);

            if (_device != null)
            {
                global::System.Diagnostics.Debug.WriteLine($"Found 4-zone RGB keyboard: VID={_device.VendorID:X4}, PID={_device.ProductID:X4}, FeatureLen={_device.GetMaxFeatureReportLength()}");
            }

            return _device;
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
            _device = null;
        }
    }
}
