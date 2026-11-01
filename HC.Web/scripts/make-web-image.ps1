<#
.SYNOPSIS
    Cuts a full-size poster photograph down to the size the storefront actually paints it at.

.DESCRIPTION
    The shop's marketing posters are shot and exported at print size: the two the home hero carries
    are 1856 x 2304 and 1145 x 1374, about 7 MB and 2 MB as PNGs. The hero paints each of them in a
    frame a little over 260 CSS pixels wide, so almost all of those pixels are detail no screen can
    show - and on a phone the pair would be most of ten megabytes downloaded before the band could
    paint, which is the first thing a visitor waits for.

    This writes a web copy at the width asked for (900 px here, roughly three times the frame, which
    is what keeps the posters' own printed words crisp on a high-density screen) as a JPEG, because a
    photograph is several times larger as a PNG for no gain. The masters stay in the folder untouched
    and the storefront points at the copies - the same arrangement as the collection banner, whose
    'home-collection.jpg' is written by 'make-banner.ps1'.

    The names keep the two apart: a master is '<name>_banner.png' and its copy '<name>-banner.jpg'
    (snake, underscore before the word, for the export; kebab for the copy the page loads). That is
    also what angular.json's asset rule leans on - 'images/banners/*_banner.png' is ignored, so the
    print files stay in the repository but never reach dist/ and are never deployed: they are 9 MB
    together, and the only copy the storefront asks for is the 225 KB one.

    Nothing is cropped: the whole poster is scaled, because the words on it are part of its design and
    a crop would take the ends off a headline. The shape is the CSS's job ('aspect-ratio' plus
    'object-fit: cover'), and the frame it uses is within a few pixels of each poster's own ratio, so
    the browser crops almost nothing either.

    Only ever scales down: a photograph already narrower than -MaxWidth is copied at its own size
    rather than blown up to a width it has no pixels for.

.EXAMPLE
    # Run from the storefront's project folder (HC.Web). The hero's two posters, as written.
    .\scripts\make-web-image.ps1 -Source src\assets\images\banners\musicians_banner.png -OutFile src\assets\images\banners\musicians-banner.jpg
    .\scripts\make-web-image.ps1 -Source src\assets\images\banners\genesha_banner.png -OutFile src\assets\images\banners\ganesha-banner.jpg

.EXAMPLE
    # A narrower cut for a thumbnail, and a better quality one for a full-width band.
    .\scripts\make-web-image.ps1 -Source src\assets\images\banners\musicians_banner.png -OutFile C:\temp\musicians-sm.jpg -MaxWidth 480 -Quality 80
#>
[CmdletBinding()]
param(
    # The full-size photograph to read. Absolute, or relative to the current directory.
    [Parameter(Mandatory = $true)]
    [string]$Source,

    # The web copy to write. Absolute, or relative to the current directory. Its folder is created if
    # it is not there yet.
    [Parameter(Mandatory = $true)]
    [string]$OutFile,

    # The widest the copy may be, in pixels. 900 is about three times the hero's print frames.
    [int]$MaxWidth = 900,

    # JPEG quality. 88 is the storefront's banner quality (see make-banner.ps1): it holds printed
    # text this small together at 900 px without the file growing past a few hundred KB.
    [int]$Quality = 88
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Absolute paths either way, so a relative -OutFile cannot land beside the caller's shell instead of
# in the folder it named.
function Resolve-FullPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        [System.IO.Path]::GetFullPath($Path)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
    }
}

$sourcePath = Resolve-FullPath $Source
$outPath = Resolve-FullPath $OutFile

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Photograph not found: $sourcePath"
}

$outDir = Split-Path -Parent $outPath
if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$image = [System.Drawing.Image]::FromFile($sourcePath)
$width = $image.Width
$height = $image.Height
try {
    if ($image.Width -gt $MaxWidth) {
        $scale = $MaxWidth / $image.Width
        $width = [int][math]::Round($image.Width * $scale)
        $height = [int][math]::Round($image.Height * $scale)
    }

    # 24bpp and a white ground: a JPEG has no alpha channel, so anything transparent in the source
    # would otherwise be written out black instead of as the page showing through. These masters are
    # opaque, and white is what the print frames paint behind them anyway.
    $canvas = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.Clear([System.Drawing.Color]::White)
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $destination = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
            $graphics.DrawImage($image, $destination)
        }
        finally {
            $graphics.Dispose()
        }

        $jpegCodec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
            Where-Object { $_.MimeType -eq 'image/jpeg' }
        $encoderParameters = New-Object System.Drawing.Imaging.EncoderParameters(1)
        $encoderParameters.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
            [System.Drawing.Imaging.Encoder]::Quality, [int64]$Quality)
        try {
            $canvas.Save($outPath, $jpegCodec, $encoderParameters)
        }
        finally {
            $encoderParameters.Dispose()
        }
    }
    finally {
        $canvas.Dispose()
    }
}
finally {
    $image.Dispose()
}

$written = Get-Item -LiteralPath $outPath
'{0} -> {1} ({2} x {3}, {4:N0} KB)' -f (Split-Path -Leaf $sourcePath), $written.Name, $width, $height, ($written.Length / 1KB)
