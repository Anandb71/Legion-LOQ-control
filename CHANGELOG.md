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
- Versioned, allowlisted diagnostics reports with atomic dashboard export
- Locked dependency restore, comprehensive automated tests, CodeQL, and accessibility contracts
- Manually generated, short-lived, broker-free read-only preview artifact

### Changed

- Migrated the active application from .NET 9 to .NET 10 WPF
- Relicensed the active rebuild under GPL-3.0-or-later
- Quarantined the old C# writer prototype behind a fail-closed global policy
- Kept Lenovo Legion Toolkit as a pinned reference-only submodule
- Selected built-in Windows PowerShell CIM cmdlets instead of a restricted MI runtime
- Selected WPF software rendering while the failing hardware-composition path is qualified
- Replaced reflection-based inventory JSON with an explicit privacy-bounded export contract

### Security

- Hardware writes, profile application, and automation execution remain absent
- Broker IPC uses a random pipe, current-user ACL, one-time nonce, strict framing, and
  mutual process-ID checks
- WMI reads use fixed getter names, static script text, capped output, and bounded execution
- Public release packaging is blocked until broker signing and protected installation ACLs
  are implemented

---

## [0.1.0] - Rust Prototype (Archived)

### Added
- Rust-based CLI and GUI prototype
- WMI bindings for device detection
- Basic thermal profile reading
- Privacy spoiler UI

---

*See [rust_prototype/CHANGELOG.md](rust_prototype/CHANGELOG.md) for detailed Rust prototype history.*
