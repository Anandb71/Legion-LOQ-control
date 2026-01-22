using System;
using System.Linq;
using System.Management;
using HidSharp;

namespace LegionLoqControl.Core.Hardware
{
    public class LightingController
    {
        private const int VENDOR_ID = 0x048D;
        private const int PRODUCT_ID_MASK = 0xFF00; // Matches Rust mask
        private const int PRODUCT_ID_MATCH = 0xC900;

        public bool SetLightingOwner(bool appControl)
        {
            try
            {
                // PowerShell: (Get-WmiObject ...).SetLightControlOwner(1)
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM LENOVO_GAMEZONE_DATA");
                foreach (ManagementObject obj in searcher.Get())
                {
                    using var mc = new ManagementClass(obj.Scope, new ManagementPath("LENOVO_GAMEZONE_DATA"), null);
                    ManagementBaseObject paramsObj = mc.GetMethodParameters("SetLightControlOwner");
                    
                    paramsObj["Data"] = appControl ? 1 : 0;
                    obj.InvokeMethod("SetLightControlOwner", paramsObj, null);
                    return true;
                }
            }
            catch (Exception) { /* Log error */ }
            return false;
        }

        public bool SetDimensions(byte brightness, byte r, byte g, byte b)
        {
            // Find device
            var device = HidSharp.DeviceList.Local.GetHidDevices(VENDOR_ID)
                .FirstOrDefault(d => (d.ProductID & PRODUCT_ID_MASK) == PRODUCT_ID_MATCH);

            if (device == null) return false;

            if (!device.TryOpen(out var stream)) return false;

            using (stream)
            {
                // Structure: [Header L, Header H, Effect, Speed, Brightness, R, G, B, R, G, B...]
                // 33 bytes total? Rust struct was 33 bytes.
                // 0xCC, 0x16 header.

                byte[] report = new byte[33];
                report[0] = 0xCC;
                report[1] = 0x16;
                report[2] = 1; // Static effect
                report[3] = 1; // Speed
                report[4] = brightness; // 1=Low, 2=High

                // Zone 1
                report[5] = r; report[6] = g; report[7] = b;
                // Zone 2
                report[8] = r; report[9] = g; report[10] = b;
                // Zone 3
                report[11] = r; report[12] = g; report[13] = b;
                // Zone 4
                report[14] = r; report[15] = g; report[16] = b;

                try
                {
                    // HidSharp SetFeature
                    stream.SetFeature(report);
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
