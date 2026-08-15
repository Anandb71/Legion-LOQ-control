# Security Policy

## Safety status

Inventory, refresh, preview, and export stay read-only. Dashboard apply may change
allowlisted firmware through the session broker after an explicit click, an expected-state
check, and a readback. Windows asks once when that session starts. Unsupported hardware
is omitted. See [SAFETY.md](SAFETY.md) for the current write set.

Do not publish a build that bypasses `HardwareWritePolicy`.
Do not treat the public 0.3.0 zip as a production install. It includes an unsigned
broker and stays in development mode. Production mode refuses unsigned or user-writable
sibling launches. The install policy is
[`docs/BROKER_INSTALL.md`](docs/BROKER_INSTALL.md). `--write` may
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
broker-free artifact, then records build provenance and SHA-256 checksums before a startup
smoke test.

## Reporting a Vulnerability

If you discover a security vulnerability or a safety issue (e.g., a bug that could cause hardware damage), please report it immediately.

**DO NOT create a public GitHub issue for critical safety exploits.**

Use the repository's **Security** tab to create a private GitHub Security Advisory. If
private reporting is unavailable, contact a maintainer without including exploit details
in a public issue.

## Supported Versions

| Version | Supported |
| :--- | :--- |
| 0.3.x public preview | Security fixes on `main`; unsigned development-mode broker |
| `main` rebuild | Security fixes; not a stable production release |
| 0.2.x / 0.1.x prototypes | Unsupported |

## Disclaimer

This software is provided "as is", without warranty of any kind. While we strive for perfection, interacting with low-level hardware always carries some risk. Use at your own risk.
