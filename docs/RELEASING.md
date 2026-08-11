# Release Process

## Artifact classes

The repository currently produces two deliberately different artifact classes:

1. **Development build** — local `dotnet build` output. It includes the unsigned elevated
   read broker for explicit hardware-boundary validation and must not be redistributed.
2. **Read-only preview artifact** — a manually dispatched, seven-day CI artifact. It
   excludes every broker file and supports only unelevated inventory and diagnostics
   export, profile preview, and automation preview.

There is no public production release workflow yet. Pushing a Git tag does not publish an
artifact or create a GitHub release.

## Read-only preview

Run the `Read-only Preview Artifact` workflow manually from GitHub Actions. It:

1. restores locked dependencies with .NET `10.0.302`;
2. builds the full solution and runs all tests;
3. enforces static accessibility and UI safety contracts;
4. publishes the framework-dependent WPF shell with
   `IncludeBrokerArtifacts=false`;
5. adds the GPL license, exact runtime-package license and notice files, safety policy,
   security policy, and preview boundary notice;
6. verifies the published version and requires notice coverage for every runtime package;
7. records the source commit, dirty state, SDK, version, runtime package versions, and
   enforced safety flags in `BUILD-INFO.json`;
8. generates `SHA256SUMS.txt`;
9. fails if a required file is absent, debug symbols are present, or any
   `LegionLoqControl.Broker*` file is present;
10. launches the packaged executable and requires a responsive main window; and
11. uploads a private workflow artifact retained for seven days.

The preview requires the .NET 10 Desktop Runtime. It is unsigned and is not an installer.
See [`READ_ONLY_PREVIEW.md`](READ_ONLY_PREVIEW.md) for the user-facing boundary.

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

Build & Test repeats the broker-free packaging and startup checks on every branch push. Also
confirm that the branch's Build & Test and CodeQL workflows pass.

## Production release blockers

A public package containing the elevated broker is prohibited until all of these are
implemented and reviewed:

- Authenticode signing for the UI, broker, installer, and update metadata;
- administrator-protected installation directories and ACL verification;
- a signed, versioned installer with clean install, upgrade, rollback, and uninstall tests;
- release provenance, checksums, software bill of materials, and reproducible inputs;
- exact supported-model and BIOS declarations;
- accessibility and startup smoke tests on supported Windows versions and scaling modes;
- update authenticity and rollback design; and
- every applicable write-path gate in [`../SAFETY.md`](../SAFETY.md).

Do not convert the preview workflow into a tag-triggered GitHub release by merely restoring
the old archive step. Public release automation must verify these gates rather than assume
them.

## Versioning

Public versions will follow Semantic Versioning once the first supported release scope is
approved. CI previews use `0.0.0-preview.<run-number>` and do not establish a public API or
support promise. Update [`../CHANGELOG.md`](../CHANGELOG.md) before any versioned release.
