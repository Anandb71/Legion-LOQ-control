# Troubleshooting Guide

This guide helps you diagnose common issues with Legion + LOQ Control.

## Common Issues

### 1. "Device not supported" Message

**Cause:** Your laptop model is not recognized.

**Solution:**
- Ensure you have a Lenovo Legion or LOQ laptop
- Check if Lenovo Energy Management driver is installed
- Run the app as Administrator

### 2. Keyboard Backlight Not Working

**Possible Causes:**
- Lenovo Vantage is running and has control
- Wrong keyboard type detected
- Missing HID drivers

**Solutions:**
1. Close Lenovo Vantage completely
2. Click "Take Control" button first
3. Check the Log panel for keyboard type detection
4. Reinstall Lenovo hotkey drivers

### 3. Fan Control Not Working

**Cause:** WMI interface not responding.

**Solutions:**
1. Ensure running as Administrator
2. Check if LENOVO_FAN_METHOD WMI class exists:
   ```powershell
   Get-WmiObject -Namespace "root\WMI" -Class "LENOVO_FAN_METHOD"
   ```
3. Update Lenovo System Interface Foundation driver

### 4. Battery Features Not Working

**Cause:** Energy driver not accessible.

**Solutions:**
1. Run as Administrator
2. Verify EnergyDrv is installed:
   ```powershell
   Get-Service | Where-Object { $_.Name -like "*Energy*" }
   ```

### 5. App Crashes on Startup

**Possible Causes:**
- Missing .NET 9 Runtime
- Corrupted installation

**Solutions:**
1. Install [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
2. Re-download the application

## Diagnostic Information

To help with troubleshooting, gather this info:

1. **Laptop Model:** Check in System Information
2. **Windows Version:** `winver`
3. **Log Output:** Copy text from the Log panel
4. **WMI Check:**
   ```powershell
   Get-WmiObject -Namespace "root\WMI" -List | Where-Object { $_.Name -like "*LENOVO*" }
   ```

## Reporting Issues

If problems persist, [open an issue](https://github.com/Anandb71/Legion-LOQ-control/issues) with:
- Your laptop model
- Windows version
- Steps to reproduce
- Log panel output
- Any error messages
