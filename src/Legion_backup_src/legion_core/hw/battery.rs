use crate::platform::windows::{WmiQueryHandler, EnergyDriver};
use log::warn;
use std::error::Error;

#[derive(Debug)]
pub struct BatteryStatus {
    pub charge_percent: u16,
    pub is_charging: bool,
}

pub fn get_battery_status() -> Option<BatteryStatus> {
    let wmi = match WmiQueryHandler::new() {
        Ok(w) => w,
        Err(e) => {
            warn!("Failed to init WMI for battery: {}", e);
            return None;
        }
    };

    match wmi.get_battery_info() {
        Ok((charge, status)) => {
            // Win32_Battery: BatteryStatus 2 = AC Power (Charging or Charged)
            let is_charging = status == 2; 
            Some(BatteryStatus {
                charge_percent: charge,
                is_charging,
            })
        },
        Err(e) => {
            warn!("Failed to read battery: {}", e);
            None
        }
    }
}

pub fn get_conservation_mode() -> Option<bool> {
    // Reading not yet ported from LLT (requires IOCTL read logic).
    // Returning None ensures GUI doesn't show false state.
    None
}

pub fn set_conservation_mode(enable: bool) -> Result<(), Box<dyn Error>> {
    // 1. Safety Check: Global Write Lock
    if !crate::legion_core::safety::guards::GlobalWriteLock::is_write_allowed() {
        return Err("Write operations are locked. Use --set-conservation-mode explicitly.".into());
    }

    // 2. Execute via EnergyDriver (IOCTL)
    let driver = EnergyDriver::new()?;
    driver.set_conservation_mode(enable)?;
    
    Ok(())
}

pub fn get_rapid_charge() -> Option<bool> {
    None
}

pub fn set_rapid_charge(enable: bool) -> Result<(), Box<dyn Error>> {
    if !crate::legion_core::safety::guards::GlobalWriteLock::is_write_allowed() {
        return Err("Write operations are locked. Use --rapid-charge explicitly.".into());
    }

    let driver = EnergyDriver::new()?;
    driver.set_rapid_charge(enable)?;
    Ok(())
}
