pub enum PowerMode {
    Performance,
    Balanced,
    Quiet,
    Unknown,
}

pub fn get_power_mode() -> PowerMode {
    // This requires Legion specific ACPI WMI calls usually.
    // For now, return Unknown as we don't have the ACPI interface yet.
    PowerMode::Unknown
}
