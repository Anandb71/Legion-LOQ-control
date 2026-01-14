# Security Policy

## Safety Philosophy

**"SAFETY IS THE FIRST PRIORITY!!"**

This tool interacts directly with hardware via WMI and IOCTLs. We take this responsibility seriously.
*   **Opt-In Writers**: Write operations are protected by a global lock and must be explicitly enabled.
*   **Validation**: Inputs are rigorously validated against known safe ranges before being sent to the hardware.

## Reporting a Vulnerability

If you discover a security vulnerability or a safety issue (e.g., a bug that could cause hardware damage), please report it immediately.

**DO NOT create a public GitHub issue for critical safety exploits.**

Instead, please email the maintainer directly or create a **Private Advisory** on GitHub if enabled.

## Supported Versions

| Version | Supported |
| :--- | :--- |
| 0.2.x | ✅ |
| 0.1.x | ❌ |

## Disclaimer

This software is provided "as is", without warranty of any kind. While we strive for perfection, interacting with low-level hardware always carries some risk. Use at your own risk.
