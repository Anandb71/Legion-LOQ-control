use wmi::{COMLibrary, WMIConnection, Variant};
use serde::Deserialize;
use std::error::Error;

pub struct WmiQueryHandler {
    con: WMIConnection,
    con_wmi: WMIConnection, // Connection to root\WMI
}

#[derive(Deserialize, Debug)]
#[serde(rename_all = "PascalCase")]
struct Win32ComputerSystemProduct {
    vendor: String,
    name: String,
    version: String,
}

#[derive(Deserialize, Debug)]
#[serde(rename_all = "PascalCase")]
struct Win32Battery {
    estimated_charge_remaining: u16,
    battery_status: u16, 
}

#[derive(Deserialize, Debug)]
#[serde(rename_all = "PascalCase")]
struct LenovoBiosSetting {
    current_setting: String,
}

#[derive(Deserialize, Debug)]
struct Win32Bios {
    #[serde(rename = "SMBIOSBIOSVersion")]
    smbios_bios_version: String,
}

impl WmiQueryHandler {
    pub fn new() -> Result<Self, Box<dyn Error>> {
        // Attempt to initialize COM (MTA).
        // eframe (GUI) likely initializes COM as STA, causing RPC_E_CHANGED_MODE (0x80010106).
        let com_con = match COMLibrary::new() {
            Ok(c) => c,
            Err(e) => {
                let err_str = format!("{:?}", e);
                if err_str.contains("0x80010106") || err_str.contains("RPC_E_CHANGED_MODE") {
                    // COM already initialized in a different mode (STA). This is expected in GUI.
                    // Safety: We are running on the thread that initialized COM (UI thread).
                    unsafe { COMLibrary::assume_initialized() }
                } else {
                    return Err(Box::new(e));
                }
            }
        };
        
        let wmi_con = WMIConnection::new(com_con)?;
        
        // Create second connection to root\WMI
        // note: COMLibrary is shared, we can clone it or assume initialized?
        // wmi crate takes COMLibrary by value.
        // We need to use `WMIConnection::with_namespace_path("root\\WMI", com_con)`... but com_con is moved.
        // We can create a new COMLibrary (unsafe assume init) for the second one.
        
        let com_con_2 = unsafe { COMLibrary::assume_initialized() };
        let wmi_con_wmi = WMIConnection::with_namespace_path("root\\WMI", com_con_2)?;

        Ok(Self { con: wmi_con, con_wmi: wmi_con_wmi })
    }

    pub fn set_thermal_mode(&self, mode: u32) -> Result<(), Box<dyn Error>> {
        // Class: LENOVO_GAMEZONE_DATA
        // Method: SetSmartFanMode
        // Arg: Data (int32)
        use std::collections::HashMap;
        use serde::Serialize;
        
        // We can use raw_query equivalent for methods or generic execute.
        // wmi crate support for methods is via `exec_method`.
        
        // Currently wmi crate doesn't easily support executing methods with arguments via raw SQL-like string.
        // But LLT does: CallAsync(..., "SetSmartFanMode", new() { { "Data", data } })
        
        // The rust `wmi` crate requires defining keys in a map.
        let mut params = HashMap::new();
        params.insert("Data".to_string(), wmi::Variant::I4(mode as i32));
        
        // Path: root\WMI:LENOVO_GAMEZONE_DATA.InstanceName='ACPI\PNP0C14\0_0' (Example)
        // Usually methods are static or on the instance. LLT uses `SELECT * FROM LENOVO_GAMEZONE_DATA`, finds the instance, then executes.
        
        // Because identifying the instance and executing method in Rust wmi crate can be tricky without the instance path,
        // and because I want this to WORK immediately for the user (who has PowerShell available),
        // I will fallback to PowerShell for this specific complex WMI Method call.
        // It's safer than guessing the InstanceName (which might differ on LOQ).
        
        self.set_lenovo_gamezone_fan_mode(mode)
    }

    fn set_lenovo_gamezone_fan_mode(&self, mode: u32) -> Result<(), Box<dyn Error>> {
        use std::process::Command;
        
        // (Get-WmiObject -Namespace root/WMI -Class LENOVO_GAMEZONE_DATA).SetSmartFanMode(1)
        let ps_script = format!(
            "(Get-WmiObject -Namespace root\\WMI -Class LENOVO_GAMEZONE_DATA).SetSmartFanMode({})",
            mode
        );
        
        let output = Command::new("powershell")
            .args(&["-NoProfile", "-Command", &ps_script])
            .output()?;
            
        if !output.status.success() {
            let stderr = String::from_utf8_lossy(&output.stderr);
            return Err(format!("Thermal Mode Write failed: {}", stderr).into());
        }
        Ok(())
    }

    pub fn get_thermal_mode(&self) -> Result<u32, Box<dyn Error>> {
        use std::process::Command;
        
        // (Get-WmiObject -Namespace root/WMI -Class LENOVO_GAMEZONE_DATA).GetSmartFanMode().Data
        let ps_script = "(Get-WmiObject -Namespace root\\WMI -Class LENOVO_GAMEZONE_DATA).GetSmartFanMode().Data";
        
        let output = Command::new("powershell")
            .args(&["-NoProfile", "-Command", ps_script])
            .output()?;
            
        if !output.status.success() {
            return Err("Failed to read thermal mode".into());
        }
        
        let stdout = String::from_utf8_lossy(&output.stdout);
        let trimmed = stdout.trim();
        
        if let Ok(val) = trimmed.parse::<u32>() {
            Ok(val)
        } else {
            Err(format!("Invalid integer from WMI: {}", trimmed).into())
        }
    }
    
    pub fn get_manufacturer(&self) -> Result<String, Box<dyn Error>> {
        // Use Win32_ComputerSystemProduct (matching LenovoLegionToolkit)
        let results: Vec<Win32ComputerSystemProduct> = self.con.raw_query("SELECT Vendor, Name, Version FROM Win32_ComputerSystemProduct")?;
        if let Some(sys) = results.first() {
            Ok(sys.vendor.clone())
        } else {
            Err("Could not retrieve Vendor".into())
        }
    }

    pub fn get_model(&self) -> Result<String, Box<dyn Error>> {
        let results: Vec<Win32ComputerSystemProduct> = self.con.raw_query("SELECT Vendor, Name, Version FROM Win32_ComputerSystemProduct")?;
        if let Some(sys) = results.first() {
            Ok(sys.name.clone())
        } else {
            Err("Could not retrieve Name".into())
        }
    }

    pub fn get_bios_version(&self) -> Result<String, Box<dyn Error>> {
        let results: Vec<Win32Bios> = self.con.raw_query("SELECT SMBIOSBIOSVersion FROM Win32_BIOS")?;
        if let Some(bios) = results.first() {
            Ok(bios.smbios_bios_version.clone())
        } else {
            Err("Could not retrieve BIOS Version".into())
        }
    }

    pub fn get_battery_info(&self) -> Result<(u16, u16), Box<dyn Error>> {
        let results: Vec<Win32Battery> = self.con.raw_query("SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery")?;
        if let Some(bat) = results.first() {
            Ok((bat.estimated_charge_remaining, bat.battery_status))
        } else {
            Err("No battery found".into())
        }
    }
}


pub struct EnergyDriver {
    handle: windows::Win32::Foundation::HANDLE,
}

impl EnergyDriver {
    pub fn new() -> Result<Self, Box<dyn Error>> {
        use windows::Win32::Storage::FileSystem::{CreateFileA, FILE_SHARE_READ, FILE_SHARE_WRITE, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL};
        use windows::Win32::Foundation::{GENERIC_READ, GENERIC_WRITE, INVALID_HANDLE_VALUE};
        
        let path = std::ffi::CString::new(r"\\.\EnergyDrv")?;
        
        let handle = unsafe {
            CreateFileA(
                windows::core::PCSTR(path.as_ptr() as *const _),
                GENERIC_READ.0 | GENERIC_WRITE.0,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                None,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                None
            )
        }?;

        if handle == INVALID_HANDLE_VALUE {
             return Err("Failed to open EnergyDrv".into());
        }

        Ok(Self { handle })
    }

    pub fn set_conservation_mode(&self, enable: bool) -> Result<(), Box<dyn Error>> {
        // IOCTL_ENERGY_BATTERY_CHARGE_MODE = 0x831020F8
        // 3 = Conservation, 5 = Disable Conservation (Normal?), 8 = Normal?
        // Logic from LLT: 
        // To Conservation: [0x3]
        // To Normal (from Conservation): [0x5]
        // We will assume [0x3] enables conservation, [0x5] disables it (returns to Normal/Rapid).
        
        let code: u32 = if enable { 0x3 } else { 0x5 };
        self.send_command(0x831020F8, code)
    }

    pub fn set_rapid_charge(&self, enable: bool) -> Result<(), Box<dyn Error>> {
        // IOCTL_ENERGY_BATTERY_CHARGE_MODE = 0x831020F8
        // 7 = Rapid, 8 = Disable Rapid (Normal?)
        // Logic from LLT:
        // To Rapid: [0x7]
        // To Normal (from Rapid): [0x8]
        
        let code: u32 = if enable { 0x7 } else { 0x8 };
        self.send_command(0x831020F8, code)
    }
    
    // Helper for DeviceIoControl
    fn send_command(&self, control_code: u32, input_val: u32) -> Result<(), Box<dyn Error>> {
        use windows::Win32::System::IO::DeviceIoControl;
        use std::ffi::c_void;
        
        let mut in_buffer = input_val;
        let mut out_buffer: u32 = 0;
        let mut bytes_returned: u32 = 0;
        
        unsafe {
            DeviceIoControl(
                self.handle,
                control_code,
                Some(&mut in_buffer as *mut _ as *mut c_void),
                std::mem::size_of::<u32>() as u32,
                Some(&mut out_buffer as *mut _ as *mut c_void),
                std::mem::size_of::<u32>() as u32,
                Some(&mut bytes_returned),
                None
            )
        }?;

        Ok(())
    }
}

impl Drop for EnergyDriver {
    fn drop(&mut self) {
        use windows::Win32::Foundation::CloseHandle;
        unsafe { let _ = CloseHandle(self.handle); };
    }
}
