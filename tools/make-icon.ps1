#Requires -Version 7
<#
.SYNOPSIS
    Draws assets/icon/wtt.svg's geometry with GDI+ and writes the multi-size assets/icon/wtt.ico.

.DESCRIPTION
    Sizes 16, 20, 24, 32, 40, 48, 64 are 32-bit bitmaps, 256 is a PNG. The geometry is the SVG's on its
    256 x 256 grid; at 24 px and below the ring, hands and centre dot are drawn a little thicker so they stay
    readable. -PreviewDir also writes one PNG per size there (not committed).

.EXAMPLE
    pwsh tools/make-icon.ps1
#>
[CmdletBinding()]
param([string] $PreviewDir)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'assets/icon/wtt.ico'
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

# Stroke widths and dot radius on the 256 grid: the SVG's own, and the hinted ones for the small sizes.
function Get-Weights([int] $size) {
    if ($size -le 20) { return @{ Ring = 32; Hand = 24; Dot = 15 } }
    if ($size -le 24) { return @{ Ring = 28; Hand = 20; Dot = 13 } }
    return @{ Ring = 22; Hand = 14; Dot = 11 }
}

function New-Color([string] $hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

function New-RoundedSquare([float] $x, [float] $y, [float] $side, [float] $radius) {
    $d = 2 * $radius
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $side - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $side - $d, $y + $side - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $side - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Pen([string] $hex, [float] $width) {
    $pen = [System.Drawing.Pen]::new((New-Color $hex), $width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    return $pen
}

function New-IconBitmap([int] $size) {
    $w = Get-Weights $size
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    $plate = New-RoundedSquare 8 8 240 56
    $g.FillPath([System.Drawing.SolidBrush]::new((New-Color '#1f2a33')), $plate)
    $g.DrawEllipse((New-Pen '#33434f' $w.Ring), 50, 50, 156, 156)
    # SVG arc from 12 o'clock, clockwise, large, to (52.4, 147.5): GDI+ angles run clockwise from 3 o'clock.
    $end = [Math]::Atan2(147.5 - 128, 52.4 - 128) * 180 / [Math]::PI
    $g.DrawArc((New-Pen '#5fb894' $w.Ring), 50, 50, 156, 156, -90, ($end + 360 + 90) % 360)
    $hand = New-Pen '#ffffff' $w.Hand
    $g.DrawLine($hand, 128, 128, 128, 82)
    $g.DrawLine($hand, 128, 128, 160, 146)
    $g.FillEllipse([System.Drawing.SolidBrush]::new([System.Drawing.Color]::White), 128 - $w.Dot, 128 - $w.Dot, 2 * $w.Dot, 2 * $w.Dot)
    $g.Dispose()
    return $bitmap
}

function Get-PngBytes([System.Drawing.Bitmap] $bitmap) {
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return ,$stream.ToArray()
}

# An ICO image entry: BITMAPINFOHEADER (height doubled), bottom-up BGRA pixels, then a 1-bit AND mask left at zero.
function Get-DibBytes([System.Drawing.Bitmap] $bitmap) {
    $size = $bitmap.Width
    $maskRow = [int]([Math]::Ceiling($size / 32.0) * 4)
    $stream = [System.IO.MemoryStream]::new()
    $out = [System.IO.BinaryWriter]::new($stream)
    $out.Write([uint32] 40); $out.Write([int32] $size); $out.Write([int32] (2 * $size))
    $out.Write([uint16] 1); $out.Write([uint16] 32); $out.Write([uint32] 0)
    $out.Write([uint32] ($size * $size * 4 + $maskRow * $size))
    $out.Write([int32] 0); $out.Write([int32] 0); $out.Write([uint32] 0); $out.Write([uint32] 0)
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $c = $bitmap.GetPixel($x, $y)
            $out.Write([byte[]] @($c.B, $c.G, $c.R, $c.A))
        }
    }
    $out.Write([byte[]]::new($maskRow * $size))
    $out.Flush()
    return ,$stream.ToArray()
}

$images = foreach ($size in $sizes) {
    $bitmap = New-IconBitmap $size
    if ($PreviewDir) {
        New-Item -ItemType Directory -Force $PreviewDir | Out-Null
        $bitmap.Save((Join-Path $PreviewDir "wtt-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    [pscustomobject]@{ Size = $size; Data = [byte[]] $(if ($size -ge 256) { Get-PngBytes $bitmap } else { Get-DibBytes $bitmap }) }
    $bitmap.Dispose()
}

$file = [System.IO.MemoryStream]::new()
$out = [System.IO.BinaryWriter]::new($file)
$out.Write([uint16] 0); $out.Write([uint16] 1); $out.Write([uint16] $images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $out.Write([byte] $dimension); $out.Write([byte] $dimension); $out.Write([byte] 0); $out.Write([byte] 0)
    $out.Write([uint16] 1); $out.Write([uint16] 32)
    $out.Write([uint32] $image.Data.Length); $out.Write([uint32] $offset)
    $offset += $image.Data.Length
}
foreach ($image in $images) { $out.Write([byte[]] $image.Data) }
$out.Flush()
New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
[System.IO.File]::WriteAllBytes($target, $file.ToArray())
Write-Host "Wrote $target ($($sizes -join ', '))"
