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
if ($Mode -eq 'Offline48h') {
    dotnet test (Join-Path $repositoryRoot 'src\Game.Tests\Game.Tests.csproj') --filter 'FullyQualifiedName~P15FeatureTests.OfflineFortyEightHours'
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
$modeArgument = if ($Mode -eq 'Tray') { '--p15-stability-tray' } else { '--p15-stability-visible' }
& $GodotPath --path $projectPath -- $modeArgument "--p15-stability-seconds=$Seconds"
if ($LASTEXITCODE -ne 0) { throw "$Mode stability run failed with exit code $LASTEXITCODE." }
Write-Host "[stability] PASS mode=$Mode seconds=$Seconds"
