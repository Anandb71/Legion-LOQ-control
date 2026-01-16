use hidapi::{HidApi, HidDevice};
use std::error::Error;
use log::{info, warn, error};

// Constants from LLT
const VENDOR_ID: u16 = 0x048D;
const PRODUCT_ID_MASKED: u16 = 0xC900;
const PRODUCT_ID_MASK: u16 = 0xFF00;
const DESCRIPTOR_LENGTH: u16 = 0x21; // 33 bytes

#[repr(C, packed)]
struct LenovoRgbKeyboardState {
    header: [u8; 2],
    effect: u8,
    speed: u8,
    brightness: u8,
    zone1_rgb: [u8; 3],
    zone2_rgb: [u8; 3],
    zone3_rgb: [u8; 3],
    zone4_rgb: [u8; 3],
    padding: u8,
    wave_ltr: u8,
    wave_rtl: u8,
    unused: [u8; 13],
}

impl Default for LenovoRgbKeyboardState {
    fn default() -> Self {
        Self {
            header: [0xCC, 0x16],
            effect: 1, // Static
            speed: 1,
            brightness: 2, // High
            zone1_rgb: [0, 0, 255], // Blue
            zone2_rgb: [0, 0, 255],
            zone3_rgb: [0, 0, 255],
            zone4_rgb: [0, 0, 255],
            padding: 0,
            wave_ltr: 0,
            wave_rtl: 0,
            unused: [0; 13],
        }
    }
}

pub struct LightingController {
    // We don't keep the device open to avoid locking it? Or should we?
    // LLT opens/closes or keeps open? LLT uses SafeFileHandle.
    // HidApi recommends keeping the api instance.
}

impl LightingController {
    pub fn new() -> Self {
        Self {}
    }

    fn find_device(api: &HidApi) -> Result<HidDevice, Box<dyn Error>> {
        for device in api.device_list() {
            if device.vendor_id() == VENDOR_ID {
                // Check Product ID Mask
                if (device.product_id() & PRODUCT_ID_MASK) == PRODUCT_ID_MASKED {
                    info!("Found potential Lighting Device: VID={:04x}, PID={:04x}", device.vendor_id(), device.product_id());
                    // Ideally we check UsagePage/Usage or Descriptor length, relying on PID mask for now as hidapi listing might not give full descriptor len easily without opening.
                    return Ok(api.open_path(device.path())?);
                }
            }
        }
        Err("Lighting device not found".into())
    }

    pub fn set_static_color(&self, r: u8, g: u8, b: u8) -> Result<(), Box<dyn Error>> {
        if !crate::legion_core::safety::guards::GlobalWriteLock::is_write_allowed() {
            return Err("Global Write Lock is active. Cannot write to lighting.".into());
        }

        let api = HidApi::new()?;
        let device = Self::find_device(&api)?;

        let mut state = LenovoRgbKeyboardState::default();
        state.effect = 1; // Static
        state.brightness = 2; // High
        state.zone1_rgb = [r, g, b];
        state.zone2_rgb = [r, g, b];
        state.zone3_rgb = [r, g, b];
        state.zone4_rgb = [r, g, b];

        // Ensure header is correct
        state.header = [0xCC, 0x16];

        // Serialize to bytes
        // unsafe due to packed struct, but we are just reading bytes
        let bytes = unsafe {
            std::slice::from_raw_parts(
                &state as *const _ as *const u8,
                std::mem::size_of::<LenovoRgbKeyboardState>()
            )
        };

        // HIDAPI expects report ID as first byte if numbered reports are used.
        // LLT sends 33 bytes. It seems report ID is not used or implicit?
        // HidD_SetFeature in LLT sends the struct directly.
        // hidapi `send_feature_report` needs Report ID as first byte? 
        // "The first byte of the data must contain the Report ID. For devices which only support a single report, this must be set to 0x0."
        
        let mut report = Vec::with_capacity(34);
        report.push(0xCC); // WAIT: LLT says Header is 0xCC, 0x16.
        // Does strict HID require a 0x00 prefix if report ID is not used?
        // Let's try sending the 33 bytes directly first? No, hidapi docs say first byte is Report ID.
        // If the device uses Report IDs, 0xCC might be it?
        // LLT uses `HidD_SetFeature`.
        // Let's assume we prepend 0 if report ID is not part of the data.
        // But wait, LLT struct starts with [0xCC, 0x16].
        
        // Trial 1: Send [0xCC, 0x16, ...] (33 bytes).
        // If hidapi requires Report ID, we might need to verify if 0xCC is the report ID.
        // LLT: `Header = [0xCC, 0x16]`
        // Maybe Report ID is 0xCC?
        
        // Let's try sending exactly what LLT sends.
        // Note: hidapi `send_feature_report` takes a slice.
        
        // IMPORTANT: hidapi on Windows might require the buffer to be effectively ReportID + Data.
        // If the first byte 0xCC is the Report ID, then we are good.
        
        match device.send_feature_report(bytes) {
            Ok(_) => Ok(()),
            Err(e) => {
               // Fallback: Try prepending 0?
               warn!("Standard feature report failed ({}), trying with 0x00 prefix...", e);
               let mut prefixed = vec![0u8];
               prefixed.extend_from_slice(bytes);
               device.send_feature_report(&prefixed)?;
               Ok(())
            }
        }
    }

    pub fn set_brightness(&self, level: u8) -> Result<(), Box<dyn Error>> {
        // level: 0 = Off, 1 = Low, 2 = High
        self.set_effect_params(1, level)
    }

    fn set_effect_params(&self, effect: u8, brightness: u8) -> Result<(), Box<dyn Error>> {
         if !crate::legion_core::safety::guards::GlobalWriteLock::is_write_allowed() {
            return Err("Global Write Lock is active.".into());
        }

        let api = HidApi::new()?;
        let device = Self::find_device(&api)?;

        let mut state = LenovoRgbKeyboardState::default();
        state.effect = effect;
        state.brightness = brightness;
        state.header = [0xCC, 0x16];

        let bytes = unsafe {
            std::slice::from_raw_parts(
                &state as *const _ as *const u8,
                std::mem::size_of::<LenovoRgbKeyboardState>()
            )
        };
        
        match device.send_feature_report(bytes) {
            Ok(_) => Ok(()),
            Err(_) => {
               let mut prefixed = vec![0u8];
               prefixed.extend_from_slice(bytes);
               device.send_feature_report(&prefixed)?;
               Ok(())
            }
        }
    }
}
