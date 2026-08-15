[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputPath,

    [string] $Version = "0.3.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src/LegionLoqControl.Application/LegionLoqControl.Application.csproj"
$resolvedOutput = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(
    $OutputPath)

if (Test-Path $resolvedOutput) {
    if (@(Get-ChildItem $resolvedOutput -Force).Count -ne 0) {
        throw "Linux portable output directory must be empty: $resolvedOutput"
    }
}
else {
    New-Item -ItemType Directory -Path $resolvedOutput | Out-Null
}

& dotnet publish $projectPath `
    --configuration Release `
    --output $resolvedOutput `
    --framework net10.0 `
    "-p:Version=$Version" `
    "-p:DebugType=None" `
    "-p:DebugSymbols=false"

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$documents = @(
    "LICENSE",
    "THIRD-PARTY-NOTICES.md",
    "docs/LINUX.md",
    "docs/releases/v0.3.0.md"
)
foreach ($relativePath in $documents) {
    Copy-Item (Join-Path $repositoryRoot $relativePath) $resolvedOutput
}

$readme = @"
Legion + LOQ Control $Version — Linux portable libraries

These net10.0 Domain and Application assemblies do not talk to Lenovo firmware.
Hardware control requires the Windows zip and the elevated session broker.
See LINUX.md.
"@
[System.IO.File]::WriteAllText(
    (Join-Path $resolvedOutput "README.txt"),
    "$readme$([Environment]::NewLine)",
    [System.Text.UTF8Encoding]::new($false))

$required = @(
    "LegionLoqControl.Application.dll",
    "LegionLoqControl.Domain.dll",
    "LICENSE",
    "LINUX.md"
)
foreach ($name in $required) {
    if (-not (Test-Path (Join-Path $resolvedOutput $name) -PathType Leaf)) {
        throw "Missing required Linux portable artifact: $name"
    }
}

Write-Host "Linux portable libraries verified at $resolvedOutput"
