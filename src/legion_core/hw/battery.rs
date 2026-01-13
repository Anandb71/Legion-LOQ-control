use crate::platform::windows::WmiQueryHandler;
use log::warn;

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
    let wmi = match WmiQueryHandler::new() {
        Ok(w) => w,
        Err(e) => {
            warn!("Failed to init WMI for conservation mode: {}", e);
            return None;
        }
    };
    
    match wmi.get_conservation_mode() {
        Ok(enabled) => Some(enabled),
        Err(e) => {
            warn!("Failed to read conservation mode: {}", e);
            None
        }
    }
}

pub fn set_conservation_mode(enable: bool) -> Result<(), Box<dyn std::error::Error>> {
    // 1. Safety Check: Global Write Lock
    if !crate::legion_core::safety::guards::GlobalWriteLock::is_write_allowed() {
        return Err("Write operations are locked. Use --set-conservation-mode explicitly (and ensure code requests access).".into());
    }

    let wmi = match WmiQueryHandler::new() {
        Ok(w) => w,
        Err(e) => return Err(format!("Failed to init WMI: {}", e).into()),
    };
    
    // 2. Execute
    wmi.set_conservation_mode(enable)?;
    
    Ok(())
}

pub fn get_rapid_charge() -> Option<bool> {
    let wmi = match WmiQueryHandler::new() {
        Ok(w) => w,
        Err(e) => {
            warn!("Failed to init WMI for rapid charge: {}", e);
            return None;
        }
    };
    
    match wmi.get_rapid_charge() {
        Ok(enabled) => Some(enabled),
        Err(e) => {
            warn!("Failed to read rapid charge: {}", e);
            None
        }
    }
}

pub fn set_rapid_charge(enable: bool) -> Result<(), Box<dyn std::error::Error>> {
    if !crate::legion_core::safety::guards::GlobalWriteLock::is_write_allowed() {
        return Err("Write operations are locked. Use --rapid-charge explicitly.".into());
    }

    let wmi = match WmiQueryHandler::new() {
        Ok(w) => w,
        Err(e) => return Err(format!("Failed to init WMI: {}", e).into()),
    };
    
    // Safety: Check if conservation mode is on? 
    // Usually harmless to try, firmware handles it, but let's just do it.
    wmi.set_rapid_charge(enable)?;
    Ok(())
}
