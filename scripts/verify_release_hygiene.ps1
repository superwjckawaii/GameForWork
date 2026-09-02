[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$removedPaths = @(
    'src\Game.Godot\assets\p1b',
    'src\Game.Godot\assets\p2',
    'src\Game.Godot\assets\p3',
    'src\Game.Godot\assets\p9',
    'src\Game.Godot\assets\p15',
    'src\Game.Godot\assets\p24',
    'src\Game.Godot\assets\p21\trees',
    'src\Game.Godot\assets\p21\vfx',
    'src\Game.Godot\assets\p21\ui\p21-jewel-atlas.png',
    'src\Game.Godot\ArenaView.cs',
    'src\Game.Godot\P1Dashboard.cs',
    'src\Game.Godot\P2MapQueuePanel.cs'
)
foreach ($relativePath in $removedPaths) {
    if (Test-Path -LiteralPath (Join-Path $RepositoryRoot $relativePath)) {
        throw "Retired runtime asset returned: $relativePath"
    }
}

$retiredRuntimeMarkers = @(
    'P23.1 开放',
    '将在 P23.1 开放',
    'P1B 精细像素角色',
    'P24 技能石与连接',
    'P24 装备与词缀库',
    'P30 核心数值',
    'P7 性能：',
    'P2 主线与构筑管理',
    'P2: 模拟48h',
    '48h P1 结算',
    'P28 T{run.Map.Tier}',
    'res://assets/p15/',
    'res://assets/p24/',
    'p21-combat-vfx.png',
    'p21-passive-backdrop.png',
    'p21-atlas-backdrop.png',
    'p21-ascendancy-backdrops.png',
    'p21-jewel-atlas.png'
)
$runtimeFiles = Get-ChildItem (Join-Path $RepositoryRoot 'src') -Recurse -File |
    Where-Object { $_.Extension -in '.cs', '.json', '.tscn', '.godot', '.cfg' }
foreach ($file in $runtimeFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($marker in $retiredRuntimeMarkers) {
        if ($content.Contains($marker, [StringComparison]::Ordinal)) {
            throw "Retired runtime marker '$marker' remains in $($file.FullName)"
        }
    }
}

Write-Host '[release-hygiene] PASS: retired assets, UI text and runtime references are absent.'
