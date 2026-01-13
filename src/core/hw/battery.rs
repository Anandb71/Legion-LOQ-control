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
