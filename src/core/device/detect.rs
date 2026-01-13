use crate::platform::windows::WmiQueryHandler;
use super::models::{LaptopModel, Series};
use log::{info, warn};
use std::error::Error;

pub fn detect_device() -> Result<LaptopModel, Box<dyn Error>> {
    let wmi = WmiQueryHandler::new()?;
    
    // 1. Query Manufacturer
    let manufacturer = wmi.get_manufacturer()?;
    let manuf_upper = manufacturer.to_uppercase();
    
    if !manuf_upper.contains("LENOVO") {
        return Ok(LaptopModel {
            manufacturer,
            model_name: "Unknown".to_string(),
            bios_version: "Unknown".to_string(),
            series: Series::Unknown,
            supported: false,
        });
    }

    // 2. Query Model
    let model_name = wmi.get_model()?;
    let series = determine_series(&model_name);

    // 3. Query BIOS
    let bios_version = wmi.get_bios_version()?;

    // 4. Mark Supported
    let supported = match series {
        Series::Legion | Series::LOQ => true,
        _ => false,
    };

    Ok(LaptopModel {
        manufacturer,
        model_name,
        series,
        bios_version,
        supported,
    })
}

fn determine_series(model: &str) -> Series {
    let model_upper = model.to_uppercase();
    
    if model_upper.contains("LEGION") {
        Series::Legion
    } else if model_upper.contains("LOQ") {
        Series::LOQ
    } else if model_upper.contains("IDEAPAD") {
        Series::IdeaPad
    } else {
        Series::Unknown
    }
}
