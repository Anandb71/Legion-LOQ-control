# Legion + LOQ Control

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)
![Status](https://img.shields.io/badge/status-Alpha-orange.svg)

**Legion + LOQ Control** is a lightweight, open-source replacment for Lenovo Vantage, designed specifically for **Lenovo Legion** and **LOQ** series laptops.

It provides essential hardware control—Power Profiles, Battery Conservation, and Rapid Charge—without the bloat, telemetry, or background services of the official software.

---

## 🚀 Features

| Feature | Status | Notes |
| :--- | :--- | :--- |
| **Power Profiles** | ✅ Active | Quiet (Blue), Balanced (White), Performance (Red) |
| **Battery Conservation** | ✅ Active | Limits charge to ~60-80% to prolong lifespan |
| **Rapid Charge** | ✅ Active | Fast charging toggle |
| **Device Detection** | ✅ Active | Strict validation for Legion & LOQ models (e.g., 83DV) |
| **GUI** | ✅ Beta | Modern, dark-mode friendly interface |
| **Telemetry** | 🚫 None | Zero data collection. Offline only. |
| **Background Services** | 🚫 None | Runs only when you open it. |

## 🛠️ Installation

### Prerequisites
- Windows 10 or Windows 11
- A supported Lenovo Legion or LOQ laptop (see below)

### Building from Source
This project is written in **Rust**. You will need the latest stable Rust toolchain.

```bash
# Clone the repository
git clone https://github.com/Anandb71/Legion-LOQ-control.git
cd Legion-LOQ-control

# Build and Run
cargo run --release -- --gui
```

## 💻 Supported Models

This tool is strictly tested on specific hardware to ensure safety.

| Series | Models | Status |
| :--- | :--- | :--- |
| **LOQ** | 15, 16 (e.g., 83DV) | **Verified** |
| **Legion** | 5, 7, Pro, Slim | **Beta Support** |
| **IdeaPad** | Gaming 3 | *Unsupported* |

> **Note**: If your device is not detected, please open an Issue with your "System Model" information.

## 🤝 Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on code style, safety rules, and pull requests.

## 🔒 Security & Safety

Safety is our #1 priority. We use a "Read-Only First" architecture and verify all hardware interactions. See [SECURITY.md](SECURITY.md) for our full policy.

## 🙏 Acknowledgements

*   **[LenovoLegionToolkit](https://github.com/BartoszCichecki/LenovoLegionToolkit)**: The gold standard for Legion tools. Use it if you want a feature-complete C# experience. This project draws heavy inspiration and technical reference from LLT's research.
*   **Lenovo**: For the hardware.

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.

---
*Disclaimer: This project is not affiliated with or endorsed by Lenovo. Use at your own risk.*
