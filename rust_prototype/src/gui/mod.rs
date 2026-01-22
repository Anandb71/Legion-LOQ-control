pub mod app;

use eframe::egui;

pub fn run_gui() -> Result<(), eframe::Error> {
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_inner_size([400.0, 500.0])
            .with_resizable(false),
        ..Default::default()
    };
    
    eframe::run_native(
        "Legion + LOQ Control",
        options,
        Box::new(|_cc| Box::new(app::LegionControlApp::default())),
    )
}
