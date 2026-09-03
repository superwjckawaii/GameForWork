[CmdletBinding()]
param([string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Security

$catalogPath = Join-Path $RepositoryRoot 'src\Game.Core\Equipment\Data\equipment_catalog.json'
$p25SourceRoot = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\p25\equipment'
$p32SourceRoot = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\p32\equipment'
$outputRoot = Join-Path $RepositoryRoot 'src\Game.Godot\assets\p25\ui'
$equipmentPath = Join-Path $outputRoot 'p25-equipment-atlas.png'
$legendaryPath = Join-Path $outputRoot 'p25-legendary-atlas.png'

$sourcePaths = @{
    legacyWeapons = Join-Path $p25SourceRoot 'master-weapons.png'
    legacyArmor = Join-Path $p25SourceRoot 'master-armor.png'
    legacyAccessories = Join-Path $p25SourceRoot 'master-accessories.png'
    legacyRanged = Join-Path $p25SourceRoot 'master-special-ranged.png'
    legacyClass = Join-Path $p25SourceRoot 'master-special-class.png'
    p32Warfront = Join-Path $p32SourceRoot 'master-warfront-and-core.png'
    p32Spirit = Join-Path $p32SourceRoot 'master-spirit-barrier.png'
    p32Weapons = Join-Path $p32SourceRoot 'master-weapons.png'
    p32SpecialWeapons = Join-Path $p32SourceRoot 'master-special-weapons.png'
    p32SpiritShields = Join-Path $p32SourceRoot 'master-spirit-shields.png'
    p32Legendary = Join-Path $p32SourceRoot 'master-legendary.png'
}
foreach ($path in $sourcePaths.Values) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing equipment art source: $path" }
}

function Open-ArtBitmap([string]$path, [bool]$cleanNeutralBackground = $false) {
    $input = [System.Drawing.Bitmap]::FromFile($path)
    try {
        $bitmap = [System.Drawing.Bitmap]::new($input.Width, $input.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.DrawImageUnscaled($input, 0, 0) } finally { $graphics.Dispose() }
    } finally { $input.Dispose() }
    if (-not $cleanNeutralBackground) { return $bitmap }

    $bounds = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
    $data = $bitmap.LockBits($bounds, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $length = [Math]::Abs($data.Stride) * $data.Height
        $bytes = [byte[]]::new($length)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $length)
        for ($offset = 0; $offset -lt $length; $offset += 4) {
            if ($bytes[$offset + 3] -le 8) { continue }
            $blue = $bytes[$offset]; $green = $bytes[$offset + 1]; $red = $bytes[$offset + 2]
            $minimum = [Math]::Min($red, [Math]::Min($green, $blue)); $maximum = [Math]::Max($red, [Math]::Max($green, $blue))
            if ($minimum -ge 225 -and ($maximum - $minimum) -le 16) { $bytes[$offset + 3] = 0 }
        }
        [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $length)
    } finally { $bitmap.UnlockBits($data) }
    return $bitmap
}

function Get-CellRectangle([System.Drawing.Bitmap]$bitmap, [int]$column, [int]$row, [int]$columns, [int]$rows) {
    $left = [int][Math]::Floor($column * $bitmap.Width / $columns); $right = [int][Math]::Floor(($column + 1) * $bitmap.Width / $columns)
    $top = [int][Math]::Floor($row * $bitmap.Height / $rows); $bottom = [int][Math]::Floor(($row + 1) * $bitmap.Height / $rows)
    return [System.Drawing.Rectangle]::new($left, $top, $right - $left, $bottom - $top)
}

function Get-AlphaBounds([System.Drawing.Bitmap]$bitmap, [System.Drawing.Rectangle]$cell) {
    $left = $cell.Right; $top = $cell.Bottom; $right = -1; $bottom = -1
    for ($y = $cell.Top; $y -lt $cell.Bottom; $y++) { for ($x = $cell.Left; $x -lt $cell.Right; $x++) {
        if ($bitmap.GetPixel($x, $y).A -le 8) { continue }
        if ($x -lt $left) { $left = $x }; if ($x -gt $right) { $right = $x }
        if ($y -lt $top) { $top = $y }; if ($y -gt $bottom) { $bottom = $y }
    }}
    if ($right -lt $left -or $bottom -lt $top) { throw "Empty source cell $cell" }
    return [System.Drawing.Rectangle]::new($left, $top, $right - $left + 1, $bottom - $top + 1)
}

function Get-TargetSize([string]$category, [string[]]$tags) {
    if ($tags -contains 'bow') { return @(29, 28) }; if ($tags -contains 'quiver') { return @(24, 27) }
    if ($tags -contains 'dagger') { return @(27, 27) }; if ($tags -contains 'wand' -or $tags -contains 'runeblade') { return @(27, 28) }
    if ($tags -contains 'focus' -or $tags -contains 'summoning_focus' -or $tags -contains 'construct_idol') { return @(25, 26) }
    if ($tags -contains 'unarmed' -or $tags -contains 'wrap') { return @(25, 24) }; if ($tags -contains 'beast_talisman') { return @(23, 25) }
    switch ($category) {
        'TwoHandWeapon' { @(29, 29) } 'OneHandWeapon' { @(27, 27) } 'Shield' { @(26, 28) } 'BodyArmor' { @(27, 28) }
        'Helmet' { @(25, 25) } 'Gloves' { @(25, 23) } 'Boots' { @(25, 24) } 'Belt' { @(27, 19) }
        'Amulet' { @(23, 24) } 'Ring' { @(24, 24) } 'LifeFlask' { @(21, 28) } default { @(26, 26) }
    }
}

function Draw-FittedIcon([System.Drawing.Graphics]$graphics, [System.Drawing.Bitmap]$source,
    [System.Drawing.Rectangle]$sourceCell, [int]$destinationIndex, [string]$category, [string[]]$tags, [int]$columns = 13) {
    $bounds = Get-AlphaBounds $source $sourceCell; $target = Get-TargetSize $category $tags
    $scale = [Math]::Min($target[0] / $bounds.Width, $target[1] / $bounds.Height)
    $width = [Math]::Max(1, [int][Math]::Round($bounds.Width * $scale)); $height = [Math]::Max(1, [int][Math]::Round($bounds.Height * $scale))
    $cellLeft = ($destinationIndex % $columns) * 32; $cellTop = [Math]::Floor($destinationIndex / $columns) * 32
    $x = $cellLeft + [Math]::Floor((32 - $width) / 2)
    $bottomAnchored = $category -in @('BodyArmor', 'Helmet', 'Gloves', 'Boots', 'LifeFlask')
    $y = if ($bottomAnchored) { $cellTop + 30 - $height } else { $cellTop + [Math]::Floor((32 - $height) / 2) }
    $graphics.DrawImage($source, [System.Drawing.Rectangle]::new($x, $y, $width, $height), $bounds, [System.Drawing.GraphicsUnit]::Pixel)
}

function Draw-TierMarks([System.Drawing.Graphics]$graphics, [int]$index, [int]$requiredLevel, [int]$columns = 13) {
    $tier = if ($requiredLevel -ge 60) { 2 } elseif ($requiredLevel -ge 30) { 1 } else { 0 }; if ($tier -eq 0) { return }
    $x = ($index % $columns) * 32; $y = [Math]::Floor($index / $columns) * 32
    $color = if ($tier -eq 2) { [System.Drawing.Color]::FromArgb(255, 229, 175, 66) } else { [System.Drawing.Color]::FromArgb(255, 174, 106, 49) }
    $brush = [System.Drawing.SolidBrush]::new($color)
    try {
        $graphics.FillRectangle($brush, $x + 2, $y + 2, 4, 2); $graphics.FillRectangle($brush, $x + 2, $y + 2, 2, 4)
        if ($tier -eq 2) { $graphics.FillRectangle($brush, $x + 26, $y + 2, 4, 2); $graphics.FillRectangle($brush, $x + 28, $y + 2, 2, 4) }
    } finally { $brush.Dispose() }
}

function Get-CellHash([System.Drawing.Bitmap]$bitmap, [int]$index, [int]$columns) {
    $cell = $bitmap.Clone([System.Drawing.Rectangle]::new(($index % $columns) * 32, [Math]::Floor($index / $columns) * 32, 32, 32), $bitmap.PixelFormat)
    $stream = [System.IO.MemoryStream]::new(); $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $cell.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png); return ([BitConverter]::ToString($sha.ComputeHash($stream.ToArray()))).Replace('-', '') }
    finally { $sha.Dispose(); $stream.Dispose(); $cell.Dispose() }
}

function Assert-Atlas([System.Drawing.Bitmap]$bitmap, [int]$count, [int]$columns, [string]$name) {
    $hashes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($index = 0; $index -lt $count; $index++) {
        if (-not $hashes.Add((Get-CellHash $bitmap $index $columns))) { throw "Duplicate $name icon at index $index" }
        $left = ($index % $columns) * 32; $top = [Math]::Floor($index / $columns) * 32; $opaque = 0
        for ($offset = 0; $offset -lt 32; $offset++) {
            if ($bitmap.GetPixel($left + $offset, $top).A -gt 8 -or $bitmap.GetPixel($left + $offset, $top + 31).A -gt 8 -or
                $bitmap.GetPixel($left, $top + $offset).A -gt 8 -or $bitmap.GetPixel($left + 31, $top + $offset).A -gt 8) { throw "$name icon touches cell boundary at index $index" }
        }
        for ($y = $top; $y -lt $top + 32; $y++) { for ($x = $left; $x -lt $left + 32; $x++) { if ($bitmap.GetPixel($x, $y).A -gt 8) { $opaque++ } } }
        if ($opaque -lt 45) { throw "$name icon is under-filled at index $index ($opaque pixels)" }
    }
}

$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$bases = @($catalog.bases); $legendaries = @($catalog.legendaryItems)
if ($bases.Count -ne 244 -or $legendaries.Count -ne 55) { throw 'Equipment art requires the sealed 244-base/55-legendary catalog.' }
$sources = @{}
foreach ($entry in $sourcePaths.GetEnumerator()) { $sources[$entry.Key] = Open-ArtBitmap $entry.Value ($entry.Key -in @('p32Warfront', 'p32Spirit', 'p32Legendary')) }

$legacyCategorySources = @{
    BodyArmor=@('legacyArmor',0,4,9); Helmet=@('legacyArmor',1,4,9); Gloves=@('legacyArmor',2,4,9); Boots=@('legacyArmor',3,4,9)
    Shield=@('legacyAccessories',0,5,8); Belt=@('legacyAccessories',1,5,8); Amulet=@('legacyAccessories',2,5,8)
    Ring=@('legacyAccessories',3,5,8); LifeFlask=@('legacyAccessories',4,5,8)
}
$legacyCategoryRows = @{ Gloves = @(0, 1, 2, 3, 4, 6) }; $legacy = @($bases | Select-Object -First 80); $legacyCategoryRanks = @{}
foreach ($category in $legacyCategorySources.Keys) { $legacyCategoryRanks[$category] = @($legacy | Where-Object category -eq $category) }
$weaponRows = @{ sword=@(0,1,2,3,4,5,7); axe=@(0,1,2,3,4,5,6); mace=@(0,1,2,3,5,6) }; $weaponColumns = @{ sword=0; axe=1; mace=2 }; $weaponRanks = @{}
foreach ($family in @('sword','axe','mace')) { $weaponRanks[$family] = @($legacy | Where-Object { $_.tags -contains $family }) }
$p24Groups = @{
    bow=@('legacyRanged',0,@(0,1,2,3,4,5)); dagger=@('legacyRanged',1,@(0,1,2,3,4,5)); wand=@('legacyRanged',2,@(0,1,2,3,4,5))
    quiver=@('legacyRanged',3,@(0,1,2,3,5)); focus=@('legacyRanged',4,@(0,1,2,3,5)); summoning_focus=@('legacyClass',0,@(0,1,2,3,5))
    unarmed_wrap=@('legacyClass',1,@(0,1,2,3,5)); beast_talisman=@('legacyClass',2,@(0,2,4,5)); runeblade=@('legacyClass',3,@(0,2,4,5)); construct_idol=@('legacyClass',4,@(0,2,4,5))
}

$atlasRows = [int][Math]::Ceiling($bases.Count / 13.0); $atlas = [System.Drawing.Bitmap]::new(416, $atlasRows * 32, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($atlas); $graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor; $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
try {
    for ($index = 0; $index -lt $bases.Count; $index++) {
        $item = $bases[$index]; $source = $null; $cell = $null
        if ($index -lt 20) {
            $family = @('sword','axe','mace') | Where-Object { $item.tags -contains $_ } | Select-Object -First 1
            $rank = [Array]::IndexOf([object[]]$weaponRanks[$family].id, $item.id); $source = $sources.legacyWeapons
            $cell = Get-CellRectangle $source $weaponColumns[$family] $weaponRows[$family][$rank] 3 8
        } elseif ($index -lt 80) {
            $mapping = $legacyCategorySources[$item.category]; $rank = [Array]::IndexOf([object[]]$legacyCategoryRanks[$item.category].id, $item.id); $source = $sources[$mapping[0]]
            $row = if ($legacyCategoryRows.ContainsKey($item.category)) { $legacyCategoryRows[$item.category][$rank] } else { $rank }
            $cell = Get-CellRectangle $source $mapping[1] $row $mapping[2] $mapping[3]
        } elseif ($index -lt 100) {
            $source = $sources.p32Warfront
            if ($index -eq 98) { $column=3; $row=0 } elseif ($index -eq 99) { $column=3; $row=1 }
            else { $column = switch ($item.category) { 'Ring' {0} 'Amulet' {1} 'Belt' {2} default { throw "Unexpected warfront category $($item.category)" } }; $row = [Array]::IndexOf([object[]]@($bases | Select-Object -Skip 80 -First 18 | Where-Object category -eq $item.category).id, $item.id) }
            $cell = Get-CellRectangle $source $column $row 4 6
        } elseif ($index -lt 150) {
            if ($item.id -notmatch '^equipment\.base\.(.+)\.(\d+)$') { throw "Unexpected P24 art ID $($item.id)" }
            $key = $Matches[1]; $variant = [int]$Matches[2]; $mapping = $p24Groups[$key]; if ($null -eq $mapping) { throw "Missing P24 art group $key" }
            $source = $sources[$mapping[0]]; $cell = Get-CellRectangle $source $mapping[1] $mapping[2][$variant - 1] 5 6
        } elseif ($index -lt 200) {
            if ($index -in @(191,195,199)) { $source=$sources.p32SpiritShields; $column=[Array]::IndexOf([object[]]@(191,195,199),$index); $cell=Get-CellRectangle $source $column 0 3 1 }
            else {
                $source=$sources.p32Spirit
                if ($index -lt 170) { $column=[Math]::Floor(($index-150)/4); $row=($index-150)%4 }
                elseif ($index -lt 173) { $column=5; $row=$index-170 }
                elseif ($index -lt 185) { $column=6+[Math]::Floor(($index-173)/4); $row=($index-173)%4 }
                elseif ($index -lt 188) { $column=9; $row=$index-185 }
                else { $column=10+[Math]::Floor(($index-188)/4); $row=($index-188)%4 }
                $cell=Get-CellRectangle $source $column $row 13 4
            }
        } else {
            if ($index -lt 228) { $source=$sources.p32Weapons; $column=[Math]::Floor(($index-200)/4); $row=($index-200)%4; $columns=7 }
            else { $source=$sources.p32SpecialWeapons; $column=[Math]::Floor(($index-228)/4); $row=($index-228)%4; $columns=4 }
            $cell=Get-CellRectangle $source $column $row $columns 4
        }
        Draw-FittedIcon $graphics $source $cell $index $item.category @($item.tags); Draw-TierMarks $graphics $index ([int]$item.requiredLevel)
    }
    Assert-Atlas $atlas $bases.Count 13 'equipment base'; [System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    $atlas.Save($equipmentPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $legendary = [System.Drawing.Bitmap]::new(160, [int][Math]::Ceiling($legendaries.Count / 5.0) * 32, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $legendaryGraphics = [System.Drawing.Graphics]::FromImage($legendary); $legendaryGraphics.Clear([System.Drawing.Color]::Transparent); $legendaryGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    try {
        for ($index = 0; $index -lt $legendaries.Count; $index++) {
            $baseName=$legendaries[$index].baseAndSource.Split('；')[0].Replace('（新增）','').Trim(); $base=$bases|Where-Object displayName -eq $baseName|Select-Object -First 1
            if ($null -eq $base) { throw "Legendary base missing for $($legendaries[$index].displayName): $baseName" }
            if ($index -lt 25) {
                $baseIndex=[Array]::IndexOf([object[]]$bases.id,$base.id)
                $sourceCell=[System.Drawing.Rectangle]::new(($baseIndex%13)*32,[Math]::Floor($baseIndex/13)*32,32,32)
                $destination=[System.Drawing.Rectangle]::new(($index%5)*32,[Math]::Floor($index/5)*32,32,32)
                $legendaryGraphics.DrawImage($atlas,$destination,$sourceCell,[System.Drawing.GraphicsUnit]::Pixel)
            } else {
                $source=$sources.p32Legendary; $generated=$index-25; $cell=Get-CellRectangle $source ($generated%6) ([Math]::Floor($generated/6)) 6 5
                Draw-FittedIcon $legendaryGraphics $source $cell $index $base.category @($base.tags) 5
            }
            $gold=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,236,179,55)); try { $x=($index%5)*32;$y=[Math]::Floor($index/5)*32;$legendaryGraphics.FillRectangle($gold,$x+2,$y+2,4,2);$legendaryGraphics.FillRectangle($gold,$x+2,$y+2,2,4) } finally {$gold.Dispose()}
        }
        Assert-Atlas $legendary $legendaries.Count 5 'legendary'; $legendary.Save($legendaryPath,[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $legendaryGraphics.Dispose(); $legendary.Dispose() }
} finally { $graphics.Dispose(); $atlas.Dispose(); foreach($source in $sources.Values){$source.Dispose()} }

Write-Host '[equipment-art] PASS: 244 base and 55 legendary/mythic icons rebuilt in stable catalog order.'
