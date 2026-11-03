# Draws the installer artwork: IPropertyManager.ico (16-256 px) and the two Inno Setup wizard bitmaps.
# Usage:  powershell -ExecutionPolicy Bypass -File installer\make-art.ps1
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot

$accent = [System.Drawing.Color]::FromArgb(6, 150, 215)
$panel  = [System.Drawing.Color]::FromArgb(59, 68, 83)
$field  = [System.Drawing.Color]::FromArgb(46, 52, 64)

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

# The mark: a property sheet (label column + value lines) with an "i" badge, on a dark rounded tile.
function Draw-Mark([System.Drawing.Graphics]$g, [float]$x, [float]$y, [float]$s) {
    $g.SmoothingMode = 'AntiAlias'
    $u = $s / 32
    $tile = New-RoundRect $x $y $s $s (6 * $u)
    $g.FillPath((New-Object System.Drawing.SolidBrush $panel), $tile)

    $sheet = New-RoundRect ($x + 6 * $u) ($y + 5 * $u) (17 * $u) (22 * $u) (2 * $u)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 238, 242))), $sheet)
    $ink = New-Object System.Drawing.SolidBrush $field
    $val = New-Object System.Drawing.SolidBrush $accent
    for ($i = 0; $i -lt 4; $i++) {
        $ly = $y + (9 + 4.5 * $i) * $u
        $g.FillRectangle($ink, $x + 9 * $u, $ly, 4 * $u, [Math]::Max(1, 2 * $u))
        $g.FillRectangle($val, $x + 14.5 * $u, $ly, 6 * $u, [Math]::Max(1, 2 * $u))
    }

    $g.FillEllipse((New-Object System.Drawing.SolidBrush $accent), $x + 17 * $u, $y + 17 * $u, 12 * $u, 12 * $u)
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $g.FillEllipse($white, $x + 22.2 * $u, $y + 19.4 * $u, 1.8 * $u, 1.8 * $u)
    $g.FillRectangle($white, $x + 22.3 * $u, $y + 22 * $u, 1.6 * $u, 5 * $u)
}

# ----- icon: PNG-compressed entries in one .ico ---------------------------------------------------------------------
$sizes = 16, 24, 32, 48, 256
$images = foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $sz, $sz, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::Transparent)
    Draw-Mark $g 0 0 $sz; $g.Dispose()
    $ms = New-Object System.IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    ,$ms.ToArray()
}
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $dim = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
[System.IO.File]::WriteAllBytes((Join-Path $dir 'IPropertyManager.ico'), $out.ToArray())

# ----- wizard bitmaps (Inno Setup 6 modern style sizes at 100 % and 200 %) -------------------------------------------
function Save-Bmp([int]$w, [int]$h, [scriptblock]$draw, [string]$name) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    & $draw $g $w $h
    $g.Dispose(); $bmp.Save((Join-Path $dir $name), [System.Drawing.Imaging.ImageFormat]::Bmp); $bmp.Dispose()
}

Save-Bmp 328 628 {
    param($g, $w, $h)
    $g.Clear($panel)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $field), 0, $h * 0.62, $w, $h * 0.38)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $accent), 0, $h * 0.62 - 6, $w, 6)
    Draw-Mark $g (($w - 200) / 2) ($h * 0.18) 200
} 'wizard-large.bmp'

Save-Bmp 110 110 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::White)
    Draw-Mark $g 5 5 100
} 'wizard-small.bmp'

"Wrote IPropertyManager.ico, wizard-large.bmp, wizard-small.bmp to $dir"
