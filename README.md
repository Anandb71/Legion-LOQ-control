# Legion + LOQ Control (C#)

<div align="center">

![License](https://img.shields.io/badge/license-GPL--3.0-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)
![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)
![WPF](https://img.shields.io/badge/UI-WPF-blue.svg)

**A lightweight, open-source replacement for Lenovo Vantage**
*Safety-first hardware control with a modern Windows interface*

</div>

---

## 🚧 Migration in Progress

**This project has migrated from Rust to C# (.NET 9 WPF).**
The previous Rust prototype has been archived in `rust_prototype/`.

The next-generation .NET 10 rebuild is being developed on `rebuild/v1`. The current
application is an experimental prototype and must not be treated as production-safe.
See [SAFETY.md](SAFETY.md) and [source provenance](docs/PROVENANCE.md).

## 🚀 Features (Planned)

| Feature | Status | Description |
| :--- | :---: | :--- |
| **Thermal Profiles** | ⏳ | Quiet / Balanced / Performance modes |
| **Battery Conservation** | ⏳ | Limit charge to ~60% |
| **Rapid Charge** | ⏳ | Fast charging toggle |
| **Keyboard Backlight** | ⏳ | Brightness & Colors (HID) |
| **Device Detection** | ⏳ | Auto-detect Legion & LOQ models |
| **GUI** | ⏳ | Modern WPF Interface |

## 🛠️ Development

### Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (with .NET Desktop Development)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Building
Open `LegionLoqControl.sln` in Visual Studio and build/run.

---

## 📄 License

GNU General Public License version 3. See [LICENSE](LICENSE) and
[third-party notices](THIRD-PARTY-NOTICES.md).

Lenovo, Legion, LOQ, and Vantage are trademarks of their respective owners. This
project is independent and is not affiliated with or endorsed by Lenovo.
