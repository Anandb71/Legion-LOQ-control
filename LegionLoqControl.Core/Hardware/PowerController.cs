using System;
using System.Management;

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
        public bool SetProfile(PowerProfile profile)
        {
            try
            {
                // PowerShell: (Get-WmiObject -Namespace root\WMI -Class LENOVO_GAMEZONE_DATA).SetSmallData(1, <mode>)
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM LENOVO_GAMEZONE_DATA");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    // Get parameters from the Class, not the instance
                    using var mc = new ManagementClass(obj.Scope, new ManagementPath("LENOVO_GAMEZONE_DATA"), null);
                    ManagementBaseObject paramsObj = mc.GetMethodParameters("SetSmallData");
                    
                    paramsObj["Data1"] = 1; // 1 = Thermal Mode ID
                    paramsObj["Data2"] = (int)profile;

                    obj.InvokeMethod("SetSmallData", paramsObj, null);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Power Profile Set Failed: {ex.Message}");
            }
            return false;
        }

        public PowerProfile GetProfile()
        {
            // Reading usually requires a different method or getting a property from LENOVO_GAMEZONE_DATA
            // Rust implementation stubbed this or used 'GetSmallData'?
            // Assuming for now we just return Unknown or implement later.
            return PowerProfile.Unknown;
        }
    }
}
