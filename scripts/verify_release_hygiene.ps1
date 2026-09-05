[CmdletBinding()]
param(
    [string]$RepositoryRoot = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

$removedPaths = @(
    'src\Game.Godot\assets\campaignb',
    'src\Game.Godot\assets\management',
    'src\Game.Godot\assets\scenes',
    'src\Game.Godot\assets\town',
    'src\Game.Godot\assets\simulationParity',
    'src\Game.Godot\assets\archetypes',
    'src\Game.Godot\assets\art\trees',
    'src\Game.Godot\assets\art\vfx',
    'src\Game.Godot\assets\art\ui\art-jewel-atlas.png',
    'src\Game.Godot\ArenaView.cs',
    'src\Game.Godot\CampaignDashboard.cs',
    'src\Game.Godot\ManagementMapQueuePanel.cs'
)
foreach ($relativePath in $removedPaths) {
    if (Test-Path -LiteralPath (Join-Path $RepositoryRoot $relativePath)) {
        throw "Retired runtime asset returned: $relativePath"
    }
}

$retiredRuntimeMarkers = @(
    'Characters.1 开放',
    '将在 Characters.1 开放',
    'CampaignB 精细像素角色',
    'Archetypes 技能石与连接',
    'Archetypes 装备与词缀库',
    'Builds 核心数值',
    'Offline 性能：',
    'Management 主线与构筑管理',
    'Management: 模拟48h',
    '48h Campaign 结算',
    'Encounters T{run.Map.Tier}',
    'res://assets/simulationParity/',
    'res://assets/archetypes/',
    'art-combat-vfx.png',
    'art-passive-backdrop.png',
    'art-atlas-backdrop.png',
    'art-ascendancy-backdrops.png',
    'art-jewel-atlas.png'
)
$runtimeFiles = Get-ChildItem (Join-Path $RepositoryRoot 'src') -Recurse -File |
    Where-Object { $_.Extension -in '.cs', '.json', '.tscn', '.godot', '.cfg' }
foreach ($file in $runtimeFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($marker in $retiredRuntimeMarkers) {
        if ($content.IndexOf($marker, [StringComparison]::Ordinal) -ge 0) {
            throw "Retired runtime marker '$marker' remains in $($file.FullName)"
        }
    }
}

Write-Host '[release-hygiene] PASS: retired assets, UI text and runtime references are absent.'
