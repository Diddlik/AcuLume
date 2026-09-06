<#
.SYNOPSIS
  Renders the before/after pairs the Sharpen panel shows when a section's help is expanded.

.DESCRIPTION
  Generated from the real engine and the real preset values, never drawn by hand: the presets have
  been recalibrated several times, and a hand-made illustration would quietly become a lie. Re-run
  this whenever the pipeline or the presets change.

  Each pair isolates one setting — everything else stays at the preset, only that one parameter
  moves between "off" and its preset value — cropped 1:1 from the 1800 px output at a region picked
  once by eye where that setting actually does something.

  The source photographs are not in the repository (samples/ is ignored), so this runs on a machine
  that has them. The generated images are committed.
#>
[CmdletBinding()]
param([string]$SamplesDir = 'samples/original')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src/AcuLume.Cli/bin/Debug/net10.0/aculume.exe'
$outDir = Join-Path $root 'src/AcuLume.Gui/Assets/help'
$tmp = Join-Path $env:TEMP 'aculume-help'
$CROP = 75          # cropped tight, then doubled: these differences need a 200% view
$ZOOM = 2

if (-not (Test-Path $exe)) { throw "Build the CLI first: dotnet build" }
New-Item -ItemType Directory -Force $outDir | Out-Null
New-Item -ItemType Directory -Force $tmp | Out-Null
Add-Type -AssemblyName System.Drawing

# Regions were first chosen by maximising the measured before/after difference, which reliably
# landed on gravel, grass and foliage: sharpening acts on fine detail, and fine detail means busy
# content, so the most-changed window is always the least photogenic one. These are architectural
# stonework instead — recognisable subjects a photographer would use to judge sharpness — at the
# cost of a smaller measured difference, which the tighter crop and the 2x magnification carry.
#
# Portrait subjects were measured and cannot work at all: on eyes and skin even a fiftyfold fine
# amount moves the crop by 0.4 out of 255, because the fine band has almost nothing to act on there.
#
# Most of these settings move a 150 px crop by only 1-3 levels out of 255 at their calibrated
# values, which would ship as two identical-looking images and teach the opposite of the truth. Those
# topics are therefore rendered with the setting pushed well past its preset, and the panel says so
# next to them. `medium` and `edge` are strong enough to be shown at their real values and are not
# exaggerated — keep that split in sync with HelpTopic.IsExaggerated in the GUI.
#
# The halo limiter is the extreme case. At its default limits (dark 0.5, light 0.3) the band
# contribution never reaches them, so it changes nothing at all; it only becomes visible with the
# limits tightened to 0.1/0.05. That is a finding about the defaults, not about this illustration.
$topics = @(
    @{ key='fine';    photo='20160618_194530_Pro2_5986.jpg'; x=548; y=356;
       off=@('--fine-amount','0'); on=@('--fine-amount','300') }
    @{ key='medium';  photo='20160618_194530_Pro2_5986.jpg'; x=548; y=356;
       off=@('--medium-amount','0'); on=@() }
    @{ key='balance'; photo='20160618_194530_Pro2_5986.jpg'; x=648; y=326;
       off=@('--darken','0.5','--lighten','0.5'); on=@('--darken','1.6','--lighten','0.15') }
    @{ key='noise';   photo='20160618_194530_Pro2_5986.jpg'; x=548; y=356;
       off=@('--fine-amount','24','--noise-protection','0'); on=@('--fine-amount','24') }
    @{ key='edge';    photo='20160618_194530_Pro2_5986.jpg'; x=548; y=356;
       off=@('--edge-protection','0'); on=@() }
    @{ key='halo';    photo='20160618_192506_Pro2_5956-HDR.jpg'; x=588; y=288;
       off=@('--fine-amount','60','--halo-protection','0')
       on=@('--fine-amount','60','--halo-protection','1.0','--halo-dark-limit','0.03','--halo-light-limit','0.02') }
    @{ key='capture'; photo='20160618_194530_Pro2_5986.jpg'; x=648; y=326;
       off=@(); on=@('--capture-sharpen','Normal','--capture-radius','1.6') }
)

function Save-Crop([string]$src, [int]$x, [int]$y, [string]$dest) {
    $img = [System.Drawing.Image]::FromFile($src)
    try {
        $cx = [Math]::Min($x, [Math]::Max(0, $img.Width - $CROP))
        $cy = [Math]::Min($y, [Math]::Max(0, $img.Height - $CROP))
        $size = $CROP * $ZOOM
        $bmp = New-Object System.Drawing.Bitmap $size, $size
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        # Nearest neighbour: a smoothing filter would hide exactly the pixel-level difference shown.
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $g.DrawImage($img, (New-Object System.Drawing.Rectangle 0, 0, $size, $size),
                     (New-Object System.Drawing.Rectangle $cx, $cy, $CROP, $CROP),
                     [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
        $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    } finally { $img.Dispose() }
}

# A pair that shows nothing is worse than no illustration, so measure what was produced.
function Get-MeanDelta([string]$a, [string]$b) {
    $ia = New-Object System.Drawing.Bitmap $a
    $ib = New-Object System.Drawing.Bitmap $b
    try {
        $sum = 0.0
        for ($y = 0; $y -lt $ia.Height; $y += 2) {
            for ($x = 0; $x -lt $ia.Width; $x += 2) {
                $pa = $ia.GetPixel($x, $y); $pb = $ib.GetPixel($x, $y)
                $sum += [Math]::Abs($pa.R - $pb.R) + [Math]::Abs($pa.G - $pb.G) + [Math]::Abs($pa.B - $pb.B)
            }
        }
        return $sum / (($ia.Width / 2) * ($ia.Height / 2) * 3)
    } finally { $ia.Dispose(); $ib.Dispose() }
}

foreach ($t in $topics) {
    $src = Join-Path $root (Join-Path $SamplesDir $t.photo)
    if (-not (Test-Path $src)) { Write-Warning "missing $($t.photo), skipping $($t.key)"; continue }

    $beforeArgs = @('--preset','web-1800-natural') + $t.off
    $afterArgs  = @('--preset','web-1800-natural') + $t.on

    $b = Join-Path $tmp "$($t.key)-b.jpg"; $a = Join-Path $tmp "$($t.key)-a.jpg"
    & $exe sharpen $src -o $b @beforeArgs --quality 100 | Out-Null
    & $exe sharpen $src -o $a @afterArgs  --quality 100 | Out-Null

    $beforePng = Join-Path $outDir "$($t.key)-before.png"
    $afterPng = Join-Path $outDir "$($t.key)-after.png"
    Save-Crop $b $t.x $t.y $beforePng
    Save-Crop $a $t.x $t.y $afterPng

    $delta = Get-MeanDelta $beforePng $afterPng
    $colour = if ($delta -lt 0.5) { 'Yellow' } else { 'Green' }
    Write-Host ("  {0,-9} mean delta {1:N2}/255" -f $t.key, $delta) -ForegroundColor $colour
    if ($delta -lt 0.5) { Write-Warning "$($t.key): the pair is nearly identical — the region or the setting no longer shows anything" }
}

Write-Host "`nHelp images in $outDir" -ForegroundColor Cyan
Get-ChildItem $outDir | Measure-Object -Property Length -Sum |
    ForEach-Object { "{0} files, {1:N0} KB total" -f $_.Count, ($_.Sum/1KB) }
