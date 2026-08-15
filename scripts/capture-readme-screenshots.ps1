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
using System.IO;
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

    public static void MosaicScreenRects(string path, IntPtr hwnd, string packedRects)
    {
        if (string.IsNullOrWhiteSpace(packedRects))
        {
            return;
        }

        RECT window;
        if (!GetWindowRect(hwnd, out window))
        {
            throw new InvalidOperationException("GetWindowRect failed.");
        }

        byte[] bytes = File.ReadAllBytes(path);
        Bitmap editable;
        using (var buffer = new MemoryStream(bytes))
        using (var loaded = new Bitmap(buffer))
        {
            editable = new Bitmap(loaded.Width, loaded.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(editable))
            {
                graphics.DrawImage(loaded, 0, 0, loaded.Width, loaded.Height);
            }
        }

        const int inflate = 12;
        const int block = 14;
        foreach (string part in packedRects.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            string[] numbers = part.Split(',');
            if (numbers.Length != 4)
            {
                continue;
            }

            int screenLeft = int.Parse(numbers[0]);
            int screenTop = int.Parse(numbers[1]);
            int screenRight = int.Parse(numbers[2]);
            int screenBottom = int.Parse(numbers[3]);
            int left = Math.Max(0, screenLeft - window.Left - inflate);
            int top = Math.Max(0, screenTop - window.Top - inflate);
            int right = Math.Min(editable.Width, screenRight - window.Left + inflate);
            int bottom = Math.Min(editable.Height, screenBottom - window.Top + inflate);
            if (right - left < 2 || bottom - top < 2)
            {
                continue;
            }

            Mosaic(editable, left, top, right, bottom, block);
        }

        editable.Save(path, ImageFormat.Png);
        editable.Dispose();
    }

    private static void Mosaic(Bitmap bitmap, int left, int top, int right, int bottom, int block)
    {
        for (int y = top; y < bottom; y += block)
        {
            int blockHeight = Math.Min(block, bottom - y);
            for (int x = left; x < right; x += block)
            {
                int blockWidth = Math.Min(block, right - x);
                long red = 0;
                long green = 0;
                long blue = 0;
                long alpha = 0;
                int count = 0;
                for (int yy = y; yy < y + blockHeight; yy++)
                {
                    for (int xx = x; xx < x + blockWidth; xx++)
                    {
                        Color pixel = bitmap.GetPixel(xx, yy);
                        red += pixel.R;
                        green += pixel.G;
                        blue += pixel.B;
                        alpha += pixel.A;
                        count++;
                    }
                }

                Color average = Color.FromArgb(
                    (int)(alpha / count),
                    (int)(red / count),
                    (int)(green / count),
                    (int)(blue / count));
                for (int yy = y; yy < y + blockHeight; yy++)
                {
                    for (int xx = x; xx < x + blockWidth; xx++)
                    {
                        bitmap.SetPixel(xx, yy, average);
                    }
                }
            }
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
    $inventoryDeadline = (Get-Date).AddSeconds(20)
    $sawInventory = $false
    while ((Get-Date) -lt $inventoryDeadline) {
        $textBlocks = $window.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            $kind)
        foreach ($element in $textBlocks) {
            $name = $element.Current.Name
            if ($name -like "Inventory complete*") {
                $sawInventory = $true
                break
            }
        }
        if ($sawInventory) {
            break
        }
        Start-Sleep -Milliseconds 250
    }

    $sessionDeadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $sessionDeadline) {
        $textBlocks = $window.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            $kind)
        foreach ($element in $textBlocks) {
            $name = $element.Current.Name
            if ($name -like "Hardware session ready*" -or
                $name -like "Hardware session partially ready*" -or
                $name -like "Hardware state verified*" -or
                $name -like "*later changes will not ask again*" -or
                $name -like "Elevation cancelled*" -or
                $name -like "Hardware state unavailable*") {
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

    Start-Sleep -Milliseconds 500
}

function Get-RedactPackedRects {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        "readme-redact")
    $elements = $window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition)
    $parts = New-Object System.Collections.Generic.List[string]
    foreach ($element in $elements) {
        $rect = $element.Current.BoundingRectangle
        if ($rect.Width -lt 2 -or $rect.Height -lt 2) {
            continue
        }

        $left = [int][Math]::Floor($rect.X)
        $top = [int][Math]::Floor($rect.Y)
        $right = [int][Math]::Ceiling($rect.X + $rect.Width)
        $bottom = [int][Math]::Ceiling($rect.Y + $rect.Height)
        $parts.Add("$left,$top,$right,$bottom")
    }

    return [string]::Join(";", $parts)
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
    $packed = Get-RedactPackedRects
    if (-not [string]::IsNullOrWhiteSpace($packed)) {
        [WindowCapture]::MosaicScreenRects($path, $hwnd, $packed)
        Write-Host "Wrote $path (identity redacted)"
    }
    else {
        Write-Host "Wrote $path"
    }
}

if ($startedHere) {
    Get-Process -Name LegionLoqControl -ErrorAction SilentlyContinue |
        Stop-Process -Force
}
