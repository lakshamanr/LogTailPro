# generate-icon.ps1 — Generates Assets/app.ico from pure GDI+ drawing
# Run from repo root: powershell -File tools/generate-icon.ps1

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Drawing.Common -ErrorAction SilentlyContinue

function New-RoundedPath {
    param([int]$x, [int]$y, [int]$w, [int]$h, [int]$r)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x,           $y,           $r*2, $r*2, 180, 90)
    $path.AddArc($x+$w-$r*2,  $y,           $r*2, $r*2, 270, 90)
    $path.AddArc($x+$w-$r*2,  $y+$h-$r*2,  $r*2, $r*2,   0, 90)
    $path.AddArc($x,           $y+$h-$r*2,  $r*2, $r*2,  90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-IconBitmap {
    param([int]$size)

    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # --- Background: dark navy rounded square ---
    $radius  = [Math]::Max(2, [int]($size * 0.15))
    $padding = 1
    $bgPath  = New-RoundedPath $padding $padding ($size-$padding*2-1) ($size-$padding*2-1) $radius

    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.Point]::new(0, 0),
        [System.Drawing.Point]::new($size, $size),
        [System.Drawing.Color]::FromArgb(255, 30, 30, 58),
        [System.Drawing.Color]::FromArgb(255, 18, 18, 42)
    )
    $g.FillPath($bgBrush, $bgPath)

    # --- Subtle border ---
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 80, 100, 140), 1)
    $g.DrawPath($borderPen, $bgPath)

    # --- Log lines (cyan #4FC3F7) ---
    $cyan1 = [System.Drawing.Color]::FromArgb(255, 79, 195, 247)
    $cyan2 = [System.Drawing.Color]::FromArgb(200, 41, 182, 246)

    $lx  = [int]($size * 0.20)     # left margin
    $lh  = [Math]::Max(1, [int]($size * 0.09))   # line height
    $lr  = [Math]::Max(1, [int]($lh * 0.4))      # corner radius

    $y1  = [int]($size * 0.22)
    $y2  = [int]($size * 0.39)
    $y3  = [int]($size * 0.55)

    $w1  = [int]($size * 0.66)
    $w2  = [int]($size * 0.47)
    $w3  = [int]($size * 0.32)

    $lineBrush1 = New-Object System.Drawing.SolidBrush($cyan1)
    $lineBrush2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 79, 195, 247))
    $lineBrush3 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(160, 79, 195, 247))

    # Draw 3 log lines (rounded if size > 24, plain rect if tiny)
    if ($size -gt 24) {
        $p1 = New-RoundedPath $lx $y1 $w1 $lh $lr
        $p2 = New-RoundedPath $lx $y2 $w2 $lh $lr
        $p3 = New-RoundedPath $lx $y3 $w3 $lh $lr
        $g.FillPath($lineBrush1, $p1)
        $g.FillPath($lineBrush2, $p2)
        $g.FillPath($lineBrush3, $p3)
        $p1.Dispose(); $p2.Dispose(); $p3.Dispose()
    } else {
        $g.FillRectangle($lineBrush1, $lx, $y1, $w1, $lh)
        $g.FillRectangle($lineBrush2, $lx, $y2, $w2, $lh)
        $g.FillRectangle($lineBrush3, $lx, $y3, $w3, $lh)
    }

    # --- Tail arrow (amber #FFB74D) — downward triangle ---
    $amberBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 183, 77))
    $cx = [int]($size / 2)
    $aw = [int]($size * 0.20)    # half-width
    $at = [int]($size * 0.70)    # arrow top
    $ab = [int]($size * 0.86)    # arrow bottom

    $arrowPts = [System.Drawing.Point[]]@(
        [System.Drawing.Point]::new($cx - $aw, $at),
        [System.Drawing.Point]::new($cx + $aw, $at),
        [System.Drawing.Point]::new($cx,        $ab)
    )
    $g.FillPolygon($amberBrush, $arrowPts)

    # Clean up
    $g.Dispose()
    $bgBrush.Dispose(); $borderPen.Dispose()
    $lineBrush1.Dispose(); $lineBrush2.Dispose(); $lineBrush3.Dispose()
    $amberBrush.Dispose()
    $bgPath.Dispose()

    return $bmp
}

function Write-IcoFile {
    param([string]$outputPath, [System.Drawing.Bitmap[]]$bitmaps)

    # Encode each bitmap as PNG bytes
    $pngChunks = @()
    foreach ($bmp in $bitmaps) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngChunks += , $ms.ToArray()
        $ms.Dispose()
    }

    $count  = $bitmaps.Count
    $stream = [System.IO.File]::Create($outputPath)
    $writer = New-Object System.IO.BinaryWriter($stream)

    # ICONDIR header (6 bytes)
    $writer.Write([uint16]0)       # Reserved = 0
    $writer.Write([uint16]1)       # Type = 1 (ICO)
    $writer.Write([uint16]$count)  # Image count

    # Each ICONDIRENTRY = 16 bytes; image data starts after header + all entries
    $dataOffset = 6 + $count * 16

    for ($i = 0; $i -lt $count; $i++) {
        $bmp    = $bitmaps[$i]
        $width  = if ($bmp.Width  -ge 256) { [byte]0 } else { [byte]$bmp.Width  }
        $height = if ($bmp.Height -ge 256) { [byte]0 } else { [byte]$bmp.Height }
        $writer.Write($width)                            # Width
        $writer.Write($height)                           # Height
        $writer.Write([byte]0)                           # ColorCount (0 = no palette)
        $writer.Write([byte]0)                           # Reserved
        $writer.Write([uint16]1)                         # Planes
        $writer.Write([uint16]32)                        # BitCount
        $writer.Write([uint32]$pngChunks[$i].Length)    # Size of image data
        $writer.Write([uint32]$dataOffset)               # Offset to image data
        $dataOffset += $pngChunks[$i].Length
    }

    # Write PNG data for each image
    foreach ($chunk in $pngChunks) {
        $writer.Write($chunk)
    }

    $writer.Dispose()
    $stream.Dispose()
}

# ── Main ───────────────────────────────────────────────────────────────────────
$outputIco = Join-Path $PSScriptRoot "..\src\LogTailPro\Assets\app.ico"
$outputIco = [System.IO.Path]::GetFullPath($outputIco)

Write-Host "Generating LogTailPro icon..." -ForegroundColor Cyan

$sizes   = @(16, 32, 48, 256)
$bitmaps = @()
foreach ($s in $sizes) {
    $bitmaps += Draw-IconBitmap $s
    Write-Host "  Drew ${s}x${s}" -ForegroundColor Gray
}

Write-IcoFile -outputPath $outputIco -bitmaps $bitmaps

foreach ($b in $bitmaps) { $b.Dispose() }

Write-Host "Icon saved: $outputIco" -ForegroundColor Green
