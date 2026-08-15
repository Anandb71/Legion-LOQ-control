# Legion + LOQ Control

<p align="center">
  <img src="LegionLoqControl/Assets/logo.svg" width="96" height="96" alt="Legion + LOQ Control" />
</p>

[![License](https://img.shields.io/badge/license-GPL--3.0--or-later-blue.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/Anandb71/Legion-LOQ-control?include_prereleases)](https://github.com/Anandb71/Legion-LOQ-control/releases)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)
![Status](https://img.shields.io/badge/status-0.3.0%20public%20preview-blue.svg)

A safety-first, free and open-source Lenovo Vantage alternative for Legion and LOQ laptops.
No ads, store, Lena, warranty upsell, serial numbers, or telemetry.

**Current public release: [0.3.0](https://github.com/Anandb71/Legion-LOQ-control/releases/tag/v0.3.0)** — unsigned development preview. Windows asks once for the session broker. Linux ships portable libraries only; there is no Linux hardware GUI.

## Download

| Artifact | What it is |
| --- | --- |
| [Windows zip](https://github.com/Anandb71/Legion-LOQ-control/releases/latest) | WPF app + unsigned elevated broker. Needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). |
| Linux portable tarball | `net10.0` Domain/Application libraries. No firmware access. See [Linux](docs/LINUX.md). |

Extract the Windows zip and run `LegionLoqControl.exe`. Keep `LegionLoqControl.Broker.exe` in the same folder. This is **not** an Authenticode production install. Production mode still refuses unsigned or user-writable layouts; see [broker install](docs/BROKER_INSTALL.md).

## Current status

Working on the recorded LOQ 15IRX9 (type `83DV`, BIOS `NECN50WW`):

- serial-free identity (manufacturer, model, type, BIOS);
- thermal mode, display overdrive, integrated-GPU mode, and battery charge mode;
- overnight charge, Always-on USB, Fn lock, touchpad lock, and Win-key lock when the
  firmware exposes them;
- 4-zone ITE lighting (`048D:C993`) with effect, speed, brightness, and zone colors;
- bounded fan-table apply plus Restore OEM (no full-speed method on this BIOS);
- local profiles and an opt-in AC/battery watcher;
- live AC/battery, CPU, RAM, and system-disk glance;
- one UAC prompt per app session, then clicks apply without a manual Refresh first.

On this machine, unelevated Lenovo WMI getters return `AccessDenied`. Privileged reads
and writes go through the broker. Spectrum chrome stays hidden unless a 960-byte
collection is present.

Not a production-ready support matrix:

- no Authenticode signature or protected installer;
- no model is marked `Supported` in [evidence policy](docs/SUPPORTED_MODELS.md);
- no GPU gauges, ITS/custom thermal sliders, process/hotkey automation, or Linux GUI.

A short-lived broker-free CI preview still exists for unelevated packaging checks. It is
not the public zip. See [the preview boundary](docs/READ_ONLY_PREVIEW.md).

See [SAFETY.md](SAFETY.md), [architecture](docs/ARCHITECTURE.md), the
[broker install policy](docs/BROKER_INSTALL.md),
[compatibility](COMPATIBILITY.md), and the
[rebuild roadmap](docs/ROADMAP.md). Artifact classes are defined in the
[release process](docs/RELEASING.md).

## Build and verify

Requirements: Windows 10 version 1809 or later and the .NET SDK pinned by
[`global.json`](global.json).

```powershell
git clone https://github.com/Anandb71/Legion-LOQ-control.git
cd Legion-LOQ-control
dotnet restore LegionLoqControl.sln --locked-mode
dotnet build LegionLoqControl.sln --configuration Release --no-restore
dotnet test LegionLoqControl.sln --configuration Release --no-build --no-restore
```

Run the default serial-free inventory:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- inventory
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- inventory --output "$env:TEMP\legion-loq-inventory.json"
```

Probe typed hardware state without writing:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- state
```

Explicitly request the same reads through the elevated UAC broker:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- state-elevated
```

Run the WPF shell:

```powershell
dotnet run --project LegionLoqControl --configuration Release
```

Run the inventory, direct state probe, and WPF shell unelevated. `state-elevated` is an
explicit development-only validation command: it prompts through UAC, serves one typed
read request, and exits. The dashboard launches `--session` so later reads and applies
reuse that approval.

Pack a public-style Windows folder (unsigned, development mode):

```powershell
./scripts/export-app-icon.ps1
$out = "$env:TEMP/LegionLoqControl-windows"
./scripts/build-windows-release.ps1 -OutputPath $out -Version 0.3.0
```

## Project goals

- Replace Lenovo Vantage controls without telemetry, ads, or background bloat.
- Model hardware state explicitly instead of converting failures into false values.
- Keep the UI unelevated and place privileged work behind a typed session broker.
- Require provenance, replay fixtures, and exact machine/BIOS evidence before support claims.
- Reuse maintained open-source components when their licenses, size, and safety fit.

## License and independence

Licensed under GNU GPL version 3 or later. See [LICENSE](LICENSE),
[third-party notices](THIRD-PARTY-NOTICES.md), and
[source governance](docs/OSS-SOURCES.md).

Lenovo, Legion, LOQ, and Vantage are trademarks of their respective owners. This project
is independent and is not affiliated with or endorsed by Lenovo.
