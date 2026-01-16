# Legion + LOQ Control

<div align="center">

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)
![Version](https://img.shields.io/badge/version-0.2.0-green.svg)
![Rust](https://img.shields.io/badge/rust-stable-orange.svg)

**A lightweight, open-source replacement for Lenovo Vantage**

*No bloat. No telemetry. No background services.*

[Features](#-features) • [Installation](#️-installation) • [Usage](#-usage) • [Contributing](#-contributing)

</div>

---

## ⚡ Why This Exists

Lenovo Vantage is bloated, collects telemetry, and runs background services 24/7. This tool provides the same essential features in a **single lightweight executable** that only runs when you need it.

## 🚀 Features

| Feature | Status | Description |
| :--- | :---: | :--- |
| **Thermal Profiles** | ✅ | Quiet / Balanced / Performance modes |
| **Battery Conservation** | ✅ | Limit charge to ~60% for battery longevity |
| **Rapid Charge** | ✅ | Fast charging toggle |
| **Keyboard Backlight** | ✅ | Brightness levels + Static RGB colors |
| **Device Detection** | ✅ | Auto-detects Legion & LOQ models |
| **GUI** | ✅ | Modern, dark-mode interface |
| **CLI** | ✅ | Full command-line support with `--help` |
| **Telemetry** | 🚫 | Zero data collection |
| **Background Services** | 🚫 | Runs only when launched |

> ⚠️ **Requires Administrator**: Right-click `.exe` → "Run as administrator"

---

## 🛠️ Installation

### Option 1: Download Release
Download the latest `.exe` from [Releases](https://github.com/Anandb71/Legion-LOQ-control/releases).

### Option 2: Build from Source
```bash
# Requires Rust toolchain
git clone https://github.com/Anandb71/Legion-LOQ-control.git
cd Legion-LOQ-control
cargo build --release

# Run the GUI
./target/release/legion-loq-control.exe --gui
```

---

## 📖 Usage

### GUI Mode
```bash
legion-loq-control --gui
```

### CLI Mode
```bash
# Show help
legion-loq-control --help

# Show device info
legion-loq-control

# Set thermal profile
legion-loq-control --set-profile perf    # Options: quiet, balanced, perf

# Toggle battery features
legion-loq-control --set-conservation-mode on
legion-loq-control --rapid-charge off

# Preview changes without applying
legion-loq-control --dry-run --set-profile quiet

# JSON output (for scripting)
legion-loq-control --json
```

---

## 💻 Supported Models

| Series | Models | Status |
| :--- | :--- | :---: |
| **LOQ** | 15, 16 (83DV, etc.) | ✅ Verified |
| **Legion** | 5, 7, Pro, Slim | ✅ Beta |
| **IdeaPad Gaming** | — | ❌ Unsupported |

> 📝 Not detected? Open an [Issue](https://github.com/Anandb71/Legion-LOQ-control/issues) with your model number.

---

## 🤝 Contributing

Contributions welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

**Core Principles:**
- 🔒 **Safety First** — Read-only by default, writes require explicit action
- 🎯 **Minimal Scope** — Legion + LOQ only, no feature creep
- 📖 **Transparency** — All hardware interactions are logged

---

## 🔒 Security

See [SECURITY.md](SECURITY.md) for vulnerability reporting and our safety philosophy.

---

## 🙏 Acknowledgements

- **[LenovoLegionToolkit](https://github.com/BartoszCichecki/LenovoLegionToolkit)** — The gold standard. This project references LLT's excellent reverse-engineering work.

---

## 📄 License

MIT License — See [LICENSE](LICENSE).

---

<div align="center">

*Not affiliated with Lenovo. Use at your own risk.*

**Made with ❤️ for the Legion community**

</div>
