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

$masteryAliases = @{
    '属性' = '属性'; '剑' = '剑类'; '斧' = '斧类'; '锤' = '锤类'; '匕首' = '匕首'; '弓' = '弓类';
    '法杖' = '法杖'; '符文刃' = '符刃'; '徒手' = '徒手'; '盾击' = '盾击'; '单手武器' = '单手';
    '双手武器' = '双手'; '双持' = '双持'; '攻击' = '攻击'; '法术' = '法术'; '暴击' = '暴击';
    '物理伤害' = '物理'; '元素伤害' = '元素通用'; '火焰' = '火焰'; '冰霜' = '冰霜'; '闪电' = '闪电';
    '虚空伤害' = '虚空'; '持续伤害' = '持续伤害通用'; '物理持续伤害' = '持续伤害通用';
    '流血' = '流血'; '中毒' = '中毒'; '点燃' = '点燃'; '冰缓/冻结' = '冰缓与冻结';
    '感电/麻痹' = '感电与麻痹'; '眩晕' = '眩晕'; '破甲' = '破甲与物理穿透';
    '物理穿透' = '破甲与物理穿透'; '曝露' = '元素曝露与元素穿透';
    '元素穿透' = '元素曝露与元素穿透'; '侵蚀/凋零' = '侵蚀与凋零';
    '生命' = '生命'; '护盾' = '能量护盾'; '法力' = '法力'; '法力消耗' = '法力'; '灵障' = '灵障';
    '再生' = '再生与持续恢复'; '护甲' = '护甲'; '闪避' = '闪避'; '格挡' = '格挡';
    '法术压制' = '法术压制'; '坚韧/护体' = '护体与承伤缓冲'; '元素抗性' = '元素抗性';
    '虚空抗性' = '虚空抗性'; '偷取' = '偷取'; '药剂' = '药剂';
    '生命＋闪避' = '生命'; '生命＋元素防御' = '生命'; '生命＋虚空防御' = '生命';
    '护甲＋护盾' = '护甲'; '护盾＋闪避' = '能量护盾'; '元素护盾' = '元素抗性'; '虚空护盾' = '虚空抗性';
    '普通召唤物' = '普通召唤物'; '核心伙伴' = '伙伴'; '构装体' = '构装体'; '陷阱' = '陷阱'; '幻身' = '幻身';
    '命中＋投射物速度' = '命中与精准'; '命中＋攻速' = '命中与精准'; '命中＋暴伤' = '命中与精准';
    '近战打击' = '近战打击'; '投射物' = '投射物'; '范围' = '范围与距离'; '距离' = '范围与距离';
    '重复施放' = '重复与引导'; '持续引导' = '重复与引导'; '触发与冷却' = '触发与冷却'; '反击' = '反击';
    '光环与保留' = '光环与保留'; '诅咒' = '诅咒'; '战吼与增益' = '战吼与增益';
    '慈悲＋暴怒' = '美德/恶德'; '慈悲＋懒惰' = '美德/恶德'; '节制＋懒惰' = '美德/恶德';
    '节制＋傲慢' = '美德/恶德'; '谦逊＋暴怒' = '美德/恶德'; '谦逊＋傲慢' = '美德/恶德'
}

function Find-Mastery([string]$theme) {
    $pool = $masteryAliases[$theme]
    if ([string]::IsNullOrWhiteSpace($pool)) { throw "No mastery pool mapping for theme '$theme'." }
    foreach ($fileName in $sourceFiles) {
        $path = Join-Path $docRoot $fileName
        $lines = Get-Content -LiteralPath $path -Encoding UTF8
        for ($index = 0; $index -lt $lines.Count; $index++) {
            $heading = Normalize-Markdown $lines[$index]
            if ($heading -notmatch '^#{2,3}\s+[0-9.]+\s+(.+)$') { continue }
            $title = $Matches[1].Trim()
            if ($title -notmatch '专精' -or !$title.StartsWith($pool, [StringComparison]::Ordinal)) { continue }
            $options = [System.Collections.Generic.List[string]]::new()
            for ($cursor = $index + 1; $cursor -lt $lines.Count; $cursor++) {
                if ($lines[$cursor] -match '^#{2,3}\s+') { break }
                $normalized = Normalize-Markdown $lines[$cursor]
                if ($normalized -match '^\d+\.\s+(.+)$') { $options.Add($Matches[1].Trim()) }
            }
            if ($options.Count -eq 7) {
                return [ordered]@{ key = ($pool -replace '[/＋与]', '_'); options = @($options) }
            }
        }
    }
    throw "Mastery pool '$pool' for theme '$theme' was not found or did not contain seven options."
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
    $mastery = Find-Mastery $theme
    $clusters.Add([ordered]@{
        slot = $slot
        stableSlug = $slot.ToLowerInvariant().Replace('-', '_')
        name = $name
        theme = $theme
        size = $(if ($slot.StartsWith('V')) { 'medium' } else { 'large' })
        sourceFile = $source.file
        descriptions = @($source.lines)
        masteryKey = $mastery.key
        masteryOptions = @($mastery.options)
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
    canvasWidth = 8192
    canvasHeight = 8192
    innerRadius = 800
    middleRadius = 1800
    outerRadius = 2800
    clusters = @($clusters)
}
$output = Join-Path $RepositoryRoot 'src/Game.Core/P30/Data/p30-passive-tree.json'
$directory = Split-Path -Parent $output
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$json = $payload | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($output, $json + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
Write-Output "Generated $output with $($clusters.Count) clusters."
