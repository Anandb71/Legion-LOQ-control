# Legion + LOQ Control

Legion + LOQ Control is a lightweight, open-source alternative to Lenovo Vantage for Legion and LOQ laptops.

Lenovo Vantage is heavy, inconsistent, and often breaks after updates.
This tool focuses on the essentials: power modes, battery limits, and hardware insight — without running background services or collecting telemetry.

**Note:** Not affiliated with Lenovo.

## Acknowledgements

-   **[LenovoLegionToolkit](https://github.com/BartoszCichecki/LenovoLegionToolkit)**: A huge inspiration and reference for this project. Although archived, its codebase provided invaluable insights into WMI device detection and specific Lenovo WMI calls. We aim to carry on its spirit of lightweight, bloat-free control.
-   **Lenovo**: For making great hardware (even if the software needs a community alternative).

## Current Status (v0.1.0)
**Read-Only Foundation (Phase B Complete)**
- ✅ **Device Detection**: Safely identifies Legion and LOQ models.
- ✅ **Hardware Monitoring**: Reads Battery charge and status.
- ✅ **Safety**: Strict gating ensures it only runs on supported hardware.
- ⚠️ **Thermals**: Currently stubbed/disabled for stability while we migrate to robust WMI readings.

**Coming Soon (Phase C)**
- Battery Charge Limiting (Conservation Mode)

## Supported Models

| Series | Models | Status |
| :--- | :--- | :--- |
| Legion | 5, 7, Pro, Slim | **Supported (Read-Only)** |
| LOQ | 15, 16 | **Supported (Read-Only)** |
| IdeaPad | - | **Unsupported** |

## Safety Notes
- **Safety beats features.** If a feature can brick hardware, it ships last or never.
- **Read-only first.** We verify we can read your hardware safely before trying to write to it.
- **Explicit scope.** Only Legion and LOQ models are supported.

## Disclaimer
This software is provided "as is", without warranty of any kind. You use this software at your own risk. The authors are not responsible for any damage to your hardware.
