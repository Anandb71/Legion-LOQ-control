# Contributing to Legion + LOQ Control

Thank you for your interest in contributing! This project aims to be a trustworthy, safe, and lightweight alternative to manufacturer software. To maintain that trust, we have strict guidelines.

## core Philosophy

1.  **Safety First**: No feature is worth bricking a user's device. If you are touching hardware registers, you must prove it is safe.
2.  **Read-Only First**: Always verify you can read the hardware state correctly before attempting to write to it.
3.  **No Telemetry**: We do not collect user data. Do not add analytics or "crash reporting" that phones home.
4.  **No Bloat**: Keep dependencies minimal. This is a system utility, not a heavy web app.

## Development Setup

1.  **Prerequisites**:
    *   Rust (latest stable)
    *   Windows 10/11 (Target OS)
2.  **Build**:
    ```bash
    cargo build
    ```
3.  **Run with GUI**:
    ```bash
    cargo run -- --gui
    ```

## Submitting Pull Requests

1.  **Format your code**: Run `cargo fmt` before committing.
2.  **Check for warnings**: Run `cargo check` and fix any warnings.
3.  **Describe your changes**: Explain *why* you are making the change, not just *what* you changed.
4.  **Hardware Verification**: If adding support for a new model, include a screenshot or log proving it works on that specific device.

## Code Style

*   Use standard Rust idioms.
*   Comments should explain "why", not "what".
*   Use the `log` crate for logging (info/warn/error), never `println!` for debug info in production code.
