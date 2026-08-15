# Release Process

## Artifact classes

1. **Development build** — local `dotnet build` output. Includes the unsigned elevated
   broker. Default install mode is development.
2. **Read-only preview artifact** — a manually dispatched, seven-day CI artifact. It
   excludes every broker file and supports only unelevated inventory, diagnostics export,
   profile preview, and automation preview.
3. **Public development preview** — a GitHub Release zip/tarball. The Windows
   zip includes the unsigned broker and is **not** Authenticode-signed. The Linux tarball
   is portable Domain/Application libraries with no firmware access. Tag `v0.3.0` is the
   first public preview.

A production package (signed broker, administrator-protected install, installer, SBOM)
remains a later gate. Do not set `LEGIONLOQ_BROKER_INSTALL_MODE=production` on the public
zip.

## Public development preview

Pushing a `v*` tag runs [`.github/workflows/publish-release.yml`](../.github/workflows/publish-release.yml).
It:

1. runs Application tests on Ubuntu and publishes the Linux portable tarball;
2. restores, builds, tests, and checks accessibility contracts on Windows;
3. verifies the broker-free preview package still packs;
4. publishes `LegionLoqControl.exe` plus the sibling broker
   (framework-dependent, .NET 10 Desktop Runtime required);
5. attaches both archives to the GitHub Release with `docs/releases/v0.3.0.md` as the body.

Local packaging:

```powershell
dotnet restore LegionLoqControl.sln --locked-mode
./scripts/export-app-icon.ps1
$win = "$env:TEMP/LegionLoqControl-win-$([guid]::NewGuid().ToString('N'))"
./scripts/build-windows-release.ps1 -OutputPath $win -Version 0.3.0
$linux = "$env:TEMP/LegionLoqControl-linux-$([guid]::NewGuid().ToString('N'))"
./scripts/build-linux-portable.ps1 -OutputPath $linux -Version 0.3.0
```

## Read-only preview

Run the `Read-only Preview Artifact` workflow manually from GitHub Actions. It remains
broker-free and is retained for seven days. See [`READ_ONLY_PREVIEW.md`](READ_ONLY_PREVIEW.md).

## Local verification

Before pushing a release-foundation change:

```powershell
dotnet restore LegionLoqControl.sln --locked-mode
dotnet build LegionLoqControl.sln --configuration Release --no-restore
./scripts/test-accessibility-contracts.ps1
dotnet test LegionLoqControl.sln --configuration Release --no-build --no-restore
$preview = "$env:TEMP/LegionLoqControl-preview-$([guid]::NewGuid().ToString('N'))"
./scripts/build-read-only-preview.ps1 -OutputPath $preview
./scripts/test-read-only-preview-startup.ps1 -PreviewPath $preview
```

Build & Test repeats the broker-free packaging and startup checks on every branch push.
Ubuntu also runs Application tests. Confirm that Build & Test and CodeQL pass.

## Production release blockers

A package that claims production install is prohibited until all of these are
implemented and reviewed:

- Authenticode signing for the UI, broker, installer, and update metadata;
- administrator-protected installation directories that pass
  [`BROKER_INSTALL.md`](BROKER_INSTALL.md) in production mode;
- a signed, versioned installer with clean install, upgrade, rollback, and uninstall tests;
- release provenance, checksums, software bill of materials, and reproducible inputs;
- exact supported-model and BIOS declarations;
- accessibility and startup smoke tests on supported Windows versions and scaling modes;
- update authenticity and rollback design; and
- every applicable write-path gate in [`../SAFETY.md`](../SAFETY.md).

The 0.3.0 GitHub Release does not satisfy those blockers. It is a public development
preview with an explicit UAC boundary.

## Versioning

Public versions follow Semantic Versioning. `0.3.0` is the first public rebuild preview.
CI read-only previews use `0.0.0-preview.<run-number>` and do not establish a support
promise. Update [`../CHANGELOG.md`](../CHANGELOG.md) before any versioned release.
