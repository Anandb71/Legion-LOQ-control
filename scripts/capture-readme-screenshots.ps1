[CmdletBinding()]
param(
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "docs/screenshots"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$exe = Join-Path $repositoryRoot "LegionLoqControl/bin/Release/net10.0-windows/LegionLoqControl.exe"
if (-not (Test-Path $exe)) {
    & dotnet build (Join-Path $repositoryRoot "LegionLoqControl/LegionLoqControl.csproj") `
        --configuration Release
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

try {
Add-Type @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class WindowCapture
{
    public const uint PW_RENDERFULLCONTENT = 2;

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static void Save(IntPtr hwnd, string path)
    {
        ShowWindow(hwnd, 9);
        SetForegroundWindow(hwnd);
        RECT rect;
        if (!GetWindowRect(hwnd, out rect))
        {
            throw new InvalidOperationException("GetWindowRect failed.");
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Window has no client area.");
        }

        using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = graphics.GetHdc();
            try
            {
                if (!PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT))
                {
                    throw new InvalidOperationException("PrintWindow failed.");
                }
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }

            bitmap.Save(path, ImageFormat.Png);
        }
    }
}
"@ -ReferencedAssemblies System.Drawing
}
catch {
    if ($_.Exception.Message -notlike "*already exists*") {
        throw
    }
}

function Get-MainWindow {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $name = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        "Legion + LOQ Control")
    $kind = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Window)
    $and = New-Object System.Windows.Automation.AndCondition($name, $kind)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $and)
}

function Wait-ForIdleDashboard {
    $kind = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline) {
        $textBlocks = $window.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            $kind)
        foreach ($element in $textBlocks) {
            $name = $element.Current.Name
            if ($name -like "Inventory complete*") {
                Start-Sleep -Milliseconds 1200
                return
            }
        }
        Start-Sleep -Milliseconds 250
    }
}

function Select-PrimaryTab([string] $header) {
    $name = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $header)
    $kind = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::TabItem)
    $and = New-Object System.Windows.Automation.AndCondition($name, $kind)
    $tab = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $and)
    if ($null -eq $tab) {
        throw "Tab '$header' was not found."
    }

    try {
        $pattern = $tab.GetCurrentPattern(
            [System.Windows.Automation.SelectionItemPattern]::Pattern)
        $pattern.Select()
    }
    catch {
        $pattern = $tab.GetCurrentPattern(
            [System.Windows.Automation.InvokePattern]::Pattern)
        $pattern.Invoke()
    }

    Start-Sleep -Milliseconds 400
}

$existing = Get-Process -Name LegionLoqControl -ErrorAction SilentlyContinue
$startedHere = $false
if ($null -eq $existing) {
    Start-Process -FilePath $exe
    $startedHere = $true
}

$window = $null
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 250
    $window = Get-MainWindow
    if ($null -ne $window) {
        break
    }
}

if ($null -eq $window) {
    throw "The Legion + LOQ Control window did not appear."
}

$hwnd = [IntPtr]$window.Current.NativeWindowHandle
Wait-ForIdleDashboard
Start-Sleep -Seconds 2

$captures = @(
    @{ File = "dashboard.png"; Tab = "DASHBOARD" },
    @{ File = "power.png"; Tab = "POWER" },
    @{ File = "lighting.png"; Tab = "LIGHTING" },
    @{ File = "device.png"; Tab = "DEVICE" }
)

foreach ($capture in $captures) {
    Select-PrimaryTab $capture.Tab
    $path = Join-Path $OutputDirectory $capture.File
    [WindowCapture]::Save($hwnd, $path)
    Write-Host "Wrote $path"
}

if ($startedHere) {
    Get-Process -Name LegionLoqControl -ErrorAction SilentlyContinue |
        Stop-Process -Force
}
