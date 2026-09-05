param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '../src/Game.Godot/assets/art/ui/art-skill-gems.png'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '../src/Game.Godot/assets/equipmentArt/ui/equipmentArt-skill-stones.png')
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
. (Join-Path $PSScriptRoot 'native-tools.ps1')
$root=Split-Path -Parent $PSScriptRoot
$metadata=Join-Path $root 'artifacts/art-audit/catalog.json'
$dotnet=Resolve-DotnetBinary
Invoke-NativeChecked -FilePath $dotnet -Arguments @('run','--project',(Join-Path $root 'tools/ArtTreeExport'),'--',$metadata) -Label 'Export runtime art identities'
$skills=@((Get-Content $metadata -Raw|ConvertFrom-Json).skills)
$source=[System.Drawing.Bitmap]::FromFile((Resolve-Path $SourcePath))
$atlas=[System.Drawing.Bitmap]::new(320,[int][Math]::Ceiling($skills.Count/10.0)*32)
$g=[System.Drawing.Graphics]::FromImage($atlas)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
try {
    foreach($skill in $skills) {
        $index=[int]$skill.index
        # Reuse the established diamond/round icon language, never the two empty Art cells.
        $template=if($skill.active){$index%30}else{30+($index-86)%48}
        $x=($index%10)*32;$y=[Math]::Floor($index/10)*32
        $g.DrawImage($source,[System.Drawing.Rectangle]::new($x,$y,32,32),[System.Drawing.Rectangle]::new(($template%10)*32,[Math]::Floor($template/10)*32,32,32),[System.Drawing.GraphicsUnit]::Pixel)
        $dark=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,21,25,35))
        $light=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,235,200,126))
        try {
            # Stable eight-bit rune signature, matching the existing Art icon system.
            $g.FillRectangle($dark,$x+4,$y+23,24,4)
            for($bit=0;$bit -lt 8;$bit++) {
                if((($index+1) -band (1 -shl $bit)) -ne 0){$g.FillRectangle($light,$x+5+$bit*3,$y+24,2,2)}
            }
        } finally {$dark.Dispose();$light.Dispose()}
    }
    $atlas.Save([System.IO.Path]::GetFullPath($OutputPath),[System.Drawing.Imaging.ImageFormat]::Png)
} finally {$g.Dispose();$atlas.Dispose();$source.Dispose()}
@{columns=10;rows=[int][Math]::Ceiling($skills.Count/10.0);cellSize=32;skills=$skills;sha256=(Get-FileHash $OutputPath).Hash.ToLowerInvariant()}|
    ConvertTo-Json -Depth 6|Set-Content (Join-Path (Split-Path $OutputPath) 'skill-art-manifest.json') -Encoding utf8
Write-Host "[skill-art] PASS: $($skills.Count) runtime identities, one nonempty cell per stone."
