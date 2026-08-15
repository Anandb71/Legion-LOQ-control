# Read-only Preview Artifact

This is a short-lived CI artifact for evaluating the unelevated rebuild. It is not a stable
release, installer, or production package.

The preview deliberately excludes every `LegionLoqControl.Broker*` file because the elevated
broker is not yet signed or installed under administrator-protected filesystem ACLs.
Consequently, the dashboard's elevated hardware-state refresh reports that the broker is
unavailable. Serial-free inventory, local profile previews, AC/battery observation, and
deterministic automation previews remain available without elevation. The dashboard can
also atomically export its retained serial-free inventory; hardware state remains
`notCaptured` because the broker is absent.

The artifact:

- requires the .NET 10 Desktop Runtime on Windows;
- contains no installer, service, updater, background watcher, or automation runner;
- performs no hardware writes and has no profile Apply path;
- exports only an explicit, versioned diagnostics allowlist and never triggers a fresh
  hardware read, elevation request, profile-store read, or upload;
- is framework-dependent and unsigned;
- carries the restored runtime packages' exact license and notice files;
- includes `BUILD-INFO.json` plus `SHA256SUMS.txt` for provenance and integrity checks; and
- is retained by CI for seven days only.

Do not redistribute the broker-free preview as a production release. The public 0.3.0
GitHub Release is a separate, unsigned development-mode zip that does include the broker.
Authenticode, protected installation ACLs, and a signed installer remain later gates.
