use eframe::egui;
use std::sync::mpsc::{channel, Receiver, Sender};
use std::thread;
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
    show_sensitive: bool,  // Privacy: hide device ID/BIOS by default
    
    // Threading
    rx: Receiver<GuiUpdate>,
    tx_action: Sender<GuiAction>,
    is_busy: bool,
}

#[derive(Debug)]
enum GuiUpdate {
    StateRefreshed(Box<DeviceState>),
    Error(String),
    ActionComplete(String),
}

#[derive(Debug, Clone)]
struct DeviceState {
    device_name: String,
    bios_version: String,
    supported: bool,
    battery_percent: Option<u16>,
    battery_charging: Option<bool>,
    conservation_mode: Option<bool>,
    rapid_charge: Option<bool>,
    power_profile: Option<legion_core::hw::power::PowerProfile>,
}

#[derive(Debug)]
enum GuiAction {
    Refresh,
    SetConservation(bool),
    SetRapidCharge(bool),
    SetProfile(legion_core::hw::power::PowerProfile),
    SetLightingOwner(bool),
    SetBrightness(u8),
    SetStaticColor(u8, u8, u8),
}

impl Default for LegionControlApp {
    fn default() -> Self {
        let (tx, rx) = channel();
        let (tx_action, rx_action) = channel();
        
        let tx_scan = tx.clone();
        
        // Spawn Background Worker Thread (MTA)
        thread::spawn(move || {
            loop {
                match rx_action.recv() {
                    Ok(action) => {
                        match action {
                            GuiAction::Refresh => {
                                let state = perform_refresh();
                                let _ = tx_scan.send(GuiUpdate::StateRefreshed(Box::new(state)));
                            },
                            GuiAction::SetConservation(enable) => {
                                // Write with global lock
                                legion_core::safety::guards::GlobalWriteLock::request_write_access();
                                match legion_core::hw::battery::set_conservation_mode(enable) {
                                    Ok(_) => { let _ = tx_scan.send(GuiUpdate::ActionComplete(format!("Conservation Mode: {}", if enable { "ON" } else { "OFF" }))); },
                                    Err(e) => { let _ = tx_scan.send(GuiUpdate::Error(format!("Failed to set Conservation Mode: {}", e))); }
                                }
                                // Auto-refresh after write
                                let state = perform_refresh();
                                let _ = tx_scan.send(GuiUpdate::StateRefreshed(Box::new(state)));
                            },
                            GuiAction::SetRapidCharge(enable) => {
                                legion_core::safety::guards::GlobalWriteLock::request_write_access();
                                match legion_core::hw::battery::set_rapid_charge(enable) {
                                    Ok(_) => { let _ = tx_scan.send(GuiUpdate::ActionComplete(format!("Rapid Charge: {}", if enable { "ON" } else { "OFF" }))); },
                                    Err(e) => { let _ = tx_scan.send(GuiUpdate::Error(format!("Failed to set Rapid Charge: {}", e))); }
                                }
                                let state = perform_refresh();
                                let _ = tx_scan.send(GuiUpdate::StateRefreshed(Box::new(state)));
                            },
                            GuiAction::SetProfile(p) => {
                                legion_core::safety::guards::GlobalWriteLock::request_write_access();
                                match legion_core::hw::power::set_power_profile(p) {
                                    Ok(_) => { let _ = tx_scan.send(GuiUpdate::ActionComplete(format!("Profile set to {:?}", p))); },
                                    Err(e) => { let _ = tx_scan.send(GuiUpdate::Error(format!("Failed to set Profile: {}", e))); }
                                }
                                let state = perform_refresh();
                                let _ = tx_scan.send(GuiUpdate::StateRefreshed(Box::new(state)));
                            },
                             GuiAction::SetLightingOwner(enable) => {
                                // WMI Call
                                let wmi = match crate::platform::windows::WmiQueryHandler::new() {
                                    Ok(w) => w,
                                    Err(e) => { let _ = tx_scan.send(GuiUpdate::Error(format!("Failed to init WMI: {}", e))); return; }
                                };
                                match wmi.set_light_control_owner(enable) {
                                    Ok(_) => { let _ = tx_scan.send(GuiUpdate::ActionComplete(format!("Lighting Control: {}", if enable { "APP" } else { "FIRMWARE" }))); },
                                    Err(e) => { let _ = tx_scan.send(GuiUpdate::Error(format!("Failed to set ownership: {}", e))); }
                                }
                            },
                            GuiAction::SetBrightness(level) => {
                                // HID Call
                                legion_core::safety::guards::GlobalWriteLock::request_write_access();
                                let lc = legion_core::hw::lighting::LightingController::new();
                                match lc.set_brightness(level) {
                                    Ok(_) => { let _ = tx_scan.send(GuiUpdate::ActionComplete(format!("Brightness set to {}", level))); },
                                    Err(e) => { let _ = tx_scan.send(GuiUpdate::Error(format!("Lighting Error: {}", e))); }
                                }
                            },
                            GuiAction::SetStaticColor(r, g, b) => {
                                // HID Call
                                legion_core::safety::guards::GlobalWriteLock::request_write_access();
                                let lc = legion_core::hw::lighting::LightingController::new();
                                match lc.set_static_color(r, g, b) {
                                    Ok(_) => { let _ = tx_scan.send(GuiUpdate::ActionComplete("Static Color Applied".to_string())); },
                                    Err(e) => { let _ = tx_scan.send(GuiUpdate::Error(format!("Lighting Error: {}", e))); }
                                }
                            }
                        }
                    },
                    Err(_) => break, // Channel closed
                }
            }
        });
        
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
            rx,
            tx_action,
            is_busy: false,
            show_sensitive: false,  // Hidden by default for privacy
        }
    }
}

fn perform_refresh() -> DeviceState {
    let mut state = DeviceState {
        device_name: "Unknown".to_string(),
        bios_version: "".to_string(),
        supported: false,
        battery_percent: None,
        battery_charging: None,
        conservation_mode: None,
        rapid_charge: None,
        power_profile: None,
    };
    
    // Device Detection (WMI)
    match legion_core::device::detect::detect_device() {
        Ok(device) => {
            state.device_name = device.model_name;
            state.bios_version = device.bios_version;
            state.supported = device.supported;
        },
        Err(e) => {
            // SHOW THE ERROR in the UI
            state.device_name = format!("Error: {}", e); 
            // Also log to console if possible, though user can't see it easily
        }
    }
    
    if let Some(bat) = legion_core::hw::battery::get_battery_status() {
        state.battery_percent = Some(bat.charge_percent);
        state.battery_charging = Some(bat.is_charging);
    }
    
    state.conservation_mode = legion_core::hw::battery::get_conservation_mode();
    state.rapid_charge = legion_core::hw::battery::get_rapid_charge();
    state.power_profile = legion_core::hw::power::get_power_profile();
    
    state
}

impl LegionControlApp {
    fn request_refresh(&mut self) {
        if !self.is_busy {
            self.status_message = "Refreshing...".to_string();
            self.is_busy = true;
            let _ = self.tx_action.send(GuiAction::Refresh);
        }
    }
}

impl eframe::App for LegionControlApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        // Poll for updates
        while let Ok(update) = self.rx.try_recv() {
            self.is_busy = false;
            match update {
                GuiUpdate::StateRefreshed(state) => {
                    self.device_name = state.device_name;
                    self.bios_version = state.bios_version;
                    self.supported = state.supported;
                    self.battery_percent = state.battery_percent;
                    self.battery_charging = state.battery_charging;
                    self.conservation_mode = state.conservation_mode;
                    self.rapid_charge = state.rapid_charge;
                    self.power_profile = state.power_profile;
                    
                    if self.supported {
                        self.status_message = "Ready".to_string();
                    } else if self.device_name == "Detection Failed" {
                        self.status_message = "Error: Device Detection Failed".to_string();
                        // Keep last error specific if we had one
                    } else {
                        self.status_message = "Unsupported Device (Read-Only)".to_string();
                    }
                },
                GuiUpdate::Error(e) => {
                    self.last_error = Some(e);
                    self.status_message = "Error".to_string();
                },
                GuiUpdate::ActionComplete(msg) => {
                    self.status_message = msg;
                }
            }
        }
    
        // Auto-refresh on start
        if self.device_name == "Detecting..." && !self.is_busy {
            self.request_refresh();
        }

        // Top Panel: Header & Theme Toggle
        egui::TopBottomPanel::top("top_panel").show(ctx, |ui| {
            ui.horizontal(|ui| {
                ui.heading("Legion Control");
                if self.is_busy {
                    ui.spinner();
                }
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    if ui.button(if ctx.style().visuals.dark_mode { "☀ Light" } else { "🌙 Dark" }).clicked() {
                        if ctx.style().visuals.dark_mode {
                            ctx.set_visuals(egui::Visuals::light());
                        } else {
                            ctx.set_visuals(egui::Visuals::dark());
                        }
                    }
                });
            });
        });

        egui::CentralPanel::default().show(ctx, |ui| {
            egui::ScrollArea::vertical().show(ui, |ui| {
            // Section: Device Info
            ui.group(|ui| {
                ui.set_width(ui.available_width());
                ui.horizontal(|ui| {
                    ui.heading("Device Information");
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        if ui.button(if self.show_sensitive { "🔓 Hide" } else { "🔒 Show" }).clicked() {
                            self.show_sensitive = !self.show_sensitive;
                        }
                    });
                });
                ui.add_space(5.0);
                
                egui::Grid::new("device_info_grid").striped(true).show(ui, |ui| {
                    ui.label("Model:");
                    if self.show_sensitive {
                        ui.strong(&self.device_name);
                    } else {
                        // Discord-style gray spoiler
                        let (rect, _) = ui.allocate_exact_size(egui::vec2(80.0, 18.0), egui::Sense::hover());
                        ui.painter().rect_filled(rect, 4.0, egui::Color32::from_rgb(70, 70, 75));
                    }
                    ui.end_row();
                    
                    ui.label("BIOS:");
                    if self.show_sensitive {
                        ui.label(&self.bios_version);
                    } else {
                        // Discord-style gray spoiler
                        let (rect, _) = ui.allocate_exact_size(egui::vec2(80.0, 18.0), egui::Sense::hover());
                        ui.painter().rect_filled(rect, 4.0, egui::Color32::from_rgb(70, 70, 75));
                    }
                    ui.end_row();
                    
                    ui.label("Status:");
                    if self.supported {
                        ui.colored_label(egui::Color32::GREEN, "Supported");
                    } else {
                        ui.colored_label(egui::Color32::from_rgb(255, 140, 0), "Unsupported");
                    }
                    ui.end_row();
                    
                    ui.label("Battery:");
                    if let Some(pct) = self.battery_percent {
                         let charging_text = if self.battery_charging == Some(true) { "⚡ " } else { "" };
                         ui.label(format!("{}{}%", charging_text, pct));
                    } else {
                        ui.label("N/A");
                    }
                    ui.end_row();
                });
            });

            ui.add_space(10.0);

            // Section: Power Controls
            ui.group(|ui| {
                ui.set_width(ui.available_width());
                ui.heading("Power Controls");
                ui.add_space(5.0);

                // Interactions disabled if busy
                ui.set_enabled(!self.is_busy && self.supported);

                // Conservation Mode
                ui.horizontal(|ui| {
                    ui.label("Conservation Mode:");
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                         let current = self.conservation_mode.unwrap_or(false);
                         let mut val = current;
                         if ui.checkbox(&mut val, if current { "ON" } else { "OFF" }).clicked() {
                             let _ = self.tx_action.send(GuiAction::SetConservation(!current));
                             self.is_busy = true;
                         }
                    });
                });
                ui.small("Limits battery charge to ~60% to extend lifespan.");
                
                ui.add_space(5.0);

                // Rapid Charge
                ui.horizontal(|ui| {
                    ui.label("Rapid Charge:");
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        let current = self.rapid_charge.unwrap_or(false);
                        let mut val = current;
                         if ui.checkbox(&mut val, if current { "ON" } else { "OFF" }).clicked() {
                             let _ = self.tx_action.send(GuiAction::SetRapidCharge(!current));
                             self.is_busy = true;
                         }
                    });
                });
                ui.small("Charges significantly faster. May generate heat.");

                ui.add_space(10.0);
                ui.separator();
                ui.add_space(5.0);
                
                // Power Profile
                ui.label("Thermal Mode:");
                ui.horizontal(|ui| {
                    let profiles = [
                        ("Quiet", legion_core::hw::power::PowerProfile::Quiet, egui::Color32::from_rgb(100, 149, 237)), // Cornflower Blue
                        ("Balanced", legion_core::hw::power::PowerProfile::Balanced, egui::Color32::WHITE),
                        ("Perf", legion_core::hw::power::PowerProfile::Performance, egui::Color32::from_rgb(220, 20, 60)), // Crimson
                    ];
                    
                    for (name, profile, color) in profiles {
                        let is_current = self.power_profile == Some(profile);
                        // Custom button with color indicator
                        if ui.add(egui::Button::new(egui::RichText::new(name).color(if is_current { color } else { ui.visuals().text_color() })).selected(is_current)).clicked() && !is_current {
                             let _ = self.tx_action.send(GuiAction::SetProfile(profile));
                             self.is_busy = true;
                        }
                    }
                });
            });
            
            ui.add_space(10.0);
            
             // Section: Keyboard Lighting
            ui.group(|ui| {
                ui.set_width(ui.available_width());
                ui.heading("Keyboard Backlight");
                ui.add_space(5.0);
                
                ui.set_enabled(!self.is_busy && self.supported);

                // Ownership Toggle
                if ui.button("Take Control (Enable App Lighting)").clicked() {
                     let _ = self.tx_action.send(GuiAction::SetLightingOwner(true));
                     self.is_busy = true;
                }
                ui.small("Must click this once to enable custom effects.");
                
                ui.add_space(5.0);
                ui.separator();
                
                ui.label("Brightness:");
                ui.horizontal(|ui| {
                    if ui.button("OFF").clicked() {
                        let _ = self.tx_action.send(GuiAction::SetBrightness(0));
                    }
                    if ui.button("LOW").clicked() {
                         let _ = self.tx_action.send(GuiAction::SetBrightness(1));
                    }
                    if ui.button("HIGH").clicked() {
                         let _ = self.tx_action.send(GuiAction::SetBrightness(2));
                    }
                });
                
                ui.add_space(5.0);
                ui.label("Static Color Presets:");
                ui.horizontal(|ui| {
                    if ui.button(egui::RichText::new("BLUE").color(egui::Color32::BLUE)).clicked() {
                         let _ = self.tx_action.send(GuiAction::SetStaticColor(0, 0, 255));
                    }
                    if ui.button(egui::RichText::new("WHITE").color(egui::Color32::WHITE)).clicked() {
                         let _ = self.tx_action.send(GuiAction::SetStaticColor(255, 255, 255));
                    }
                    if ui.button(egui::RichText::new("RED").color(egui::Color32::RED)).clicked() {
                         let _ = self.tx_action.send(GuiAction::SetStaticColor(255, 0, 0));
                    }
                });
            });
            
            ui.add_space(10.0);

            // Footer / Status
            ui.vertical_centered(|ui| {
                if let Some(ref err) = self.last_error {
                    ui.colored_label(egui::Color32::RED, format!("⚠ {}", err));
                } else {
                    ui.label(egui::RichText::new(&self.status_message).italics());
                }
                
                if ui.button("⟳ Refresh State").clicked() {
                    self.request_refresh();
                }
            });
            });
        });
        
        // Repaint if we are expecting updates
        if self.is_busy {
            ctx.request_repaint();
        }
    }
}
