param([string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$output = Join-Path $RepositoryRoot 'artifacts/art-audit'
New-Item -ItemType Directory -Force $output | Out-Null
$catalog = Get-Content (Join-Path $RepositoryRoot 'src/Game.Core/Equipment/Data/equipment_catalog.json') -Raw | ConvertFrom-Json
function Contact([string]$path, [int]$columns, [int]$rows, [int]$cw, [int]$ch, [string[]]$labels, [string]$name, [int]$stride=1) {
    $source = [System.Drawing.Bitmap]::FromFile((Join-Path $RepositoryRoot $path))
    $count = $labels.Count
    $pageSize=80
    try { for($start=0; $start -lt $count; $start+=$pageSize) {
        $length=[Math]::Min($pageSize,$count-$start)
        $sheet=[System.Drawing.Bitmap]::new(1000,[int][Math]::Ceiling($length/8.0)*112)
        $g=[System.Drawing.Graphics]::FromImage($sheet); $g.Clear([System.Drawing.Color]::FromArgb(35,39,46))
        $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $font=[System.Drawing.Font]::new('Microsoft YaHei',8)
        try { for($j=0; $j -lt $length; $j++) {
            $index=$start+$j; $cell=$index*$stride
            $x=($j%8)*125; $y=[Math]::Floor($j/8)*112
            $src=[System.Drawing.Rectangle]::new(($cell%$columns)*$cw,[Math]::Floor($cell/$columns)*$ch,$cw,$ch)
            $g.DrawImage($source,[System.Drawing.Rectangle]::new($x+25,$y,72,80),$src,[System.Drawing.GraphicsUnit]::Pixel)
            $g.DrawString("$index $($labels[$index])",$font,[System.Drawing.Brushes]::White,[System.Drawing.RectangleF]::new($x+2,$y+81,121,30))
        }
        $sheet.Save((Join-Path $output "$name-$start.png"),[System.Drawing.Imaging.ImageFormat]::Png)
        } finally {$font.Dispose();$g.Dispose();$sheet.Dispose()}
    }} finally {$source.Dispose()}
}
Contact 'src/Game.Godot/assets/equipmentArt/ui/equipmentArt-equipment-atlas.png' 13 19 32 32 @($catalog.bases.displayName) 'equipment'
Contact 'src/Game.Godot/assets/equipmentArt/ui/equipmentArt-legendary-atlas.png' 5 11 32 32 @($catalog.legendaryItems.displayName) 'legendary'
Contact 'src/Game.Godot/assets/art/characters/art-actor-animation.png' 31 20 48 64 @(0..19|ForEach-Object {"actor row $_"}) 'actors' 31
Contact 'src/Game.Godot/assets/art/enemies/art-enemy-animation.png' 31 104 48 64 @(0..103|ForEach-Object {"enemy row $_"}) 'enemies' 31
Contact 'src/Game.Godot/assets/art/enemies/art-boss-animation.png' 31 48 72 80 @(0..47|ForEach-Object {"boss row $_"}) 'bosses' 31
Contact 'src/Game.Godot/assets/art/town/art-building-atlas.png' 4 2 160 120 @(0..6|ForEach-Object {"building $_"}) 'buildings'
Contact 'src/Game.Godot/assets/presentation/vfx/presentation-combat-vfx.png' 4 4 64 64 @(0..15|ForEach-Object {"effect $_"}) 'vfx'
Write-Host "Contacts: $output"
