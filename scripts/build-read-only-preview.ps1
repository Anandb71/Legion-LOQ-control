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

if (Test-Path $resolvedOutput) {
    if (@(Get-ChildItem $resolvedOutput -Force).Count -ne 0) {
        throw "Preview output directory must be empty: $resolvedOutput"
    }
}
else {
    New-Item -ItemType Directory -Path $resolvedOutput | Out-Null
}

& dotnet publish $projectPath `
    --configuration Release `
    --output $resolvedOutput `
    --no-restore `
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

$commit = if ([string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) {
    (& git -C $repositoryRoot rev-parse HEAD).Trim()
}
else {
    $env:GITHUB_SHA
}
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$buildInfo = [ordered]@{
    schemaVersion = 1
    product = "Legion + LOQ Control"
    version = $Version
    commit = $commit
    dotnetSdk = (& dotnet --version).Trim()
    frameworkDependent = $true
    elevatedBrokerIncluded = $false
    hardwareWritesIncluded = $false
}
$buildInfoJson = $buildInfo | ConvertTo-Json
[System.IO.File]::WriteAllText(
    (Join-Path $resolvedOutput "BUILD-INFO.json"),
    "$buildInfoJson$([Environment]::NewLine)",
    [System.Text.UTF8Encoding]::new($false))

$required = @(
    "LegionLoqControl.exe",
    "LegionLoqControl.dll",
    "BUILD-INFO.json",
    "LICENSE",
    "THIRD-PARTY-NOTICES.md",
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

$checksumLines = Get-ChildItem $resolvedOutput -File |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash *$($_.Name)"
    }
$checksumLines |
    Set-Content (Join-Path $resolvedOutput "SHA256SUMS.txt") -Encoding ascii

Write-Host "Read-only preview verified at $resolvedOutput"
