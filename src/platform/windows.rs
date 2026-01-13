use wmi::{COMLibrary, WMIConnection};
use serde::Deserialize;
use std::error::Error;

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
}
