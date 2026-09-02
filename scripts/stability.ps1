[CmdletBinding()]
param(
    [ValidateSet('Visible', 'Tray', 'Offline48h')]
    [string]$Mode = 'Visible',
    [int]$Seconds = 7200,
    [string]$GodotPath = $env:GODOT_BIN
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$projectPath = Join-Path $repositoryRoot 'src\Game.Godot'
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
. (Join-Path $PSScriptRoot 'native-tools.ps1')
$dotnetBinary = Resolve-DotnetBinary
Set-DotnetEnvironment -DotnetBinary $dotnetBinary
if ($Mode -eq 'Offline48h') {
    Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('test',
        (Join-Path $repositoryRoot 'src\Game.Tests\Game.Tests.csproj'), '--configuration', 'Release', '--filter',
        'FullyQualifiedName~P15FeatureTests.OfflineFortyEightHours|FullyQualifiedName~P22FeatureTests.OfflineFortyEightHour') `
        -Label '48 hour offline-equivalent test'
    return
}
if ($Seconds -lt 10) { throw 'Seconds must be at least 10.' }
$GodotPath = Resolve-GodotBinary -RequestedPath $GodotPath
& (Join-Path $repositoryRoot 'scripts\verify_p21_assets.ps1') -RepositoryRoot $repositoryRoot
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('build', (Join-Path $projectPath 'GameForWork.csproj'), '--configuration', 'Debug') -Label 'Godot C# build'
Invoke-NativeChecked -FilePath $GodotPath -Arguments @('--headless', '--path', $projectPath, '--editor', '--quit') `
    -Label 'Godot asset import before stability run' -RejectGodotErrors
$modeArgument = if ($Mode -eq 'Tray') { '--p22-stability-tray' } else { '--p22-stability-visible' }
New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
$reportPath = Join-Path $artifactsRoot "p22-stability-$($Mode.ToLowerInvariant()).json"
if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath -Force }
Invoke-NativeChecked -FilePath $GodotPath -Arguments @('--path', $projectPath, '--', $modeArgument,
    "--p22-stability-seconds=$Seconds", "--p22-stability-report=$reportPath") `
    -Label "$Mode stability run" -RejectGodotErrors
if (-not (Test-Path -LiteralPath $reportPath)) { throw "P22 stability report was not created: $reportPath" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$megabyte = 1MB
if ([double]$report.PeakWorkingSetBytes -gt 700 * $megabyte) {
    throw "Peak working set exceeded 700 MB: $([math]::Round($report.PeakWorkingSetBytes / $megabyte, 1)) MB"
}
if ($Seconds -ge 60 -and [double]$report.WorkingSetGrowthBytes -gt 80 * $megabyte) {
    throw "Working-set growth exceeded 80 MB: $([math]::Round($report.WorkingSetGrowthBytes / $megabyte, 1)) MB"
}
if ($Mode -eq 'Tray' -and $Seconds -ge 60 -and [double]$report.AverageCpuPercent -gt 2.0) {
    throw "Tray CPU exceeded 2%: $([math]::Round($report.AverageCpuPercent, 2))%"
}
if ($Mode -eq 'Visible' -and [double]$report.PeakFrameMilliseconds -gt 40) {
    throw "Visible frame hitch exceeded 40 ms after warmup: $([math]::Round($report.PeakFrameMilliseconds, 2)) ms"
}
if ([double]$report.PeakSimulationMilliseconds -gt 10) {
    throw "Simulation tick exceeded 10 ms: $([math]::Round($report.PeakSimulationMilliseconds, 2)) ms"
}
if ([double]$report.PeakUiRefreshMilliseconds -gt 16) {
    throw "UI refresh exceeded 16 ms: $([math]::Round($report.PeakUiRefreshMilliseconds, 2)) ms"
}
Write-Host ("[stability] PASS mode={0} seconds={1} peakMB={2:N1} growthMB={3:N1} cpu={4:N2}% framePeakMs={5:N2} tickPeakMs={6:N2} uiPeakMs={7:N2}" -f `
    $Mode, $Seconds, ($report.PeakWorkingSetBytes / $megabyte), ($report.WorkingSetGrowthBytes / $megabyte), `
    $report.AverageCpuPercent, $report.PeakFrameMilliseconds, $report.PeakSimulationMilliseconds, `
    $report.PeakUiRefreshMilliseconds)
