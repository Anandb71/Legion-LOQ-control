# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-08-15

First public development preview of the .NET 10 rebuild. The Windows zip includes the
unsigned session broker and is **not** an Authenticode production install. Linux ships
portable `net10.0` libraries and CI coverage only; there is no Linux hardware GUI.

### Added

- Layered .NET 10 rebuild with platform-neutral Domain and Application projects
- Serial-free machine identity, Lenovo WMI metadata, and HID product-ID diagnostics
- Typed battery, thermal, display-overdrive, and integrated-GPU state results
- Session-lived authenticated elevation broker (one UAC prompt per app session)
- Exact zero-access EnergyDrv battery-mode reader and typed battery-mode writer
- Unelevated precision dashboard with explicit brokered state refresh and apply
- POWER, INPUT, LIGHTING, DISPLAY, and DEVICE workspaces with no ads, warranty, or serials
- Overnight charge, Fn lock, Always-on USB, touchpad lock, and Win-key lock, omitted
  when the machine does not support them
- 4-zone lighting editor (effect, speed, divide-area colors) with live color swatches
- Bounded fan-curve apply with a local OEM snapshot and Restore OEM
- Strict local battery and thermal profile drafts with typed previews and Apply
- Strict local AC/battery automation rules with an opt-in session watcher
- Versioned atomic JSON stores for profiles and automation rules
- Live AC/battery glance plus unelevated CPU, RAM, and system-disk numbers
- Product mark (SVG + Windows `.ico`) used as the application icon
- Public Windows zip and Linux portable library tarball
- Ubuntu CI for Application tests

### Fixed

- Lighting, profile, and automation combo boxes showed `NamedValue ( Label = … )`
  instead of the label
- Dashboard and page titles clipped the top of Bahnschrift 28 px glyphs
- Build & Test failed because preview packaging required a Unix MI runtime notice
  that Windows artifacts do not ship

### Changed

- Apply rebases onto the live firmware capture, so a click does not require Refresh first
- Migrated the active application from .NET 9 to .NET 10 WPF
- Relicensed the active rebuild under GPL-3.0-or-later
- Keep one elevated broker for the app session so Windows asks once at start
- Navigation labels `PROFILE` and `AUTOMATION` stay on one row

### Security

- Fan-table writes require a persisted OEM snapshot and stay bounded to 1–10 points
- Spectrum HID is hidden unless a 960-byte non-4-zone C9xx collection is present
- Device identity copy is limited to manufacturer, model, machine type, and BIOS
- Broker IPC uses a random pipe, current-user ACL, one-time nonce, strict framing, and
  mutual process-ID checks
- Production-mode broker launches are refused when the sibling path is unsigned or
  user-writable; this public zip stays in development mode

---

## [0.1.0] - Rust Prototype (Archived)

### Added
- Rust-based CLI and GUI prototype
- WMI bindings for device detection
- Basic thermal profile reading
- Privacy spoiler UI

---

*See [rust_prototype/CHANGELOG.md](rust_prototype/CHANGELOG.md) for detailed Rust prototype history.*
