[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$viewRoot = Join-Path $repositoryRoot "LegionLoqControl"
$files = @(
    Get-Item (Join-Path $viewRoot "MainWindow.xaml")
    Get-ChildItem (Join-Path $viewRoot "Views") -File -Filter "*.xaml"
)

$presentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
$automationNamespace = "clr-namespace:System.Windows.Automation;assembly=PresentationCore"
$interactiveControls = @(
    "Button",
    "CheckBox",
    "ComboBox",
    "ListBox",
    "TabControl",
    "TextBox"
)
$failures = [System.Collections.Generic.List[string]]::new()
$checkedControls = 0

foreach ($file in $files) {
    [xml] $document = Get-Content -LiteralPath $file.FullName -Raw
    $namespaces = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
    $namespaces.AddNamespace("presentation", $presentationNamespace)

    $interactiveXPath = ($interactiveControls |
        ForEach-Object { "//presentation:$_" }) -join " | "
    $nodes = @($document.SelectNodes($interactiveXPath, $namespaces))

    foreach ($node in $nodes) {
        $checkedControls++
        $accessibleName = @($node.Attributes | Where-Object {
            $_.LocalName -eq "AutomationProperties.Name" -and
            $_.NamespaceURI -eq $automationNamespace
        })

        if ($accessibleName.Count -ne 1 -or
            [string]::IsNullOrWhiteSpace($accessibleName[0].Value)) {
            $failures.Add(
                "$($file.Name): <$($node.LocalName)> requires a non-empty AutomationProperties.Name.")
        }
    }

    $tabs = @($document.SelectNodes("//presentation:TabItem", $namespaces))
    foreach ($tab in $tabs) {
        if ([string]::IsNullOrWhiteSpace($tab.GetAttribute("Header"))) {
            $failures.Add("$($file.Name): every <TabItem> requires a non-empty Header.")
        }
    }

    $commands = @($document.SelectNodes("//*[@Command]", $namespaces))
    foreach ($node in $commands) {
        $command = $node.GetAttribute("Command")
        if ($command -match "(?i)(Apply|Execute|Write).*Command" -and
            $command -notmatch "ApplyOptionCommand" -and
            $command -notmatch "ApplyProfileCommand") {
            $failures.Add(
                "$($file.Name): prohibited write-capable command binding '$command'.")
        }
    }
}

if ($failures.Count -ne 0) {
    throw ($failures -join [Environment]::NewLine)
}

Write-Host "Accessibility contracts passed for $checkedControls interactive controls."
