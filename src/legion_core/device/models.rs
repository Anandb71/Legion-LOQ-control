use serde::{Serialize, Deserialize};

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum Series {
    Legion,
    LOQ,
    IdeaPad,
    Unknown,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct LaptopModel {
    pub manufacturer: String,
    pub model_name: String,
    pub series: Series,
    pub bios_version: String,
    pub supported: bool,
}

impl LaptopModel {
    pub fn is_supported(&self) -> bool {
        self.supported
    }
}
