[CmdletBinding()]
param(
    [string]$Fork = "Anandb71/LenovoLegionToolkit",
    [string]$Upstream = "LenovoLegionToolkit-Team/LenovoLegionToolkit",
    [string]$Branch = "master"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Native {
    param(
        [Parameter(Mandatory)]
        [string]$Command,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$FailureMessage
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code $LASTEXITCODE)"
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$referencePath = Join-Path $repositoryRoot "LLT_Reference"

Push-Location $repositoryRoot
try {
    Invoke-Native git @("submodule", "update", "--init", "--", "LLT_Reference") `
        "Could not initialize LLT_Reference"

    $referenceStatus = (& git -C $referencePath status --porcelain)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect LLT_Reference"
    }

    if ($referenceStatus) {
        throw "LLT_Reference has local changes. Commit or discard them before syncing."
    }

    $oldCommit = (& git -C $referencePath rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Could not resolve the current LLT reference commit."
    }

    Write-Host "Syncing $Fork from $Upstream ($Branch)..."
    Invoke-Native gh @(
        "repo", "sync", $Fork,
        "--source", $Upstream,
        "--branch", $Branch
    ) "Could not fast-forward the LLT fork"

    $forkUrl = "https://github.com/$Fork.git"
    $upstreamUrl = "https://github.com/$Upstream.git"

    Invoke-Native git @("-C", $referencePath, "remote", "set-url", "origin", $forkUrl) `
        "Could not configure the LLT fork remote"

    & git -C $referencePath remote get-url upstream *> $null
    if ($LASTEXITCODE -eq 0) {
        Invoke-Native git @("-C", $referencePath, "remote", "set-url", "upstream", $upstreamUrl) `
            "Could not update the LLT upstream remote"
    }
    else {
        Invoke-Native git @("-C", $referencePath, "remote", "add", "upstream", $upstreamUrl) `
            "Could not add the LLT upstream remote"
    }

    Invoke-Native git @("-C", $referencePath, "fetch", "--prune", "origin", $Branch) `
        "Could not fetch the synced LLT fork"
    Invoke-Native git @("-C", $referencePath, "switch", $Branch) `
        "Could not switch LLT_Reference to $Branch"
    Invoke-Native git @("-C", $referencePath, "merge", "--ff-only", "origin/$Branch") `
        "LLT_Reference cannot be fast-forwarded"
    Invoke-Native git @("-C", $referencePath, "fetch", "--prune", "upstream", $Branch) `
        "Could not fetch LLT upstream metadata"

    $newCommit = (& git -C $referencePath rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Could not resolve the updated LLT reference commit."
    }

    if ($oldCommit -eq $newCommit) {
        Write-Host "LLT_Reference is current at $newCommit."
    }
    else {
        Write-Host "LLT_Reference updated: $oldCommit -> $newCommit"
        Write-Host "Review the upstream diff, update docs/OSS-SOURCES.md, then commit the gitlink."
    }
}
finally {
    Pop-Location
}
