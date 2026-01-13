use wmi::{COMLibrary, WMIConnection};
use serde::Deserialize;
use std::error::Error;
use log::info;

pub struct WmiQueryHandler {
    con: WMIConnection,
}

#[derive(Deserialize, Debug)]
struct Win32_ComputerSystem {
    Manufacturer: String,
    Model: String,
}

#[derive(Deserialize, Debug)]
struct Win32_Battery {
    EstimatedChargeRemaining: u16,
    BatteryStatus: u16, // 1=Discharging, 2=AC, etc. (simplified)
}

#[derive(Deserialize, Debug)]
#[serde(rename_all = "PascalCase")]
struct Lenovo_BiosSetting {
    CurrentSetting: String,
}

#[derive(Deserialize, Debug)]
struct Win32_BIOS {
    SMBIOSBIOSVersion: String,
}

impl WmiQueryHandler {
    pub fn new() -> Result<Self, Box<dyn Error>> {
        let com_con = COMLibrary::new()?;
        let wmi_con = WMIConnection::new(com_con)?;
        Ok(Self { con: wmi_con })
    }

    pub fn get_manufacturer(&self) -> Result<String, Box<dyn Error>> {
        let results: Vec<Win32_ComputerSystem> = self.con.raw_query("SELECT Manufacturer FROM Win32_ComputerSystem")?;
        if let Some(sys) = results.first() {
            Ok(sys.Manufacturer.clone())
        } else {
            Err("Could not retrieve Manufacturer".into())
        }
    }

    pub fn get_model(&self) -> Result<String, Box<dyn Error>> {
        let results: Vec<Win32_ComputerSystem> = self.con.raw_query("SELECT Model FROM Win32_ComputerSystem")?;
        if let Some(sys) = results.first() {
            Ok(sys.Model.clone())
        } else {
            Err("Could not retrieve Model".into())
        }
    }

    pub fn get_bios_version(&self) -> Result<String, Box<dyn Error>> {
        let results: Vec<Win32_BIOS> = self.con.raw_query("SELECT SMBIOSBIOSVersion FROM Win32_BIOS")?;
        if let Some(bios) = results.first() {
            Ok(bios.SMBIOSBIOSVersion.clone())
        } else {
            Err("Could not retrieve BIOS Version".into())
        }
    }

    pub fn get_battery_info(&self) -> Result<(u16, u16), Box<dyn Error>> {
        let results: Vec<Win32_Battery> = self.con.raw_query("SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery")?;
        if let Some(bat) = results.first() {
            Ok((bat.EstimatedChargeRemaining, bat.BatteryStatus))
        } else {
            Err("No battery found".into())
        }
    }

    pub fn get_conservation_mode(&self) -> Result<bool, Box<dyn Error>> {
        let val = self.get_lenovo_bios_setting("ConservationMode")?;
        Ok(val.eq_ignore_ascii_case("Enable"))
    }

    pub fn set_conservation_mode(&self, enable: bool) -> Result<(), Box<dyn Error>> {
        self.set_lenovo_bios_setting("ConservationMode", if enable { "Enable" } else { "Disable" })
    }

    pub fn get_rapid_charge(&self) -> Result<bool, Box<dyn Error>> {
        let qs = self.get_lenovo_bios_setting("RapidCharge")?;
        Ok(qs.eq_ignore_ascii_case("Enable"))
    }

    pub fn set_rapid_charge(&self, enable: bool) -> Result<(), Box<dyn Error>> {
        self.set_lenovo_bios_setting("RapidCharge", if enable { "Enable" } else { "Disable" })
    }

    pub fn get_performance_mode(&self) -> Result<String, Box<dyn Error>> {
        self.get_lenovo_bios_setting("SystemPerformanceMode")
    }

    pub fn set_performance_mode(&self, mode: &str) -> Result<(), Box<dyn Error>> {
        // Mode expected: "Quiet", "Balanced", "Performance"
        // Validating inputs should happen in core, but we can adhere to basic strings here.
        self.set_lenovo_bios_setting("SystemPerformanceMode", mode)
    }

    // Helper to get a specific setting value
    fn get_lenovo_bios_setting(&self, key: &str) -> Result<String, Box<dyn Error>> {
        let results: Vec<Lenovo_BiosSetting> = self.con.raw_query("SELECT CurrentSetting FROM Lenovo_BiosSetting")?;
        
        for setting in results {
            let parts: Vec<&str> = setting.CurrentSetting.split(',').collect();
            if parts.len() >= 2 {
                if parts[0].eq_ignore_ascii_case(key) {
                     return Ok(parts[1].trim().to_string());
                }
            }
        }
        Err(format!("Setting '{}' not found", key).into())
    }

    // Helper to write a BIOS setting via PowerShell
    fn set_lenovo_bios_setting(&self, parameter: &str, value: &str) -> Result<(), Box<dyn Error>> {
        use std::process::Command;
        
        // Command: (gwmi -class Lenovo_BiosSetting -namespace root\wmi).SetBiosSetting("Key", "Value", "Password")
        let ps_script = format!(
            "(Get-WmiObject -Class Lenovo_BiosSetting -Namespace root\\wmi).SetBiosSetting('{}', '{}', '')",
            parameter, value
        );
        
        info!("Executing WMI write via PowerShell: {}", ps_script);
        
        let output = Command::new("powershell")
            .args(&["-NoProfile", "-Command", &ps_script])
            .output()?;
            
        if !output.status.success() {
            let stderr = String::from_utf8_lossy(&output.stderr);
            return Err(format!("WMI Write failed: {}", stderr).into());
        }
        
        let stdout = String::from_utf8_lossy(&output.stdout);
        // Check for WMI return code success (usually 0 or just check if it ran without throwing)
        if stdout.contains("return") && !stdout.contains(": 0") {
             return Err(format!("WMI method returned error: {}", stdout).into());
        }

        Ok(())
    }
}
