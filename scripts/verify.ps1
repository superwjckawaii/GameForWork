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
$knownGodot = 'D:\OtherTools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'

function Resolve-GodotBinary {
    if ($env:GODOT_BIN -and (Test-Path -LiteralPath $env:GODOT_BIN)) {
        return (Resolve-Path -LiteralPath $env:GODOT_BIN).Path
    }

    if (Test-Path -LiteralPath $knownGodot) {
        return $knownGodot
    }

    foreach ($name in @('godot', 'godot4', 'Godot_v4.7.2-stable_mono_win64_console')) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }
    }

    throw 'Godot 4.7.2 Mono console executable was not found. Set GODOT_BIN to its full path.'
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [Parameter(Mandatory)] [string]$Label
    )

    Write-Host "[verify] $Label"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE."
    }
}

$godotBinary = Resolve-GodotBinary
Write-Host "[verify] configuration=$Configuration"
Write-Host "[verify] godot=$godotBinary"

Invoke-Checked -FilePath 'dotnet' -Arguments @('restore', $solutionPath) -Label 'Restore solution'
Invoke-Checked -FilePath 'dotnet' -Arguments @('build', $solutionPath, '--no-restore', '--configuration', $Configuration) -Label 'Build solution'
Invoke-Checked -FilePath 'dotnet' -Arguments @('test', (Join-Path $repositoryRoot 'src\Game.Tests\Game.Tests.csproj'), '--no-build', '--configuration', $Configuration) -Label 'Run unit tests'
Invoke-Checked -FilePath $godotBinary -Arguments @('--headless', '--path', $godotProject, '--editor', '--quit') -Label 'Godot import check'
Invoke-Checked -FilePath $godotBinary -Arguments @('--headless', '--path', $godotProject, '--quit-after', '10') -Label 'Godot startup check'

Write-Host '[verify] PASS: restore, build, tests, Godot import and startup all succeeded.'
if ($Launch) {
    $guiGodot = $godotBinary -replace '_console\.exe$', '.exe'
    if (-not (Test-Path -LiteralPath $guiGodot)) {
        $guiGodot = $godotBinary
    }

    Write-Host '[verify] Launching the P0 client.'
    Start-Process -FilePath $guiGodot -ArgumentList @('--path', $godotProject)
}
