[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputPath,

    [string] $Version = "0.3.0"
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

if (Test-Path $resolvedOutput) {
    if (@(Get-ChildItem $resolvedOutput -Force).Count -ne 0) {
        throw "Release output directory must be empty: $resolvedOutput"
    }
}
else {
    New-Item -ItemType Directory -Path $resolvedOutput | Out-Null
}

& dotnet publish $projectPath `
    --configuration Release `
    --output $resolvedOutput `
    --self-contained false `
    "-p:Version=$Version" `
    "-p:DebugType=None" `
    "-p:DebugSymbols=false" `
    "-p:IncludeBrokerArtifacts=true"

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$documents = @(
    "LICENSE",
    "THIRD-PARTY-NOTICES.md",
    "README.md",
    "SAFETY.md",
    "SECURITY.md",
    "COMPATIBILITY.md",
    "docs/LINUX.md",
    "docs/releases/v0.3.0.md"
)
foreach ($relativePath in $documents) {
    $source = Join-Path $repositoryRoot $relativePath
    if (Test-Path $source) {
        Copy-Item $source $resolvedOutput
    }
}

$dependencyManifestPath = Join-Path $resolvedOutput "LegionLoqControl.deps.json"
$dependencyManifest = Get-Content $dependencyManifestPath -Raw | ConvertFrom-Json
$runtimePackages = @(
    $dependencyManifest.libraries.PSObject.Properties |
        Where-Object { $_.Value.type -eq "package" } |
        ForEach-Object { $_.Name } |
        Where-Object {
            $_ -notmatch '(?i)\.Runtime\.(Unix|Osx|Linux|Android|iOS|Browser)'
        } |
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

$buildInfo = [ordered]@{
    schemaVersion = 1
    product = "Legion + LOQ Control"
    version = $Version
    commit = $commit
    dotnetSdk = (& dotnet --version).Trim()
    frameworkDependent = $true
    runtimeIdentifier = $null
    elevatedBrokerIncluded = $true
    hardwareWritesIncluded = $true
    brokerInstallMode = "development"
    authenticodeSigned = $false
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
    "LegionLoqControl.Broker.exe",
    "LegionLoqControl.Broker.dll",
    "BUILD-INFO.json",
    "LICENSE",
    "THIRD-PARTY-NOTICES.md",
    "SAFETY.md",
    "SECURITY.md"
)
foreach ($name in $required) {
    if (-not (Test-Path (Join-Path $resolvedOutput $name) -PathType Leaf)) {
        throw "Missing required Windows release artifact: $name"
    }
}

$debugSymbols = @(Get-ChildItem $resolvedOutput -File -Filter "*.pdb")
if ($debugSymbols.Count -ne 0) {
    throw "Debug symbols must not enter Windows release packages."
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

Write-Host "Windows development preview verified at $resolvedOutput"
