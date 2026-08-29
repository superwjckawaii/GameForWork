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
if ($Mode -eq 'Offline48h') {
    dotnet test (Join-Path $repositoryRoot 'src\Game.Tests\Game.Tests.csproj') --configuration Release --filter `
        'FullyQualifiedName~P15FeatureTests.OfflineFortyEightHours|FullyQualifiedName~P22FeatureTests.OfflineFortyEightHour'
    if ($LASTEXITCODE -ne 0) { throw '48 hour offline-equivalent test failed.' }
    return
}
if ($Seconds -lt 10) { throw 'Seconds must be at least 10.' }
if (-not $GodotPath) {
    $GodotPath = 'D:\OtherTools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
}
if (-not (Test-Path -LiteralPath $GodotPath)) { throw "Godot executable not found: $GodotPath" }
dotnet build (Join-Path $projectPath 'GameForWork.csproj') --configuration Debug
if ($LASTEXITCODE -ne 0) { throw 'Godot C# build failed.' }
$modeArgument = if ($Mode -eq 'Tray') { '--p22-stability-tray' } else { '--p22-stability-visible' }
New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
$reportPath = Join-Path $artifactsRoot "p22-stability-$($Mode.ToLowerInvariant()).json"
if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath -Force }
& $GodotPath --path $projectPath -- $modeArgument "--p22-stability-seconds=$Seconds" "--p22-stability-report=$reportPath"
if ($LASTEXITCODE -ne 0) { throw "$Mode stability run failed with exit code $LASTEXITCODE." }
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
Write-Host ("[stability] PASS mode={0} seconds={1} peakMB={2:N1} growthMB={3:N1} cpu={4:N2}% tickPeakMs={5:N2}" -f `
    $Mode, $Seconds, ($report.PeakWorkingSetBytes / $megabyte), ($report.WorkingSetGrowthBytes / $megabyte), `
    $report.AverageCpuPercent, $report.PeakSimulationMilliseconds)
