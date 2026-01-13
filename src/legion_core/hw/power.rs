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

    match wmi.get_performance_mode() {
        Ok(mode_str) => Some(match mode_str.as_str() {
            "Performance" => PowerProfile::Performance,
            "Balanced" => PowerProfile::Balanced,
            "Quiet" => PowerProfile::Quiet,
            _ => PowerProfile::Unknown,
        }),
        Err(e) => {
            warn!("Failed to read power profile: {}", e);
            None
        }
    }
}

pub fn set_power_profile(profile: PowerProfile) -> Result<(), Box<dyn std::error::Error>> {
    // Safety Check
    if !crate::legion_core::safety::guards::GlobalWriteLock::is_write_allowed() {
         return Err("Write operations are locked. Use --set-profile explicitly.".into());
    }

    let mode_str = match profile {
        PowerProfile::Performance => "Performance",
        PowerProfile::Balanced => "Balanced",
        PowerProfile::Quiet => "Quiet",
        PowerProfile::Unknown => return Err("Cannot set profile to Unknown".into()),
    };

    let wmi = match WmiQueryHandler::new() {
        Ok(w) => w,
        Err(e) => return Err(format!("Failed to init WMI: {}", e).into()),
    };

    wmi.set_performance_mode(mode_str)?;
    Ok(())
}
