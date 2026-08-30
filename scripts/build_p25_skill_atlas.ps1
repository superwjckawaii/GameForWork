param(
    [Parameter(Mandatory = $true)][string]$SourcePath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $SourcePath))
if ($source.Width -ne 320 -or $source.Height -ne 256) {
    throw "Expected the P21 10x8 32px skill atlas, received $($source.Width)x$($source.Height)."
}

$atlas = New-Object System.Drawing.Bitmap 320, 288, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($atlas)
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy

try {
    $graphics.DrawImageUnscaled($source, 0, 0)
    for ($column = 0; $column -lt 10; $column++) {
        # The final P21 row has two intentionally empty cells; row seven is the last complete ten-icon row.
        $sourceCell = [System.Drawing.Rectangle]::new($column * 32, 192, 32, 32)
        $destination = [System.Drawing.Rectangle]::new($column * 32, 256, 32, 32)
        $attributes = New-Object System.Drawing.Imaging.ImageAttributes
        $matrix = New-Object System.Drawing.Imaging.ColorMatrix
        $matrix.Matrix00 = 1.15
        $matrix.Matrix11 = 0.82
        $matrix.Matrix22 = 1.25
        $attributes.SetColorMatrix($matrix)
        try {
            $graphics.DrawImage($source, $destination, $sourceCell.X, $sourceCell.Y, 32, 32,
                [System.Drawing.GraphicsUnit]::Pixel, $attributes)
        } finally { $attributes.Dispose() }
        $marker = [System.Drawing.Color]::FromArgb(230, 225, 174, 76)
        $brush = New-Object System.Drawing.SolidBrush $marker
        try { $graphics.FillRectangle($brush, $column * 32 + 25, 25 + 256, 4, 4) }
        finally { $brush.Dispose() }
    }
    $directory = Split-Path -Parent $OutputPath
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $atlas.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose()
    $atlas.Dispose()
    $source.Dispose()
}

Write-Host "[p25-art] wrote $OutputPath (90 stable skill-stone cells, 32px)"
