# Architecture Overview

This document describes the architecture of Legion + LOQ Control.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    WPF Application                       │
│                   (LegionLoqControl)                     │
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│                     Core Library                         │
│                (LegionLoqControl.Core)                   │
├─────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │   Device    │  │   Hardware   │  │    System     │  │
│  │  Detection  │  │  Controllers │  │   Drivers     │  │
│  └─────────────┘  └──────────────┘  └───────────────┘  │
└────────────────────────┬────────────────────────────────┘
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
    ┌──────────┐  ┌───────────┐  ┌─────────────┐
    │   WMI    │  │  EnergyDrv│  │  HID (USB)  │
    │ Classes  │  │   IOCTL   │  │   Devices   │
    └──────────┘  └───────────┘  └─────────────┘
```

## Components

### WPF Application
- **MainWindow**: Primary UI for user interaction
- Uses MVVM-lite pattern with code-behind

### Core Library

#### Device Detection
- `DeviceDetector`: Identifies supported Lenovo models via WMI

#### Hardware Controllers
- `BatteryController`: Conservation mode, rapid charge (IOCTL)
- `PowerController`: Thermal profiles (WMI GameZone)
- `LightingController`: 4-zone RGB keyboard (HID)
- `SpectrumKeyboardController`: Per-key RGB (HID)
- `WhiteKeyboardController`: White backlight (IOCTL)
- `CustomModeController`: Fan control (WMI)

#### System Layer
- `Drivers`: EnergyDrv handle management
- `NativeMethods`: P/Invoke definitions
- `WMI`: WMI query helpers

## Communication Protocols

| Feature | Protocol | Interface |
|---------|----------|-----------|
| Power Profiles | WMI | LENOVO_GAMEZONE_DATA |
| Fan Control | WMI | LENOVO_FAN_METHOD |
| Battery | IOCTL | EnergyDrv |
| 4-Zone RGB | HID | 33-byte feature report |
| Spectrum RGB | HID | 960-byte feature report |

## Security Considerations

- Requires Administrator privileges
- Direct hardware access via drivers
- No network communication
- No data collection
