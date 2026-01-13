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
        // Namespace for Legion settings is usually root/WMI
        // The class is often Lenovo_BiosSetting or similar.
        // We need to query where InstanceName="Lenovo_ConservationMode" or similar logic.
        // NOTE: The exact WMI persistence for Conservation Mode varies.
        // Common method: Query 'Lenovo_BiosSetting' where CurrentSetting is relevant.
        // For safety/Phase C1, we will try to find the specific setting 'Conservation Mode'.
        
        let query = "SELECT CurrentSetting FROM Lenovo_BiosSetting";
        let results: Vec<Lenovo_BiosSetting> = self.con.raw_query(query)?;
        
        // This returns ALL settings. We need to filter manually or refine query if WMI supports it.
        // 'wmi' crate raw_query usually returns a list.
        // The encoding of CurrentSetting is usually "SettingName,Value".
        
        for setting in results {
            let parts: Vec<&str> = setting.CurrentSetting.split(',').collect();
            if parts.len() >= 2 {
                if parts[0].eq_ignore_ascii_case("ConservationMode") {
                     return Ok(parts[1].trim().eq_ignore_ascii_case("Enable"));
                }
            }
        }
        
        Err("Conservation Mode setting not found".into())
    }

    pub fn set_conservation_mode(&self, enable: bool) -> Result<(), Box<dyn Error>> {
        // Method: SetBiosSetting
        // Path: root/wmi:Lenovo_BiosSetting
        // Args: (parameter: String, value: String, password: String)
        
        let parameter = "ConservationMode";
        let value = if enable { "Enable" } else { "Disable" };
        let password = ""; // Assuming no BIOS password set for this setting access
        
        // We need to execute the method on the class instance.
        // First, find the instance.
        // query: SELECT * FROM Lenovo_BiosSetting
        
        let results: Vec<Lenovo_BiosSetting> = self.con.raw_query("SELECT * FROM Lenovo_BiosSetting")?;
        
        // Usually we execute on the *class* or the first instance.
        // sysinfo/wmi crates might vary. Wmi crate supports executing methods?
        // Actually, wmi crate `raw_query` is read only. We need `exec_method` or similar if available, 
        // OR we just use `WMIConnection` to execute.
        // Check `wmi` crate docs or capabilities. If `wmi` crate doesn't support methods easily, 
        // we might strictly fail here or need raw FFI.
        // 
        // `wmi` crate 0.13 usually supports `conn.svc().ExecMethod(...)`? No, it wraps it.
        // If `wmi` crate is insufficient, we'd default to powershell or winapi directly.
        //
        // SAFE APPROACH: For Phase C, since we might not have 'exec' in `wmi` crate handy without checking,
        // and we want to stay "boring":
        // We will call PowerShell for the WRITE operation. It is auditable, safe, and standard for "one-off" configs.
        // 
        // Command: (gwmi -class Lenovo_BiosSetting -namespace root\wmi).SetBiosSetting("ConservationMode", "Enable", "")
        
        use std::process::Command;
        
        let ps_script = format!(
            "(Get-WmiObject -Class Lenovo_BiosSetting -Namespace root\\wmi).SetBiosSetting('{}', '{}', '{}')",
            parameter, value, password
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
        // Check for return code in stdout? usually it returns a struct with 'return'
        // If it returns 0, success.
        
        if stdout.contains("return") && !stdout.contains(": 0") {
             return Err(format!("WMI method returned error: {}", stdout).into());
        }

        Ok(())
    }
}
