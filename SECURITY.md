# Security Policy

## Safety status

The current rebuild is intentionally read-only. Hardware writes remain disabled until the
isolated broker, capability validation, serialization, readback, and recovery controls in
[SAFETY.md](SAFETY.md) pass their release gates.

Do not publish a build that bypasses `HardwareWritePolicy`.
Do not publish the elevated broker until it is signed and installed in an
administrator-protected directory. The current broker is a read-only development
validation path and must not gain a write dispatcher before the gates in
[SAFETY.md](SAFETY.md) pass review.

The temporary CIM bridge may launch only the built-in system Windows PowerShell executable
with profiles disabled, a system-only module path, and the repository's static encoded
script. Caller-provided PowerShell, WMI identifiers, arguments, modules, or paths are
security violations.

## Reporting a Vulnerability

If you discover a security vulnerability or a safety issue (e.g., a bug that could cause hardware damage), please report it immediately.

**DO NOT create a public GitHub issue for critical safety exploits.**

Use the repository's **Security** tab to create a private GitHub Security Advisory. If
private reporting is unavailable, contact a maintainer without including exploit details
in a public issue.

## Supported Versions

| Version | Supported |
| :--- | :--- |
| `rebuild/v1` | Security fixes only; not a stable release |
| 0.2.x prototype | Unsupported |
| 0.1.x | Unsupported |

## Disclaimer

This software is provided "as is", without warranty of any kind. While we strive for perfection, interacting with low-level hardware always carries some risk. Use at your own risk.
