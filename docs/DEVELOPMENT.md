# Development Setup

## Requirements

- Windows 10 version 1809 or later
- .NET SDK `10.0.302` (selected by `global.json`)
- Git
- Visual Studio 2022 17.14+ / Visual Studio 2026, Rider, or Cursor with C# tooling

The .NET CLI is sufficient. Development and diagnostics do not require administrator
privileges.

## Clone and verify

```powershell
git clone --recurse-submodules https://github.com/Anandb71/Legion-LOQ-control.git
cd Legion-LOQ-control
dotnet restore LegionLoqControl.sln --locked-mode
dotnet build LegionLoqControl.sln --configuration Release --no-restore
dotnet test LegionLoqControl.sln --configuration Release --no-build --no-restore
```

Run diagnostics:

```powershell
dotnet run --project src/LegionLoqControl.Diagnostics --configuration Release
```

Run the read-only desktop shell:

```powershell
dotnet run --project LegionLoqControl --configuration Release
```

## Repository layout

- `src/`: new layered platform code
- `tests/`: safety, domain, contract, diagnostics, and evidence-redaction tests
- `hardware-evidence/`: redacted exact machine/BIOS observations
- `LegionLoqControl/`: WPF shell
- `LegionLoqControl.Core/`: quarantined legacy prototype
- `LLT_Reference/`: reference-only fork submodule; never compile or package it
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

Use the diagnostics CLI before proposing model support. Evidence must not contain serial
numbers, usernames, device paths, account data, or full exception messages. A metadata
match is only a candidate interface; it does not prove that a write is safe.

Real write validation is prohibited until the broker milestone defines a reviewed test
procedure. See [DIAGNOSTICS.md](DIAGNOSTICS.md), [SAFETY.md](../SAFETY.md), and
[PROVENANCE.md](PROVENANCE.md).
