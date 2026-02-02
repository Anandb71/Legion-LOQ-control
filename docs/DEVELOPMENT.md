# Development Setup Guide

This guide helps you set up your development environment to contribute to Legion + LOQ Control.

## Prerequisites

### Required Software
- **Windows 10/11** (WPF requires Windows)
- **Visual Studio 2022** or **VS Code** with C# extension
- **.NET 9 SDK** ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **Git** for version control

### Recommended Extensions (VS Code)
- C# Dev Kit
- .NET Install Tool
- EditorConfig for VS Code

## Getting Started

### 1. Clone the Repository
```bash
git clone https://github.com/Anandb71/Legion-LOQ-control.git
cd Legion-LOQ-control
```

### 2. Restore Dependencies
```bash
dotnet restore LegionLoqControl.sln
```

### 3. Build the Project
```bash
dotnet build LegionLoqControl.sln
```

### 4. Run the Application
```bash
# Must run as Administrator for WMI/driver access
dotnet run --project LegionLoqControl
```

## Project Structure

```
├── LegionLoqControl/           # WPF Application (UI)
│   ├── MainWindow.xaml         # Main window UI
│   └── MainWindow.xaml.cs      # UI code-behind
│
├── LegionLoqControl.Core/      # Core Library
│   ├── Device/                 # Device detection
│   ├── Hardware/               # Hardware controllers
│   ├── Native/                 # P/Invoke definitions
│   └── System/                 # WMI and driver access
│
├── LLT_Reference/              # Reference from LenovoLegionToolkit
│
└── rust_prototype/             # Archived Rust prototype
```

## Testing on Lenovo Hardware

To test the application, you need a Lenovo Legion or LOQ laptop. The app requires:
- Administrator privileges
- Lenovo Energy Management driver
- WMI classes provided by Lenovo ACPI

## Code Style

We use EditorConfig for consistent formatting. Key rules:
- 4 spaces for indentation
- Allman brace style
- `var` when type is apparent
- Expression-bodied members when single line

## Submitting Changes

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run `dotnet build` to verify
5. Submit a pull request

See [CONTRIBUTING.md](CONTRIBUTING.md) for more details.
