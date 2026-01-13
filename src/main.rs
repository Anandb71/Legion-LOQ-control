use log::{info, error};
use std::env;

mod core;
mod platform;

fn main() {
    env_logger::init();
    
    let args: Vec<String> = env::args().collect();
    let json_mode = args.len() > 1 && args[1] == "--json";

    if !json_mode {
        info!("Starting Legion + LOQ Control...");
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
            println!("Detected device:");
            println!("  Manufacturer: {}", device.manufacturer);
            println!("  Model:        {}", device.model_name);
            println!("  BIOS:         {}", device.bios_version);
            
            if !device.supported {
                println!("\nStatus: Unsupported device");
                println!("Reason: Model not recognized as Legion or LOQ.");
                println!("No hardware control enabled.");
                if device.manufacturer.to_uppercase().contains("LENOVO") {
                     println!("Series detected: {:?}", device.series);
                }
            } else {
                println!("\nStatus: Supported (read-only mode)");
                println!("Series: {:?}", device.series);
                
                // Hardware Monitoring
                println!("\n--- Hardware Status ---");
                
                match core::hw::battery::get_battery_status() {
                    Some(bat) => println!("Battery:  {}% (Charging: {})", bat.charge_percent, bat.is_charging),
                    None => println!("Battery:  Not detected"),
                }
                
                match core::hw::thermal::get_cpu_temp() {
                    Some(t) => println!("CPU Temp: {:.1}°C", t),
                    None => println!("CPU Temp: N/A"),
                }

                 match core::hw::thermal::get_gpu_temp() {
                    Some(t) => println!("GPU Temp: {:.1}°C", t),
                    None => println!("GPU Temp: N/A"),
                }
            }
        },
        Err(e) => {
            if json_mode {
                println!("{{ \"error\": \"{}\" }}", e);
            } else {
                error!("Failed to detect device: {}", e);
                println!("Error: Could not detect device information. Ensure you are running as Administrator.");
            }
        }
    }
}
