# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Layered .NET 10 rebuild with platform-neutral Domain and Application projects
- Serial-free machine identity, Lenovo WMI metadata, and HID product-ID diagnostics
- Typed battery, thermal, display-overdrive, and integrated-GPU state results
- One-request authenticated read-only elevation broker
- Exact zero-access EnergyDrv battery-mode reader
- Unelevated precision dashboard with explicit brokered state refresh
- Strict local battery and thermal profile drafts with typed previews
- Strict local AC/battery automation rules with deterministic priority previews
- Versioned atomic JSON stores for profiles and automation rules
- Cross-process exclusive locking for local profile and automation JSON
- Versioned, allowlisted diagnostics reports with atomic dashboard export
- Broker install policy that refuses production launches from unsigned or user-writable paths
- Diagnostics `inventory --output` using the same atomic allowlisted writer as the dashboard
- Explicit elevated apply for thermal mode, display overdrive, integrated-GPU mode,
  battery charge mode, and 4-zone keyboard brightness
- Privileged read-only OEM fan table through `Fan_Get_Table`
- Locked dependency restore, comprehensive automated tests, CodeQL, and accessibility contracts
- Manually generated, short-lived, broker-free read-only preview artifact
- Live AC/battery glance from `GetSystemPowerStatus`, plus silent privileged refresh
  that skips the fan table so cards stay current without a click

### Changed

- Apply rebases onto the live firmware capture, so a click does not require Refresh first
- Migrated the active application from .NET 9 to .NET 10 WPF
- Relicensed the active rebuild under GPL-3.0-or-later
- Quarantined the old C# writer prototype behind a fail-closed global policy
- Studied Lenovo Legion Toolkit as an external protocol reference
- Selected built-in Windows PowerShell CIM cmdlets instead of a restricted MI runtime
- Selected WPF software rendering while the failing hardware-composition path is qualified
- Replaced reflection-based inventory JSON with an explicit privacy-bounded export contract
- Keep one elevated broker for the app session so Windows asks once at start
- Skip the fan-table probe on apply expected-state and readback captures
- Use in-process WMI for privileged reads and writes instead of PowerShell child processes
- Invoke Lenovo GameZone methods through the class parameter object so thermal, overdrive, and GPU reads resolve
- Call GameZone and fan methods through in-process CIM, the same stack that already worked on this LOQ
- Release the session broker lock after each read or apply so refresh can run more than once

### Security

- Fan-table writes and per-zone colors remain absent
- Broker IPC uses a random pipe, current-user ACL, one-time nonce, strict framing, and
  mutual process-ID checks
- WMI reads use fixed getter names, static script text, capped output, and bounded execution
- Production-mode broker launches are refused when the sibling path is unsigned or
  user-writable; public release packaging still requires Authenticode and a protected
  installer

---

## [0.1.0] - Rust Prototype (Archived)

### Added
- Rust-based CLI and GUI prototype
- WMI bindings for device detection
- Basic thermal profile reading
- Privacy spoiler UI

---

*See [rust_prototype/CHANGELOG.md](rust_prototype/CHANGELOG.md) for detailed Rust prototype history.*
