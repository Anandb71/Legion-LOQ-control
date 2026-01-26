using global::System;
using global::System.Linq;
using global::System.Runtime.InteropServices;
using HidSharp;
using LegionLoqControl.Core.System.Management;
using Task = global::System.Threading.Tasks.Task;

namespace LegionLoqControl.Core.Hardware
{
    // Structs mirroring LLT Native.cs
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LENOVO_RGB_KEYBOARD_STATE
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Header;
        public byte Effect;
        public byte Speed;
        public byte Brightness;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Zone1Rgb;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Zone2Rgb;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Zone3Rgb;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Zone4Rgb;
        public byte Padding;
        public byte WaveLTR;
        public byte WaveRTL;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
        public byte[] Unused;
    }

    public class LightingController
    {
        private const int VENDOR_ID = 0x048D;
        private const int PRODUCT_ID_MASK = 0xFF00;
        private const int PRODUCT_ID_MATCH = 0xC900;

        public async Task<bool> SetLightingOwnerAsync(bool appControl)
        {
            try
            {
                await WMI.LenovoGameZoneData.SetLightControlOwnerAsync(appControl ? 1 : 0);
                return true;
            }
            catch { return false; }
        }

        public bool SetValues(byte brightness, byte r, byte g, byte b)
        {
            var device = DeviceList.Local.GetHidDevices(VENDOR_ID)
                .FirstOrDefault(d => (d.ProductID & PRODUCT_ID_MASK) == PRODUCT_ID_MATCH);

            if (device == null) return false;

            if (!device.TryOpen(out var stream)) return false;

            using (stream)
            {
                var state = new LENOVO_RGB_KEYBOARD_STATE
                {
                    Header = new byte[] { 0xCC, 0x16 },
                    Effect = 1, // Static
                    Speed = 1,
                    Brightness = brightness,
                    Zone1Rgb = new byte[] { r, g, b },
                    Zone2Rgb = new byte[] { r, g, b },
                    Zone3Rgb = new byte[] { r, g, b },
                    Zone4Rgb = new byte[] { r, g, b },
                    Padding = 0,
                    WaveLTR = 0,
                    WaveRTL = 0,
                    Unused = new byte[13]
                };

                try
                {
                    // Marshal struct to byte array
                    int size = Marshal.SizeOf(state);
                    byte[] arr = new byte[size];

                    IntPtr ptr = Marshal.AllocHGlobal(size);
                    try
                    {
                        Marshal.StructureToPtr(state, ptr, true);
                        Marshal.Copy(ptr, arr, 0, size);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }

                    stream.SetFeature(arr);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
