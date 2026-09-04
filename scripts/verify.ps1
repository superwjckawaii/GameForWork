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
Initialize-NativeConsoleEncoding

$dotnetBinary = Resolve-DotnetBinary
Set-DotnetEnvironment -DotnetBinary $dotnetBinary
$godotBinary = Resolve-GodotBinary
Write-Host "[verify] configuration=$Configuration"
Write-Host "[verify] dotnet=$dotnetBinary"
Write-Host "[verify] godot=$godotBinary"

& (Join-Path $repositoryRoot 'scripts\verify_release_hygiene.ps1') -RepositoryRoot $repositoryRoot
& (Join-Path $repositoryRoot 'scripts\verify_p21_assets.ps1') -RepositoryRoot $repositoryRoot
& (Join-Path $repositoryRoot 'scripts\verify_p31_assets.ps1') -RepositoryRoot $repositoryRoot

Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('restore', $solutionPath) -Label 'Restore solution'
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('build', $solutionPath, '--no-restore', '--configuration', $Configuration) -Label 'Build solution'
Invoke-NativeChecked -FilePath $godotBinary -Arguments @('--headless', '--path', $godotProject, '--editor', '--quit') -Label 'Godot import check' -RejectGodotErrors
if ($Configuration -ne 'Debug') {
    Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @(
        'build', (Join-Path $godotProject 'GameForWork.csproj'), '--no-restore', '--configuration', 'Debug'
    ) -Label 'Build Godot startup assembly'
}
Invoke-NativeChecked -FilePath $godotBinary -Arguments @('--headless', '--path', $godotProject, '--quit-after', '10') -Label 'Godot startup check' -RejectGodotErrors

if ($Launch) {
    $guiGodot = $godotBinary -replace '_console\.exe$', '.exe'
    if (-not (Test-Path -LiteralPath $guiGodot)) {
        $guiGodot = $godotBinary
    }

    Write-Host '[verify] Build and startup checks passed; launching the client while the remaining tests continue.'
    Start-Process -FilePath $guiGodot -ArgumentList @('--path', $godotProject)
}

Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('test', (Join-Path $repositoryRoot 'src\Game.Tests\Game.Tests.csproj'), '--no-build', '--configuration', $Configuration) -Label 'Run unit tests'
$auditRoot = Join-Path $repositoryRoot 'artifacts\release-gate'
New-Item -ItemType Directory -Path $auditRoot -Force | Out-Null
$economyAudit = Join-Path $auditRoot 'economy.md'
$combatAudit = Join-Path $auditRoot 'combat.md'
$buildAudit = Join-Path $auditRoot 'p30-builds.md'
$equipmentAudit = Join-Path $auditRoot 'p32-equipment.md'
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('run', '--project',
    (Join-Path $repositoryRoot 'tools\P20Audit\P20Audit.csproj'), '--configuration', $Configuration, '--no-build', '--',
    '100000', $economyAudit) -Label 'Regenerate P29 economy audit'
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('run', '--project',
    (Join-Path $repositoryRoot 'tools\P22Audit\P22Audit.csproj'), '--configuration', $Configuration, '--no-build', '--',
    '100', $combatAudit) -Label 'Regenerate P22 combat audit'
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('run', '--project',
    (Join-Path $repositoryRoot 'tools\P30Audit\P30Audit.csproj'), '--configuration', $Configuration, '--no-build', '--',
    $buildAudit) -Label 'Regenerate P30 build audit'
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('run', '--project',
    (Join-Path $repositoryRoot 'tools\P32Audit\P32Audit.csproj'), '--configuration', $Configuration, '--no-build', '--',
    $equipmentAudit) -Label 'Regenerate P32 equipment audit'
function Read-NormalizedText([string]$Path) {
    return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8).Replace("`r`n", "`n")
}
if ((Read-NormalizedText $economyAudit) -ne
    (Read-NormalizedText (Join-Path $repositoryRoot 'docs\v0.4\P29_ECONOMY_AUDIT.md'))) {
    throw 'Generated P29 economy audit differs from the committed release audit.'
}
if ((Read-NormalizedText $combatAudit) -ne
    (Read-NormalizedText (Join-Path $repositoryRoot 'docs\v0.2\P22_COMBAT_AUDIT.md'))) {
    throw 'Generated P22 combat audit differs from the committed release audit.'
}
if ((Read-NormalizedText $buildAudit) -ne
    (Read-NormalizedText (Join-Path $repositoryRoot 'docs\v0.4\P30_BUILD_AUDIT.md'))) {
    throw 'Generated P30 build audit differs from the committed release audit.'
}
if ((Read-NormalizedText $equipmentAudit) -ne
    (Read-NormalizedText (Join-Path $repositoryRoot 'docs\v0.5\P32_EQUIPMENT_AUTOMATED_AUDIT.md'))) {
    throw 'Generated P32 equipment audit differs from the committed release audit.'
}
Write-Host '[verify] PASS: assets, restore, build, tests, audits, Godot import and startup all succeeded.'
