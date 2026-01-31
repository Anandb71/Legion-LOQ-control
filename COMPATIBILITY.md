# Version History

## Current Version: 0.2.0

### Supported Models
- Legion 5/5i/5 Pro series
- Legion 7/7i series  
- LOQ 15/16 series
- IdeaPad Gaming 3 series

### Tested Features by Model

| Feature | Legion 5 Pro | LOQ 15IRX9 | Notes |
|---------|-------------|------------|-------|
| Device Detection | ✅ | ✅ | WMI-based |
| Power Profiles | ✅ | ✅ | Quiet/Balanced/Performance |
| Conservation Mode | ✅ | ✅ | Battery limit ~60% |
| Rapid Charge | ✅ | ✅ | Fast charging |
| Fan Control | ✅ | ⚠️ | Full speed toggle |
| Keyboard (Spectrum) | ✅ | 🔧 | Per-key RGB |
| Keyboard (4-Zone) | ✅ | N/A | Older models |

### Legend
- ✅ Fully working
- ⚠️ Partially working
- 🔧 In development
- N/A Not applicable

### Known Issues
- Some LOQ models may require Vantage to be fully closed
- Keyboard control requires "Take Control" button first
- Fan control may not work on all BIOS versions

### Requirements
- Windows 10/11
- .NET 9 Desktop Runtime
- Lenovo Energy Management driver
- Administrator privileges
