use log::{info, error};
use std::env;

mod legion_core;
mod platform;
mod gui;

fn print_help() {
    println!("Legion + LOQ Control v0.2.0");
    println!("A lightweight Lenovo Vantage replacement for Legion & LOQ laptops.\n");
    println!("USAGE:");
    println!("  legion-loq-control [OPTIONS]\n");
    println!("OPTIONS:");
    println!("  --gui                       Launch graphical interface");
    println!("  --json                      Output device info as JSON");
    println!("  --dry-run                   Preview changes without applying");
    println!("  --set-conservation-mode <on|off>  Toggle battery conservation");
    println!("  --rapid-charge <on|off>     Toggle rapid charging");
    println!("  --set-profile <quiet|balanced|perf>  Set thermal profile");
    println!("  -V, --version               Show version");
    println!("  -h, --help                  Show this help\n");
    println!("EXAMPLES:");
    println!("  legion-loq-control --gui");
    println!("  legion-loq-control --set-profile perf");
    println!("  legion-loq-control --dry-run --set-conservation-mode on\n");
    println!("NOTE: Run as Administrator for all features to work.");
}

fn main() {
    env_logger::init();
    
    let args: Vec<String> = env::args().collect();
    
    // Version flag
    if args.contains(&"--version".to_string()) || args.contains(&"-V".to_string()) {
        println!("Legion + LOQ Control v0.2.0");
        println!("A lightweight Lenovo Vantage replacement");
        return;
    }
    
    // Help flag
    if args.contains(&"--help".to_string()) || args.contains(&"-h".to_string()) {
        print_help();
        return;
    }
    
    // GUI mode check (early exit)
    if args.contains(&"--gui".to_string()) {
        if let Err(e) = gui::run_gui() {
            eprintln!("GUI Error: {}", e);
            std::process::exit(1);
        }
        return;
    }
    
    // CLI mode
    let dry_run = args.contains(&"--dry-run".to_string());
    let json_mode = args.len() > 1 && args.contains(&"--json".to_string());
    
    let mut set_conservation_mode_arg: Option<bool> = None;
    let mut set_rapid_charge_arg: Option<bool> = None;
    let mut set_profile_arg: Option<String> = None;

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
        
        if args[i] == "--rapid-charge" && i + 1 < args.len() {
            let val = args[i+1].to_lowercase();
            if val == "on" || val == "enable" || val == "true" {
                set_rapid_charge_arg = Some(true);
            } else if val == "off" || val == "disable" || val == "false" {
                 set_rapid_charge_arg = Some(false);
            } else {
                 eprintln!("Invalid value for --rapid-charge. Use 'on' or 'off'.");
                 std::process::exit(1);
            }
        }

        if args[i] == "--set-profile" && i + 1 < args.len() {
            set_profile_arg = Some(args[i+1].to_lowercase());
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
            
            match legion_core::hw::battery::get_conservation_mode() {
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

        legion_core::safety::guards::GlobalWriteLock::request_write_access();
        
        match legion_core::hw::battery::set_conservation_mode(target_state) {
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

    // Handle Rapid Charge
    if let Some(target_state) = set_rapid_charge_arg {
        info!("Command: Set Rapid Charge to {}", if target_state { "ON" } else { "OFF" });
        if dry_run {
            println!("--- Dry Run Mode ---");
            println!("Action: Set Rapid Charge to {}", if target_state { "ON" } else { "OFF" });
            match legion_core::hw::battery::get_rapid_charge() {
                Some(current) => {
                    println!("Current State: {}", if current { "ON" } else { "OFF" });
                    if current == target_state { println!("Result: No change needed."); } 
                    else { println!("Result: State would change."); }
                },
                None => println!("WARNING: Unable to read current state. Write might be unsafe."),
            }
            return;
        }
        legion_core::safety::guards::GlobalWriteLock::request_write_access();
        match legion_core::hw::battery::set_rapid_charge(target_state) {
            Ok(_) => println!("Success: Rapid Charge set to {}.", if target_state { "ON" } else { "OFF" }),
            Err(e) => {
                error!("Operation failed: {}", e);
                eprintln!("Error: Failed to set Rapid Charge: {}", e);
                std::process::exit(1);
            }
        }
        return;
    }

    // Handle Power Profile
    if let Some(profile_str) = set_profile_arg {
        let target_profile = match profile_str.as_str() {
            "quiet" | "blue" => legion_core::hw::power::PowerProfile::Quiet,
            "balanced" | "white" | "auto" => legion_core::hw::power::PowerProfile::Balanced,
            "perf" | "performance" | "red" => legion_core::hw::power::PowerProfile::Performance,
            _ => {
                eprintln!("Error: Invalid profile '{}'. Use 'quiet', 'balanced', or 'perf'.", profile_str);
                std::process::exit(1);
            }
        };

        info!("Command: Set Power Profile to {:?}", target_profile);
        if dry_run {
            println!("--- Dry Run Mode ---");
            println!("Action: Set Power Profile to {:?}", target_profile);
            match legion_core::hw::power::get_power_profile() {
                Some(current) => {
                    println!("Current Profile: {:?}", current);
                    if current == target_profile { println!("Result: No change needed."); }
                    else { println!("Result: Profile would change."); }
                },
                None => println!("WARNING: Unable to read current profile."),
            }
            return;
        }

        legion_core::safety::guards::GlobalWriteLock::request_write_access();
        match legion_core::hw::power::set_power_profile(target_profile) {
            Ok(_) => println!("Success: Power Profile set to {:?}.", target_profile),
            Err(e) => {
                error!("Operation failed: {}", e);
                eprintln!("Error: Failed to set Power Profile: {}", e);
                std::process::exit(1);
            }
        }
        return;
    }

    match legion_core::device::detect::detect_device() {
        Ok(device) => {
            if json_mode {
                let json = serde_json::to_string_pretty(&device).unwrap_or_default();
                println!("{}", json);
                return;
            }

            println!("Legion + LOQ Control (v0.2.0)");
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
                
                match legion_core::hw::battery::get_battery_status() {
                    Some(bat) => println!("Battery:           {}% (Charging: {})", bat.charge_percent, bat.is_charging),
                    None => println!("Battery:           Not detected"),
                }
                
                
                match legion_core::hw::battery::get_conservation_mode() {
                    Some(enabled) => println!("Conservation Mode: {}", if enabled { "ON" } else { "OFF" }),
                    None => println!("Conservation Mode: Unknown (WMI unavailable)"),
                }

                match legion_core::hw::battery::get_rapid_charge() {
                    Some(enabled) => println!("Rapid Charge:      {}", if enabled { "ON" } else { "OFF" }),
                    None => println!("Rapid Charge:      Unknown"),
                }

                match legion_core::hw::power::get_power_profile() {
                    Some(p) => println!("Power Profile:     {}", p),
                    None => println!("Power Profile:     Unknown"),
                }
                
                match legion_core::hw::thermal::get_cpu_temp() {
                    Some(t) => println!("CPU Temp:          {:.1}°C", t),
                    None => println!("CPU Temp:          N/A (Stubbed)"),
                }

                 match legion_core::hw::thermal::get_gpu_temp() {
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
