param([string]$RepositoryRoot=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'art-crop.ps1')
$assetRoot=Join-Path $RepositoryRoot 'src/Game.Godot/assets'
$manifest=Get-Content (Join-Path $assetRoot 'crop-manifest.json') -Raw|ConvertFrom-Json
$files=@(Get-ChildItem $assetRoot -Recurse -Filter '*.png'|ForEach-Object {[IO.Path]::GetRelativePath($assetRoot,$_.FullName).Replace('\','/')})
$diff=Compare-Object @($manifest.assets.path|Sort-Object) @($files|Sort-Object)
if($diff){throw 'Runtime PNG inventory differs from crop-manifest.json.'}
$cells=0
foreach($asset in $manifest.assets){
    $path=Join-Path $assetRoot $asset.path
    $bitmap=[Drawing.Bitmap]::FromFile($path)
    try {
        if($bitmap.Width -ne $asset.width -or $bitmap.Height -ne $asset.height){throw "Wrong dimensions: $($asset.path)"}
        $errorText=[ArtCrop]::Validate($bitmap,$asset.columns,$asset.rows,$asset.count,$asset.gutters,$asset.unique)
        if($errorText){throw "$($asset.path): $errorText"}
    } finally {$bitmap.Dispose()}
    if((Get-FileHash $path).Hash.ToLowerInvariant() -ne $asset.sha256){throw "Unreviewed asset hash: $($asset.path)"}
    $cells+=$asset.count
}
$skills=Get-Content (Join-Path $assetRoot 'equipmentArt/ui/skill-art-manifest.json') -Raw|ConvertFrom-Json
if($skills.skills.Count -ne 184 -or @($skills.skills.id|Select-Object -Unique).Count -ne 184){throw 'Skill art requires 184 unique runtime identities.'}
Write-Host "[current-assets] PASS: $($files.Count) PNGs, $cells occupied crops, dimensions, gutters, identities and hashes."
