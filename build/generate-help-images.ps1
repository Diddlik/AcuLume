<#
.SYNOPSIS
  Renders the before/after pairs the Sharpen panel shows when a section's help is expanded.

.DESCRIPTION
  The pairs are made from synthetic targets, not photographs, and that is deliberate. On a real
  photograph these settings move a 150 px crop by two to six levels out of 255 - the effect is
  genuinely there but nearly invisible at that size, which forced the earlier photographic pairs to
  be exaggerated well past their preset values to show anything at all.

  A target built for the purpose does not need that: the content is chosen so the effect is
  unmistakable at the preset's real settings. A bar pattern shows which spatial scales a band acts
  on, a hard edge shows overshoot as a visible rim, and flat grey with grain shows noise handling
  directly.

  Targets are 1800 px wide so the resize is a no-op and the sharpening parameters act at the scale
  they were calibrated for. Everything is rendered through the real engine and the real preset.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src/AcuLume.Cli/bin/Debug/net10.0/aculume.exe'
$outDir = Join-Path $root 'src/AcuLume.Gui/Assets/help'
$tmp = Join-Path $env:TEMP 'aculume-help'
$CROP = 75          # cropped tight, then doubled: these differences are read like a 200% view
$ZOOM = 2
$W = 1800
$H = 600

if (-not (Test-Path $exe)) { throw 'Build the CLI first: dotnet build' }
New-Item -ItemType Directory -Force $outDir | Out-Null
New-Item -ItemType Directory -Force $tmp | Out-Null
Add-Type -AssemblyName System.Drawing

# Deterministic noise: the same target every run, so a regenerated pair differs only where the
# pipeline changed.
$script:rng = [System.Random]::new(20260907)
function Get-Gauss([double]$sigma) {
    $u1 = [Math]::Max($script:rng.NextDouble(), 1e-9)
    $u2 = $script:rng.NextDouble()
    return $sigma * [Math]::Sqrt(-2 * [Math]::Log($u1)) * [Math]::Cos(2 * [Math]::PI * $u2)
}

function New-Target([string]$kind, [string]$path) {
    $bmp = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $rect = New-Object System.Drawing.Rectangle 0, 0, $W, $H
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $bmp.PixelFormat)
    $stride = $data.Stride
    $buffer = New-Object byte[] ($stride * $H)

    # Bar periods in pixels, coarse to fine across the frame: the medium band acts on the left-hand
    # bands, the fine band on the right-hand ones.
    $periods = @(16, 12, 8, 6, 4, 3, 2)
    $bandWidth = $W / $periods.Count

    for ($y = 0; $y -lt $H; $y++) {
        for ($x = 0; $x -lt $W; $x++) {
            $v = 128.0
            if ($kind -eq 'scales') {
                # Sine rather than square: a square wave is already at full contrast, so sharpening
                # can only add a one-pixel rim to it. A soft modulation is what the bands deepen,
                # and the depth change is what the illustration needs to show.
                $band = [Math]::Min([int]($x / $bandWidth), $periods.Count - 1)
                $p = $periods[$band]
                $v = 128.0 + (44.0 * [Math]::Sin((2.0 * [Math]::PI * $x) / $p))
            }
            elseif ($kind -eq 'edge') {
                $v = if ($x -lt ($W / 2)) { 55.0 } else { 205.0 }
            }
            elseif ($kind -eq 'grain') {
                $v = [Math]::Clamp(128.0 + (Get-Gauss 7.0), 0.0, 255.0)
            }
            elseif ($kind -eq 'fineGrain') {
                # Quieter than 'grain', so the grain sits near the sharpener's own noise threshold
                # rather than well above it.
                $v = [Math]::Clamp(128.0 + (Get-Gauss 2.5), 0.0, 255.0)
            }
            elseif ($kind -eq 'soft') {
                # A smoothly modulated fine pattern: soft to begin with, so capture sharpening has
                # something to restore rather than an already-perfect edge.
                $v = 96.0 + (64.0 * (([Math]::Sin($x / 1.6) * 0.5) + 0.5))
            }
            elseif ($kind -eq 'mixed') {
                # Texture on the left, a hard edge in the middle, flat on the right: shows a setting
                # treating detail and edges differently within one frame.
                if ($x -lt ($W / 2)) { $v = [Math]::Clamp(120.0 + (Get-Gauss 18.0), 0.0, 255.0) }
                else { $v = 210.0 }
            }

            $i = ($y * $stride) + ($x * 3)
            $b = [byte][Math]::Round($v)
            $buffer[$i] = $b
            $buffer[$i + 1] = $b
            $buffer[$i + 2] = $b
        }
    }

    [System.Runtime.InteropServices.Marshal]::Copy($buffer, 0, $data.Scan0, $buffer.Length)
    $bmp.UnlockBits($data)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# topic, target, crop position, and the flags that switch the setting off or on
$topics = @(
    # The fine band works at a 0.35 px radius, so it acts almost entirely at the pixel scale: on a
    # bar pattern the blur leaves the bars alone and there is nothing to amplify. Its real signature
    # is the sharp one-pixel rim it puts on an edge, which the profile shows and a tile cannot.
    @{ key = 'fine';    target = 'edge';   x = 888;  y = 300; plot = $true;
       off = @('--fine-amount', '0', '--halo-protection', '0'); on = @('--halo-protection', '0') }
    @{ key = 'medium';  target = 'scales'; x = 900;  y = 260; plot = $false; off = @('--medium-amount', '0'); on = @() }
    # Both of these switch the halo limiter off on both sides: at its calibrated limits it clamps
    # the excursion before the setting being illustrated can express itself, so leaving it on would
    # show the limiter's work rather than theirs.
    @{ key = 'balance'; target = 'edge';   x = 888;  y = 300; plot = $true;
       off = @('--darken', '0.5', '--lighten', '0.5', '--halo-protection', '0'); on = @('--halo-protection', '0') }
    @{ key = 'noise';   target = 'fineGrain'; x = 862; y = 260; plot = $false;
       off = @('--denoise', '0', '--noise-protection', '0', '--halo-protection', '0')
       on = @('--denoise', '0', '--halo-protection', '0') }
    @{ key = 'denoise'; target = 'grain';  x = 862;  y = 260; plot = $false; off = @('--denoise', '0');       on = @() }
    @{ key = 'edge';    target = 'mixed';  x = 862;  y = 260; plot = $false; off = @('--edge-protection', '0'); on = @() }
    @{ key = 'halo';    target = 'edge';   x = 888;  y = 300; plot = $true;  off = @('--halo-protection', '0'); on = @() }
    @{ key = 'capture'; target = 'soft';   x = 862;  y = 260; plot = $false; off = @();                       on = @('--capture-sharpen', 'Normal') }
)

# An overshoot rim is one or two pixels wide. In a 150 px tile that is invisible, but as a plot of
# brightness across the edge it is the most legible illustration there is - which is why the
# textbooks draw it this way. The dashed line is the original edge, so the excursions beyond it are
# exactly the halo the setting governs.
function Save-Profile([string]$src, [int]$x, [int]$y, [string]$dest) {
    # A one-pixel excursion spread over 75 samples is a hairline. Sampling a narrow window across
    # the edge instead gives each pixel room to be seen, which is the whole point of the plot.
    $span = 24
    $img = New-Object System.Drawing.Bitmap $src
    try {
        $size = $CROP * $ZOOM
        $bmp = New-Object System.Drawing.Bitmap $size, $size
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.Clear([System.Drawing.Color]::FromArgb(255, 22, 26, 31))

        $pad = 10.0
        $plotWidth = $size - (2 * $pad)
        # The target's own step, drawn as the reference the curve is judged against.
        $flat = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 150, 160, 170)), 1.0
        $flat.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
        foreach ($level in @(55.0, 205.0)) {
            $ly = $pad + ($plotWidth * (1.0 - ($level / 255.0)))
            $g.DrawLine($flat, [float]$pad, [float]$ly, [float]($size - $pad), [float]$ly)
        }

        $points = New-Object System.Collections.Generic.List[System.Drawing.PointF]
        for ($i = 0; $i -lt $span; $i++) {
            $v = $img.GetPixel($x + $i, $y).R
            $px = $pad + ($plotWidth * ($i / [double]($span - 1)))
            $py = $pad + ($plotWidth * (1.0 - ($v / 255.0)))
            $points.Add([System.Drawing.PointF]::new([float]$px, [float]$py))
        }
        $curve = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 120, 170, 255)), 2.0
        $g.DrawLines($curve, $points.ToArray())
        $g.Dispose()
        $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    } finally { $img.Dispose() }
}

function Save-Crop([string]$src, [int]$x, [int]$y, [string]$dest) {
    $img = [System.Drawing.Image]::FromFile($src)
    try {
        $size = $CROP * $ZOOM
        $bmp = New-Object System.Drawing.Bitmap $size, $size
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        # Nearest neighbour: a smoothing filter would hide the pixel-level difference being shown.
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $dst = New-Object System.Drawing.Rectangle 0, 0, $size, $size
        $crop = New-Object System.Drawing.Rectangle $x, $y, $CROP, $CROP
        $g.DrawImage($img, $dst, $crop, [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
        $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    } finally { $img.Dispose() }
}

# A pair that shows nothing is worse than no illustration, so measure what the pipeline actually
# did in the region being illustrated. The largest difference, not the average: an overshoot rim is
# two columns wide, so averaging it over the region reports zero for a setting that plainly works.
function Get-MaxDelta([string]$a, [string]$b, [int]$x, [int]$y) {
    $ia = New-Object System.Drawing.Bitmap $a
    $ib = New-Object System.Drawing.Bitmap $b
    try {
        $worst = 0
        for ($dy = 0; $dy -lt $CROP; $dy++) {
            for ($dx = 0; $dx -lt $CROP; $dx++) {
                $d = [Math]::Abs($ia.GetPixel($x + $dx, $y + $dy).R - $ib.GetPixel($x + $dx, $y + $dy).R)
                if ($d -gt $worst) { $worst = $d }
            }
        }
        return $worst
    } finally { $ia.Dispose(); $ib.Dispose() }
}

foreach ($kind in @('scales', 'edge', 'grain', 'fineGrain', 'soft', 'mixed')) {
    New-Target $kind (Join-Path $tmp "$kind.png")
    Write-Host "  target $kind" -ForegroundColor DarkGray
}

foreach ($t in $topics) {
    $src = Join-Path $tmp "$($t.target).png"
    $preset = @('--preset', 'web-1800-natural', '--quality', '100')
    $b = Join-Path $tmp "$($t.key)-b.png"
    $a = Join-Path $tmp "$($t.key)-a.png"
    & $exe sharpen $src -o $b @preset @($t.off) | Out-Null
    & $exe sharpen $src -o $a @preset @($t.on) | Out-Null

    $beforePng = Join-Path $outDir "$($t.key)-before.png"
    $afterPng = Join-Path $outDir "$($t.key)-after.png"
    if ($t.plot) {
        Save-Profile $b $t.x $t.y $beforePng
        Save-Profile $a $t.x $t.y $afterPng
    } else {
        Save-Crop $b $t.x $t.y $beforePng
        Save-Crop $a $t.x $t.y $afterPng
    }

    $delta = Get-MaxDelta $b $a $t.x $t.y
    # Four levels is the floor worth illustrating. The fine band sits just above it, which is not a
    # weakness of the target: at a 0.35 px radius its own contribution to an edge really is that
    # small, which is why it was dead for so long without anyone noticing.
    $colour = if ($delta -lt 4) { 'Yellow' } else { 'Green' }
    Write-Host ("  {0,-9} largest difference {1,3}/255" -f $t.key, $delta) -ForegroundColor $colour
    if ($delta -lt 4) { Write-Warning "$($t.key): the target no longer shows this setting" }
}

Write-Host ""
Write-Host "Help images in $outDir" -ForegroundColor Cyan
