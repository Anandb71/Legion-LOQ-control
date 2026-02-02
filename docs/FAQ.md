# FAQ (Frequently Asked Questions)

## General Questions

### What is Legion + LOQ Control?
A lightweight Windows application for controlling Lenovo Legion and LOQ laptop hardware features without Lenovo Vantage.

### Why use this instead of Lenovo Vantage?
- **Lightweight**: ~2MB vs 500MB+
- **Fast**: Instant startup vs 10+ seconds
- **No telemetry**: No data collection
- **Open source**: Transparent and auditable

### Is this safe to use?
Yes. The app uses the same WMI and driver interfaces as Lenovo Vantage. See [SAFETY.md](../SAFETY.md) for details.

## Technical Questions

### Why does it need Administrator privileges?
The app needs to access:
- WMI classes (for power profiles and fan control)
- EnergyDrv driver (for battery settings)
- HID devices (for keyboard backlight)

### Can I run this alongside Lenovo Vantage?
Yes, but there may be conflicts with keyboard lighting control. Click "Take Control" to override Vantage's lighting settings.

### Why isn't my keyboard backlight working?
1. Close Lenovo Vantage completely
2. Click "Take Control" button
3. Check the Log panel for error messages
4. See [TROUBLESHOOTING.md](../TROUBLESHOOTING.md)

### What keyboard types are supported?
- **Spectrum (Per-Key RGB)**: Full RGB control on newer models
- **4-Zone RGB**: Zone-based lighting on older Legion models
- **White**: Simple on/off/brightness on some LOQ/IdeaPad

### Does this work on AMD/Intel/Nvidia variants?
Yes! The app works with all CPU/GPU combinations on supported Lenovo models.

## Feature Questions

### Can I customize RGB colors per zone?
Currently only brightness and basic effects are supported. Per-zone color customization is planned for a future update.

### Can I set custom power limits (TDP)?
This feature is in development. Currently only preset profiles (Quiet/Balanced/Performance) are supported.

### Can I control fan curves?
Currently only full-speed toggle is available. Custom fan curves are planned for a future update.

## Troubleshooting

### "Device not supported" error
Your laptop model may not be recognized. Please [open an issue](https://github.com/Anandb71/Legion-LOQ-control/issues) with your model number.

### App crashes when clicking a button
Run the app as Administrator and check that Lenovo drivers are installed.

### Nothing happens when I change settings
Check the Log panel at the bottom of the window for error messages.
