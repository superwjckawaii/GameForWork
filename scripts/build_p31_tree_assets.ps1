[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourceRoot = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\p31\imagegen\trees'
$assetRoot = Join-Path $RepositoryRoot 'src\Game.Godot\assets\p31\trees'
$ascendancyRoot = Join-Path $assetRoot 'ascendancy'
New-Item -ItemType Directory -Force -Path $assetRoot, $ascendancyRoot | Out-Null

$layoutPath = Join-Path ([System.IO.Path]::GetTempPath()) 'gameforwork-p31-tree-layout.json'
$exportProject = Join-Path $RepositoryRoot 'tools\P21TreeExport\P21TreeExport.csproj'
& dotnet run --project $exportProject --configuration Release --no-launch-profile -- $layoutPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'P31 tree layout export failed.' }
$layout = Get-Content -LiteralPath $layoutPath -Raw | ConvertFrom-Json

function New-Bitmap([int]$width, [int]$height) {
    $bitmap = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    return $bitmap
}

function New-Graphics([System.Drawing.Image]$image) {
    $graphics = [System.Drawing.Graphics]::FromImage($image)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    return $graphics
}

function Get-Color([string]$hex, [int]$alpha = 255) {
    $base = [System.Drawing.ColorTranslator]::FromHtml($hex)
    return [System.Drawing.Color]::FromArgb($alpha, $base.R, $base.G, $base.B)
}

function Draw-Base([System.Drawing.Graphics]$graphics, [string]$path, [int]$width, [int]$height) {
    $source = [System.Drawing.Bitmap]::FromFile($path)
    try {
        $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $width, $height))
    } finally { $source.Dispose() }
}

function Node-Point([object]$node, [System.Drawing.Rectangle]$bounds) {
    $x = $bounds.X + ([float]$node.normalizedX + 1.0) * $bounds.Width / 2.0
    $y = $bounds.Y + ([float]$node.normalizedY + 1.0) * $bounds.Height / 2.0
    return [System.Drawing.PointF]::new([float]$x, [float]$y)
}

function Draw-TreeGeometry {
    param(
        [System.Drawing.Graphics]$Graphics,
        [object]$Tree,
        [System.Drawing.Rectangle]$Bounds,
        [string]$Accent,
        [float]$LineScale = 1.0
    )
    $nodes = @{}
    foreach ($node in @($Tree.nodes)) { $nodes[[string]$node.id] = $node }
    # The bitmap supplies recessed rails only. Runtime owns every socket because
    # small nodes are intentionally culled at low zoom and their state colors change.
    $shadow = [System.Drawing.Pen]::new((Get-Color '#010307' 220), [math]::Max(7.0, 11.0 * $LineScale))
    $rail = [System.Drawing.Pen]::new((Get-Color $Accent 92), [math]::Max(2.0, 3.2 * $LineScale))
    $highlight = [System.Drawing.Pen]::new((Get-Color '#b9c2cb' 34), [math]::Max(0.8, 1.0 * $LineScale))
    try {
        foreach ($edge in @($Tree.edges)) {
            $toNode = $nodes[[string]$edge.to]
            if ($null -eq $toNode) { continue }
            $to = Node-Point $toNode $Bounds
            $fromNode = $nodes[[string]$edge.from]
            $from = if ($null -eq $fromNode) {
                [System.Drawing.PointF]::new($Bounds.X + $Bounds.Width / 2.0, $Bounds.Y + $Bounds.Height / 2.0)
            } else { Node-Point $fromNode $Bounds }
            $Graphics.DrawLine($shadow, $from, $to)
            $Graphics.DrawLine($rail, $from, $to)
            $Graphics.DrawLine($highlight, $from, $to)
        }
    } finally { $shadow.Dispose(); $rail.Dispose(); $highlight.Dispose() }
}

function Draw-RotatedBase([System.Drawing.Graphics]$graphics, [string]$path, [int]$width, [int]$height) {
    $source = [System.Drawing.Bitmap]::FromFile($path)
    try {
        $source.RotateFlip([System.Drawing.RotateFlipType]::Rotate90FlipNone)
        $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $width, $height))
    } finally { $source.Dispose() }
}

function Draw-AscendancyHexBase([System.Drawing.Graphics]$graphics, [int]$size, [int]$variant, [object]$tree) {
    $graphics.Clear((Get-Color '#06090e'))
    $center = [System.Drawing.PointF]::new($size / 2.0, $size / 2.0)
    $bounds = [System.Drawing.Rectangle]::new(0, 0, $size, $size)
    $coreNodes = @($tree.nodes | Where-Object { [bool]$_.major })
    if ($coreNodes.Count -ne 6) { throw "Ascendancy backdrop requires 6 core nodes, got $($coreNodes.Count)." }
    [System.Drawing.PointF[]]$vertices = @($coreNodes |
        ForEach-Object { Node-Point $_ $bounds } |
        Sort-Object { [math]::Atan2($_.Y - $center.Y, $_.X - $center.X) })

    $field = [System.Drawing.SolidBrush]::new((Get-Color '#0c1118'))
    $sectorA = [System.Drawing.SolidBrush]::new((Get-Color '#111720'))
    $sectorB = [System.Drawing.SolidBrush]::new((Get-Color '#0b1017'))
    $outerShadow = [System.Drawing.Pen]::new((Get-Color '#010205' 235), 24)
    $outerRail = [System.Drawing.Pen]::new((Get-Color '#665a47' 210), 7)
    $outerLight = [System.Drawing.Pen]::new((Get-Color '#b29a70' 105), 2)
    try {
        $graphics.FillPolygon($field, [System.Drawing.PointF[]]$vertices)
        for ($i = 0; $i -lt 6; $i++) {
            $triangle = [System.Drawing.PointF[]]@($center, $vertices[$i], $vertices[($i + 1) % 6])
            $graphics.FillPolygon($(if (($i + $variant) % 2 -eq 0) { $sectorA } else { $sectorB }), [System.Drawing.PointF[]]$triangle)
        }
        $graphics.DrawPolygon($outerShadow, [System.Drawing.PointF[]]$vertices)
        $graphics.DrawPolygon($outerRail, [System.Drawing.PointF[]]$vertices)
        $graphics.DrawPolygon($outerLight, [System.Drawing.PointF[]]$vertices)
    } finally {
        $field.Dispose(); $sectorA.Dispose(); $sectorB.Dispose()
        $outerShadow.Dispose(); $outerRail.Dispose(); $outerLight.Dispose()
    }

    $spokeShadow = [System.Drawing.Pen]::new((Get-Color '#010205' 240), 38)
    $spokeRail = [System.Drawing.Pen]::new((Get-Color '#3d4651' 230), 24)
    $spokeInset = [System.Drawing.Pen]::new((Get-Color '#151c25' 255), 16)
    $spokeEdge = [System.Drawing.Pen]::new((Get-Color '#8b7960' 130), 2)
    try {
        foreach ($vertex in $vertices) {
            $graphics.DrawLine($spokeShadow, $center, $vertex)
            $graphics.DrawLine($spokeRail, $center, $vertex)
            $graphics.DrawLine($spokeInset, $center, $vertex)
            $graphics.DrawLine($spokeEdge, $center, $vertex)
        }
    } finally {
        $spokeShadow.Dispose(); $spokeRail.Dispose(); $spokeInset.Dispose(); $spokeEdge.Dispose()
    }

    $hubFill = [System.Drawing.SolidBrush]::new((Get-Color '#090d13'))
    $hubOuter = [System.Drawing.Pen]::new((Get-Color '#06080c'), 16)
    $hubRail = [System.Drawing.Pen]::new((Get-Color '#79684f' 215), 6)
    $hubLight = [System.Drawing.Pen]::new((Get-Color '#c0a576' 105), 2)
    try {
        foreach ($vertex in $vertices) {
            $graphics.FillEllipse($hubFill, $vertex.X - 58, $vertex.Y - 58, 116, 116)
            $graphics.DrawEllipse($hubOuter, $vertex.X - 58, $vertex.Y - 58, 116, 116)
            $graphics.DrawEllipse($hubRail, $vertex.X - 51, $vertex.Y - 51, 102, 102)
            $graphics.DrawEllipse($hubLight, $vertex.X - 44, $vertex.Y - 44, 88, 88)
        }
        $graphics.FillEllipse($hubFill, $center.X - 74, $center.Y - 74, 148, 148)
        $graphics.DrawEllipse($hubOuter, $center.X - 74, $center.Y - 74, 148, 148)
        $graphics.DrawEllipse($hubRail, $center.X - 66, $center.Y - 66, 132, 132)
        $graphics.DrawEllipse($hubLight, $center.X - 56, $center.Y - 56, 112, 112)
    } finally {
        $hubFill.Dispose(); $hubOuter.Dispose(); $hubRail.Dispose(); $hubLight.Dispose()
    }

    $frameDark = [System.Drawing.Pen]::new((Get-Color '#020408'), 22)
    $frameRail = [System.Drawing.Pen]::new((Get-Color '#4e4639'), 6)
    $frameLight = [System.Drawing.Pen]::new((Get-Color '#9a835e' 100), 2)
    try {
        $graphics.DrawRectangle($frameDark, 20, 20, $size - 40, $size - 40)
        $graphics.DrawRectangle($frameRail, 27, 27, $size - 54, $size - 54)
        $graphics.DrawRectangle($frameLight, 32, 32, $size - 64, $size - 64)
    } finally { $frameDark.Dispose(); $frameRail.Dispose(); $frameLight.Dispose() }
}

function Save-Passive {
    $size = 2048
    $bitmap = New-Bitmap $size $size
    $graphics = New-Graphics $bitmap
    try {
        Draw-RotatedBase $graphics (Join-Path $sourceRoot 'p31-passive-tree-master.png') $size $size
        $shade = [System.Drawing.SolidBrush]::new((Get-Color '#02060d' 210))
        $graphics.FillRectangle($shade, 0, 0, $size, $size); $shade.Dispose()
        Draw-TreeGeometry $graphics $layout.passive ([System.Drawing.Rectangle]::new(0, 0, $size, $size)) '#7c6540' 1.0
        $bitmap.Save((Join-Path $assetRoot 'p31-passive-backdrop.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}

function Save-Atlas {
    $size = 2048
    $bitmap = New-Bitmap $size $size
    $graphics = New-Graphics $bitmap
    try {
        Draw-Base $graphics (Join-Path $sourceRoot 'p31-atlas-tree-master.png') $size $size
        $field = [System.Drawing.Rectangle]::new(96, 170, 1856, 1710)
        $fieldBrush = [System.Drawing.SolidBrush]::new((Get-Color '#070d14' 225))
        $graphics.FillRectangle($fieldBrush, $field); $fieldBrush.Dispose()
        for ($lane = 0; $lane -lt 10; $lane++) {
            $xWorld = -630 + $lane * 140
            $x = $size / 2.0 + $xWorld / [float]$layout.atlas.extent * $size / 2.0
            $laneRect = [System.Drawing.RectangleF]::new([float]($x - 50), 205, 100, 1635)
            $laneFill = [System.Drawing.SolidBrush]::new((Get-Color $(if (($lane % 2) -eq 0) { '#111b25' } else { '#0c151e' }) 235))
            $laneRim = [System.Drawing.Pen]::new((Get-Color '#47697a' 105), 2)
            $graphics.FillRectangle($laneFill, $laneRect); $graphics.DrawRectangle($laneRim, $laneRect.X, $laneRect.Y, $laneRect.Width, $laneRect.Height)
            $laneFill.Dispose(); $laneRim.Dispose()
        }
        Draw-TreeGeometry $graphics $layout.atlas ([System.Drawing.Rectangle]::new(0, 0, $size, $size)) '#496d78' 1.0
        $bitmap.Save((Join-Path $assetRoot 'p31-atlas-backdrop.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}

function Save-Ascendancies {
    $names = @(
        'blood-fighter', 'iron-guardian', 'warbreaker', 'marksman', 'shadowblade', 'venomist',
        'soul-shepherd', 'spirit-cantor', 'hexbinder', 'elementalist', 'void-scholar', 'aegis-mage',
        'martial-monk', 'beast-keeper', 'phantom-master', 'runecarver', 'spellarmor', 'idol-forger'
    )
    $size = 768
    for ($index = 0; $index -lt 18; $index++) {
        $bitmap = New-Bitmap $size $size
        $graphics = New-Graphics $bitmap
        try {
            Draw-AscendancyHexBase $graphics $size $index $layout.ascendancies[$index]
            Draw-TreeGeometry $graphics $layout.ascendancies[$index] ([System.Drawing.Rectangle]::new(0, 0, $size, $size)) '#625e57' 1.0

            $center = [System.Drawing.PointF]::new($size / 2.0, $size / 2.0)
            $emblem = [System.Drawing.Pen]::new((Get-Color '#8a8172' 120), 3)
            $radius = 28 + ($index % 3) * 5
            $points = [System.Drawing.PointF[]]::new(6)
            for ($pointIndex = 0; $pointIndex -lt 6; $pointIndex++) {
                $angle = -[math]::PI / 2 + $pointIndex * [math]::PI / 3 + ($index % 2) * [math]::PI / 6
                $points[$pointIndex] = [System.Drawing.PointF]::new([float]($center.X + [math]::Cos($angle) * $radius), [float]($center.Y + [math]::Sin($angle) * $radius))
            }
            $graphics.DrawPolygon($emblem, [System.Drawing.PointF[]]$points); $emblem.Dispose()

            $destination = Join-Path $ascendancyRoot ("p31-ascendancy-{0:D2}-{1}.png" -f ($index + 1), $names[$index])
            $bitmap.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
}

Save-Passive
Save-Atlas
Save-Ascendancies
Write-Host '[p31-tree-assets] generated main passive, atlas and 18 ascendancy backdrops from exact runtime coordinates.'
