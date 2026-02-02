# API Reference

This document provides reference for the core APIs in Legion + LOQ Control.

## Device Detection

### DeviceDetector

```csharp
public class DeviceDetector
{
    // Properties
    public string Model { get; }
    public bool IsSupported { get; }
    
    // Methods
    public void Detect();
}
```

## Hardware Controllers

### BatteryController

```csharp
public class BatteryController
{
    public bool SetConservationMode(bool enable);
    public bool GetConservationMode();
    public bool SetRapidCharge(bool enable);
    public bool GetRapidCharge();
}
```

### PowerController

```csharp
public enum PowerProfile { Quiet = 1, Balanced = 2, Performance = 3 }

public class PowerController
{
    public Task<bool> SetProfileAsync(PowerProfile profile);
    public Task<PowerProfile> GetProfileAsync();
}
```

### LightingController (4-Zone RGB)

```csharp
public class LightingController
{
    public bool IsSupported { get; }
    public Task<bool> SetLightingOwnerAsync(bool appControl);
    public bool SetValues(byte brightness, byte r, byte g, byte b);
    public bool SetOff();
}
```

### SpectrumKeyboardController (Per-Key RGB)

```csharp
public class SpectrumKeyboardController : IDisposable
{
    public bool IsSupported { get; }
    public bool SetBrightness(int brightness);  // 0-9
}
```

### WhiteKeyboardController

```csharp
public enum WhiteKeyboardState { Off, Low, High }

public class WhiteKeyboardController
{
    public bool IsSupported { get; }
    public bool SetState(WhiteKeyboardState state);
    public WhiteKeyboardState GetState();
}
```

### CustomModeController

```csharp
public class CustomModeController
{
    public bool IsSupported { get; }
    public Task<bool> SetFanFullSpeedAsync(bool enabled);
    public Task<bool> GetFanFullSpeedAsync();
}
```

## WMI Classes

### Used WMI Namespaces

| Namespace | Classes |
|-----------|---------|
| `root\WMI` | LENOVO_GAMEZONE_DATA, LENOVO_FAN_METHOD |

### Key Methods

- `GetSmartFanMode()` / `SetSmartFanMode(int)`
- `SetLightControlOwner(int)`
- `Fan_Set_FullSpeed(int)` / `Fan_Get_FullSpeed()`

## Driver IOCTLs

| IOCTL Code | Purpose |
|------------|---------|
| 0x831020F8 | Battery charge mode |
| 0x83102144 | Keyboard backlight |
