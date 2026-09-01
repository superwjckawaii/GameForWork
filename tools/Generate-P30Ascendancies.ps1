$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$files = 1..3 | ForEach-Object { Join-Path $root "docs/v0.4/P30_ASCENDANCY_REVIEW_$_.md" }
$nameToEnum = [ordered]@{
  '血战士'='BloodFighter'; '铁壁卫'='IronGuardian'; '破军者'='Warbreaker'
  '神射手'='Marksman'; '影刃客'='Shadowblade'; '毒术师'='Venomist'
  '牧魂师'='SoulShepherd'; '颂灵师'='SpiritCantor'; '咒契师'='Hexbinder'
  '元素使'='Elementalist'; '虚空学者'='VoidScholar'; '秘盾师'='AegisMage'
  '行武僧'='MartialMonk'; '灵兽使'='BeastKeeper'; '幻身宗师'='PhantomMaster'
  '刻印师'='Runecarver'; '魔铠师'='Spellarmor'; '铸像师'='IdolForger'
}
$result = @()
foreach ($file in $files) {
  $lines = Get-Content -LiteralPath $file -Encoding UTF8
  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -notmatch '^## \d+\. .*[：:]([\p{L}]+)$') { continue }
    $display = $Matches[1]
    if (-not $nameToEnum.Contains($display)) { continue }
    while ($i -lt $lines.Count -and $lines[$i] -notmatch '^\| 方向 \| 强化节点 \| 核心节点 \|$') { $i++ }
    $branches = @()
    $i += 2
    while ($i -lt $lines.Count -and $lines[$i] -match '^\|') {
      $cells = $lines[$i].Trim('|').Split('|') | ForEach-Object { $_.Trim() }
      if ($cells.Count -ge 3) {
        $small = $cells[1] -replace '\*\*',''
        $core = $cells[2] -replace '\*\*',''
        $smallParts = $small.Split('：',2); $coreParts = $core.Split('：',2)
        $branches += [ordered]@{ direction=$cells[0]; reinforcementName=$smallParts[0]; reinforcementEffect=$smallParts[1]; coreName=$coreParts[0]; coreEffect=$coreParts[1] }
      }
      $i++
    }
    if ($branches.Count -ne 6) { throw "$display expected 6 branches, got $($branches.Count)" }
    $result += [ordered]@{ ascendancy=$nameToEnum[$display]; displayName=$display; branches=$branches }
  }
}
if ($result.Count -ne 18) { throw "Expected 18 ascendancies, got $($result.Count)" }
$output = Join-Path $root 'src/Game.Core/P30/Data/p30-ascendancies.json'
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $output -Encoding UTF8
Write-Output "Generated $output with $($result.Count) ascendancies and $((($result | ForEach-Object {$_.branches.Count}) | Measure-Object -Sum).Sum) branches."
