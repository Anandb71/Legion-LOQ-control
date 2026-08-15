# Security Policy

## Safety status

The current rebuild is write-gated. Inventory, refresh, preview, and export stay
read-only. Dashboard apply may change thermal mode, display overdrive, integrated-GPU
mode, battery charge mode, or 4-zone keyboard brightness only after an explicit click, a
UAC prompt, an expected-state check, and a readback. The same privileged refresh can read
the OEM fan table. Fan-table writes, per-zone colors, profile Apply, and automation
execution remain disabled until the remaining controls in [SAFETY.md](SAFETY.md) pass
their release gates.

Do not publish a build that bypasses `HardwareWritePolicy`.
Do not publish the elevated broker until it is signed and installed in an
administrator-protected directory. The install policy in
[`docs/BROKER_INSTALL.md`](docs/BROKER_INSTALL.md) distinguishes a development sibling
from a production install and refuses production-mode launches from user-writable or
unsigned locations. The current broker is a development validation path. `--write` may
run one allowlisted setter or the typed battery write; it must not gain a generic IOCTL
or caller-supplied WMI name.

The temporary CIM bridge may launch only the built-in system Windows PowerShell executable
with profiles disabled, a system-only module path, and the repository's static encoded
script. Caller-provided PowerShell, WMI identifiers, arguments, modules, or paths are
security violations.

The broker's battery reader may issue only IOCTL `0x831020F8` with selector `0xFF` through
an EnergyDrv handle opened with zero requested access. The typed battery writer uses the
same control code with only selectors `0x03`, `0x05`, `0x07`, and `0x08` on a
`GENERIC_READ | GENERIC_WRITE` handle. A generic driver API, caller-provided control
code/input, or use outside the elevated broker is a security violation.

Local profile and automation JSON is untrusted input. The stores cap file size and entry
count, reject unknown members and numeric enums, validate all domain values, and replace
files atomically. Load and save operations take a same-directory exclusive lock so two
app instances cannot silently overwrite each other; a busy lock is reported instead of a
partial write. Names and rule fields are data only; they must never become executable
commands, scripts, protocol identifiers, or broker requests.

Diagnostics export maps retained typed snapshots into an explicit versioned allowlist. It
must not serialize domain objects by reflection, include observation details or local
drafts, trigger a new hardware read, launch the broker, display destination paths, or write
an unbounded document. The current writer caps output at 256 KiB and uses a same-directory
temporary file plus atomic rename.

GitHub Actions dependencies are pinned to full commit SHAs, and NuGet restore uses committed
lock files. Preview packaging fails closed if the unsigned broker or debug symbols enter the
artifact, then records build provenance and SHA-256 checksums before a startup smoke test.

## Reporting a Vulnerability

If you discover a security vulnerability or a safety issue (e.g., a bug that could cause hardware damage), please report it immediately.

**DO NOT create a public GitHub issue for critical safety exploits.**

Use the repository's **Security** tab to create a private GitHub Security Advisory. If
private reporting is unavailable, contact a maintainer without including exploit details
in a public issue.

## Supported Versions

| Version | Supported |
| :--- | :--- |
| `main` rebuild | Security fixes only; not a stable release |
| 0.2.x prototype | Unsupported |
| 0.1.x | Unsupported |

## Disclaimer

This software is provided "as is", without warranty of any kind. While we strive for perfection, interacting with low-level hardware always carries some risk. Use at your own risk.
