<#
.SYNOPSIS
    Composes the storefront's collection banner from the shop's own product photographs.

.DESCRIPTION
    Lays a row of product photographs out on the theme's light surface, each cut to the same
    portrait window and framed with the theme's product-card corner radius, so the strip reads as
    one editorial band rather than four unrelated pictures.

    Why a script and not a one-off edit: the photographs are 576 or 1200 square, the tile
    backgrounds differ from picture to picture (grey, cream, dark green), and every slot the banner
    might fill has a different shape - the home hero card is 4:3, the benefits mosaic's main panel
    is roughly 2:3, a full-width band is ~2.5:1. Re-running this with different -Width/-Height
    re-cuts the same photographs to that shape instead of somebody re-cropping by hand.

    Only each product's primary photograph ('_P_01') is used: the secondary and detail shots are
    for the product page, and these four are the ones whose subjects are vertical or compact enough
    to survive a portrait window without losing their subject.

    No text is drawn into the image: the storefront's headlines are HTML, because they have to
    wrap, be read out by a screen reader and be translated. The banner carries photography only.

.EXAMPLE
    # Run from the storefront's project folder (HC.Web). The banner as written (1600 x 620,
    # four photographs, 16px corners), written to src\assets\images\banners\home-collection.jpg.
    .\scripts\make-banner.ps1

.EXAMPLE
    # The same photographs re-cut as a 2:3 panel, for the benefits mosaic's main tile: no margins,
    # no gaps, square corners (the panel's CSS already rounds it), pushed in a little further.
    .\scripts\make-banner.ps1 -Width 600 -Height 900 -Margin 0 -Gap 0 -Radius 0 -Zoom 1.25 -OutFile src\assets\images\banners\benefits-main.jpg

.EXAMPLE
    # A 4:3 cut for the hero's main showcase card, run from anywhere (-OutFile absolute here).
    .\scripts\make-banner.ps1 -Width 1200 -Height 900 -Radius 12 -OutFile C:\temp\hero-4x3.jpg
#>
[CmdletBinding()]
param(
    # Where the product photographs and the output live. Empty asks for this script's own project:
    # '$PSScriptRoot' is not populated yet while parameters are being bound, so the paths are
    # resolved at the top of the body instead of in the defaults.
    [string]$ImageRoot = '',

    # The banner to write, absolute or relative to the current directory. Its folder is created if
    # it is not there yet. Empty writes 'src\assets\images\banners\home-collection.jpg' beside this
    # script's project.
    [string]$OutFile = '',

    [int]$Width = 1600,
    [int]$Height = 620,

    # Air above, below, left and right of the photographs.
    [int]$Margin = 60,

    # Surface colour between one photograph and the next.
    [int]$Gap = 20,

    # The theme's product-card corner radius is 0.8rem; a 1600px band is roughly twice an 800px
    # card grid, hence 16px here. 0 gives square corners, for a panel CSS already rounds or a
    # photograph that must sit flush.
    [int]$Radius = 16,

    # JPEG quality. 88 keeps a 1600px band under ~500 KB with no visible artefacts on these
    # photographs; raise it only if the banner is going to be printed.
    [int]$Quality = 88,

    # How far to push in on each photograph. 1 lays the full height of the picture in the tile; the
    # shop's tiles have very different backdrops (grey paper, cream, dark green), so pushing in a
    # little gives the product more of the tile and the backdrop less, which is what makes four
    # photographs from four different settings read as one band. Past ~1.25 the subjects start to
    # run into the rounded corners. A single photograph can override this with its own 'Zoom' - see
    # the wooden cat below.
    [double]$Zoom = 1.12
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# This script's own project, whichever way it was started (.\make-banner.ps1, -File, or dot-sourced).
$scriptRoot = Split-Path -Parent $PSCommandPath
if (-not $ImageRoot) {
    $ImageRoot = Join-Path $scriptRoot '..\src\assets\images'
}
if (-not $OutFile) {
    $OutFile = Join-Path $scriptRoot '..\src\assets\images\banners\home-collection.jpg'
}

# The theme's light surface (#f3f3f3) rather than white: the storefront's pages are white, so a
# white banner would have no edge of its own, and this is the one grey the theme already uses.
$Surface = [System.Drawing.ColorTranslator]::FromHtml('#f3f3f3')

# The four photographs, in the order they read left to right. FocalX/FocalY say where the subject
# sits in the frame (0..1); the cut window is placed on that point rather than on the middle of the
# picture, which is what keeps a bottle's neck and a shelf's top row inside a portrait window. An
# entry may also carry its own 'Zoom', for a photograph that is framed more tightly than the rest.
$photos = @(
    @{ File = 'products\I10019_P_01.jpg'; Subject = 'blue geometric bottle vase'; FocalX = 0.50; FocalY = 0.52 },
    @{ File = 'products\I10022_P_01.jpg'; Subject = 'black leaf bottle vase';       FocalX = 0.52; FocalY = 0.50 },
    @{ File = 'products\I10031_P_01.jpg'; Subject = 'house shelf of painted pots';   FocalX = 0.50; FocalY = 0.50 },
    # The cat already fills its own frame, so it is cut at 1: pushing in the way the bottles are
    # pushed in would take the tops of its ears off.
    @{ File = 'products\I10026_P_01.jpg'; Subject = 'wooden cat toy on woven cloth'; FocalX = 0.50; FocalY = 0.50; Zoom = 1.0 }
)

$resolvedRoot = (Resolve-Path $ImageRoot).Path
$outPath = if ([System.IO.Path]::IsPathRooted($OutFile)) {
    [System.IO.Path]::GetFullPath($OutFile)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutFile))
}

# Every photograph must be there before a canvas is made, so a typo cannot leave half a banner on
# disk. 'wait.gif' in the same folder is the placeholder the storefront shows while a photograph
# loads - it is not a product shot and is deliberately not in the list above.
foreach ($photo in $photos) {
    $source = Join-Path $resolvedRoot $photo.File
    if (-not (Test-Path $source)) {
        throw "Product photograph not found: $source"
    }
}

$outDir = Split-Path -Parent $outPath
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

# The window every photograph is cut to: equal widths fill the band between the margins and gaps.
$count = $photos.Count
$cellWidth = [int][math]::Floor(($Width - 2 * $Margin - $Gap * ($count - 1)) / $count)
$cellHeight = $Height - 2 * $Margin
if ($cellWidth -le 0 -or $cellHeight -le 0) {
    throw "A $Width x $Height banner cannot hold $count photographs at $Margin px margins and $Gap px gaps."
}
$cellAspect = $cellWidth / $cellHeight

$canvas = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)

try {
    $graphics.Clear($Surface)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    for ($i = 0; $i -lt $count; $i++) {
        $photo = $photos[$i]
        $image = [System.Drawing.Image]::FromFile((Join-Path $resolvedRoot $photo.File))
        try {
            # The source side whose aspect does not match the cell is the side that gets cut. The
            # window is then divided by the zoom - this photograph's own, if it has one - which
            # magnifies what is left of the picture.
            $sourceAspect = $image.Width / $image.Height
            if ($sourceAspect -gt $cellAspect) {
                $cropHeight = $image.Height
                $cropWidth = [int][math]::Round($cropHeight * $cellAspect)
            }
            else {
                $cropWidth = $image.Width
                $cropHeight = [int][math]::Round($cropWidth / $cellAspect)
            }

            $photoZoom = if ($photo.ContainsKey('Zoom')) { [double]$photo.Zoom } else { $Zoom }
            if ($photoZoom -gt 1) {
                $cropWidth = [int][math]::Round($cropWidth / $photoZoom)
                $cropHeight = [int][math]::Round($cropHeight / $photoZoom)
            }

            # Centre the window on the subject, then pull it back inside the picture - a subject
            # near an edge would otherwise ask for a window that starts off-canvas.
            $cropX = [int][math]::Round($photo.FocalX * $image.Width - $cropWidth / 2)
            $cropY = [int][math]::Round($photo.FocalY * $image.Height - $cropHeight / 2)
            $cropX = [math]::Max(0, [math]::Min($cropX, $image.Width - $cropWidth))
            $cropY = [math]::Max(0, [math]::Min($cropY, $image.Height - $cropHeight))

            $left = $Margin + $i * ($cellWidth + $Gap)
            $destination = New-Object System.Drawing.Rectangle($left, $Margin, $cellWidth, $cellHeight)

            if ($Radius -gt 0) {
                # Rounded corners the same way the panel's CSS does it: a rounded rectangle used as
                # the clip, so the photograph itself is cut rather than masked by a drawn shape.
                $path = New-Object System.Drawing.Drawing2D.GraphicsPath
                $d = $Radius * 2
                $path.AddArc($left, $Margin, $d, $d, 180, 90)
                $path.AddArc($left + $cellWidth - $d, $Margin, $d, $d, 270, 90)
                $path.AddArc($left + $cellWidth - $d, $Margin + $cellHeight - $d, $d, $d, 0, 90)
                $path.AddArc($left, $Margin + $cellHeight - $d, $d, $d, 90, 90)
                $path.CloseFigure()

                $previousClip = $graphics.Clip
                $graphics.SetClip($path)
                $graphics.DrawImage($image, $destination, $cropX, $cropY, $cropWidth, $cropHeight, [System.Drawing.GraphicsUnit]::Pixel)
                $graphics.Clip = $previousClip
                $path.Dispose()
            }
            else {
                $graphics.DrawImage($image, $destination, $cropX, $cropY, $cropWidth, $cropHeight, [System.Drawing.GraphicsUnit]::Pixel)
            }

            Write-Verbose ("{0}: {1}x{2} -> {3}x{4} from ({5},{6}) at zoom {7} [{8}]" -f $photo.File, $image.Width, $image.Height, $cellWidth, $cellHeight, $cropX, $cropY, $photoZoom, $photo.Subject)
        }
        finally {
            $image.Dispose()
        }
    }
}
finally {
    $graphics.Dispose()
}

$encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$encoderParams = New-Object System.Drawing.Imaging.EncoderParameters(1)
$encoderParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality, [int64]$Quality)

try {
    $canvas.Save($outPath, $encoder, $encoderParams)
}
finally {
    $encoderParams.Dispose()
    $canvas.Dispose()
}

$written = Get-Item $outPath
Write-Output ("Wrote {0} ({1} x {2}, {3} KB, {4} photographs: {5})" -f $written.FullName, $Width, $Height, [math]::Round($written.Length / 1KB), $count, (($photos | ForEach-Object { $_.Subject }) -join '; '))
