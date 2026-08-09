# Read-only Preview Artifact

This is a short-lived CI artifact for evaluating the unelevated rebuild. It is not a stable
release, installer, or production package.

The preview deliberately excludes every `LegionLoqControl.Broker*` file because the elevated
broker is not yet signed or installed under administrator-protected filesystem ACLs.
Consequently, the dashboard's elevated hardware-state refresh reports that the broker is
unavailable. Serial-free inventory, local profile previews, AC/battery observation, and
deterministic automation previews remain available without elevation.

The artifact:

- requires the .NET 10 Desktop Runtime on Windows;
- contains no installer, service, updater, background watcher, or automation runner;
- performs no hardware writes and has no profile Apply path;
- is framework-dependent and unsigned;
- includes `BUILD-INFO.json` plus `SHA256SUMS.txt` for provenance and integrity checks; and
- is retained by CI for seven days only.

Do not redistribute it as a production release. Public release packaging remains blocked on
broker signing, protected installation ACLs, release provenance, and final compatibility
validation.
