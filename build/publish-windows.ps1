<#
.SYNOPSIS
  Builds a self-contained Windows x64 release and packs it with Velopack.

.DESCRIPTION
  Publishes the GUI and the CLI into one folder so an install carries both, then hands that folder
  to vpk. The output in build/releases is what a GitHub release must contain: the installer, the
  portable zip, the .nupkg and RELEASES — the updater reads all of them, so publish the whole folder.

  Version must match the tag a release is published under, because that is what the updater compares
  against.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $root 'build/stage'
$releases = Join-Path $root 'build/releases'

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

Write-Host "Publishing AcuLume $Version for $Runtime" -ForegroundColor Cyan

# Self-contained so the target machine needs no .NET install (spec section 47). NativeAOT is
# deliberately not used: libvips is loaded natively and AOT makes that loading fragile.
dotnet publish (Join-Path $root 'src/AcuLume.Gui/AcuLume.Gui.csproj') `
    --configuration Release --runtime $Runtime --self-contained true `
    -p:Version=$Version -p:PublishSingleFile=false `
    --output $stage

# The CLI ships alongside the GUI, so an install gives both entry points.
dotnet publish (Join-Path $root 'src/AcuLume.Cli/AcuLume.Cli.csproj') `
    --configuration Release --runtime $Runtime --self-contained true `
    -p:Version=$Version --output $stage

$icon = Join-Path $root 'src/AcuLume.Gui/Assets/aculume.ico'

dotnet vpk pack `
    --packId AcuLume `
    --packVersion $Version `
    --packDir $stage `
    --mainExe 'aculume-gui.exe' `
    --packTitle 'AcuLume' `
    --packAuthors 'AcuLume' `
    --icon $icon `
    --outputDir $releases

Write-Host "`nRelease artifacts in $releases" -ForegroundColor Green
Get-ChildItem $releases | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,1)}} | Format-Table -AutoSize
