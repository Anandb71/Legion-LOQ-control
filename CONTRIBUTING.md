# Contributing

Legion + LOQ Control accepts focused, reviewable changes that preserve the read-only
safety baseline.

## Non-negotiable rules

1. Never bypass `HardwareWritePolicy` or add a write path to the UI or diagnostics CLI.
2. Treat unknown, unavailable, and failed state separately from real hardware values.
3. Do not add raw WMI, IOCTL, HID, scripts, or plugin payloads to broker contracts.
4. Do not collect telemetry, serial numbers, usernames, device paths, or account data.
5. Record protocol provenance and exact evidence before claiming feature/model support.
6. Keep dependencies minimal, centrally pinned, license-compatible, and locked.

## Development workflow

```powershell
dotnet restore LegionLoqControl.sln --locked-mode
dotnet build LegionLoqControl.sln --configuration Release --no-restore
dotnet test LegionLoqControl.sln --configuration Release --no-build --no-restore
```

Use C# with nullable reference types and warnings-as-errors. Put platform-neutral state in
Domain, orchestration in Application, Windows access in Infrastructure, and composition in
the UI or CLI.

## Hardware and protocol changes

A hardware feature pull request must include:

- a completed provenance record;
- stable typed errors and bounded values;
- fake or replay tests for success, malformed data, timeout, and unsupported cases;
- redacted exact machine type, model, and BIOS evidence;
- confirmation that startup, refresh, cancellation, and shutdown send zero writes.

Do not test writes on real hardware until the repository contains an approved procedure
for that feature.

## Pull requests

Explain why the change is needed, its safety boundary, and the verification performed.
Keep unrelated cleanup separate. Do not include generated build output or private machine
data.
