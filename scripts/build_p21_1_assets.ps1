[CmdletBinding()]
param([string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourceRoot = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\p21\imagegen'
$assetRoot = Join-Path $RepositoryRoot 'src\Game.Godot\assets\p21'
$uiRoot = Join-Path $assetRoot 'ui'
$treeRoot = Join-Path $assetRoot 'trees'
$brandRoot = Join-Path $assetRoot 'brand'
foreach ($directory in @($uiRoot, $treeRoot, $brandRoot)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

function New-Bitmap([int]$width, [int]$height) {
    $bitmap = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    return $bitmap
}

function New-Graphics([System.Drawing.Image]$image) {
    $graphics = [System.Drawing.Graphics]::FromImage($image)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    return $graphics
}

function Get-Hash([string]$value) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
    return [BitConverter]::ToUInt32([System.Security.Cryptography.SHA256]::HashData($bytes), 0)
}

function Get-Color([string]$hex) { return [System.Drawing.ColorTranslator]::FromHtml($hex) }

function New-Pen([System.Drawing.Color]$color, [int]$width) {
    $pen = [System.Drawing.Pen]::new($color, $width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Square
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Square
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    return $pen
}

function Draw-ItemGlyph {
    param([System.Drawing.Graphics]$Graphics, [int]$X, [int]$Y, [string]$Category, [uint32]$Hash, [int]$Index, [switch]$Unique)
    $palette = @('#aeb7c2', '#b88a54', '#6ca8bd', '#c65d42', '#8d6ac0', '#7fb076', '#d1aa43')
    $main = Get-Color $palette[$Hash % $palette.Count]
    if ($Unique) { $main = Get-Color '#d4a642' }
    $dark = Get-Color '#171b24'
    $light = [System.Drawing.Color]::FromArgb(255, [math]::Min(255, $main.R + 55), [math]::Min(255, $main.G + 55), [math]::Min(255, $main.B + 55))
    $outline = New-Pen $dark 5
    $fill = New-Pen $main 3
    $shine = New-Pen $light 1
    $brush = [System.Drawing.SolidBrush]::new($main)
    $darkBrush = [System.Drawing.SolidBrush]::new($dark)
    $lightBrush = [System.Drawing.SolidBrush]::new($light)
    switch ($Category) {
        { $_ -in 'TwoHandWeapon', 'OneHandWeapon' } {
            $Graphics.DrawLine($outline, $X + 8, $Y + 25, $X + 24, $Y + 7)
            $Graphics.DrawLine($fill, $X + 8, $Y + 25, $X + 24, $Y + 7)
            $Graphics.DrawLine($shine, $X + 11, $Y + 22, $X + 23, $Y + 9)
            $Graphics.DrawLine($outline, $X + 6, $Y + 20, $X + 13, $Y + 27)
            $Graphics.DrawLine($fill, $X + 7, $Y + 20, $X + 13, $Y + 26)
        }
        'Shield' {
            $points = [System.Drawing.Point[]]@([System.Drawing.Point]::new($X+7,$Y+7),[System.Drawing.Point]::new($X+25,$Y+7),[System.Drawing.Point]::new($X+23,$Y+21),[System.Drawing.Point]::new($X+16,$Y+27),[System.Drawing.Point]::new($X+9,$Y+21))
            $Graphics.FillPolygon($darkBrush, $points); $Graphics.DrawPolygon($fill, $points)
            $Graphics.FillRectangle($lightBrush, $X+14, $Y+10, 3, 12)
        }
        'Helmet' {
            $Graphics.FillRectangle($darkBrush, $X+7, $Y+13, 18, 12); $Graphics.FillRectangle($brush, $X+9, $Y+14, 14, 9)
            $Graphics.FillRectangle($darkBrush, $X+10, $Y+8, 12, 7); $Graphics.FillRectangle($lightBrush, $X+12, $Y+9, 8, 4)
            $Graphics.FillRectangle($darkBrush, $X+15, $Y+15, 3, 10)
        }
        'BodyArmor' {
            $points = [System.Drawing.Point[]]@([System.Drawing.Point]::new($X+6,$Y+10),[System.Drawing.Point]::new($X+12,$Y+7),[System.Drawing.Point]::new($X+20,$Y+7),[System.Drawing.Point]::new($X+26,$Y+10),[System.Drawing.Point]::new($X+22,$Y+26),[System.Drawing.Point]::new($X+10,$Y+26))
            $Graphics.FillPolygon($darkBrush,$points); $Graphics.DrawPolygon($fill,$points); $Graphics.DrawLine($shine,$X+12,$Y+10,$X+12,$Y+22)
        }
        'Gloves' {
            $Graphics.FillRectangle($darkBrush,$X+8,$Y+11,16,15); $Graphics.FillRectangle($brush,$X+10,$Y+14,12,10)
            for($finger=0;$finger -lt 4;$finger++){ $Graphics.FillRectangle($lightBrush,$X+10+$finger*3,$Y+8+($finger%2),2,8) }
        }
        'Boots' {
            $Graphics.FillRectangle($darkBrush,$X+10,$Y+7,10,15); $Graphics.FillRectangle($brush,$X+12,$Y+9,6,12)
            $Graphics.FillRectangle($darkBrush,$X+9,$Y+20,16,7); $Graphics.FillRectangle($lightBrush,$X+12,$Y+21,10,3)
        }
        'Belt' {
            $Graphics.FillRectangle($darkBrush,$X+4,$Y+12,24,9); $Graphics.FillRectangle($brush,$X+6,$Y+14,20,5)
            $Graphics.FillRectangle($darkBrush,$X+13,$Y+11,7,11); $Graphics.FillRectangle($lightBrush,$X+15,$Y+13,3,7)
        }
        'Amulet' {
            $Graphics.DrawEllipse($outline,$X+7,$Y+5,18,18); $Graphics.DrawEllipse($fill,$X+8,$Y+6,16,16)
            $Graphics.FillRectangle($darkBrush,$X+13,$Y+20,7,7); $Graphics.FillRectangle($lightBrush,$X+15,$Y+21,3,4)
        }
        'Ring' {
            $Graphics.DrawEllipse($outline,$X+7,$Y+9,18,16); $Graphics.DrawEllipse($fill,$X+8,$Y+10,16,14)
            $Graphics.FillPolygon($lightBrush,[System.Drawing.Point[]]@([System.Drawing.Point]::new($X+16,$Y+4),[System.Drawing.Point]::new($X+21,$Y+9),[System.Drawing.Point]::new($X+16,$Y+13),[System.Drawing.Point]::new($X+11,$Y+9)))
        }
        default {
            $Graphics.FillRectangle($darkBrush,$X+10,$Y+5,12,6); $Graphics.FillRectangle($lightBrush,$X+13,$Y+6,6,4)
            $Graphics.FillPolygon($darkBrush,[System.Drawing.Point[]]@([System.Drawing.Point]::new($X+9,$Y+10),[System.Drawing.Point]::new($X+23,$Y+10),[System.Drawing.Point]::new($X+26,$Y+19),[System.Drawing.Point]::new($X+21,$Y+27),[System.Drawing.Point]::new($X+11,$Y+27),[System.Drawing.Point]::new($X+6,$Y+19)))
            $Graphics.FillRectangle($brush,$X+10,$Y+16,12,8); $Graphics.FillRectangle($lightBrush,$X+11,$Y+14,3,9)
        }
    }
    $markerX = $X + 3 + [int]($Hash % 5); $markerY = $Y + 26 - [int](($Hash / 7) % 5)
    $Graphics.FillRectangle($lightBrush, $markerX, $markerY, 2, 2)
    for ($bit = 0; $bit -lt 7; $bit++) {
        $markerBrush = if ((($Index + 1) -band (1 -shl $bit)) -ne 0) { $lightBrush } else { $brush }
        $Graphics.FillRectangle($markerBrush, $X + 2 + $bit * 4, $Y + 28, 2, 2)
    }
    $outline.Dispose(); $fill.Dispose(); $shine.Dispose(); $brush.Dispose(); $darkBrush.Dispose(); $lightBrush.Dispose()
}

function Build-ItemAtlases {
    $catalog = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'src\Game.Core\P19\Data\p19_catalog.json') -Raw | ConvertFrom-Json
    $bases = @($catalog.bases | Sort-Object StableId)
    $atlas = New-Bitmap 320 256; $graphics = New-Graphics $atlas
    for($index=0;$index -lt $bases.Count;$index++){
        Draw-ItemGlyph $graphics (($index%10)*32) ([math]::Floor($index/10)*32) ([string]$bases[$index].Category) (Get-Hash ([string]$bases[$index].StableId)) $index
    }
    $graphics.Dispose(); $atlas.Save((Join-Path $uiRoot 'p21-item-bases.png'),[System.Drawing.Imaging.ImageFormat]::Png); $atlas.Dispose()

    $unique = New-Bitmap 160 160; $graphics = New-Graphics $unique
    $categories = @('TwoHandWeapon','Shield','Helmet','BodyArmor','Gloves','Boots','Belt','Amulet','Ring','LifeFlask')
    for($index=0;$index -lt 25;$index++){
        Draw-ItemGlyph $graphics (($index%5)*32) ([math]::Floor($index/5)*32) $categories[$index%$categories.Count] (Get-Hash "unique-$index") $index -Unique
    }
    $graphics.Dispose(); $unique.Save((Join-Path $uiRoot 'p21-unique-items.png'),[System.Drawing.Imaging.ImageFormat]::Png); $unique.Dispose()
}

function Build-SkillAtlas {
    $atlas = New-Bitmap 320 256; $graphics = New-Graphics $atlas
    $colors = @('#d9573f','#e77b32','#d5ad36','#58a8cc','#557bd1','#8d5bd0','#52aa73','#b24b68')
    $dark = Get-Color '#151923'
    for($index=0;$index -lt 78;$index++){
        $x=($index%10)*32; $y=[math]::Floor($index/10)*32; $hash=Get-Hash "skill-$index"
        $color=Get-Color $colors[$hash%$colors.Count]; $outline=New-Pen $dark 5; $fill=New-Pen $color 3
        if($index -lt 30){
            $points=[System.Drawing.Point[]]@([System.Drawing.Point]::new($x+16,$y+6),[System.Drawing.Point]::new($x+26,$y+16),[System.Drawing.Point]::new($x+16,$y+26),[System.Drawing.Point]::new($x+6,$y+16))
            $graphics.DrawPolygon($outline,$points); $graphics.DrawPolygon($fill,$points)
        } else { $graphics.DrawEllipse($outline,$x+4,$y+4,24,24); $graphics.DrawEllipse($fill,$x+5,$y+5,22,22) }
        $rune=New-Pen ([System.Drawing.Color]::FromArgb(255,[math]::Min(255,$color.R+70),[math]::Min(255,$color.G+70),[math]::Min(255,$color.B+70))) 2
        $mode=$hash%6
        if($mode -in 0,3){$graphics.DrawLine($rune,$x+10,$y+22,$x+22,$y+10)}
        if($mode -in 1,3,5){$graphics.DrawLine($rune,$x+10,$y+11,$x+22,$y+21)}
        if($mode -in 2,4,5){$graphics.DrawLine($rune,$x+16,$y+9,$x+16,$y+23)}
        $colorBrush=[System.Drawing.SolidBrush]::new($color);$darkBrush=[System.Drawing.SolidBrush]::new($dark)
        $graphics.FillRectangle($colorBrush,$x+14+($index%3)-1,$y+14,3,3)
        for($bit=0;$bit -lt 7;$bit++){$markerBrush=if((($index+1)-band(1-shl$bit))-ne0){$colorBrush}else{$darkBrush};$graphics.FillRectangle($markerBrush,$x+5+$bit*3,$y+24,2,2)}
        $outline.Dispose();$fill.Dispose();$rune.Dispose();$colorBrush.Dispose();$darkBrush.Dispose()
    }
    $graphics.Dispose();$atlas.Save((Join-Path $uiRoot 'p21-skill-gems.png'),[System.Drawing.Imaging.ImageFormat]::Png);$atlas.Dispose()
}

function Build-UiSkin {
    $atlas=New-Bitmap 256 64;$graphics=New-Graphics $atlas
    $styles=@(
        @('#151a22','#485463','#2b3340'),@('#202630','#596675','#303946'),@('#272e38','#c09a55','#3b4653'),@('#171c24','#e0bd72','#252d38'),
        @('#13171d','#343a43','#1c222b'),@('#10151c','#697481','#1b222c'),@('#151922','#b88b45','#252c36'),@('#0d1117','#d0aa61','#191f28'))
    for($index=0;$index -lt 8;$index++){
        $x=($index%4)*64;$y=[math]::Floor($index/4)*32
        $background=Get-Color $styles[$index][0];$border=Get-Color $styles[$index][1];$inner=Get-Color $styles[$index][2]
        $graphics.FillRectangle([System.Drawing.SolidBrush]::new($background),$x+1,$y+1,62,30)
        $graphics.DrawRectangle((New-Pen $border 2),$x+2,$y+2,59,27)
        $graphics.DrawRectangle((New-Pen $inner 1),$x+5,$y+5,53,21)
        $graphics.FillRectangle([System.Drawing.SolidBrush]::new($border),$x+3,$y+3,3,3)
        $graphics.FillRectangle([System.Drawing.SolidBrush]::new($border),$x+58,$y+26,3,3)
    }
    $graphics.Dispose();$atlas.Save((Join-Path $uiRoot 'p21-ui-skin.png'),[System.Drawing.Imaging.ImageFormat]::Png);$atlas.Dispose()
}

function Build-Crop {
    param([string]$Source,[System.Drawing.Rectangle]$Crop,[string]$Destination,[int]$Width,[int]$Height)
    $input=[System.Drawing.Bitmap]::FromFile($Source);$output=New-Bitmap $Width $Height;$graphics=New-Graphics $output
    $graphics.DrawImage($input,[System.Drawing.Rectangle]::new(0,0,$Width,$Height),$Crop,[System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose();$output.Save($Destination,[System.Drawing.Imaging.ImageFormat]::Png);$output.Dispose();$input.Dispose()
}

function Build-TreeArt {
    $passivePath = Join-Path $treeRoot 'p21-passive-backdrop.png'
    Build-Crop (Join-Path $sourceRoot 'passive-tree-master.png') ([System.Drawing.Rectangle]::new(0,0,960,1024)) $passivePath 512 512
    $loaded = [System.Drawing.Bitmap]::FromFile($passivePath)
    $passive = $loaded.Clone([System.Drawing.Rectangle]::new(0, 0, 512, 512), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $loaded.Dispose(); $cleanup = New-Graphics $passive
    $cleanup.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $cleanup.FillRectangle([System.Drawing.Brushes]::Transparent, 482, 0, 30, 512)
    $cleanup.FillRectangle([System.Drawing.Brushes]::Transparent, 478, 0, 34, 200)
    $cleanup.FillRectangle([System.Drawing.Brushes]::Transparent, 478, 310, 34, 202); $cleanup.Dispose()
    $passiveTemporary = "$passivePath.tmp.png"
    $passive.Save($passiveTemporary, [System.Drawing.Imaging.ImageFormat]::Png); $passive.Dispose()
    [System.IO.File]::Move($passiveTemporary, $passivePath, $true)
    Build-Crop (Join-Path $sourceRoot 'atlas-tree-master.png') ([System.Drawing.Rectangle]::new(0,0,1024,1024)) (Join-Path $treeRoot 'p21-atlas-backdrop.png') 512 512
    $source=[System.Drawing.Bitmap]::FromFile((Join-Path $sourceRoot 'ascendancy-master.png'));$atlas=New-Bitmap 1152 384;$graphics=New-Graphics $atlas
    for($index=0;$index -lt 3;$index++){
        $crop=[System.Drawing.Rectangle]::new($index*512,0,512,630)
        $graphics.DrawImage($source,[System.Drawing.Rectangle]::new($index*384,0,384,384),$crop,[System.Drawing.GraphicsUnit]::Pixel)
    }
    $graphics.Dispose();$atlas.Save((Join-Path $treeRoot 'p21-ascendancy-backdrops.png'),[System.Drawing.Imaging.ImageFormat]::Png);$atlas.Dispose();$source.Dispose()
}

function Get-AlphaBounds([System.Drawing.Bitmap]$bitmap) {
    $left=$bitmap.Width;$top=$bitmap.Height;$right=-1;$bottom=-1
    for($y=0;$y -lt $bitmap.Height;$y++){for($x=0;$x -lt $bitmap.Width;$x++){if($bitmap.GetPixel($x,$y).A -le 8){continue};$left=[math]::Min($left,$x);$top=[math]::Min($top,$y);$right=[math]::Max($right,$x);$bottom=[math]::Max($bottom,$y)}}
    return [System.Drawing.Rectangle]::new($left,$top,$right-$left+1,$bottom-$top+1)
}

function Save-PngIco([System.Drawing.Bitmap]$source,[string]$destination) {
    $sizes=@(16,24,32,48,64,128,256);$payloads=[System.Collections.Generic.List[byte[]]]::new()
    foreach($size in $sizes){$bmp=New-Bitmap $size $size;$g=New-Graphics $bmp;$g.DrawImage($source,[System.Drawing.Rectangle]::new(0,0,$size,$size));$g.Dispose();$stream=[System.IO.MemoryStream]::new();$bmp.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png);$payloads.Add($stream.ToArray());$stream.Dispose();$bmp.Dispose()}
    $file=[System.IO.File]::Create($destination);$writer=[System.IO.BinaryWriter]::new($file);$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count);$offset=6+16*$sizes.Count
    for($i=0;$i -lt $sizes.Count;$i++){$sizeByte=if($sizes[$i]-eq256){[byte]0}else{[byte]$sizes[$i]};$writer.Write($sizeByte);$writer.Write($sizeByte);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$payloads[$i].Length);$writer.Write([uint32]$offset);$offset+=$payloads[$i].Length}
    foreach($payload in $payloads){$writer.Write($payload)};$writer.Dispose();$file.Dispose()
}

function Build-Brand {
    $source=[System.Drawing.Bitmap]::FromFile((Join-Path $sourceRoot 'app-icon-master.png'));$bounds=Get-AlphaBounds $source
    $icon=New-Bitmap 256 256;$graphics=New-Graphics $icon
    $scale=[math]::Min(232/$bounds.Width,232/$bounds.Height);$width=[math]::Round($bounds.Width*$scale);$height=[math]::Round($bounds.Height*$scale)
    $graphics.DrawImage($source,[System.Drawing.Rectangle]::new([math]::Floor((256-$width)/2),[math]::Floor((256-$height)/2),$width,$height),$bounds,[System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose();$icon.Save((Join-Path $brandRoot 'p21-app-icon.png'),[System.Drawing.Imaging.ImageFormat]::Png);Save-PngIco $icon (Join-Path $brandRoot 'p21-app-icon.ico')
    $states=@{normal='#4d9fd1';waiting='#d8af48';paused='#777d88';error='#d45c57'}
    foreach($entry in $states.GetEnumerator()){$tray=New-Bitmap 32 32;$g=New-Graphics $tray;$g.DrawImage($icon,[System.Drawing.Rectangle]::new(1,1,30,30));$brush=[System.Drawing.SolidBrush]::new((Get-Color $entry.Value));$g.FillRectangle([System.Drawing.Brushes]::Black,22,22,9,9);$g.FillRectangle($brush,24,24,5,5);$brush.Dispose();$g.Dispose();$tray.Save((Join-Path $brandRoot "p21-tray-$($entry.Key).png"),[System.Drawing.Imaging.ImageFormat]::Png);$tray.Dispose()}
    $icon.Dispose();$source.Dispose()
}

Build-ItemAtlases
Build-SkillAtlas
Build-UiSkin
Build-TreeArt
Build-Brand
Write-Host '[p21.1-assets] Generated deterministic icons, tree art, UI skin and application branding.'
