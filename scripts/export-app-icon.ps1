[CmdletBinding()]
param(
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot "LegionLoqControl/Assets/logo.ico"
}

function New-LogoBitmap([int] $size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        if ($size -ge 48) {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        }
        else {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        }

        $canvas = [System.Drawing.Color]::FromArgb(255, 0x0B, 0x0D, 0x0F)
        $border = [System.Drawing.Color]::FromArgb(255, 0x27, 0x31, 0x3A)
        $cyan = [System.Drawing.Color]::FromArgb(255, 0x5C, 0xC8, 0xE8)
        $graphics.Clear($canvas)

        $unit = $size / 32.0
        $penWidth = [Math]::Max(1, [int][Math]::Round($unit))
        $pen = New-Object System.Drawing.Pen $border, $penWidth
        try {
            $inset = $penWidth / 2.0
            $graphics.DrawRectangle(
                $pen,
                $inset,
                $inset,
                $size - $penWidth,
                $size - $penWidth)
        }
        finally {
            $pen.Dispose()
        }

        $brush = New-Object System.Drawing.SolidBrush $cyan
        try {
            $rects = @(
                @(8, 7, 4, 18),
                @(8, 21, 12, 4),
                @(21, 8, 3, 9),
                @(18, 11, 9, 3)
            )
            foreach ($rect in $rects) {
                $x = [int][Math]::Round($rect[0] * $unit)
                $y = [int][Math]::Round($rect[1] * $unit)
                $width = [Math]::Max(1, [int][Math]::Round($rect[2] * $unit))
                $height = [Math]::Max(1, [int][Math]::Round($rect[3] * $unit))
                $graphics.FillRectangle($brush, $x, $y, $width, $height)
            }
        }
        finally {
            $brush.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function ConvertTo-PngBytes([System.Drawing.Bitmap] $bitmap) {
    $stream = New-Object System.IO.MemoryStream
    try {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

function Save-PngIcon([string] $path, [byte[][]] $images, [int[]] $sizes) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $count = $images.Count
    $headerSize = 6
    $entrySize = 16
    $offset = $headerSize + ($entrySize * $count)
    $offsets = New-Object int[] $count
    for ($i = 0; $i -lt $count; $i++) {
        $offsets[$i] = $offset
        $offset += $images[$i].Length
    }

    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    $writer = New-Object System.IO.BinaryWriter $stream
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$count)
        for ($i = 0; $i -lt $count; $i++) {
            $size = $sizes[$i]
            $widthByte = if ($size -ge 256) { [byte]0 } else { [byte]$size }
            $writer.Write($widthByte)
            $writer.Write($widthByte)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$images[$i].Length)
            $writer.Write([uint32]$offsets[$i])
        }

        foreach ($image in $images) {
            $writer.Write($image)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$sizes = @(16, 32, 48, 256)
$bitmaps = @()
$pngs = @()
try {
    foreach ($size in $sizes) {
        $bitmap = New-LogoBitmap $size
        $bitmaps += $bitmap
        $pngs += ,(ConvertTo-PngBytes $bitmap)
    }

    Save-PngIcon $OutputPath $pngs $sizes
}
finally {
    foreach ($bitmap in $bitmaps) {
        $bitmap.Dispose()
    }
}

Write-Host "Wrote icon $OutputPath"
