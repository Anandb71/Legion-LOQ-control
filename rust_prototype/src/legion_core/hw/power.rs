use crate::platform::windows::WmiQueryHandler;
use log::warn;
use std::fmt;

#[derive(Debug, PartialEq, Clone, Copy)]
pub enum PowerProfile {
    Performance,
    Balanced,
    Quiet,
    Unknown,
}

impl fmt::Display for PowerProfile {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match self {
            PowerProfile::Performance => write!(f, "Performance (Red)"),
            PowerProfile::Balanced => write!(f, "Balanced (White)"),
            PowerProfile::Quiet => write!(f, "Quiet (Blue)"),
            PowerProfile::Unknown => write!(f, "Unknown"),
        }
    }
}

pub fn get_power_profile() -> Option<PowerProfile> {
    let wmi = match WmiQueryHandler::new() {
        Ok(w) => w,
        Err(e) => {
            warn!("Failed to init WMI for power profile: {}", e);
            return None;
        }
    };

    match wmi.get_thermal_mode() {
        Ok(mode_int) => Some(match mode_int {
            3 => PowerProfile::Performance, // Assuming 3 based on offset. Might be 4.
            2 => PowerProfile::Balanced,
            1 => PowerProfile::Quiet,
            _ => PowerProfile::Unknown,
        }),
        Err(e) => {
            warn!("Failed to read power profile: {}", e);
            None
        }
    }
}

pub fn set_power_profile(profile: PowerProfile) -> Result<(), Box<dyn std::error::Error>> {
    if !crate::legion_core::safety::guards::GlobalWriteLock::is_write_allowed() {
         return Err("Write operations locked.".into());
    }

    let mode_int = match profile {
        PowerProfile::Quiet => 1,
        PowerProfile::Balanced => 2,
        PowerProfile::Performance => 3, // Assuming offset 1 from Enum 2. If fails, try 4.
        _ => return Err("Unsupported mode".into()),
    };

    let wmi = crate::platform::windows::WmiQueryHandler::new()?;
    wmi.set_thermal_mode(mode_int)?;
    
    Ok(())
}
