[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipAnimations
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourceRoot = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\p21\imagegen'
$p27SourceRoot = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\p27\imagegen'
$assetRoot = Join-Path $RepositoryRoot 'src\Game.Godot\assets\p21'
$directories = @(
    $assetRoot,
    (Join-Path $assetRoot 'characters'),
    (Join-Path $assetRoot 'enemies'),
    (Join-Path $assetRoot 'regions'),
    (Join-Path $assetRoot 'town'),
    (Join-Path $assetRoot 'ui'),
    (Join-Path $assetRoot 'vfx')
)
foreach ($directory in $directories) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

function New-TransparentBitmap {
    param([int]$Width, [int]$Height)
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    return $bitmap
}

function New-PixelGraphics {
    param([System.Drawing.Image]$Image)
    $graphics = [System.Drawing.Graphics]::FromImage($Image)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    return $graphics
}

function Get-GridRectangle {
    param(
        [System.Drawing.Image]$Image,
        [int]$Index,
        [int]$Columns,
        [int]$Rows
    )
    $column = $Index % $Columns
    $row = [math]::Floor($Index / $Columns)
    $left = [math]::Floor($column * $Image.Width / $Columns)
    $top = [math]::Floor($row * $Image.Height / $Rows)
    $right = [math]::Floor(($column + 1) * $Image.Width / $Columns)
    $bottom = [math]::Floor(($row + 1) * $Image.Height / $Rows)
    return [System.Drawing.Rectangle]::new($left, $top, $right - $left, $bottom - $top)
}

function Get-AlphaBounds {
    param([System.Drawing.Bitmap]$Bitmap)
    $left = $Bitmap.Width
    $top = $Bitmap.Height
    $right = -1
    $bottom = -1
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -le 8) { continue }
            $left = [math]::Min($left, $x)
            $top = [math]::Min($top, $y)
            $right = [math]::Max($right, $x)
            $bottom = [math]::Max($bottom, $y)
        }
    }
    if ($right -lt $left -or $bottom -lt $top) {
        return [System.Drawing.Rectangle]::Empty
    }
    return [System.Drawing.Rectangle]::new($left, $top, $right - $left + 1, $bottom - $top + 1)
}

function Get-GridSprite {
    param(
        [System.Drawing.Bitmap]$Atlas,
        [int]$Index,
        [int]$Columns,
        [int]$Rows
    )
    $cell = Get-GridRectangle -Image $Atlas -Index $Index -Columns $Columns -Rows $Rows
    $raw = New-TransparentBitmap -Width $cell.Width -Height $cell.Height
    $graphics = New-PixelGraphics -Image $raw
    $graphics.DrawImage($Atlas, [System.Drawing.Rectangle]::new(0, 0, $cell.Width, $cell.Height), $cell, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    $bounds = Get-AlphaBounds -Bitmap $raw
    if ($bounds.IsEmpty) { return $raw }
    $cropped = New-TransparentBitmap -Width $bounds.Width -Height $bounds.Height
    $graphics = New-PixelGraphics -Image $cropped
    $graphics.DrawImage($raw, [System.Drawing.Rectangle]::new(0, 0, $bounds.Width, $bounds.Height), $bounds, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    $raw.Dispose()
    return $cropped
}

function Draw-ContainedSprite {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Bitmap]$Sprite,
        [System.Drawing.Rectangle]$Cell,
        [int]$Padding,
        [int]$OffsetX = 0,
        [int]$OffsetY = 0,
        [double]$ScaleMultiplier = 1.0,
        [switch]$FlipHorizontal,
        [double]$Rotation = 0
    )
    if ($Sprite.Width -le 0 -or $Sprite.Height -le 0) { return }
    $availableWidth = [math]::Max(1, $Cell.Width - 2 * $Padding)
    $availableHeight = [math]::Max(1, $Cell.Height - 2 * $Padding)
    $scale = [math]::Min($availableWidth / $Sprite.Width, $availableHeight / $Sprite.Height) * $ScaleMultiplier
    $width = [math]::Max(1, [math]::Round($Sprite.Width * $scale))
    $height = [math]::Max(1, [math]::Round($Sprite.Height * $scale))
    $x = $Cell.X + [math]::Floor(($Cell.Width - $width) / 2) + $OffsetX
    $y = $Cell.Bottom - $Padding - $height + $OffsetY
    $state = $Graphics.Save()
    $centerX = $x + $width / 2.0
    $centerY = $y + $height / 2.0
    $Graphics.TranslateTransform($centerX, $centerY)
    if ($Rotation -ne 0) { $Graphics.RotateTransform([single]$Rotation) }
    $flip = if ($FlipHorizontal) { -1.0 } else { 1.0 }
    $Graphics.ScaleTransform([single]$flip, 1.0)
    $target = [System.Drawing.Rectangle]::new([math]::Round(-$width / 2), [math]::Round(-$height / 2), $width, $height)
    $Graphics.DrawImage($Sprite, $target, 0, 0, $Sprite.Width, $Sprite.Height, [System.Drawing.GraphicsUnit]::Pixel)
    $Graphics.Restore($state)
}

function Get-FrameTransform {
    param([int]$Column, [int]$Direction)
    $offsetX = 0
    $offsetY = 0
    $scale = 1.0
    $rotation = 0.0
    if ($Column -lt 4) {
        $offsetY = @(0, -1, 0, 1)[$Column]
    } elseif ($Column -lt 10) {
        $phase = $Column - 4
        $offsetX = @(0, 1, 2, 1, 0, -1)[$phase]
        $offsetY = @(0, -1, 0, 1, 0, -1)[$phase]
    } elseif ($Column -lt 16) {
        $phase = $Column - 10
        $offsetX = @(0, 1, 3, 5, 2, 0)[$phase]
        $offsetY = @(0, -1, -1, 0, 0, 0)[$phase]
        $rotation = @(-3, 0, 6, 10, 4, 0)[$phase]
    } elseif ($Column -lt 22) {
        $phase = $Column - 16
        $offsetY = @(0, -1, -2, -1, 0, 0)[$phase]
        $scale = @(1.0, 1.02, 1.05, 1.04, 1.02, 1.0)[$phase]
    } elseif ($Column -lt 25) {
        $phase = $Column - 22
        $offsetX = @(-3, 2, 0)[$phase]
        $rotation = @(-5, 3, 0)[$phase]
    } else {
        $phase = $Column - 25
        $rotation = @(0, 8, 20, 38, 65, 90)[$phase]
        $offsetY = -8
        $scale = @(1.0, 1.0, .98, .96, .93, .9)[$phase]
    }
    if ($Direction -eq 1) { $offsetX = -[math]::Abs($offsetX); $rotation = -$rotation }
    if ($Direction -eq 2) { $offsetX = [math]::Abs($offsetX) }
    return [pscustomobject]@{ X = $offsetX; Y = $offsetY; Scale = $scale; Rotation = $rotation }
}

function Build-AnimationAtlas {
    param(
        [System.Collections.Generic.List[System.Drawing.Bitmap]]$Sprites,
        [string]$Destination,
        [int]$CellWidth,
        [int]$CellHeight,
        [int]$Padding
    )
    $columns = 31
    $directions = 4
    $atlas = New-TransparentBitmap -Width ($columns * $CellWidth) -Height ($Sprites.Count * $directions * $CellHeight)
    $graphics = New-PixelGraphics -Image $atlas
    for ($rig = 0; $rig -lt $Sprites.Count; $rig++) {
        for ($direction = 0; $direction -lt $directions; $direction++) {
            for ($column = 0; $column -lt $columns; $column++) {
                $cell = [System.Drawing.Rectangle]::new($column * $CellWidth, ($rig * $directions + $direction) * $CellHeight, $CellWidth, $CellHeight)
                $transform = Get-FrameTransform -Column $column -Direction $direction
                $sideScale = if ($direction -in 1, 2) { .84 } else { 1.0 }
                $frameScale = $transform.Scale * $sideScale
                if ($column -ge 10 -and $column -lt 16) { $frameScale *= .76 }
                if ($column -ge 25) { $frameScale *= .45 }
                Draw-ContainedSprite -Graphics $graphics -Sprite $Sprites[$rig] -Cell $cell -Padding $Padding `
                    -OffsetX $transform.X -OffsetY $transform.Y -ScaleMultiplier $frameScale `
                    -FlipHorizontal:($direction -eq 2) -Rotation $transform.Rotation
            }
        }
    }
    $graphics.Dispose()
    $atlas.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    $atlas.Dispose()
}

function Build-GridAtlas {
    param(
        [System.Drawing.Bitmap]$Source,
        [int]$SourceColumns,
        [int]$SourceRows,
        [int[]]$SourceIndices,
        [string]$Destination,
        [int]$Columns,
        [int]$CellWidth,
        [int]$CellHeight,
        [int]$Padding = 2
    )
    $rows = [math]::Ceiling($SourceIndices.Count / $Columns)
    $atlas = New-TransparentBitmap -Width ($Columns * $CellWidth) -Height ($rows * $CellHeight)
    $graphics = New-PixelGraphics -Image $atlas
    for ($index = 0; $index -lt $SourceIndices.Count; $index++) {
        $sprite = Get-GridSprite -Atlas $Source -Index $SourceIndices[$index] -Columns $SourceColumns -Rows $SourceRows
        $cell = [System.Drawing.Rectangle]::new(($index % $Columns) * $CellWidth, [math]::Floor($index / $Columns) * $CellHeight, $CellWidth, $CellHeight)
        Draw-ContainedSprite -Graphics $graphics -Sprite $sprite -Cell $cell -Padding $Padding
        $sprite.Dispose()
    }
    $graphics.Dispose()
    $atlas.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    $atlas.Dispose()
}

function Build-RegionAtlas {
    param([System.Drawing.Bitmap]$Source, [string]$Destination)
    $cellWidth = 256
    $cellHeight = 144
    $atlas = New-TransparentBitmap -Width ($cellWidth * 4) -Height ($cellHeight * 3)
    $graphics = New-PixelGraphics -Image $atlas
    for ($index = 0; $index -lt 12; $index++) {
        $sourceCell = Get-GridRectangle -Image $Source -Index $index -Columns 4 -Rows 3
        $target = [System.Drawing.Rectangle]::new(($index % 4) * $cellWidth, [math]::Floor($index / 4) * $cellHeight, $cellWidth, $cellHeight)
        $graphics.DrawImage($Source, $target, $sourceCell, [System.Drawing.GraphicsUnit]::Pixel)
        if ($index -lt 5) {
            $act = New-TransparentBitmap -Width $cellWidth -Height $cellHeight
            $actGraphics = New-PixelGraphics -Image $act
            $actGraphics.DrawImage($Source, [System.Drawing.Rectangle]::new(0, 0, $cellWidth, $cellHeight), $sourceCell, [System.Drawing.GraphicsUnit]::Pixel)
            $actGraphics.Dispose()
            $act.Save((Join-Path $assetRoot "regions\act-$($index + 1).png"), [System.Drawing.Imaging.ImageFormat]::Png)
            $act.Dispose()
        }
    }
    $graphics.Dispose()
    $atlas.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    $atlas.Dispose()
}

function Build-MetalAtlas {
    param([string]$Destination)
    $colors = @(
        '#8a6b45', '#73818d', '#c4c0b1', '#d1a339', '#d9e1e7', '#75848f', '#566a78', '#b87848',
        '#9d957d', '#87b7b4', '#b8983e', '#d5a944', '#e1c05a', '#f2d66e', '#c5ced4', '#6f685f',
        '#d7d1b2', '#527e9e', '#a53b35'
    )
    $atlas = New-TransparentBitmap -Width 160 -Height 128
    $graphics = New-PixelGraphics -Image $atlas
    for ($index = 0; $index -lt 19; $index++) {
        $x = ($index % 5) * 32
        $y = [math]::Floor($index / 5) * 32
        $base = [System.Drawing.ColorTranslator]::FromHtml($colors[$index])
        $dark = [System.Drawing.Color]::FromArgb(255, [math]::Max(0, $base.R - 55), [math]::Max(0, $base.G - 55), [math]::Max(0, $base.B - 55))
        $light = [System.Drawing.Color]::FromArgb(255, [math]::Min(255, $base.R + 55), [math]::Min(255, $base.G + 55), [math]::Min(255, $base.B + 55))
        $points = [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new($x + 16, $y + 3), [System.Drawing.Point]::new($x + 27, $y + 9),
            [System.Drawing.Point]::new($x + 25, $y + 24), [System.Drawing.Point]::new($x + 15, $y + 29),
            [System.Drawing.Point]::new($x + 5, $y + 23), [System.Drawing.Point]::new($x + 4, $y + 10)
        )
        $darkBrush = [System.Drawing.SolidBrush]::new($dark)
        $baseBrush = [System.Drawing.SolidBrush]::new($base)
        $lightBrush = [System.Drawing.SolidBrush]::new($light)
        $graphics.FillPolygon($darkBrush, $points)
        $graphics.FillRectangle($baseBrush, $x + 8, $y + 9, 16, 14)
        $graphics.FillRectangle($lightBrush, $x + 11 + ($index % 4), $y + 7, 4, 13)
        $graphics.FillRectangle($lightBrush, $x + 17, $y + 13 + ($index % 3), 5, 4)
        $darkBrush.Dispose(); $baseBrush.Dispose(); $lightBrush.Dispose()
    }
    $graphics.Dispose()
    $atlas.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    $atlas.Dispose()
}

function Build-JewelAtlas {
    param([string]$Destination)
    $colors = @('#d45f52', '#60b57a', '#5c9ed8')
    $atlas = New-TransparentBitmap -Width 96 -Height 32
    $graphics = New-PixelGraphics -Image $atlas
    for ($index = 0; $index -lt 3; $index++) {
        $x = $index * 32
        $color = [System.Drawing.ColorTranslator]::FromHtml($colors[$index])
        $brush = [System.Drawing.SolidBrush]::new($color)
        $dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 31, 27, 35))
        $graphics.FillPolygon($dark, [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new($x + 16, 2), [System.Drawing.Point]::new($x + 29, 16),
            [System.Drawing.Point]::new($x + 16, 30), [System.Drawing.Point]::new($x + 3, 16)))
        $graphics.FillPolygon($brush, [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new($x + 16, 6), [System.Drawing.Point]::new($x + 25, 16),
            [System.Drawing.Point]::new($x + 16, 26), [System.Drawing.Point]::new($x + 7, 16)))
        $graphics.FillRectangle([System.Drawing.Brushes]::White, $x + 14, 9, 3, 8)
        $brush.Dispose(); $dark.Dispose()
    }
    $graphics.Dispose()
    $atlas.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    $atlas.Dispose()
}

function Build-TownPreview {
    param([System.Drawing.Bitmap]$Source, [string]$Destination)
    $output = New-TransparentBitmap -Width 480 -Height 270
    $graphics = New-PixelGraphics -Image $output
    $graphics.DrawImage($Source, [System.Drawing.Rectangle]::new(0, 0, 480, 270), 0, 0, $Source.Width, $Source.Height, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    $output.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    $output.Dispose()
}

if (-not $SkipAnimations) {
    $actorSource = [System.Drawing.Bitmap]::FromFile((Join-Path $sourceRoot 'actor-master.png'))
    $actorSprites = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
    for ($index = 0; $index -lt 5; $index++) { $actorSprites.Add((Get-GridSprite -Atlas $actorSource -Index $index -Columns 5 -Rows 5)) }
    Build-AnimationAtlas -Sprites $actorSprites -Destination (Join-Path $assetRoot 'characters\p21-actor-animation.png') -CellWidth 48 -CellHeight 64 -Padding 7
    foreach ($sprite in $actorSprites) { $sprite.Dispose() }

    $enemySprites = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
    for ($index = 5; $index -lt 21; $index++) { $enemySprites.Add((Get-GridSprite -Atlas $actorSource -Index $index -Columns 5 -Rows 5)) }
    $p27MonsterSource = [System.Drawing.Bitmap]::FromFile((Join-Path $p27SourceRoot 'p27-monster-family-master.png'))
    for ($index = 0; $index -lt 10; $index++) { $enemySprites.Add((Get-GridSprite -Atlas $p27MonsterSource -Index $index -Columns 5 -Rows 2)) }
    Build-AnimationAtlas -Sprites $enemySprites -Destination (Join-Path $assetRoot 'enemies\p21-enemy-animation.png') -CellWidth 48 -CellHeight 64 -Padding 7
    foreach ($sprite in $enemySprites) { $sprite.Dispose() }
    $p27MonsterSource.Dispose()
    $actorSource.Dispose()

    $bossSource = [System.Drawing.Bitmap]::FromFile((Join-Path $sourceRoot 'boss-master.png'))
    $bossSprites = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
    for ($index = 0; $index -lt 12; $index++) { $bossSprites.Add((Get-GridSprite -Atlas $bossSource -Index $index -Columns 4 -Rows 3)) }
    Build-AnimationAtlas -Sprites $bossSprites -Destination (Join-Path $assetRoot 'enemies\p21-boss-animation.png') -CellWidth 72 -CellHeight 80 -Padding 8
    foreach ($sprite in $bossSprites) { $sprite.Dispose() }
    $bossSource.Dispose()
}

$skillSource = [System.Drawing.Bitmap]::FromFile((Join-Path $sourceRoot 'skill-gem-master.png'))
Build-GridAtlas -Source $skillSource -SourceColumns 10 -SourceRows 8 -SourceIndices (0..77) `
    -Destination (Join-Path $assetRoot 'ui\p21-skill-gems.png') -Columns 10 -CellWidth 32 -CellHeight 32 -Padding 1
$skillSource.Dispose()

$vfxSource = [System.Drawing.Bitmap]::FromFile((Join-Path $sourceRoot 'vfx-master.png'))
Build-GridAtlas -Source $vfxSource -SourceColumns 8 -SourceRows 6 -SourceIndices (0..47) `
    -Destination (Join-Path $assetRoot 'vfx\p21-combat-vfx.png') -Columns 8 -CellWidth 64 -CellHeight 64 -Padding 2
$vfxSource.Dispose()

$regionSource = [System.Drawing.Bitmap]::FromFile((Join-Path $sourceRoot 'region-master.png'))
Build-RegionAtlas -Source $regionSource -Destination (Join-Path $assetRoot 'regions\p21-region-atlas.png')
$regionSource.Dispose()

$townSource = [System.Drawing.Bitmap]::FromFile((Join-Path $sourceRoot 'town-master.png'))
Build-TownPreview -Source $townSource -Destination (Join-Path $assetRoot 'town\p21-town-district.png')
Build-GridAtlas -Source $townSource -SourceColumns 4 -SourceRows 2 -SourceIndices (0..6) `
    -Destination (Join-Path $assetRoot 'town\p21-building-atlas.png') -Columns 4 -CellWidth 160 -CellHeight 120 -Padding 3
$townSource.Dispose()

Build-MetalAtlas -Destination (Join-Path $assetRoot 'ui\p21-metal-atlas.png')
Build-JewelAtlas -Destination (Join-Path $assetRoot 'ui\p21-jewel-atlas.png')
& (Join-Path $PSScriptRoot 'build_p21_1_assets.ps1') -RepositoryRoot $RepositoryRoot
if ($LASTEXITCODE -ne 0) { throw "P21.1 asset build failed with exit code $LASTEXITCODE." }

$manifest = [ordered]@{
    version = 1
    style = 'original-low-density-dark-fantasy-pixel-art'
    animation = [ordered]@{
        actions = [ordered]@{ idle = 4; move = 6; attack = 6; cast = 6; hit = 3; death = 6 }
        columns = 31
        directions = @('down', 'left', 'right', 'up')
        actorCell = @(48, 64)
        enemyCell = @(48, 64)
        bossCell = @(72, 80)
        anchor = 'bottom-center'
    }
    counts = [ordered]@{
        actorRigs = 5
        enemyBodyRigs = 26
        enemyTypes = 80
        bossBodyRigs = 12
        bosses = 24
        skillGems = 78
        metalCurrencies = 19
        jewels = 3
        regions = 12
        buildings = 7
        vfx = 48
    }
    sources = @('actor-master.png', 'boss-master.png', 'skill-gem-master.png', 'vfx-master.png', 'region-master.png', 'town-master.png',
        'app-icon-master.png', 'ui-skin-master.png', 'passive-tree-master.png', 'ascendancy-master.png', 'atlas-tree-master.png',
        'p27/p27-monster-family-master.png')
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $assetRoot 'p21-assets.json') -Encoding utf8
Write-Host "P21 assets generated at $assetRoot"
