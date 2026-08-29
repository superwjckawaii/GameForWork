[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Security

$assetRoot = Join-Path $RepositoryRoot 'src\Game.Godot\assets\p21'
$sourceRoot = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\p21\imagegen'
$expected = [ordered]@{
    'characters\p21-actor-animation.png' = @(1488, 1280)
    'enemies\p21-enemy-animation.png' = @(1488, 4096)
    'enemies\p21-boss-animation.png' = @(2232, 3840)
    'regions\p21-region-atlas.png' = @(1024, 432)
    'town\p21-town-district.png' = @(480, 270)
    'town\p21-building-atlas.png' = @(640, 240)
    'ui\p21-item-bases.png' = @(320, 256)
    'ui\p21-unique-items.png' = @(160, 160)
    'ui\p21-skill-gems.png' = @(320, 256)
    'ui\p21-metal-atlas.png' = @(160, 128)
    'ui\p21-jewel-atlas.png' = @(96, 32)
    'ui\p21-ui-skin.png' = @(256, 64)
    'vfx\p21-combat-vfx.png' = @(512, 384)
    'trees\p21-passive-backdrop.png' = @(512, 512)
    'trees\p21-ascendancy-backdrops.png' = @(1152, 384)
    'trees\p21-atlas-backdrop.png' = @(512, 512)
    'brand\p21-app-icon.png' = @(256, 256)
    'brand\p21-tray-normal.png' = @(32, 32)
    'brand\p21-tray-waiting.png' = @(32, 32)
    'brand\p21-tray-paused.png' = @(32, 32)
    'brand\p21-tray-error.png' = @(32, 32)
}

foreach ($entry in $expected.GetEnumerator()) {
    $path = Join-Path $assetRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing P21 asset: $($entry.Key)" }
    $image = [System.Drawing.Bitmap]::FromFile($path)
    try {
        if ($image.Width -ne $entry.Value[0] -or $image.Height -ne $entry.Value[1]) {
            throw "Unexpected dimensions for $($entry.Key): $($image.Width)x$($image.Height)"
        }
    } finally { $image.Dispose() }
}

foreach ($source in @('actor-master.png', 'boss-master.png', 'equipment-master.png', 'skill-gem-master.png',
        'vfx-master.png', 'region-master.png', 'town-master.png', 'visual-direction-board.png', 'app-icon-master.png',
        'ui-skin-master.png', 'passive-tree-master.png', 'ascendancy-master.png', 'atlas-tree-master.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot $source))) { throw "Missing P21 editable source: $source" }
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
            $hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream.ToArray()))
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

Assert-TransparentGutters 'characters\p21-actor-animation.png' 31 20 48 64
Assert-TransparentGutters 'enemies\p21-enemy-animation.png' 31 64 48 64
Assert-TransparentGutters 'enemies\p21-boss-animation.png' 31 48 72 80
Assert-TransparentGutters 'ui\p21-item-bases.png' 10 8 32 32
Assert-TransparentGutters 'ui\p21-unique-items.png' 5 5 32 32
Assert-TransparentGutters 'ui\p21-skill-gems.png' 10 8 32 32
Assert-TransparentGutters 'ui\p21-metal-atlas.png' 5 4 32 32
Assert-TransparentGutters 'ui\p21-jewel-atlas.png' 3 1 32 32
Assert-TransparentGutters 'vfx\p21-combat-vfx.png' 8 6 64 64

Assert-UniqueCells 'ui\p21-item-bases.png' 10 80 32
Assert-UniqueCells 'ui\p21-unique-items.png' 5 25 32
Assert-UniqueCells 'ui\p21-skill-gems.png' 10 78 32
Assert-UniqueCells 'ui\p21-metal-atlas.png' 5 19 32
Assert-CellOccupancy 'ui\p21-item-bases.png' 10 80 32 55
Assert-CellOccupancy 'ui\p21-unique-items.png' 5 25 32 55
Assert-CellOccupancy 'ui\p21-skill-gems.png' 10 78 32 90

$iconPath = Join-Path $assetRoot 'brand\p21-app-icon.ico'
if (-not (Test-Path -LiteralPath $iconPath) -or (Get-Item -LiteralPath $iconPath).Length -lt 1kb) {
    throw 'Missing or invalid multi-size Windows application icon.'
}

$manifestPath = Join-Path $assetRoot 'p21-assets.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.counts.itemBases -ne 80 -or $manifest.counts.skillGems -ne 78 -or
    $manifest.counts.enemyTypes -ne 48 -or $manifest.animation.columns -ne 31) {
    throw 'P21 asset manifest counts do not match the frozen content contract.'
}

Write-Host '[p21-assets] PASS: dimensions, transparent gutters, stable counts and unique icons.'
