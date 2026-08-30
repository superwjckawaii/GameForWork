param(
    [Parameter(Mandatory = $true)][string]$SourcePath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Find-ContentBounds([System.Drawing.Bitmap]$bitmap, [System.Drawing.Rectangle]$cell) {
    $left = $cell.Right
    $top = $cell.Bottom
    $right = $cell.Left
    $bottom = $cell.Top
    for ($y = $cell.Top; $y -lt $cell.Bottom; $y += 2) {
        for ($x = $cell.Left; $x -lt $cell.Right; $x += 2) {
            if ($bitmap.GetPixel($x, $y).A -lt 12) { continue }
            $left = [Math]::Min($left, $x)
            $top = [Math]::Min($top, $y)
            $right = [Math]::Max($right, $x)
            $bottom = [Math]::Max($bottom, $y)
        }
    }
    if ($right -lt $left -or $bottom -lt $top) { throw "No visible icon in cell $cell" }
    $padding = 4
    return [System.Drawing.Rectangle]::FromLTRB(
        [Math]::Max($cell.Left, $left - $padding), [Math]::Max($cell.Top, $top - $padding),
        [Math]::Min($cell.Right, $right + $padding), [Math]::Min($cell.Bottom, $bottom + $padding))
}

$source = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $SourcePath))
$atlas = New-Object System.Drawing.Bitmap 320, 192, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($atlas)
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy

try {
    for ($category = 0; $category -lt 10; $category++) {
        $sourceColumn = $category % 5
        $sourceRow = [Math]::Floor($category / 5)
        $x0 = [Math]::Floor($sourceColumn * $source.Width / 5)
        $x1 = [Math]::Floor(($sourceColumn + 1) * $source.Width / 5)
        $y0 = [Math]::Floor($sourceRow * $source.Height / 2)
        $y1 = [Math]::Floor(($sourceRow + 1) * $source.Height / 2)
        $bounds = Find-ContentBounds $source ([System.Drawing.Rectangle]::FromLTRB($x0, $y0, $x1, $y1))
        for ($variant = 0; $variant -lt 6; $variant++) {
            $scale = [Math]::Min(26.0 / $bounds.Width, 26.0 / $bounds.Height)
            $width = [Math]::Max(1, [Math]::Round($bounds.Width * $scale))
            $height = [Math]::Max(1, [Math]::Round($bounds.Height * $scale))
            $destinationX = [int]($category * 32 + [Math]::Floor((32 - $width) / 2))
            $destinationY = [int]($variant * 32 + [Math]::Floor((32 - $height) / 2))
            $destination = [System.Drawing.Rectangle]::new($destinationX, $destinationY, [int]$width, [int]$height)
            $attributes = New-Object System.Drawing.Imaging.ImageAttributes
            $attributes.SetGamma([single]@(1.45, 1.28, 1.14, 1.00, 0.90, 0.82)[$variant])
            try {
                $graphics.DrawImage($source, $destination, $bounds.X, $bounds.Y, $bounds.Width, $bounds.Height,
                    [System.Drawing.GraphicsUnit]::Pixel, $attributes)
            } finally { $attributes.Dispose() }
        }
    }
    $directory = Split-Path -Parent $OutputPath
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $atlas.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose()
    $atlas.Dispose()
    $source.Dispose()
}

Write-Host "[p25-art] wrote $OutputPath (10 categories x 6 variants, 32px cells)"
