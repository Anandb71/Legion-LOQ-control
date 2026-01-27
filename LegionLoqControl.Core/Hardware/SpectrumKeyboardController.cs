using global::System;
using global::System.Linq;
using global::System.Runtime.InteropServices;
using HidSharp;
using Task = global::System.Threading.Tasks.Task;

namespace LegionLoqControl.Core.Hardware
{
    /// <summary>
    /// Controller for Spectrum (per-key RGB) keyboards on newer Legion/LOQ models.
    /// Based on LLT's SpectrumKeyboardBacklightController protocol analysis.
    /// </summary>
    public class SpectrumKeyboardController
    {
        private const int VENDOR_ID = 0x048D;
        private const int PRODUCT_ID_MASK = 0xFF00;
        private const int PRODUCT_ID_MATCH = 0xC900;
        private const int FEATURE_REPORT_SIZE = 960;

        // Operation types from LLT Native.cs
        private const byte OP_GET_BRIGHTNESS = 0xCD;
        private const byte OP_SET_BRIGHTNESS = 0xCE;

        private HidDevice? _device;
        private HidStream? _stream;

        public bool IsSupported => FindDevice() != null;

        /// <summary>
        /// Set keyboard brightness (0-9 range as per LLT).
        /// </summary>
        public bool SetBrightness(int brightness)
        {
            if (brightness < 0 || brightness > 9)
                return false;

            try
            {
                var device = FindDevice();
                if (device == null) return false;

                if (_stream == null || !_stream.CanWrite)
                {
                    if (!device.TryOpen(out _stream)) return false;
                }

                // Build SET_BRIGHTNESS request packet (960 bytes)
                // Format: [Head=7, Type=0xCE, Size=0xC0, Tail=3, Brightness, ...padding...]
                var packet = new byte[FEATURE_REPORT_SIZE];
                packet[0] = 7;                      // Head
                packet[1] = OP_SET_BRIGHTNESS;      // Type
                packet[2] = 0xC0;                   // Size (192)
                packet[3] = 3;                      // Tail
                packet[4] = (byte)brightness;       // Brightness value

                _stream.SetFeature(packet);
                return true;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"Spectrum SetBrightness failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get current keyboard brightness.
        /// </summary>
        public int GetBrightness()
        {
            try
            {
                var device = FindDevice();
                if (device == null) return -1;

                if (_stream == null || !_stream.CanWrite)
                {
                    if (!device.TryOpen(out _stream)) return -1;
                }

                // Build GET_BRIGHTNESS request
                var request = new byte[FEATURE_REPORT_SIZE];
                request[0] = 7;                     // Head
                request[1] = OP_GET_BRIGHTNESS;     // Type
                request[2] = 0xC0;                  // Size
                request[3] = 3;                     // Tail

                _stream.SetFeature(request);

                // Read response
                var response = new byte[FEATURE_REPORT_SIZE];
                _stream.GetFeature(response);

                // Response format: [ReportId, Type, Length, Unknown1, Brightness, ...]
                return response[4]; // Brightness at offset 4
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"Spectrum GetBrightness failed: {ex.Message}");
                return -1;
            }
        }

        private HidDevice? FindDevice()
        {
            if (_device != null) return _device;

            _device = DeviceList.Local.GetHidDevices(VENDOR_ID)
                .FirstOrDefault(d => 
                    (d.ProductID & PRODUCT_ID_MASK) == PRODUCT_ID_MATCH &&
                    d.GetMaxFeatureReportLength() >= FEATURE_REPORT_SIZE);

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
