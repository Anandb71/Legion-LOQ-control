# Development Setup

## Requirements

- Windows 10 version 1809 or later
- Built-in Windows PowerShell 5.1 with the system `CimCmdlets` module
- .NET SDK `10.0.302` (selected by `global.json`)
- Git
- Visual Studio 2022 17.14+ / Visual Studio 2026, Rider, or Cursor with C# tooling

The .NET CLI is sufficient. Builds, tests, inventory diagnostics, and the WPF shell do not
require administrator privileges.

## Clone and verify

```powershell
git clone https://github.com/Anandb71/Legion-LOQ-control.git
cd Legion-LOQ-control
dotnet restore LegionLoqControl.sln --locked-mode
dotnet build LegionLoqControl.sln --configuration Release --no-restore
dotnet test LegionLoqControl.sln --configuration Release --no-build --no-restore
```

Run serial-free inventory diagnostics:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- inventory
```

Run typed state diagnostics:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- state
```

The state command can run unelevated and preserves `AccessDenied`. To validate the
privileged boundary explicitly:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release -- state-elevated
```

This displays UAC and runs the copied sibling broker for that command. Do not automate
the prompt or use the development broker for writes. The default install mode is
`development`, which permits that unsigned sibling after the prompt. Set
`LEGIONLOQ_BROKER_INSTALL_MODE=production` only to verify that unsigned or user-writable
paths are refused before UAC. See [`BROKER_INSTALL.md`](BROKER_INSTALL.md).

Run the read-only desktop shell:

```powershell
dotnet run --project LegionLoqControl --configuration Release
```

The current low-motion WPF shell deliberately uses software rendering. Hardware composition
produced blank client surfaces on the validated Windows build; do not re-enable it without
cross-version startup and visual regression coverage.

Preview packaging and production release blockers are documented in
[`RELEASING.md`](RELEASING.md).

## Repository layout

- `src/`: new layered platform code
- `src/LegionLoqControl.Broker/`: session-lived elevated process that exits with the parent
- `tests/`: safety, domain, contract, diagnostics, and evidence-redaction tests
- `hardware-evidence/`: redacted exact machine/BIOS observations
- `LegionLoqControl/`: WPF shell
- `LegionLoqControl.Core/`: quarantined legacy prototype
- `rust_prototype/`: archived first prototype

## Dependency updates

Versions are centralized in `Directory.Packages.props`. After an intentional update:

```powershell
dotnet restore LegionLoqControl.sln
dotnet restore LegionLoqControl.sln --locked-mode
dotnet build LegionLoqControl.sln --configuration Release --no-restore
dotnet test LegionLoqControl.sln --configuration Release --no-build --no-restore
```

Commit all resulting `packages.lock.json` changes with the package update.

## Hardware evidence

Use the `inventory` command before proposing model support. Evidence must not contain
serial numbers, usernames, device paths, account data, or full exception messages. A
metadata match is only a candidate interface; it does not prove that a write is safe.

Read-only getter validation must record exact model/BIOS, execution privilege, raw value,
typed interpretation, and failure status. Real write validation is prohibited until the
broker milestone defines a reviewed test procedure. See
[DIAGNOSTICS.md](DIAGNOSTICS.md), [SAFETY.md](../SAFETY.md), and
[PROVENANCE.md](PROVENANCE.md).
