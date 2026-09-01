param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$docRoot = Join-Path $RepositoryRoot 'docs/v0.4'
$placementPath = Join-Path $docRoot 'P30_PASSIVE_TREE_PLACEMENT.md'
$sourceFiles = @(
    'P30_PASSIVE_CLUSTERS.md',
    'P30_DAMAGE_AILMENT_CLUSTERS.md',
    'P30_DEFENSE_RESOURCE_CLUSTERS.md',
    'P30_UNIT_CLUSTERS.md',
    'P30_SKILL_MECHANISM_CLUSTERS.md',
    'P30_AUXILIARY_GENERAL_CLUSTERS.md',
    'P30_VIRTUE_VICE_CLUSTERS.md',
    'P30_PASSIVE_TREE_TOPOLOGY.md'
)

function Normalize-Markdown([string]$text) {
    return ($text -replace '`', '' -replace '\*', '' -replace '\s+', ' ').Trim()
}

function Find-Source([string]$name) {
    foreach ($fileName in $sourceFiles) {
        $path = Join-Path $docRoot $fileName
        $lines = Get-Content -LiteralPath $path -Encoding UTF8
        for ($index = 0; $index -lt $lines.Count; $index++) {
            $line = Normalize-Markdown $lines[$index]
            if ($line -match '^###\s+[0-9.]+\s+(.+?)(?:（.*）)?$') {
                $headingName = ($Matches[1] -split '[：:]')[0].Trim()
                if ($headingName -ne $name) { continue }
                $captured = [System.Collections.Generic.List[string]]::new()
                for ($cursor = $index + 1; $cursor -lt $lines.Count; $cursor++) {
                    if ($lines[$cursor] -match '^#{2,3}\s+') { break }
                    $normalized = Normalize-Markdown $lines[$cursor]
                    if ($normalized -match '^\|\s*([^|]+?)\s*\|\s*(.+?)\s*\|$' -and
                        $Matches[1] -notmatch '^(---|节点|路线|簇|入口)$') {
                        $captured.Add("$($Matches[1].Trim())：$($Matches[2].Trim())")
                    }
                    elseif ($normalized -match '^[-0-9]+\.\s+(.+)$' -or $normalized -match '^-\s+(.+)$') {
                        $captured.Add($Matches[1].Trim())
                    }
                }
                return [ordered]@{ file = $fileName; lines = @($captured) }
            }
        }

        foreach ($line in $lines) {
            $normalized = Normalize-Markdown $line
            if ($normalized -match '^\|\s*([^|]+?)\s*\|\s*(中型|大型)\s*\|\s*([^|]+?)\s*\|\s*(.+?)\s*\|$' -and
                $Matches[1].Trim() -eq $name) {
                return [ordered]@{
                    file = $fileName
                    lines = @("小点：$($Matches[3].Trim())", "显著：$($Matches[4].Trim())")
                }
            }
        }
    }
    return [ordered]@{ file = ''; lines = @("$name：效果见已确认设计表") }
}

$placement = Get-Content -LiteralPath $placementPath -Encoding UTF8
$clusters = [System.Collections.Generic.List[object]]::new()
foreach ($line in $placement) {
    $normalized = Normalize-Markdown $line
    if ($normalized -notmatch '^\|\s*((?:V[0-5]-[IRO]\d\d)|(?:E[0-5]-P\d\d))\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|$') {
        continue
    }
    $slot = $Matches[1].Trim()
    $name = $Matches[2].Trim()
    $theme = $Matches[3].Trim()
    $source = Find-Source $name
    $clusters.Add([ordered]@{
        slot = $slot
        stableSlug = $slot.ToLowerInvariant().Replace('-', '_')
        name = $name
        theme = $theme
        size = $(if ($slot.StartsWith('V')) { 'medium' } else { 'large' })
        sourceFile = $source.file
        descriptions = @($source.lines)
    })
}

if ($clusters.Count -ne 168) {
    throw "Expected 168 clusters, generated $($clusters.Count)."
}
if (($clusters | Where-Object size -eq 'medium').Count -ne 131 -or
    ($clusters | Where-Object size -eq 'large').Count -ne 37) {
    throw 'P30 cluster size totals are invalid.'
}

$payload = [ordered]@{
    version = 'p30.v1'
    canvasWidth = 4096
    canvasHeight = 4096
    innerRadius = 384
    middleRadius = 708
    outerRadius = 1032
    clusters = @($clusters)
}
$output = Join-Path $RepositoryRoot 'src/Game.Core/P30/Data/p30-passive-tree.json'
$directory = Split-Path -Parent $output
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$json = $payload | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($output, $json + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
Write-Output "Generated $output with $($clusters.Count) clusters."
