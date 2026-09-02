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
Write-Host '[p31-assets] PASS: 16 isolated cells, dimensions and hash are valid.'
