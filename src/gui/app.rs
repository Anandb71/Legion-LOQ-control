use eframe::egui;
use crate::legion_core;

pub struct LegionControlApp {
    device_name: String,
    bios_version: String,
    supported: bool,
    battery_percent: Option<u16>,
    battery_charging: Option<bool>,
    conservation_mode: Option<bool>,
    rapid_charge: Option<bool>,
    power_profile: Option<legion_core::hw::power::PowerProfile>,
    status_message: String,
    last_error: Option<String>,
}

impl Default for LegionControlApp {
    fn default() -> Self {
        Self {
            device_name: "Detecting...".to_string(),
            bios_version: "".to_string(),
            supported: false,
            battery_percent: None,
            battery_charging: None,
            conservation_mode: None,
            rapid_charge: None,
            power_profile: None,
            status_message: "Initializing...".to_string(),
            last_error: None,
        }
    }
}

impl LegionControlApp {
    fn refresh_state(&mut self) {
        self.last_error = None;
        
        // Device Detection
        match legion_core::device::detect::detect_device() {
            Ok(device) => {
                self.device_name = device.model_name.clone();
                self.bios_version = device.bios_version.clone();
                self.supported = device.supported;
                
                if device.supported {
                    self.status_message = "Ready".to_string();
                } else {
                    self.status_message = "Unsupported Device (Read-Only)".to_string();
                }
            },
            Err(e) => {
                self.device_name = "Detection Failed".to_string();
                self.last_error = Some(format!("Device detection: {}", e));
                self.status_message = "Error".to_string();
                return;
            }
        }
        
        // Battery
        if let Some(bat) = legion_core::hw::battery::get_battery_status() {
            self.battery_percent = Some(bat.charge_percent);
            self.battery_charging = Some(bat.is_charging);
        }
        
        // Conservation Mode
        self.conservation_mode = legion_core::hw::battery::get_conservation_mode();
        
        // Rapid Charge
        self.rapid_charge = legion_core::hw::battery::get_rapid_charge();
        
        // Power Profile
        self.power_profile = legion_core::hw::power::get_power_profile();
    }

    fn set_conservation_mode(&mut self, enable: bool) {
        legion_core::safety::guards::GlobalWriteLock::request_write_access();
        match legion_core::hw::battery::set_conservation_mode(enable) {
            Ok(_) => {
                self.conservation_mode = Some(enable);
                self.status_message = format!("Conservation Mode: {}", if enable { "ON" } else { "OFF" });
            },
            Err(e) => {
                self.last_error = Some(format!("Failed: {}", e));
            }
        }
    }

    fn set_rapid_charge(&mut self, enable: bool) {
        legion_core::safety::guards::GlobalWriteLock::request_write_access();
        match legion_core::hw::battery::set_rapid_charge(enable) {
            Ok(_) => {
                self.rapid_charge = Some(enable);
                self.status_message = format!("Rapid Charge: {}", if enable { "ON" } else { "OFF" });
            },
            Err(e) => {
                self.last_error = Some(format!("Failed: {}", e));
            }
        }
    }

    fn set_power_profile(&mut self, profile: legion_core::hw::power::PowerProfile) {
        legion_core::safety::guards::GlobalWriteLock::request_write_access();
        match legion_core::hw::power::set_power_profile(profile) {
            Ok(_) => {
                self.power_profile = Some(profile);
                self.status_message = format!("Profile: {:?}", profile);
            },
            Err(e) => {
                self.last_error = Some(format!("Failed: {}", e));
            }
        }
    }
}

impl eframe::App for LegionControlApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        // Auto-refresh on first frame
        if self.device_name == "Detecting..." {
            self.refresh_state();
        }

        egui::CentralPanel::default().show(ctx, |ui| {
            ui.heading("Legion + LOQ Control");
            ui.separator();

            // Device Info
            ui.horizontal(|ui| {
                ui.label("Device:");
                ui.strong(&self.device_name);
            });
            ui.horizontal(|ui| {
                ui.label("BIOS:");
                ui.label(&self.bios_version);
            });
            ui.horizontal(|ui| {
                ui.label("Status:");
                if self.supported {
                    ui.colored_label(egui::Color32::GREEN, "Supported");
                } else {
                    ui.colored_label(egui::Color32::YELLOW, "Unsupported");
                }
            });

            ui.separator();
            ui.heading("Hardware");

            // Battery
            if let Some(pct) = self.battery_percent {
                ui.horizontal(|ui| {
                    ui.label("Battery:");
                    ui.label(format!("{}%", pct));
                    if let Some(charging) = self.battery_charging {
                        if charging {
                            ui.label("⚡ Charging");
                        }
                    }
                });
            }

            // Conservation Mode Toggle
            ui.horizontal(|ui| {
                ui.label("Conservation Mode:");
                if let Some(current) = self.conservation_mode {
                    if ui.selectable_label(current, "ON").clicked() && !current {
                        self.set_conservation_mode(true);
                    }
                    if ui.selectable_label(!current, "OFF").clicked() && current {
                        self.set_conservation_mode(false);
                    }
                } else {
                    ui.label("N/A");
                }
            });

            // Rapid Charge Toggle
            ui.horizontal(|ui| {
                ui.label("Rapid Charge:");
                if let Some(current) = self.rapid_charge {
                    if ui.selectable_label(current, "ON").clicked() && !current {
                        self.set_rapid_charge(true);
                    }
                    if ui.selectable_label(!current, "OFF").clicked() && current {
                        self.set_rapid_charge(false);
                    }
                } else {
                    ui.label("N/A");
                }
            });

            // Power Profile
            ui.horizontal(|ui| {
                ui.label("Power Profile:");
                let profiles = [
                    ("Quiet", legion_core::hw::power::PowerProfile::Quiet),
                    ("Balanced", legion_core::hw::power::PowerProfile::Balanced),
                    ("Performance", legion_core::hw::power::PowerProfile::Performance),
                ];
                for (name, profile) in profiles {
                    let is_current = self.power_profile == Some(profile);
                    if ui.selectable_label(is_current, name).clicked() && !is_current {
                        self.set_power_profile(profile);
                    }
                }
            });

            ui.separator();

            // Status / Error
            if let Some(ref err) = self.last_error {
                ui.colored_label(egui::Color32::RED, format!("⚠ {}", err));
            } else {
                ui.label(&self.status_message);
            }

            ui.separator();

            // Refresh Button
            if ui.button("🔄 Refresh").clicked() {
                self.refresh_state();
            }
        });
    }
}
