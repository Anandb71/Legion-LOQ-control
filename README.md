# Legion + LOQ Control

[![License](https://img.shields.io/badge/license-GPL--3.0--or--later-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)
![Status](https://img.shields.io/badge/status-read--only%20rebuild-orange.svg)

A safety-first, free and open-source Lenovo Vantage alternative for Legion and LOQ laptops.

## Current status

The next-generation rebuild lives on `rebuild/v1`. It is intentionally **read-only** while
the privileged boundary and per-model verification system are built.

Working today:

- serial-free machine identity capture;
- non-invoking Lenovo WMI method inventory;
- non-opening HID device inventory;
- typed hardware-read results that preserve access denied, unsupported, malformed, and
  timeout states;
- strict allowlisted getters for thermal mode, display overdrive, and integrated-GPU mode;
- a redacted JSON diagnostics CLI with inventory, direct-state, and brokered-state commands;
- a short-lived, read-only UAC broker with strict framing, a one-time nonce, peer-process
  checks, a current-user pipe ACL, and no write dispatcher;
- an unelevated precision dashboard with explicit brokered state refresh and no reference
  to legacy hardware writers;
- locked dependencies and automated safety, contract, mapping, and redaction tests.

On the recorded LOQ 15IRX9, the Lenovo provider denies the instance access needed by
the state getters in an unelevated process. The probe reports `AccessDenied` rather than
inventing values. The read-only broker successfully validated Performance thermal mode,
disabled display overdrive, and integrated-only GPU mode on the same machine/BIOS. The
current adapter batches those fixed getters through the built-in Windows PowerShell CIM
host so no restricted MI runtime is distributed. The broker also validated Normal battery
mode through one exact EnergyDrv read selector opened with zero requested device access.
The unelevated `state` command intentionally does not open EnergyDrv.

Not enabled today:

- battery, thermal, fan, GPU, display, or keyboard writes;
- production broker signing, installer ACLs, or hardware-write UI;
- profile application or automation execution;
- any claim of production-ready model support.

See [SAFETY.md](SAFETY.md), [architecture](docs/ARCHITECTURE.md), and the
[current evidence policy](docs/SUPPORTED_MODELS.md). The staged path from read-only
profile previews to validated controls is tracked in the [rebuild roadmap](docs/ROADMAP.md).

## Build and verify

Requirements: Windows 10 version 1809 or later and the .NET SDK pinned by
[`global.json`](global.json).

```powershell
git clone --recurse-submodules https://github.com/Anandb71/Legion-LOQ-control.git
cd Legion-LOQ-control
dotnet restore LegionLoqControl.sln --locked-mode
dotnet build LegionLoqControl.sln --configuration Release --no-restore
dotnet test LegionLoqControl.sln --configuration Release --no-build --no-restore
```

Run the default serial-free inventory:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- inventory
```

Probe typed hardware state without writing:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- state
```

Explicitly request the same reads through the short-lived UAC broker:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- state-elevated
```

Run the read-only WPF shell:

```powershell
dotnet run --project LegionLoqControl --configuration Release
```

Run the inventory, direct state probe, and WPF shell unelevated. `state-elevated` is an
explicit development-only validation command: it prompts through UAC, serves one typed
read request, and exits. It cannot execute hardware writes.

## Project goals

- Replace Lenovo Vantage controls without telemetry, ads, or background bloat.
- Model hardware state explicitly instead of converting failures into false values.
- Keep the UI unelevated and place every future write behind a short-lived typed broker.
- Require provenance, replay fixtures, and exact machine/BIOS evidence before support claims.
- Reuse maintained open-source components when their licenses, size, and safety fit.

## License and independence

Licensed under GNU GPL version 3 or later. See [LICENSE](LICENSE),
[third-party notices](THIRD-PARTY-NOTICES.md), and
[source governance](docs/OSS-SOURCES.md).

Lenovo, Legion, LOQ, and Vantage are trademarks of their respective owners. This project
is independent and is not affiliated with or endorsed by Lenovo.
