[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Security

$catalogPath = Join-Path $RepositoryRoot 'src\Game.Core\P19\Data\p19_catalog.json'
$sourceRoot = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\p25\equipment'
$outputRoot = Join-Path $RepositoryRoot 'src\Game.Godot\assets\p25\ui'
$equipmentPath = Join-Path $outputRoot 'p25-equipment-atlas.png'
$legendaryPath = Join-Path $outputRoot 'p25-legendary-atlas.png'

$sourcePaths = @{
    weapons = Join-Path $sourceRoot 'master-weapons.png'
    armor = Join-Path $sourceRoot 'master-armor.png'
    accessories = Join-Path $sourceRoot 'master-accessories.png'
    ranged = Join-Path $sourceRoot 'master-special-ranged.png'
    class = Join-Path $sourceRoot 'master-special-class.png'
}
foreach ($path in $sourcePaths.Values) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing P25 equipment source: $path" }
}

function Get-CellRectangle([System.Drawing.Bitmap]$bitmap, [int]$column, [int]$row, [int]$columns, [int]$rows) {
    $left = [int][Math]::Floor($column * $bitmap.Width / $columns)
    $right = [int][Math]::Floor(($column + 1) * $bitmap.Width / $columns)
    $top = [int][Math]::Floor($row * $bitmap.Height / $rows)
    $bottom = [int][Math]::Floor(($row + 1) * $bitmap.Height / $rows)
    return [System.Drawing.Rectangle]::new($left, $top, $right - $left, $bottom - $top)
}

function Get-AlphaBounds([System.Drawing.Bitmap]$bitmap, [System.Drawing.Rectangle]$cell) {
    $left = $cell.Right; $top = $cell.Bottom; $right = -1; $bottom = -1
    for ($y = $cell.Top; $y -lt $cell.Bottom; $y++) {
        for ($x = $cell.Left; $x -lt $cell.Right; $x++) {
            if ($bitmap.GetPixel($x, $y).A -le 8) { continue }
            if ($x -lt $left) { $left = $x }; if ($x -gt $right) { $right = $x }
            if ($y -lt $top) { $top = $y }; if ($y -gt $bottom) { $bottom = $y }
        }
    }
    if ($right -lt $left -or $bottom -lt $top) { throw "Empty source cell $cell" }
    return [System.Drawing.Rectangle]::new($left, $top, $right - $left + 1, $bottom - $top + 1)
}

function Get-TargetSize([string]$category, [string[]]$tags) {
    if ($tags -contains 'bow') { return @(29, 28) }
    if ($tags -contains 'quiver') { return @(24, 27) }
    if ($tags -contains 'dagger') { return @(27, 27) }
    if ($tags -contains 'wand' -or $tags -contains 'runeblade') { return @(27, 28) }
    if ($tags -contains 'focus' -or $tags -contains 'summoning_focus' -or $tags -contains 'construct_idol') { return @(25, 26) }
    if ($tags -contains 'unarmed' -or $tags -contains 'wrap') { return @(25, 24) }
    if ($tags -contains 'beast_talisman') { return @(23, 25) }
    switch ($category) {
        'TwoHandWeapon' { @(29, 29) }
        'OneHandWeapon' { @(27, 27) }
        'Shield' { @(26, 28) }
        'BodyArmor' { @(27, 28) }
        'Helmet' { @(25, 25) }
        'Gloves' { @(25, 23) }
        'Boots' { @(25, 24) }
        'Belt' { @(27, 19) }
        'Amulet' { @(23, 24) }
        'Ring' { @(24, 24) }
        'LifeFlask' { @(21, 28) }
        default { @(26, 26) }
    }
}

function Draw-FittedIcon(
    [System.Drawing.Graphics]$graphics,
    [System.Drawing.Bitmap]$source,
    [System.Drawing.Rectangle]$sourceCell,
    [int]$destinationIndex,
    [string]$category,
    [string[]]$tags
) {
    $bounds = Get-AlphaBounds $source $sourceCell
    $target = Get-TargetSize $category $tags
    $scale = [Math]::Min($target[0] / $bounds.Width, $target[1] / $bounds.Height)
    $width = [Math]::Max(1, [int][Math]::Round($bounds.Width * $scale))
    $height = [Math]::Max(1, [int][Math]::Round($bounds.Height * $scale))
    $cellLeft = ($destinationIndex % 13) * 32
    $cellTop = [Math]::Floor($destinationIndex / 13) * 32
    $x = $cellLeft + [Math]::Floor((32 - $width) / 2)
    $bottomAnchored = $category -in @('BodyArmor', 'Helmet', 'Gloves', 'Boots', 'LifeFlask')
    $y = if ($bottomAnchored) { $cellTop + 30 - $height } else { $cellTop + [Math]::Floor((32 - $height) / 2) }
    $destination = [System.Drawing.Rectangle]::new($x, $y, $width, $height)
    $graphics.DrawImage($source, $destination, $bounds, [System.Drawing.GraphicsUnit]::Pixel)
}

function Draw-TierMarks([System.Drawing.Graphics]$graphics, [int]$index, [int]$requiredLevel) {
    $tier = if ($requiredLevel -ge 60) { 2 } elseif ($requiredLevel -ge 30) { 1 } else { 0 }
    if ($tier -eq 0) { return }
    $x = ($index % 13) * 32; $y = [Math]::Floor($index / 13) * 32
    $color = if ($tier -eq 2) { [System.Drawing.Color]::FromArgb(255, 229, 175, 66) } else { [System.Drawing.Color]::FromArgb(255, 174, 106, 49) }
    $brush = [System.Drawing.SolidBrush]::new($color)
    try {
        $graphics.FillRectangle($brush, $x + 2, $y + 2, 4, 2)
        $graphics.FillRectangle($brush, $x + 2, $y + 2, 2, 4)
        if ($tier -eq 2) {
            $graphics.FillRectangle($brush, $x + 26, $y + 2, 4, 2)
            $graphics.FillRectangle($brush, $x + 28, $y + 2, 2, 4)
        }
    } finally { $brush.Dispose() }
}

function Get-CellHash([System.Drawing.Bitmap]$bitmap, [int]$index, [int]$columns) {
    $cell = $bitmap.Clone([System.Drawing.Rectangle]::new(($index % $columns) * 32,
        [Math]::Floor($index / $columns) * 32, 32, 32), $bitmap.PixelFormat)
    $stream = [System.IO.MemoryStream]::new(); $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $cell.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ([BitConverter]::ToString($sha.ComputeHash($stream.ToArray()))).Replace('-', '')
    } finally { $sha.Dispose(); $stream.Dispose(); $cell.Dispose() }
}

function Assert-Atlas([System.Drawing.Bitmap]$bitmap, [int]$count, [int]$columns, [string]$name) {
    $hashes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($index = 0; $index -lt $count; $index++) {
        if (-not $hashes.Add((Get-CellHash $bitmap $index $columns))) { throw "Duplicate $name icon at index $index" }
        $left = ($index % $columns) * 32; $top = [Math]::Floor($index / $columns) * 32; $opaque = 0
        for ($offset = 0; $offset -lt 32; $offset++) {
            if ($bitmap.GetPixel($left + $offset, $top).A -gt 8 -or $bitmap.GetPixel($left + $offset, $top + 31).A -gt 8 -or
                $bitmap.GetPixel($left, $top + $offset).A -gt 8 -or $bitmap.GetPixel($left + 31, $top + $offset).A -gt 8) {
                throw "$name icon touches cell boundary at index $index"
            }
        }
        for ($y = $top; $y -lt $top + 32; $y++) { for ($x = $left; $x -lt $left + 32; $x++) {
            if ($bitmap.GetPixel($x, $y).A -gt 8) { $opaque++ }
        }}
        if ($opaque -lt 45) { throw "$name icon is under-filled at index $index ($opaque pixels)" }
    }
}

$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$p19 = @($catalog.bases)
$weaponFamilies = @{
    'core.base.rusted_greatsword'='sword'; 'core.base.heavy_battleaxe'='axe'; 'core.base.pole_warhammer'='mace';
    'core.base.ash_glaive'='axe'; 'core.base.warden_maul'='mace'; 'core.base.blood_halberd'='axe';
    'core.base.glass_greatblade'='sword'; 'core.base.oathbreaker_axe'='axe';
    'p19.base.headman_s_sword'='sword'; 'p19.base.ezomyte_blade'='sword'; 'p19.base.imperial_maul'='mace';
    'p19.base.void_axe'='axe'; 'core.base.rusted_warhammer'='mace'; 'p19.base.broad_sword'='sword';
    'p19.base.ceremonial_mace'='mace'; 'p19.base.cutlass'='sword'; 'p19.base.flanged_mace'='mace';
    'p19.base.karui_axe'='axe'; 'p19.base.butcher_axe'='axe'; 'p19.base.harpy_rapier'='sword'
}
$weaponColumns = @{ sword=0; axe=1; mace=2 }
$weaponRows = @{ sword=@(0,1,2,3,4,5,7); axe=@(0,1,2,3,4,5,6); mace=@(0,1,2,3,5,6) }
$categorySources = @{
    BodyArmor=@('armor',0,4,9); Helmet=@('armor',1,4,9); Gloves=@('armor',2,4,9); Boots=@('armor',3,4,9);
    Shield=@('accessories',0,5,8); Belt=@('accessories',1,5,8); Amulet=@('accessories',2,5,8);
    Ring=@('accessories',3,5,8); LifeFlask=@('accessories',4,5,8)
}
$categoryRows = @{
    Gloves = @(0, 1, 2, 3, 4, 6)
}
$categoryRanks = @{}
foreach ($category in $categorySources.Keys) {
    $categoryRanks[$category] = @($p19 | Where-Object Category -eq $category | Sort-Object RequiredLevel,StableId)
}
$familyRanks = @{}
foreach ($family in @('sword','axe','mace')) {
    $familyRanks[$family] = @($p19 | Where-Object { $weaponFamilies[$_.StableId] -eq $family } | Sort-Object RequiredLevel,StableId)
    if ($familyRanks[$family].Count -ne $weaponRows[$family].Count) { throw "Unexpected $family base count." }
}

$p24Groups = @(
    [pscustomobject]@{ Key='bow'; Count=6; Master='ranged'; Column=0; Rows=@(0,1,2,3,4,5) },
    [pscustomobject]@{ Key='dagger'; Count=6; Master='ranged'; Column=1; Rows=@(0,1,2,3,4,5) },
    [pscustomobject]@{ Key='wand'; Count=6; Master='ranged'; Column=2; Rows=@(0,1,2,3,4,5) },
    [pscustomobject]@{ Key='quiver'; Count=5; Master='ranged'; Column=3; Rows=@(0,1,2,3,5) },
    [pscustomobject]@{ Key='focus'; Count=5; Master='ranged'; Column=4; Rows=@(0,1,2,3,5) },
    [pscustomobject]@{ Key='summoning_focus'; Count=5; Master='class'; Column=0; Rows=@(0,1,2,3,5) },
    [pscustomobject]@{ Key='unarmed_wrap'; Count=5; Master='class'; Column=1; Rows=@(0,1,2,3,5) },
    [pscustomobject]@{ Key='beast_talisman'; Count=4; Master='class'; Column=2; Rows=@(0,2,4,5) },
    [pscustomobject]@{ Key='runeblade'; Count=4; Master='class'; Column=3; Rows=@(0,2,4,5) },
    [pscustomobject]@{ Key='construct_idol'; Count=4; Master='class'; Column=4; Rows=@(0,2,4,5) }
)
foreach ($group in $p24Groups) {
    if ($group.Rows.Count -ne $group.Count -or $group.Rows[0] -ne 0 -or $group.Rows[-1] -ne 5) {
        throw "P25 special-art progression for $($group.Key) must cover its catalog count and highest source tier."
    }
}
$p24 = foreach ($group in $p24Groups) { for ($variant = 1; $variant -le $group.Count; $variant++) {
    [pscustomobject]@{ StableId="p24.base.$($group.Key).$variant"; DisplayName=$group.Key; Category='P24';
        RequiredLevel=1+($variant-1)*15; Tags=@($group.Key); Group=$group; Variant=$variant }
}}
$all = @($p19 + $p24 | Sort-Object StableId)
if ($all.Count -ne 130 -or @($all.StableId | Select-Object -Unique).Count -ne 130) {
    throw 'The unified equipment atlas requires exactly 130 unique base IDs.'
}

$sources = @{}
foreach ($entry in $sourcePaths.GetEnumerator()) { $sources[$entry.Key] = [System.Drawing.Bitmap]::FromFile($entry.Value) }
$atlas = [System.Drawing.Bitmap]::new(416, 320, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($atlas)
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver

try {
    for ($index = 0; $index -lt $all.Count; $index++) {
        $item = $all[$index]
        if ($item.StableId -like 'p24.base.*') {
            $source = $sources[$item.Group.Master]
            $sourceRow = $item.Group.Rows[$item.Variant - 1]
            $sourceCell = Get-CellRectangle $source $item.Group.Column $sourceRow 5 6
            Draw-FittedIcon $graphics $source $sourceCell $index $item.Category @($item.Tags)
        } elseif ($weaponFamilies.ContainsKey($item.StableId)) {
            $family = $weaponFamilies[$item.StableId]
            $rank = [Array]::IndexOf([object[]]$familyRanks[$family].StableId, $item.StableId)
            $sourceCell = Get-CellRectangle $sources.weapons $weaponColumns[$family] $weaponRows[$family][$rank] 3 8
            Draw-FittedIcon $graphics $sources.weapons $sourceCell $index $item.Category @($family)
        } else {
            $mapping = $categorySources[$item.Category]
            if ($null -eq $mapping) { throw "No P25 art source for $($item.StableId) ($($item.Category))." }
            $rank = [Array]::IndexOf([object[]]$categoryRanks[$item.Category].StableId, $item.StableId)
            $source = $sources[$mapping[0]]
            $sourceRow = if ($categoryRows.ContainsKey($item.Category)) { $categoryRows[$item.Category][$rank] } else { $rank }
            $sourceCell = Get-CellRectangle $source $mapping[1] $sourceRow $mapping[2] $mapping[3]
            Draw-FittedIcon $graphics $source $sourceCell $index $item.Category @($item.Tags)
        }
        Draw-TierMarks $graphics $index ([int]$item.RequiredLevel)
    }

    Assert-Atlas $atlas 130 13 'equipment base'
    [System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    $atlas.Save($equipmentPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $legendaryBases = @(
        'core.base.heavy_battleaxe','core.base.march_boots','core.base.raven_mask','core.base.ember_ring',
        'core.base.focus_ring','core.base.chain_belt','core.base.bastion_plate','core.base.glass_greatblade',
        'core.base.oracle_crown','core.base.gloom_raiment','core.base.starweave_robe','core.base.ember_amulet',
        'core.base.rusted_greatsword','core.base.ash_iron_shield','core.base.ritual_gloves','core.base.shadow_treads',
        'core.base.ration_belt','core.base.spirit_amulet','core.base.rusted_warhammer','core.base.hunter_hood',
        'core.base.ash_circlet','core.base.crude_chainmail','core.base.iron_ring','core.base.warlord_helm',
        'core.base.triune_carapace'
    )
    $legendary = [System.Drawing.Bitmap]::new(160, 160, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $legendaryGraphics = [System.Drawing.Graphics]::FromImage($legendary)
    $legendaryGraphics.Clear([System.Drawing.Color]::Transparent)
    $legendaryGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    try {
        for ($index = 0; $index -lt $legendaryBases.Count; $index++) {
            $baseIndex = [Array]::IndexOf([object[]]$all.StableId, $legendaryBases[$index])
            if ($baseIndex -lt 0) { throw "Legendary base missing: $($legendaryBases[$index])" }
            $sourceCell = [System.Drawing.Rectangle]::new(($baseIndex % 13) * 32, [Math]::Floor($baseIndex / 13) * 32, 32, 32)
            $destination = [System.Drawing.Rectangle]::new(($index % 5) * 32, [Math]::Floor($index / 5) * 32, 32, 32)
            $legendaryGraphics.DrawImage($atlas, $destination, $sourceCell, [System.Drawing.GraphicsUnit]::Pixel)
            $gold = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 236, 179, 55))
            $accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,
                110 + (($index * 37) % 130), 45 + (($index * 53) % 120), 75 + (($index * 71) % 150)))
            try {
                $legendaryGraphics.FillRectangle($gold, $destination.X + 2, $destination.Y + 2, 4, 2)
                $legendaryGraphics.FillRectangle($gold, $destination.X + 2, $destination.Y + 2, 2, 4)
                $legendaryGraphics.FillRectangle($accent, $destination.X + 27, $destination.Y + 27, 2, 2)
            } finally { $accent.Dispose(); $gold.Dispose() }
        }
        Assert-Atlas $legendary 25 5 'legendary'
        $legendary.Save($legendaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $legendaryGraphics.Dispose(); $legendary.Dispose() }
} finally {
    $graphics.Dispose(); $atlas.Dispose()
    foreach ($source in $sources.Values) { $source.Dispose() }
}

Write-Host '[p25-equipment-art] PASS: 130 category-correct bases and 25 legendary icons rebuilt from unified sources.'
