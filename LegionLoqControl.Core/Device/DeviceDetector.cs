using System;
using global::System.Management;

namespace LegionLoqControl.Core.Device
{
    public class DeviceDetector
    {
        public bool IsSupported { get; private set; }
        public string Model { get; private set; } = "Unknown";
        public string Manufacturer { get; private set; } = "Unknown";
        public string BiosVersion { get; private set; } = "Unknown";
        public string Series { get; private set; } = "Unknown";

        public void Detect()
        {
            try
            {
                // Query Win32_ComputerSystemProduct for Model/Vendor
                using var searcher = new ManagementObjectSearcher("SELECT Vendor, Name, Version FROM Win32_ComputerSystemProduct");
                foreach (ManagementObject obj in searcher.Get())
                {
                    Manufacturer = obj["Vendor"]?.ToString() ?? "Unknown";
                    Model = obj["Name"]?.ToString() ?? "Unknown";
                }

                // Query Win32_Bios for Version
                using var biosSearcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_Bios");
                foreach (ManagementObject obj in biosSearcher.Get())
                {
                    BiosVersion = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
                }

                IsSupported = CheckSupport();
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"Error querying WMI: {ex.Message}");
                IsSupported = false;
            }
        }

        private bool CheckSupport()
        {
            if (!Manufacturer.Contains("Lenovo", StringComparison.OrdinalIgnoreCase)) return false;

            // Simple logic matching Rust version
            if (Model.Contains("Legion", StringComparison.OrdinalIgnoreCase))
            {
                Series = "Legion";
                return true;
            }
            if (Model.Contains("LOQ", StringComparison.OrdinalIgnoreCase) || Model.Contains("83DV")) // 83DV is LOQ 15
            {
                Series = "LOQ";
                return true;
            }

            Series = "Unsupported Lenovo";
            return false;
        }
    }
}
