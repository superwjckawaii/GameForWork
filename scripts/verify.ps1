[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'GameForWork.sln'
$godotProject = Join-Path $repositoryRoot 'src\Game.Godot'
. (Join-Path $PSScriptRoot 'native-tools.ps1')

$dotnetBinary = Resolve-DotnetBinary
Set-DotnetEnvironment -DotnetBinary $dotnetBinary
$godotBinary = Resolve-GodotBinary
Write-Host "[verify] configuration=$Configuration"
Write-Host "[verify] dotnet=$dotnetBinary"
Write-Host "[verify] godot=$godotBinary"

& (Join-Path $repositoryRoot 'scripts\verify_p21_assets.ps1') -RepositoryRoot $repositoryRoot

Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('restore', $solutionPath) -Label 'Restore solution'
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('build', $solutionPath, '--no-restore', '--configuration', $Configuration) -Label 'Build solution'
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('test', (Join-Path $repositoryRoot 'src\Game.Tests\Game.Tests.csproj'), '--no-build', '--configuration', $Configuration) -Label 'Run unit tests'
$auditRoot = Join-Path $repositoryRoot 'artifacts\release-gate'
New-Item -ItemType Directory -Path $auditRoot -Force | Out-Null
$economyAudit = Join-Path $auditRoot 'economy.md'
$combatAudit = Join-Path $auditRoot 'combat.md'
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('run', '--project',
    (Join-Path $repositoryRoot 'tools\P20Audit\P20Audit.csproj'), '--configuration', $Configuration, '--no-build', '--',
    '100000', $economyAudit) -Label 'Regenerate P22 economy audit'
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('run', '--project',
    (Join-Path $repositoryRoot 'tools\P22Audit\P22Audit.csproj'), '--configuration', $Configuration, '--no-build', '--',
    '100', $combatAudit) -Label 'Regenerate P22 combat audit'
if ((Get-Content -LiteralPath $economyAudit -Raw) -ne
    (Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs\v0.2\P22_ECONOMY_AUDIT.md') -Raw)) {
    throw 'Generated P22 economy audit differs from the committed release audit.'
}
if ((Get-Content -LiteralPath $combatAudit -Raw) -ne
    (Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs\v0.2\P22_COMBAT_AUDIT.md') -Raw)) {
    throw 'Generated P22 combat audit differs from the committed release audit.'
}
Invoke-NativeChecked -FilePath $godotBinary -Arguments @('--headless', '--path', $godotProject, '--editor', '--quit') -Label 'Godot import check' -RejectGodotErrors
Invoke-NativeChecked -FilePath $godotBinary -Arguments @('--headless', '--path', $godotProject, '--quit-after', '10') -Label 'Godot startup check' -RejectGodotErrors

Write-Host '[verify] PASS: assets, restore, build, tests, audits, Godot import and startup all succeeded.'
if ($Launch) {
    $guiGodot = $godotBinary -replace '_console\.exe$', '.exe'
    if (-not (Test-Path -LiteralPath $guiGodot)) {
        $guiGodot = $godotBinary
    }

    Write-Host '[verify] Launching the P1 client.'
    Start-Process -FilePath $guiGodot -ArgumentList @('--path', $godotProject)
}
