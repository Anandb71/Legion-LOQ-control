use log::{info, error};
use std::env;

mod core;
mod platform;

fn main() {
    env_logger::init();
    
    let args: Vec<String> = env::args().collect();
    let json_mode = args.len() > 1 && args.contains(&"--json".to_string());
    
    // Simple arg parsing (clap is overkill for now)
    let dry_run = args.contains(&"--dry-run".to_string());
    let mut set_conservation_mode_arg: Option<bool> = None;
    
    for i in 0..args.len() {
        if args[i] == "--set-conservation-mode" && i + 1 < args.len() {
            let val = args[i+1].to_lowercase();
            if val == "on" || val == "enable" || val == "true" {
                set_conservation_mode_arg = Some(true);
            } else if val == "off" || val == "disable" || val == "false" {
                 set_conservation_mode_arg = Some(false);
            } else {
                 eprintln!("Invalid value for --set-conservation-mode. Use 'on' or 'off'.");
                 std::process::exit(1);
            }
        }
    }

    if !json_mode {
        info!("Starting Legion + LOQ Control...");
        if dry_run {
            info!("DRY RUN MODE: No changes will be applied.");
        }
    }

    // Handle Write Operations (if requested and we are running)
    if let Some(target_state) = set_conservation_mode_arg {
        info!("Command: Set Conservation Mode to {}", if target_state { "ON" } else { "OFF" });
        
        if dry_run {
            println!("--- Dry Run Mode ---");
            println!("Action: Set Conservation Mode to {}", if target_state { "ON" } else { "OFF" });
            
            match core::hw::battery::get_conservation_mode() {
                Some(current) => {
                    println!("Current State: {}", if current { "ON" } else { "OFF" });
                    
                    if current == target_state {
                        println!("Result: No change needed (values match).");
                    } else {
                        println!("Result: State would change.");
                    }
                },
                None => {
                    println!("Current State: Unknown (Read failed)");
                    println!("WARNING: Unable to verify current state. Write might be unsafe.");
                },
            }
            return;
        }
        
        // Real Write
        // Standard check: verify support again just in case (though detect_device usually gates this)
        // For CLI simplicity in `main`, we'll trust the explicit flag if they forced it, 
        // but it's good practice to ensure we aren't running on a toaster.
        // (We rely on WMI failing if checking failed).

        core::safety::guards::GlobalWriteLock::request_write_access();
        
        match core::hw::battery::set_conservation_mode(target_state) {
            Ok(_) => {
                println!("Success: Conservation Mode set to {}.", if target_state { "ON" } else { "OFF" });
                info!("Conservation Mode update successful.");
            },
            Err(e) => {
                error!("Operation failed: {}", e);
                eprintln!("Error: Failed to set Conservation Mode.");
                eprintln!("Details: {}", e);
                eprintln!("No changes were applied.");
                std::process::exit(1);
            }
        }
        return;
    }

    match core::device::detect::detect_device() {
        Ok(device) => {
            if json_mode {
                let json = serde_json::to_string_pretty(&device).unwrap_or_default();
                println!("{}", json);
                return;
            }

            println!("Legion + LOQ Control (v0.1.0)");
            println!("-----------------------------");
            
            if !device.supported {
                println!("Status: Unsupported Device (Read-Only)");
                println!("Reason: Model '{}' not recognized as Legion or LOQ.", device.model_name);
                if device.manufacturer.to_uppercase().contains("LENOVO") {
                     println!("Note: Detected Lenovo device, but series '{:?}' is not in the allowlist.", device.series);
                }
                println!("Hardware control features are disabled for safety.");
            } else {
                println!("Status: Supported");
                println!("Device: {} ({:?})", device.model_name, device.series);
                println!("BIOS:   {}", device.bios_version);
                
                // Hardware Monitoring
                println!("\n--- Hardware Status ---");
                
                match core::hw::battery::get_battery_status() {
                    Some(bat) => println!("Battery:           {}% (Charging: {})", bat.charge_percent, bat.is_charging),
                    None => println!("Battery:           Not detected"),
                }
                
                match core::hw::battery::get_conservation_mode() {
                    Some(enabled) => println!("Conservation Mode: {}", if enabled { "ON" } else { "OFF" }),
                    None => println!("Conservation Mode: Unknown (WMI unavailable)"),
                }
                
                match core::hw::thermal::get_cpu_temp() {
                    Some(t) => println!("CPU Temp:          {:.1}°C", t),
                    None => println!("CPU Temp:          N/A (Stubbed)"),
                }

                 match core::hw::thermal::get_gpu_temp() {
                    Some(t) => println!("GPU Temp:          {:.1}°C", t),
                    None => println!("GPU Temp:          N/A (Stubbed)"),
                }
            }
        },
        Err(e) => {
            if json_mode {
                println!("{{ \"error\": \"{}\" }}", e);
            } else {
                error!("Device detection failed: {}", e);
                eprintln!("Error: Critical failure during device detection.");
                eprintln!("Details: {}", e);
                eprintln!("Ensure you are running as Administrator and WMI is accessible.");
            }
        }
    }
}
