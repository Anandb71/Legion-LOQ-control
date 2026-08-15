# Legion + LOQ Control

[![License](https://img.shields.io/badge/license-GPL--3.0--or--later-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)
![Status](https://img.shields.io/badge/status-write--gated%20rebuild-orange.svg)

A safety-first, free and open-source Lenovo Vantage alternative for Legion and LOQ laptops.

## Current status

The next-generation rebuild is now active on `main`. Inventory, refresh, preview, and
export stay read-only. Dashboard apply is an explicit click plus one UAC prompt.

Working today:

- serial-free machine identity capture;
- non-invoking Lenovo WMI method inventory;
- non-opening HID device inventory;
- typed hardware-read results that preserve access denied, unsupported, malformed, and
  timeout states;
- strict allowlisted getters for thermal mode, display overdrive, and integrated-GPU mode;
- a redacted JSON diagnostics CLI with inventory, direct-state, and brokered-state commands;
- a versioned, explicitly allowlisted diagnostics report with atomic in-app export of
  retained snapshots and no export-triggered read or elevation;
- a short-lived UAC broker with strict framing, a one-time nonce, peer-process checks, a
  current-user pipe ACL, one allowlisted write per `--write` launch, and an install policy
  that keeps unsigned sibling launches in development mode;
- an unelevated precision dashboard with explicit brokered state refresh and apply for
  thermal mode, display overdrive, integrated-GPU mode, battery charge mode, and 4-zone
  keyboard brightness, plus a read-only OEM fan table;
- strict, versioned local battery and thermal drafts with typed current-versus-target
  previews and no Apply path;
- strict, versioned AC/battery automation rules with deterministic priority evaluation,
  explicit conflict states, and no scheduler or execution path;
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

- per-zone RGB color editors, fan-table writes, or custom-thermal writes;
- production broker signing or installer ACLs;
- profile application or automation execution;
- any claim of production-ready model support.

The manual CI preview artifact is intentionally broker-free and short-lived; it is not a
public release. See [the preview boundary](docs/READ_ONLY_PREVIEW.md).

See [SAFETY.md](SAFETY.md), [architecture](docs/ARCHITECTURE.md), the
[broker install policy](docs/BROKER_INSTALL.md), and the
[current evidence policy](docs/SUPPORTED_MODELS.md). The staged path from read-only
profile and automation previews to validated controls is tracked in the
[rebuild roadmap](docs/ROADMAP.md). Artifact classes and production blockers are defined in
the [release process](docs/RELEASING.md).

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
