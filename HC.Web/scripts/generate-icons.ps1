#Requires -Version 5.1
<#
.SYNOPSIS
    Regenerates the Homecuties browser-tab and device icons (the butterfly from
    the logo) and the share card (the whole logo lockup).

.DESCRIPTION
    The icons carry the butterfly that opens the lockup in
    src/assets/images/logo.png - the two striped wings, the black body and the
    two antennae - cut out of the artwork rather than redrawn. The mark is six
    colours banded along two curved wings (orange #F58634 and #DE6B38, red
    #ED3237, yellow #FFCC29, light green #A8CF45, black #373435 for the body and
    antennae), and a version drawn from scratch sits next to the real one in the
    site header looking like a different butterfly.

    The cut-out is measured, not guessed. In the 1800 x 564 lockup the butterfly
    occupies x 4..823, y 1..557. Pixel 824 is already the orange "C" of the
    Cuties script, which butts against the wing tip with no white gap, so the
    region below stops just before that column and is then trimmed to the ink
    that is actually there. Whatever resolution the logo is re-exported at, the
    crop comes out the same - and the script fails loudly, rather than writing
    icons with half a wing, if the region no longer holds the black body, if the
    crop ends on a solid column rather than the wing's thin tip, or if the crop
    is not the shape of the mark. Colour cannot be used for that: the red band
    of the wing and the pink of the HOME wordmark are the same #E03253, so the
    only thing that tells the butterfly from the "H" next to it is where the wing
    tapers away.

    Sizes are resampled by repeated halving (Resize-Smooth) and drawn on a
    canvas of their own: one bicubic step from 820 px to 16 px samples a 51 px
    box for some output pixels and almost nothing for others, which is what
    turns the stripes into a dotted smear.

    Files written to public/ (angular.json copies public/** to the build root):
        favicon.ico          16, 32, 48, 64, 128 and 256 px PNG-compressed frames
        favicon-16x16.png
        favicon-32x32.png
        favicon-48x48.png
        apple-touch-icon.png 180 px on an opaque background (iOS paints
                             transparency black)
        icon-192.png          Android / PWA
        icon-512.png          Android / PWA
        og-image.png         1200 x 630 card a shared link draws (the whole
                             lockup, which a preview is large enough to carry)

    Nothing here is part of the Angular build; run it by hand after the logo
    changes, and commit the generated files.

.PARAMETER LogoPath
    Source lockup: the butterfly is cut from it and og-image.png is built from
    it. Defaults to src/assets/images/logo.png.

.PARAMETER OutputDir
    Where the icons are written. Defaults to public/.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\generate-icons.ps1
#>
[CmdletBinding()]
param(
    [string] $LogoPath,
    [string] $OutputDir
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# ---------------------------------------------------------------- the artwork

# Where the butterfly sits in the lockup, as a fraction of the canvas. 0.4575 of
# 1800 px is 823.5: the region keeps column 823, the wing's tip, and stops one
# column short of the orange "C" that begins at 824.
$ButterflyRegion = [System.Drawing.RectangleF]::new(0.0, 0.0, 0.4575, 1.0)

# The body and antennae. The crop must contain plenty of it: none means the
# region has drifted off the butterfly.
$BodyInk = [System.Drawing.Color]::FromArgb(255, 55, 52, 53)

# The body and antennae are a tapering blade and two hairline curves: solid black
# covers only about a quarter of a per cent of the crop. The test is therefore a
# share of the crop's area, not a pixel count, so it stays true if logo.png is
# re-exported at another size.
$MinimumBodyShare = 0.001

# The mark is 820 x 557 px in the lockup, i.e. 1.47 times as wide as it is tall.
# A crop outside this range is not the butterfly - it has picked up the "H" to
# the right, or lost a wing.
$AspectRange = @(1.30, 1.65)

# How far a pixel may sit from a reference colour and still count as it. Wide
# enough for the mark's anti-aliased edges, narrow enough to keep the striped
# orange (#F58634) out of the black of the body.
$ColourTolerance = 10


# --------------------------------------------------------------- reading ink

function Get-ArgbBitmap {
    <#
        A 32bppArgb copy. logo.png is already that, but LockBits only promises
        the B, G, R, A byte order for this format, so anything else is converted
        once here rather than trusted later.
    #>
    param([System.Drawing.Bitmap] $Bitmap)

    $copy = [System.Drawing.Bitmap]::new($Bitmap.Width, $Bitmap.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($copy)

    try {
        # SourceCopy: copy the artwork's own alpha, do not blend it with black.
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        # Not DrawImageUnscaled: that draws at the image's *physical* size, and
        # logo.png carries a 288 dpi header, so a 1800 px line art would land at
        # 600 px in the top-left corner as a third-size copy of itself.
        $destination = [System.Drawing.Rectangle]::new(0, 0, $Bitmap.Width, $Bitmap.Height)
        $graphics.DrawImage($Bitmap, $destination, 0, 0, $Bitmap.Width, $Bitmap.Height, [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $graphics.Dispose()
    }

    return $copy
}

function Read-Pixels {
    <# The bitmap's bytes, top row first, four per pixel in B, G, R, A order. #>
    param([System.Drawing.Bitmap] $Bitmap)

    $rect = [System.Drawing.Rectangle]::new(0, 0, $Bitmap.Width, $Bitmap.Height)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    try {
        $bytes = [byte[]]::new($data.Stride * $data.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        $stride = $data.Stride
    }
    finally {
        $Bitmap.UnlockBits($data)
    }

    return [pscustomobject]@{ Pixels = $bytes; Stride = $stride }
}

function Measure-Mark {
    <#
        One pass over the butterfly region of the lockup, which answers
        everything the crop needs to be trusted:

          Bounds      the ink, trimmed - the crop is exactly this
          BodyPixels  black body and antennae found
          TipPixels   ink in the rightmost ink column (a wing tip tapers to a
                      handful; a solid column there means the crop ends inside a
                      shape - the "C" of Cuties, typically)
    #>
    param(
        [System.Drawing.Bitmap] $Bitmap,
        [System.Drawing.Rectangle] $Region,
        [int] $AlphaThreshold = 32
    )

    $scan = Read-Pixels $Bitmap
    $pixels = $scan.Pixels
    $stride = $scan.Stride

    $left = [int]::MaxValue
    $top = [int]::MaxValue
    $right = -1
    $bottom = -1
    $bodyPixels = 0
    $columnCounts = [int[]]::new($Region.Width)

    for ($y = $Region.Top; $y -lt $Region.Bottom; $y++) {
        $row = $y * $stride

        for ($x = $Region.Left; $x -lt $Region.Right; $x++) {
            $index = $row + ($x * 4)

            if ($pixels[$index + 3] -le $AlphaThreshold) { continue }

            $columnCounts[$x - $Region.Left]++

            if ($x -lt $left) { $left = $x }
            if ($x -gt $right) { $right = $x }
            if ($y -lt $top) { $top = $y }
            if ($y -gt $bottom) { $bottom = $y }

            # Only solid pixels are classified: the soft edge of a wing blends
            # towards transparent, and those blends are not the body's black.
            if ($pixels[$index + 3] -lt 200) { continue }

            $blue = $pixels[$index]
            $green = $pixels[$index + 1]
            $red = $pixels[$index + 2]

            if ([Math]::Abs($red - $BodyInk.R) -le $ColourTolerance -and
                [Math]::Abs($green - $BodyInk.G) -le $ColourTolerance -and
                [Math]::Abs($blue - $BodyInk.B) -le $ColourTolerance) {
                $bodyPixels++
            }
        }
    }

    if ($right -lt 0) {
        throw 'The butterfly region of the logo is empty - has logo.png been replaced with a blank image?'
    }

    return [pscustomobject]@{
        Bounds     = [System.Drawing.Rectangle]::new($left, $top, ($right - $left) + 1, ($bottom - $top) + 1)
        BodyPixels = $bodyPixels
        TipPixels  = $columnCounts[$right - $Region.Left]
    }
}

function Get-ButterflyBitmap {
    <#
        The butterfly, cut out of the lockup and trimmed to its ink.

        Returns a bitmap the caller disposes. The checks are the point of doing
        this in code instead of cropping once in an image editor: the next person
        to re-export logo.png is told, not silently shipped, if the mark has
        moved inside the canvas.
    #>
    param([System.Drawing.Bitmap] $Logo)

    $canvas = Get-ArgbBitmap $Logo

    try {
        $region = [System.Drawing.Rectangle]::new(
            0,
            0,
            [int][Math]::Round($ButterflyRegion.Width * $canvas.Width),
            [int][Math]::Round($ButterflyRegion.Height * $canvas.Height))

        $mark = Measure-Mark $canvas $region
        $aspect = $mark.Bounds.Width / [double]$mark.Bounds.Height
        $minimumBodyPixels = [Math]::Max(50, [int]($mark.Bounds.Width * $mark.Bounds.Height * $MinimumBodyShare))

        Write-Host ('region: x {0}..{1} of a {2} x {3} canvas' -f $region.Left, ($region.Right - 1), $canvas.Width, $canvas.Height)
        Write-Host ('mark  : x {0}..{1}, y {2}..{3} -> {4} x {5} px, aspect {6:N2}, body {7:N0} px, tip {8} px' -f
            $mark.Bounds.Left, ($mark.Bounds.Right - 1), $mark.Bounds.Top, ($mark.Bounds.Bottom - 1),
            $mark.Bounds.Width, $mark.Bounds.Height, $aspect, $mark.BodyPixels, $mark.TipPixels)

        if ($mark.BodyPixels -lt $minimumBodyPixels) {
            throw ("The butterfly region holds only {0} pixels of the black body and antennae (expected at least {1}), so the crop is not the butterfly. Was logo.png redrawn with a different layout?" -f $mark.BodyPixels, $minimumBodyPixels)
        }

        # The wing tip leaves a tapering sliver in the last column. A solid one
        # means the crop ends inside artwork - the "H" of HOME, once the region
        # has run past the tip into the wordmark.
        $tipLimit = [Math]::Max(4, [int]($mark.Bounds.Height * 0.10))
        if ($mark.TipPixels -gt $tipLimit) {
            throw ("The crop ends on a column of {0} px of solid ink (limit {1}), so it stops inside a shape rather than at the wing tip. Move the right edge of `$ButterflyRegion (currently {2:P2} of the canvas width) left." -f $mark.TipPixels, $tipLimit, $ButterflyRegion.Width)
        }

        if (($aspect -lt $AspectRange[0]) -or ($aspect -gt $AspectRange[1])) {
            throw ("The crop is {0:N2} times as wide as it is tall, and the butterfly is {1:N2}. The region no longer holds the mark alone - check both edges of `$ButterflyRegion." -f $aspect, (820 / 557))
        }

        return $canvas.Clone($mark.Bounds, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    }
    finally {
        $canvas.Dispose()
    }
}


# ---------------------------------------------------------------- resampling

function New-ScaledBitmap {
    <#
        One resampling step, to exactly the width and height asked for.

        The result is Format32bppPArgb - premultiplied alpha - on purpose. The
        artwork's transparent pixels are white, so resampling straight alpha
        averages a wing's red with the white of the pixels outside it and the
        edge of every stripe comes out pale. Premultiplied, the transparent
        neighbours carry no colour into the average.
    #>
    param(
        [System.Drawing.Bitmap] $Bitmap,
        [int] $Width,
        [int] $Height
    )

    $scaled = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppPArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($scaled)

    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        # TileFlipXY keeps the resampler from sampling past the edge of the
        # artwork, which otherwise darkens the border with transparent black.
        $attributes = [System.Drawing.Imaging.ImageAttributes]::new()

        try {
            $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $destination = [System.Drawing.Rectangle]::new(0, 0, $Width, $Height)
            $graphics.DrawImage($Bitmap, $destination, 0, 0, $Bitmap.Width, $Bitmap.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
        }
        finally {
            $attributes.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    return $scaled
}

function Resize-Smooth {
    <#
        Halves the artwork until it is within twice the target size, then takes
        one final step.

        A single bicubic step from 820 px to 16 px is not a sample of the whole
        picture: the resampler reads a 51 px box for one output pixel and a
        fraction of one for the next, so the wing stripes come out uneven and
        some of them disappear. Halving averages every pixel of the source, and
        the stripes survive.
    #>
    param(
        [System.Drawing.Bitmap] $Bitmap,
        [int] $Width,
        [int] $Height
    )

    $current = $Bitmap
    $owned = $false

    try {
        while (($current.Width -ge (2 * $Width)) -and ($current.Height -ge (2 * $Height))) {
            $nextWidth = [Math]::Max($Width, [int][Math]::Floor($current.Width / 2))
            $nextHeight = [Math]::Max($Height, [int][Math]::Floor($current.Height / 2))

            $next = New-ScaledBitmap $current $nextWidth $nextHeight

            if ($owned) { $current.Dispose() }

            $current = $next
            $owned = $true
        }

        return New-ScaledBitmap $current $Width $Height
    }
    finally {
        if ($owned) { $current.Dispose() }
    }
}


function New-IconBitmap {
    <#
        One icon at one size.

        Padding is the fraction of the icon edge kept clear of the mark. 0.03 is
        about as tight as a tab icon can be before the tips touch the edge of a
        rounded tab; the PWA tiles get 0.06 because a launcher puts them on a
        white tile of its own, and the 180 px iOS one gets 0.14 because iOS
        rounds its corners much harder.

        The mark is fitted, not stretched: it is 1.47 times as wide as it is
        tall, so the width is what fills the canvas and the height follows.
    #>
    param(
        [System.Drawing.Bitmap] $Mark,
        [int] $Size,
        [double] $Padding = 0.03,
        [System.Drawing.Color] $Background = [System.Drawing.Color]::Transparent
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.Clear($Background)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        # The mark arrives already at the size it is drawn (Resize-Smooth did the
        # resampling), so this blit is one-to-one: nearest neighbour is exact,
        # and anything smoother would blur the pixels a second time.
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        $inner = [double]$Size * (1.0 - (2.0 * $Padding))
        $scale = [Math]::Min($inner / $Mark.Width, $inner / $Mark.Height)
        $width = [int][Math]::Max(1, [Math]::Round($Mark.Width * $scale))
        $height = [int][Math]::Max(1, [Math]::Round($Mark.Height * $scale))
        $x = [int][Math]::Round(($Size - $width) / 2.0)
        $y = [int][Math]::Round(($Size - $height) / 2.0)

        $resized = Resize-Smooth $Mark $width $height

        try {
            $attributes = [System.Drawing.Imaging.ImageAttributes]::new()

            try {
                $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
                $destination = [System.Drawing.Rectangle]::new($x, $y, $width, $height)
                $graphics.DrawImage($resized, $destination, 0, 0, $resized.Width, $resized.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
            }
            finally {
                $attributes.Dispose()
            }
        }
        finally {
            $resized.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}


function New-ShareCardBitmap {
    <#
        The card a shared link draws (Open Graph / Twitter): the whole logo
        lockup - not the butterfly - centred on white at the 1200 x 630 that
        Facebook, WhatsApp, LinkedIn, Slack and X crop to. A preview is large
        enough to carry the wordmark, and the wordmark is what says who the shop
        is when the card appears in someone else's timeline.

        PNG rather than JPEG: the Cuties script is a set of thin strokes that a
        JPEG smears even at a high quality setting, and this file is fetched once
        per shared link rather than once per visit.
    #>
    param(
        [System.Drawing.Bitmap] $Logo,
        [int] $Width = 1200,
        [int] $Height = 630,
        [double] $Padding = 0.10,
        [System.Drawing.Color] $Background = [System.Drawing.Color]::White
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.Clear($Background)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        # One-to-one again: Resize-Smooth has already produced the lockup at the
        # size it is drawn.
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        $innerWidth = [double]$Width * (1.0 - (2.0 * $Padding))
        $innerHeight = [double]$Height * (1.0 - (2.0 * $Padding))
        $scale = [Math]::Min($innerWidth / $Logo.Width, $innerHeight / $Logo.Height)
        $logoWidth = [Math]::Max(1, [int][Math]::Round($Logo.Width * $scale))
        $logoHeight = [Math]::Max(1, [int][Math]::Round($Logo.Height * $scale))
        $x = [int][Math]::Round(($Width - $logoWidth) / 2.0)
        $y = [int][Math]::Round(($Height - $logoHeight) / 2.0)

        $resized = Resize-Smooth $Logo $logoWidth $logoHeight

        try {
            $attributes = [System.Drawing.Imaging.ImageAttributes]::new()

            try {
                $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
                $destination = [System.Drawing.Rectangle]::new($x, $y, $logoWidth, $logoHeight)
                $graphics.DrawImage($resized, $destination, 0, 0, $resized.Width, $resized.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
            }
            finally {
                $attributes.Dispose()
            }
        }
        finally {
            $resized.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}


function Save-Png {
    param([System.Drawing.Bitmap] $Bitmap, [string] $Path)

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host ('  wrote {0,-24} {1,4} x {2,-4} {3,7:N0} bytes' -f (Split-Path $Path -Leaf), $Bitmap.Width, $Bitmap.Height, (Get-Item -LiteralPath $Path).Length)
}

function Save-Ico {
    <#
        Writes a multi-size .ico with PNG-compressed frames, which every browser
        since IE11 and the Windows Vista+ shell understand.
    #>
    param([System.Drawing.Bitmap[]] $Bitmaps, [string] $Path)

    $frames = @(foreach ($bitmap in $Bitmaps) {
        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        , $stream.ToArray()
    })

    $offset = 6 + (16 * $Bitmaps.Count)
    $memory = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($memory)

    $writer.Write([UInt16]0)                    # reserved
    $writer.Write([UInt16]1)                    # 1 = icon
    $writer.Write([UInt16]$Bitmaps.Count)

    for ($i = 0; $i -lt $Bitmaps.Count; $i++) {
        $bitmap = $Bitmaps[$i]
        $frame = $frames[$i]
        # 256 px is stored as 0 in the ICONDIRENTRY.
        $dimension = if ($bitmap.Width -ge 256) { 0 } else { $bitmap.Width }

        $writer.Write([Byte]$dimension)          # width
        $writer.Write([Byte]$dimension)          # height
        $writer.Write([Byte]0)                   # palette colours
        $writer.Write([Byte]0)                   # reserved
        $writer.Write([UInt16]1)                 # colour planes
        $writer.Write([UInt16]32)                # bits per pixel
        $writer.Write([UInt32]$frame.Length)
        $writer.Write([UInt32]$offset)

        $offset += $frame.Length
    }

    foreach ($frame in $frames) { $writer.Write($frame) }
    $writer.Flush()
    [System.IO.File]::WriteAllBytes($Path, $memory.ToArray())
    $writer.Dispose()

    Write-Host ('  wrote {0,-24} {1,4} frames ({2}) {3,7:N0} bytes' -f (Split-Path $Path -Leaf), $Bitmaps.Count, (($Bitmaps | ForEach-Object { $_.Width }) -join '/'), (Get-Item -LiteralPath $Path).Length)
}


# ----------------------------------------------------------------------- main

# Resolved here rather than in the param block: $PSScriptRoot is empty while
# parameter defaults are evaluated when the script is started with -File.
if (-not $LogoPath) {
    $LogoPath = Join-Path $PSScriptRoot '..\src\assets\images\logo.png'
}
if (-not $OutputDir) {
    $OutputDir = Join-Path $PSScriptRoot '..\public'
}

$logoFullPath = [System.IO.Path]::GetFullPath($LogoPath)
$outputFullPath = [System.IO.Path]::GetFullPath($OutputDir)

if (-not (Test-Path -LiteralPath $logoFullPath)) {
    throw "Logo not found: $logoFullPath"
}
if (-not (Test-Path -LiteralPath $outputFullPath)) {
    throw "Output folder not found: $outputFullPath"
}

Write-Host "logo : $logoFullPath"
Write-Host "out  : $outputFullPath"

$logo = [System.Drawing.Bitmap]::FromFile($logoFullPath)
$butterfly = $null
$targets = @()

try {
    $butterfly = Get-ButterflyBitmap $logo

    # Browser tab icons: transparent, the butterfly as large as it can be. The
    # mark is 1.47 times as wide as it is tall, so the width is what fills the
    # square and this padding is what keeps the tips off the edge of a rounded
    # tab - any more and a 16 px icon is a small mark in a lot of air.
    foreach ($size in 16, 32, 48) {
        $targets += [pscustomobject]@{ Bitmap = (New-IconBitmap $butterfly $size -Padding 0.03); Name = "favicon-${size}x${size}.png" }
    }
    # PWA / Android home screen: same mark, a little more air, because the
    # launcher puts it on a white tile of its own.
    foreach ($size in 192, 512) {
        $targets += [pscustomobject]@{ Bitmap = (New-IconBitmap $butterfly $size -Padding 0.06); Name = "icon-$size.png" }
    }
    # iOS rounds the corners and paints transparency black, so this one is opaque
    # and keeps a margin the launcher rounding never eats into.
    $targets += [pscustomobject]@{ Bitmap = (New-IconBitmap $butterfly 180 -Padding 0.14 -Background ([System.Drawing.Color]::White)); Name = 'apple-touch-icon.png' }

    # The card a shared link draws. Built from the whole lockup rather than the
    # butterfly: a preview is large enough to carry the wordmark, and index.html
    # names this file as its og:image / twitter:image.
    $targets += [pscustomobject]@{ Bitmap = (New-ShareCardBitmap $logo); Name = 'og-image.png' }

    foreach ($target in $targets) {
        Save-Png $target.Bitmap (Join-Path $outputFullPath $target.Name)
    }

    $icoFrames = @(foreach ($size in 16, 32, 48, 64, 128, 256) { New-IconBitmap $butterfly $size -Padding 0.03 })
    try {
        Save-Ico $icoFrames (Join-Path $outputFullPath 'favicon.ico')
    }
    finally {
        $icoFrames | ForEach-Object { $_.Dispose() }
    }
}
finally {
    $targets | ForEach-Object { $_.Bitmap.Dispose() }
    if ($butterfly) { $butterfly.Dispose() }
    $logo.Dispose()
}

Write-Host ''
Write-Host 'Done. src/index.html should reference:'
Write-Host '  <link rel="icon" type="image/x-icon" href="favicon.ico">'
Write-Host '  <link rel="icon" type="image/png" sizes="32x32" href="favicon-32x32.png">'
Write-Host '  <link rel="icon" type="image/png" sizes="16x16" href="favicon-16x16.png">'
Write-Host '  <link rel="apple-touch-icon" sizes="180x180" href="apple-touch-icon.png">'
