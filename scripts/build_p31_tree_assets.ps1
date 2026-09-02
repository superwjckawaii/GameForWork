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

function Node-Point([object]$node, [System.Drawing.Rectangle]$bounds, [float]$extent) {
    $x = $bounds.X + $bounds.Width / 2.0 + [float]$node.x / $extent * $bounds.Width / 2.0
    $y = $bounds.Y + $bounds.Height / 2.0 + [float]$node.y / $extent * $bounds.Height / 2.0
    return [System.Drawing.PointF]::new([float]$x, [float]$y)
}

function Draw-TreeGeometry {
    param(
        [System.Drawing.Graphics]$Graphics,
        [object]$Tree,
        [System.Drawing.Rectangle]$Bounds,
        [string]$Accent,
        [float]$LineScale = 1.0,
        [bool]$DrawSmallSockets = $false
    )
    $extent = [float]$Tree.extent
    $nodes = @{}
    foreach ($node in @($Tree.nodes)) { $nodes[[string]$node.id] = $node }
    $shadow = [System.Drawing.Pen]::new((Get-Color '#020407' 180), [math]::Max(2.0, 3.2 * $LineScale))
    $line = [System.Drawing.Pen]::new((Get-Color $Accent 105), [math]::Max(1.0, 1.25 * $LineScale))
    try {
        foreach ($edge in @($Tree.edges)) {
            $toNode = $nodes[[string]$edge.to]
            if ($null -eq $toNode) { continue }
            $to = Node-Point $toNode $Bounds $extent
            $fromNode = $nodes[[string]$edge.from]
            $from = if ($null -eq $fromNode) {
                [System.Drawing.PointF]::new($Bounds.X + $Bounds.Width / 2.0, $Bounds.Y + $Bounds.Height / 2.0)
            } else { Node-Point $fromNode $Bounds $extent }
            $Graphics.DrawLine($shadow, $from, $to)
            $Graphics.DrawLine($line, $from, $to)
        }
    } finally { $shadow.Dispose(); $line.Dispose() }

    foreach ($node in @($Tree.nodes)) {
        $kind = [string]$node.kind
        $major = [bool]$node.major -or $kind -in @('Start', 'Notable', 'Mastery', 'Rule', 'JewelSocket', 'Core')
        if (-not $major -and -not $DrawSmallSockets) { continue }
        $point = Node-Point $node $Bounds $extent
        $radius = if ($major) { [math]::Max(4.0, 7.0 * $LineScale) } else { [math]::Max(1.5, 2.2 * $LineScale) }
        $fill = [System.Drawing.SolidBrush]::new((Get-Color '#080d14' 225))
        $rim = [System.Drawing.Pen]::new((Get-Color $Accent $(if ($major) { 190 } else { 90 })), [math]::Max(1.0, 1.4 * $LineScale))
        try {
            $Graphics.FillEllipse($fill, $point.X - $radius, $point.Y - $radius, $radius * 2, $radius * 2)
            $Graphics.DrawEllipse($rim, $point.X - $radius, $point.Y - $radius, $radius * 2, $radius * 2)
        } finally { $fill.Dispose(); $rim.Dispose() }
    }
}

function Save-Passive {
    $size = 2048
    $bitmap = New-Bitmap $size $size
    $graphics = New-Graphics $bitmap
    try {
        Draw-Base $graphics (Join-Path $sourceRoot 'p31-passive-tree-master.png') $size $size
        $shade = [System.Drawing.SolidBrush]::new((Get-Color '#02060d' 78))
        $graphics.FillRectangle($shade, 0, 0, $size, $size); $shade.Dispose()
        Draw-TreeGeometry $graphics $layout.passive ([System.Drawing.Rectangle]::new(0, 0, $size, $size)) '#d2a34d' 1.0 $false
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
        Draw-TreeGeometry $graphics $layout.atlas ([System.Drawing.Rectangle]::new(0, 0, $size, $size)) '#68bfd0' 1.0 $true
        $bitmap.Save((Join-Path $assetRoot 'p31-atlas-backdrop.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}

function Save-Ascendancies {
    $names = @(
        'blood-fighter', 'iron-guardian', 'warbreaker', 'marksman', 'shadowblade', 'venomist',
        'soul-shepherd', 'spirit-cantor', 'hexbinder', 'elementalist', 'void-scholar', 'aegis-mage',
        'martial-monk', 'beast-keeper', 'phantom-master', 'runecarver', 'spellarmor', 'idol-forger'
    )
    $accents = @(
        '#d94b45', '#b6a47d', '#df7835', '#78b9d9', '#a66bd4', '#62b779',
        '#94c6b3', '#e2c56f', '#b05ac8', '#e48b45', '#7259c5', '#69b6d1',
        '#e0b35c', '#72a972', '#9178cf', '#da6952', '#7696d8', '#d3984e'
    )
    $size = 768
    for ($index = 0; $index -lt 18; $index++) {
        $bitmap = New-Bitmap $size $size
        $graphics = New-Graphics $bitmap
        try {
            Draw-Base $graphics (Join-Path $sourceRoot 'p31-ascendancy-master.png') $size $size
            $tint = [System.Drawing.SolidBrush]::new((Get-Color $accents[$index] 34))
            $shade = [System.Drawing.SolidBrush]::new((Get-Color '#03060b' 70))
            $graphics.FillRectangle($tint, 0, 0, $size, $size)
            $graphics.FillRectangle($shade, 0, 0, $size, $size)
            $tint.Dispose(); $shade.Dispose()
            Draw-TreeGeometry $graphics $layout.ascendancies[$index] ([System.Drawing.Rectangle]::new(0, 0, $size, $size)) $accents[$index] 1.15 $false

            $center = [System.Drawing.PointF]::new($size / 2.0, $size / 2.0)
            $emblem = [System.Drawing.Pen]::new((Get-Color $accents[$index] 190), 3)
            $radius = 28 + ($index % 3) * 5
            $points = [System.Drawing.PointF[]]@()
            for ($pointIndex = 0; $pointIndex -lt 6; $pointIndex++) {
                $angle = -[math]::PI / 2 + $pointIndex * [math]::PI / 3 + ($index % 2) * [math]::PI / 6
                $points += [System.Drawing.PointF]::new([float]($center.X + [math]::Cos($angle) * $radius), [float]($center.Y + [math]::Sin($angle) * $radius))
            }
            $graphics.DrawPolygon($emblem, $points); $emblem.Dispose()

            $destination = Join-Path $ascendancyRoot ("p31-ascendancy-{0:D2}-{1}.png" -f ($index + 1), $names[$index])
            $bitmap.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
}

Save-Passive
Save-Atlas
Save-Ascendancies
Write-Host '[p31-tree-assets] generated main passive, atlas and 18 ascendancy backdrops from exact runtime coordinates.'
