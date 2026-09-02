[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetRoot = Join-Path $RepositoryRoot 'src\Game.Godot\assets\p31'
$manifestPath = Join-Path $assetRoot 'p31-assets.json'
$atlasPath = Join-Path $assetRoot 'vfx\p31-combat-vfx.png'
if (-not (Test-Path -LiteralPath $manifestPath) -or -not (Test-Path -LiteralPath $atlasPath)) {
    throw 'P31 asset manifest or combat VFX atlas is missing.'
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$bitmap = [System.Drawing.Bitmap]::new($atlasPath)
try {
    if ($bitmap.Width -ne 256 -or $bitmap.Height -ne 256) {
        throw "P31 combat VFX atlas must be 256x256, got $($bitmap.Width)x$($bitmap.Height)."
    }
    for ($row = 0; $row -lt 4; $row += 1) {
        for ($column = 0; $column -lt 4; $column += 1) {
            $visible = $false
            for ($y = $row * 64; $y -lt ($row + 1) * 64 -and -not $visible; $y += 1) {
                for ($x = $column * 64; $x -lt ($column + 1) * 64; $x += 1) {
                    if ($bitmap.GetPixel($x, $y).A -ge 8) { $visible = $true; break }
                }
            }
            if (-not $visible) { throw "P31 VFX cell $column,$row is empty." }
        }
    }
} finally {
    $bitmap.Dispose()
}
$actual = (Get-FileHash -LiteralPath $atlasPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $manifest.sha256) { throw "P31 VFX hash mismatch: $actual" }

$treeRoot = Join-Path $assetRoot 'trees'
$treeDimensions = @{
    'p31-passive-backdrop.png' = @(2048, 2048)
    'p31-atlas-backdrop.png' = @(2048, 2048)
}
foreach ($entry in $treeDimensions.GetEnumerator()) {
    $path = Join-Path $treeRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing P31 tree asset: $($entry.Key)" }
    $tree = [System.Drawing.Bitmap]::new($path)
    try {
        if ($tree.Width -ne $entry.Value[0] -or $tree.Height -ne $entry.Value[1]) {
            throw "P31 tree asset $($entry.Key) must be $($entry.Value[0])x$($entry.Value[1])."
        }
    } finally { $tree.Dispose() }
}

$ascendancyFiles = @(Get-ChildItem -LiteralPath (Join-Path $treeRoot 'ascendancy') -Filter 'p31-ascendancy-*.png')
if ($ascendancyFiles.Count -ne 18) { throw "P31 requires 18 ascendancy backdrops, got $($ascendancyFiles.Count)." }
foreach ($file in $ascendancyFiles) {
    $tree = [System.Drawing.Bitmap]::new($file.FullName)
    try {
        if ($tree.Width -ne 768 -or $tree.Height -ne 768) {
            throw "P31 ascendancy backdrop $($file.Name) must be 768x768."
        }
    } finally { $tree.Dispose() }
}

Write-Host '[p31-assets] PASS: VFX atlas plus exact-coordinate main, atlas and 18 ascendancy backdrops are valid.'
