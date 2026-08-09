[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputPath,

    [string] $Version = "0.0.0-preview.local"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Version -notmatch "^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$") {
    throw "Version must be a valid three-part semantic version with an optional suffix."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "LegionLoqControl/LegionLoqControl.csproj"
$resolvedOutput = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(
    $OutputPath)

if ([string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) {
    $commit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
else {
    $commit = $env:GITHUB_SHA
}

$sourceStatus = @(& git -C $repositoryRoot status --porcelain --untracked-files=normal)
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
$sourceDirty = $sourceStatus.Count -ne 0
if ($env:GITHUB_ACTIONS -eq "true" -and $sourceDirty) {
    throw "CI preview packaging requires a clean source checkout."
}

if (Test-Path $resolvedOutput) {
    if (@(Get-ChildItem $resolvedOutput -Force).Count -ne 0) {
        throw "Preview output directory must be empty: $resolvedOutput"
    }
}
else {
    New-Item -ItemType Directory -Path $resolvedOutput | Out-Null
}

& dotnet build $projectPath `
    --configuration Release `
    --no-restore `
    --no-incremental `
    "-p:Version=$Version" `
    "-p:DebugType=None" `
    "-p:DebugSymbols=false" `
    "-p:IncludeBrokerArtifacts=false"

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& dotnet publish $projectPath `
    --configuration Release `
    --output $resolvedOutput `
    --no-build `
    --self-contained false `
    "-p:Version=$Version" `
    "-p:DebugType=None" `
    "-p:DebugSymbols=false" `
    "-p:IncludeBrokerArtifacts=false"

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$documents = @(
    "LICENSE",
    "THIRD-PARTY-NOTICES.md",
    "README.md",
    "SAFETY.md",
    "SECURITY.md",
    "docs/READ_ONLY_PREVIEW.md"
)
foreach ($relativePath in $documents) {
    Copy-Item (Join-Path $repositoryRoot $relativePath) $resolvedOutput
}

$dependencyManifestPath = Join-Path $resolvedOutput "LegionLoqControl.deps.json"
$dependencyManifest = Get-Content $dependencyManifestPath -Raw | ConvertFrom-Json
$expectedProductLibrary = "LegionLoqControl/$Version"
$publishedLibraries = @($dependencyManifest.libraries.PSObject.Properties.Name)
if ($publishedLibraries -notcontains $expectedProductLibrary) {
    throw "Published dependency manifest does not contain version $Version."
}

$runtimePackages = @(
    $dependencyManifest.libraries.PSObject.Properties |
        Where-Object { $_.Value.type -eq "package" } |
        ForEach-Object { $_.Name } |
        Sort-Object)
$thirdPartyNotices = Get-Content `
    (Join-Path $resolvedOutput "THIRD-PARTY-NOTICES.md") -Raw
foreach ($package in $runtimePackages) {
    $packageName = $package.Split("/")[0]
    if ($thirdPartyNotices.IndexOf(
        $packageName,
        [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Third-party notices do not mention runtime package $packageName."
    }
}

$assetsPath = Join-Path $repositoryRoot "LegionLoqControl/obj/project.assets.json"
$assets = Get-Content $assetsPath -Raw | ConvertFrom-Json
$packageRoots = @($assets.packageFolders.PSObject.Properties.Name)
if ($packageRoots.Count -eq 0) {
    throw "NuGet package roots are missing from project.assets.json."
}

$licenseOutput = Join-Path $resolvedOutput "THIRD-PARTY-LICENSES"
New-Item -ItemType Directory -Path $licenseOutput | Out-Null
Copy-Item (Join-Path $repositoryRoot "licenses/LICENSE-MIT.txt") $licenseOutput

foreach ($package in $runtimePackages) {
    $packageParts = @($package -split "/", 2)
    $packageName = $packageParts[0]
    $packageVersion = $packageParts[1]
    $packagePath = $null
    foreach ($root in $packageRoots) {
        $candidate = Join-Path $root (
            "$($packageName.ToLowerInvariant())/$packageVersion")
        if (Test-Path $candidate -PathType Container) {
            $packagePath = $candidate
            break
        }
    }

    if ($null -eq $packagePath) {
        throw "Restored runtime package was not found: $package"
    }

    $packageNotices = @(
        Get-ChildItem $packagePath -File |
            Where-Object { $_.Name -match "^(?i:license|third.?party.?notices)" })
    if ($packageNotices.Count -eq 0) {
        throw "Runtime package does not expose a license or notice file: $package"
    }

    foreach ($notice in $packageNotices) {
        $destinationName = "$packageName-$packageVersion-$($notice.Name)"
        Copy-Item $notice.FullName (Join-Path $licenseOutput $destinationName)
    }
}

$buildInfo = [ordered]@{
    schemaVersion = 1
    product = "Legion + LOQ Control"
    version = $Version
    commit = $commit
    sourceDirty = $sourceDirty
    dotnetSdk = (& dotnet --version).Trim()
    frameworkDependent = $true
    elevatedBrokerIncluded = $false
    hardwareWritesIncluded = $false
    runtimePackages = $runtimePackages
}
$buildInfoJson = $buildInfo | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText(
    (Join-Path $resolvedOutput "BUILD-INFO.json"),
    "$buildInfoJson$([Environment]::NewLine)",
    [System.Text.UTF8Encoding]::new($false))

$required = @(
    "LegionLoqControl.exe",
    "LegionLoqControl.dll",
    "LegionLoqControl.deps.json",
    "LegionLoqControl.runtimeconfig.json",
    "BUILD-INFO.json",
    "LICENSE",
    "THIRD-PARTY-NOTICES.md",
    "THIRD-PARTY-LICENSES/LICENSE-MIT.txt",
    "READ_ONLY_PREVIEW.md",
    "SAFETY.md",
    "SECURITY.md"
)
foreach ($name in $required) {
    if (-not (Test-Path (Join-Path $resolvedOutput $name) -PathType Leaf)) {
        throw "Missing required preview artifact: $name"
    }
}

$brokerArtifacts = @(
    Get-ChildItem $resolvedOutput -File -Filter "LegionLoqControl.Broker*")
if ($brokerArtifacts.Count -ne 0) {
    throw "Unsigned elevated broker artifacts must not enter preview packages."
}

$debugSymbols = @(Get-ChildItem $resolvedOutput -File -Filter "*.pdb")
if ($debugSymbols.Count -ne 0) {
    throw "Debug symbols must not enter preview packages."
}

$outputPrefixLength = $resolvedOutput.TrimEnd("\").Length + 1
$checksumLines = Get-ChildItem $resolvedOutput -File -Recurse |
    Sort-Object FullName |
    ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $relativePath = $_.FullName.Substring($outputPrefixLength).Replace("\", "/")
        "$hash *$relativePath"
    }
$checksumLines |
    Set-Content (Join-Path $resolvedOutput "SHA256SUMS.txt") -Encoding ascii

Write-Host "Read-only preview verified at $resolvedOutput"
