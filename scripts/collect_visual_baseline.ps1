[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RunPath,
    [Parameter(Mandatory)][string]$EvidencePath,
    [switch]$RecordFailedRun
)
$ErrorActionPreference = 'Stop'
$runRoot = (Resolve-Path -LiteralPath $RunPath).Path
$environmentReport = Get-Content -LiteralPath (Join-Path $runRoot 'environment.json') -Raw | ConvertFrom-Json
if ($environmentReport.Mode -eq 'smoke') { throw 'Smoke runs are not baseline evidence.' }
if ($environmentReport.Mode -notin @('representative','exhaustive')) { throw 'Unknown baseline protocol.' }
if (-not (Select-String -LiteralPath (Join-Path $runRoot 'run.log') -SimpleMatch 'BASELINE_COMPLETE' -Quiet)) {
    throw 'Run has not completed.'
}
$hasEngineDiagnostics = (Get-Item -LiteralPath (Join-Path $runRoot 'errors.log')).Length -ne 0 -or
    (Select-String -LiteralPath (Join-Path $runRoot 'run.log') -Pattern '"level":"Error"|ERROR:|SCRIPT ERROR:' -Quiet)
if ($hasEngineDiagnostics -and -not $RecordFailedRun) {
    throw 'Engine errors must be investigated before collecting evidence.'
}
$reports = @(Get-Content -LiteralPath (Join-Path $runRoot 'baseline.json') -Raw | ConvertFrom-Json)
$expectedCount = if ($environmentReport.Mode -eq 'exhaustive') { 42 } else { 20 }
if ($reports.Count -ne $expectedCount) { throw "Expected $expectedCount cases; found $($reports.Count)." }
$longFrames = [Collections.Generic.List[object]]::new()
for ($index = 0; $index -lt $reports.Count; $index++) {
    $report = $reports[$index]
    if ($report.ActualWidth -ne $report.Width -or $report.ActualHeight -ne $report.Height) {
        throw "Window size mismatch in case $index."
    }
    if ($null -eq $report.RenderedFrames -or $report.RenderedFrames -lt $report.Frames * 0.95) {
        throw "Insufficient actual rendering in case $index."
    }
    $trace = @(Get-Content -LiteralPath (Join-Path $runRoot ('frames-{0:D2}.json' -f $index)) -Raw | ConvertFrom-Json)
    if ($trace.Count -ne $report.Frames) { throw "Frame count mismatch in case $index." }
    $measured = $trace | Measure-Object -Property FrameMs -Maximum -Average -Sum
    if ([Math]::Abs($measured.Maximum - $report.MaxMs) -gt 0.000001 -or
        [Math]::Abs($measured.Average - $report.MeanMs) -gt 0.000001 -or
        $measured.Sum / 1000 -lt $report.Capture) { throw "Timing evidence mismatch in case $index." }
    for ($frame = 0; $frame -lt $trace.Count; $frame++) {
        if ($trace[$frame].FrameMs -le 40) { continue }
        $row = $trace[$frame]
        $previousGc = if ($frame -eq 0) { $row.Gc } else { $trace[$frame - 1].Gc }
        $longFrames.Add([pscustomobject]@{
            Case = $index; Scenario = $report.Scenario; AtSeconds = $row.AtSeconds
            FrameMs = $row.FrameMs; SimulationMs = $row.SimulationMs; UiMs = $row.UiMs; SaveMs = $row.SaveMs
            GcDelta = @(0..2 | ForEach-Object { $row.Gc[$_] - $previousGc[$_] })
        })
    }
}
foreach ($scenario in @('normal','dense')) {
    $fixture = Get-Content -LiteralPath (Join-Path $runRoot "fixture-$scenario.json") -Raw | ConvertFrom-Json
    $buildSuffix = if ($scenario -eq 'normal') { 'entry' } else { 'endgame' }
    $expectedLevel = if ($scenario -eq 'normal') { 80 } else { 120 }
    $expectedTier = if ($scenario -eq 'normal') { 1 } else { 20 }
    if ($fixture.BuildId -ne "core.benchmark.breaker.$buildSuffix" -or
        $fixture.Build.Sheet.Level -ne $expectedLevel -or $fixture.Map.AreaLevel -ne $expectedTier -or
        $fixture.Seed -ne 20260907 -or $fixture.TimelineHash -notmatch '^[0-9a-f]{64}$') {
        throw "Unexpected frozen fixture for $scenario."
    }
    $longSamples = @($reports | Where-Object { $_.Scenario -eq $scenario -and $_.Capture -eq 120 -and $_.Warmup -eq 30 })
    if ($longSamples.Count -lt 3) { throw "Missing repeated long samples for $scenario." }
    $sequence = @(Get-ChildItem -LiteralPath $runRoot -Filter "sequence-$scenario-*.webp")
    if ($sequence.Count -ne 30) { throw "Missing sequential capture for $scenario." }
}
if (Test-Path -LiteralPath $EvidencePath) { throw 'Evidence destination already exists; use a new directory.' }
$evidenceRoot = (New-Item -ItemType Directory -Path $EvidencePath).FullName
foreach ($name in @('baseline.json','fixture-normal.json','fixture-dense.json','host.json','power.txt','battery.json')) {
    Copy-Item -LiteralPath (Join-Path $runRoot $name) -Destination $evidenceRoot
}
$environmentReport.SaveRoot = 'Isolated process-specific temporary stability directory'
$environmentReport | ConvertTo-Json -Depth 12 | Out-File (Join-Path $evidenceRoot 'environment.json') -Encoding utf8
ConvertTo-Json -InputObject $longFrames.ToArray() -Depth 6 | Out-File (Join-Path $evidenceRoot 'long-frames.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $runRoot 'errors.log') -Destination (Join-Path $evidenceRoot 'engine-diagnostics.txt')
$failedTargets = @($reports | Where-Object {
    $_.MaxMs -gt 40 -or $_.SimulationPeakMs -gt 10 -or $_.UiPeakMs -gt 16 -or
    $_.WorkingSetPeakBytes -gt 700MB -or ($_.FrameCap -eq 60 -and ($_.Quantile95Ms -gt 18 -or $_.Quantile99Ms -gt 25))
})
[pscustomobject]@{
    DataCoverageValidated = $true
    EngineGatePassed = -not $hasEngineDiagnostics
    PerformanceTargetsPassed = $failedTargets.Count -eq 0
    Status = if ($hasEngineDiagnostics -or $failedTargets.Count) { 'FailedBaseline' } else { 'RepresentativeBaselineWithinTargets' }
    FailedCaseIndices = @($failedTargets | Select-Object -ExpandProperty CaseIndex)
    Scope = 'Pre-optimization baseline, not full release acceptance'
} | ConvertTo-Json -Depth 4 | Out-File (Join-Path $evidenceRoot 'collection.json') -Encoding utf8
Get-ChildItem -LiteralPath $runRoot -Filter 'case-*.webp' | Copy-Item -Destination $evidenceRoot
$tracePaths = @(
    Get-ChildItem -LiteralPath $runRoot -Filter 'frames-*.json'
    Get-ChildItem -LiteralPath $runRoot -Filter 'sequence-*.webp'
    Get-ChildItem -LiteralPath $runRoot -Filter 'sequence-*.json'
) | Select-Object -ExpandProperty FullName
Compress-Archive -LiteralPath $tracePaths -DestinationPath (Join-Path $evidenceRoot 'traces-and-sequences.zip') -CompressionLevel Optimal
$hashes = Get-ChildItem -LiteralPath $evidenceRoot -File | Where-Object Name -ne 'checksums.json' | Get-FileHash -Algorithm SHA256 |
    Select-Object @{Name='File';Expression={Split-Path -Leaf $_.Path}},Hash |
    ConvertTo-Json
$hashes | Out-File (Join-Path $evidenceRoot 'checksums.json') -Encoding utf8
Write-Host "[visual-baseline] Validated $($reports.Count) cases; retained $($longFrames.Count) frames above 40 ms. Evidence: $evidenceRoot"
if ($hasEngineDiagnostics -or $failedTargets.Count) { Write-Warning 'FAILED baseline recorded. This does not pass the engine/performance gate.' }
