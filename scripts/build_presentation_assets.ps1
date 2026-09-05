[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourcePath = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\presentation\imagegen\presentation-vfx-master.png'
$assetRoot = Join-Path $RepositoryRoot 'src\Game.Godot\assets\presentation'
$vfxRoot = Join-Path $assetRoot 'vfx'
$outputPath = Join-Path $vfxRoot 'presentation-combat-vfx.png'
$manifestPath = Join-Path $assetRoot 'presentation-assets.json'
if (-not (Test-Path -LiteralPath $sourcePath)) { throw "Missing Presentation VFX source: $sourcePath" }
New-Item -ItemType Directory -Force -Path $vfxRoot | Out-Null

function Find-AlphaBounds([System.Drawing.Bitmap]$Bitmap, [System.Drawing.Rectangle]$Cell) {
    $left = $Cell.Right
    $top = $Cell.Bottom
    $right = $Cell.Left - 1
    $bottom = $Cell.Top - 1
    for ($y = $Cell.Top; $y -lt $Cell.Bottom; $y += 1) {
        for ($x = $Cell.Left; $x -lt $Cell.Right; $x += 1) {
            if ($Bitmap.GetPixel($x, $y).A -lt 8) { continue }
            $left = [Math]::Min($left, $x)
            $top = [Math]::Min($top, $y)
            $right = [Math]::Max($right, $x)
            $bottom = [Math]::Max($bottom, $y)
        }
    }
    if ($right -lt $left -or $bottom -lt $top) {
        return [System.Drawing.Rectangle]::new($Cell.Left, $Cell.Top, 1, 1)
    }
    return [System.Drawing.Rectangle]::FromLTRB($left, $top, $right + 1, $bottom + 1)
}

$source = [System.Drawing.Bitmap]::new($sourcePath)
$atlas = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($atlas)
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy

$cellWidth = [Math]::Floor($source.Width / 4)
$cellHeight = [Math]::Floor($source.Height / 4)
for ($row = 0; $row -lt 4; $row += 1) {
    for ($column = 0; $column -lt 4; $column += 1) {
        $sourceCell = [System.Drawing.Rectangle]::new(
            $column * $cellWidth,
            $row * $cellHeight,
            $(if ($column -eq 3) { $source.Width - $column * $cellWidth } else { $cellWidth }),
            $(if ($row -eq 3) { $source.Height - $row * $cellHeight } else { $cellHeight }))
        $bounds = Find-AlphaBounds $source $sourceCell
        $scale = [Math]::Min(56.0 / $bounds.Width, 56.0 / $bounds.Height)
        $width = [Math]::Max(1, [Math]::Round($bounds.Width * $scale))
        $height = [Math]::Max(1, [Math]::Round($bounds.Height * $scale))
        $destination = [System.Drawing.Rectangle]::new(
            $column * 64 + [Math]::Floor((64 - $width) / 2),
            $row * 64 + [Math]::Floor((64 - $height) / 2),
            $width,
            $height)
        $graphics.DrawImage($source, $destination, $bounds, [System.Drawing.GraphicsUnit]::Pixel)
    }
}

$atlas.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$atlas.Dispose()
$source.Dispose()

$hash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [ordered]@{
    version = '0.4.0'
    source = 'art-source/presentation/imagegen/presentation-vfx-master.png'
    generated = 'assets/presentation/vfx/presentation-combat-vfx.png'
    grid = '4x4'
    cell = '64x64'
    isolation = 'per-cell alpha bounds with 4px destination padding'
    sha256 = $hash
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Host "[presentation-assets] generated=$outputPath sha256=$hash"
