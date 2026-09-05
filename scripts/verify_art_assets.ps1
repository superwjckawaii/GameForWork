[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Security

$assetRoot = Join-Path $RepositoryRoot 'src\Game.Godot\assets\art'
$sourceRoot = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\art\imagegen'
$expected = [ordered]@{
    'characters\art-actor-animation.png' = @(1488, 1280)
    'enemies\art-enemy-animation.png' = @(1488, 6656)
    'enemies\art-boss-animation.png' = @(2232, 3840)
    'regions\art-region-atlas.png' = @(1024, 432)
    'town\art-town-district.png' = @(480, 270)
    'town\art-building-atlas.png' = @(640, 240)
    'ui\art-skill-gems.png' = @(320, 256)
    'ui\art-metal-atlas.png' = @(160, 128)
    'ui\art-ui-skin.png' = @(256, 64)
    'brand\art-app-icon.png' = @(256, 256)
    'brand\art-tray-normal.png' = @(32, 32)
    'brand\art-tray-waiting.png' = @(32, 32)
    'brand\art-tray-paused.png' = @(32, 32)
    'brand\art-tray-error.png' = @(32, 32)
}

foreach ($entry in $expected.GetEnumerator()) {
    $path = Join-Path $assetRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing Art asset: $($entry.Key)" }
    $image = [System.Drawing.Bitmap]::FromFile($path)
    try {
        if ($image.Width -ne $entry.Value[0] -or $image.Height -ne $entry.Value[1]) {
            throw "Unexpected dimensions for $($entry.Key): $($image.Width)x$($image.Height)"
        }
    } finally { $image.Dispose() }
}

foreach ($source in @('actor-master.png', 'boss-master.png', 'skill-gem-master.png',
        'region-master.png', 'town-master.png', 'visual-direction-board.png', 'app-icon-master.png',
        'ui-skin-master.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot $source))) { throw "Missing Art editable source: $source" }
}
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'src\Game.Godot\art-source\monsters\imagegen\monsters-monster-family-master.png'))) {
    throw 'Missing Monsters editable monster-family source.'
}

function Assert-TransparentGutters {
    param(
        [string]$RelativePath,
        [int]$Columns,
        [int]$Rows,
        [int]$CellWidth,
        [int]$CellHeight
    )
    $path = Join-Path $assetRoot $RelativePath
    $bitmap = [System.Drawing.Bitmap]::FromFile($path)
    try {
        for ($row = 0; $row -lt $Rows; $row++) {
            for ($column = 0; $column -lt $Columns; $column++) {
                $left = $column * $CellWidth
                $top = $row * $CellHeight
                for ($x = $left; $x -lt $left + $CellWidth; $x++) {
                    if ($bitmap.GetPixel($x, $top).A -gt 8 -or $bitmap.GetPixel($x, $top + $CellHeight - 1).A -gt 8) {
                        throw "Atlas content touches a horizontal cell boundary: $RelativePath cell $column,$row"
                    }
                }
                for ($y = $top; $y -lt $top + $CellHeight; $y++) {
                    if ($bitmap.GetPixel($left, $y).A -gt 8 -or $bitmap.GetPixel($left + $CellWidth - 1, $y).A -gt 8) {
                        throw "Atlas content touches a vertical cell boundary: $RelativePath cell $column,$row"
                    }
                }
            }
        }
    } finally { $bitmap.Dispose() }
}

function Assert-UniqueCells {
    param(
        [string]$RelativePath,
        [int]$Columns,
        [int]$Count,
        [int]$CellSize
    )
    $path = Join-Path $assetRoot $RelativePath
    $bitmap = [System.Drawing.Bitmap]::FromFile($path)
    $hashes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    try {
        for ($index = 0; $index -lt $Count; $index++) {
            $cell = [System.Drawing.Bitmap]::new($CellSize, $CellSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [System.Drawing.Graphics]::FromImage($cell)
            $source = [System.Drawing.Rectangle]::new(($index % $Columns) * $CellSize,
                [math]::Floor($index / $Columns) * $CellSize, $CellSize, $CellSize)
            $graphics.DrawImage($bitmap, [System.Drawing.Rectangle]::new(0, 0, $CellSize, $CellSize), $source,
                [System.Drawing.GraphicsUnit]::Pixel)
            $graphics.Dispose()
            $stream = [System.IO.MemoryStream]::new()
            $cell.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $sha256 = [System.Security.Cryptography.SHA256]::Create()
            try {
                $hashBytes = $sha256.ComputeHash($stream.ToArray())
                $hash = [BitConverter]::ToString($hashBytes).Replace('-', '')
            } finally { $sha256.Dispose() }
            $stream.Dispose(); $cell.Dispose()
            if (-not $hashes.Add($hash)) { throw "Duplicate icon cell in $RelativePath at index $index" }
        }
    } finally { $bitmap.Dispose() }
}

function Assert-CellOccupancy {
    param([string]$RelativePath, [int]$Columns, [int]$Count, [int]$CellSize, [int]$MinimumPixels)
    $path = Join-Path $assetRoot $RelativePath
    $bitmap = [System.Drawing.Bitmap]::FromFile($path)
    try {
        for ($index = 0; $index -lt $Count; $index++) {
            $opaque = 0
            $left = ($index % $Columns) * $CellSize
            $top = [math]::Floor($index / $Columns) * $CellSize
            for ($y = $top; $y -lt $top + $CellSize; $y++) {
                for ($x = $left; $x -lt $left + $CellSize; $x++) {
                    if ($bitmap.GetPixel($x, $y).A -gt 8) { $opaque++ }
                }
            }
            if ($opaque -lt $MinimumPixels) { throw "Incomplete icon cell in $RelativePath at index $index ($opaque opaque pixels)" }
        }
    } finally { $bitmap.Dispose() }
}

Assert-TransparentGutters 'characters\art-actor-animation.png' 31 20 48 64
Assert-TransparentGutters 'enemies\art-enemy-animation.png' 31 104 48 64
Assert-TransparentGutters 'enemies\art-boss-animation.png' 31 48 72 80
Assert-TransparentGutters 'ui\art-skill-gems.png' 10 8 32 32
Assert-TransparentGutters 'ui\art-metal-atlas.png' 5 4 32 32

Assert-UniqueCells 'ui\art-skill-gems.png' 10 78 32
Assert-UniqueCells 'ui\art-metal-atlas.png' 5 19 32
Assert-CellOccupancy 'ui\art-skill-gems.png' 10 78 32 90

$iconPath = Join-Path $assetRoot 'brand\art-app-icon.ico'
if (-not (Test-Path -LiteralPath $iconPath) -or (Get-Item -LiteralPath $iconPath).Length -lt 1kb) {
    throw 'Missing or invalid multi-size Windows application icon.'
}

$manifestPath = Join-Path $assetRoot 'art-assets.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.counts.skillGems -ne 78 -or
    $manifest.counts.enemyTypes -ne 80 -or $manifest.counts.enemyBodyRigs -ne 26 -or
    $manifest.counts.bossBodyRigs -ne 12 -or $manifest.counts.bosses -ne 24 -or $manifest.animation.columns -ne 31) {
    throw 'Art asset manifest counts do not match the frozen content contract.'
}

Write-Host '[art-assets] PASS: dimensions, transparent gutters, stable counts and unique icons.'
