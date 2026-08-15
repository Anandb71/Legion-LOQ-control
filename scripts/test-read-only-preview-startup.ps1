[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PreviewPath,

    [ValidateRange(1, 60)]
    [int] $TimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedPreview = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(
    $PreviewPath)
$executable = Join-Path $resolvedPreview "LegionLoqControl.exe"
if (-not (Test-Path $executable -PathType Leaf)) {
    throw "Preview executable was not found: $executable"
}

$brokerArtifacts = @(
    Get-ChildItem $resolvedPreview -File -Filter "LegionLoqControl.Broker*")
if ($brokerArtifacts.Count -ne 0) {
    throw "The startup smoke test accepts broker-free previews only."
}

$process = Start-Process -FilePath $executable -WorkingDirectory $resolvedPreview -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) {
            throw "Preview exited during startup with code $($process.ExitCode)."
        }
    }
    while (
        [string]::IsNullOrWhiteSpace($process.MainWindowTitle) -and
        [DateTime]::UtcNow -lt $deadline)

    if ([string]::IsNullOrWhiteSpace($process.MainWindowTitle)) {
        throw "Preview did not create a main window within $TimeoutSeconds seconds."
    }

    Write-Host "Preview startup passed: $($process.MainWindowTitle)"
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
