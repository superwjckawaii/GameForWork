[CmdletBinding()]
param(
    [string]$GodotPath = $env:GODOT_BIN,
    [string]$Version = '0.2.0'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$projectPath = Join-Path $repositoryRoot 'src\Game.Godot'
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$outputDirectory = Join-Path $artifactsRoot "GameForWork-v$Version"
$archivePath = Join-Path $artifactsRoot "GameForWork-v$Version-win-x64.zip"

if (-not $GodotPath) {
    $GodotPath = 'D:\OtherTools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
}
if (-not (Test-Path -LiteralPath $GodotPath)) { throw "Godot executable not found: $GodotPath" }

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
if (Test-Path -LiteralPath $outputDirectory) {
    $resolvedOutput = (Resolve-Path -LiteralPath $outputDirectory).Path
    if (-not $resolvedOutput.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear path outside artifacts: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$exportOutput = & $GodotPath --headless --path $projectPath --export-release 'Windows Portable x64' (Join-Path $outputDirectory 'GameForWork.exe') 2>&1
$exportExitCode = $LASTEXITCODE
$exportOutput | ForEach-Object { Write-Host $_ }
$exportText = $exportOutput | Out-String
if ($exportExitCode -ne 0 -or $exportText -match '(?m)^ERROR:') {
    throw "Godot release export failed or emitted an error (exit code $exportExitCode)."
}
$executable = Join-Path $outputDirectory 'GameForWork.exe'
if (-not (Test-Path -LiteralPath $executable) -or (Get-Item -LiteralPath $executable).Length -lt 1MB) {
    throw 'Godot release export did not create a valid executable.'
}

$readme = Join-Path $repositoryRoot 'README.md'
if (Test-Path -LiteralPath $readme) { Copy-Item -LiteralPath $readme -Destination $outputDirectory }
$releaseNotes = Join-Path $repositoryRoot 'docs\v0.2\V0_2_RELEASE_NOTES.md'
if (Test-Path -LiteralPath $releaseNotes) {
    Copy-Item -LiteralPath $releaseNotes -Destination (Join-Path $outputDirectory 'VERSION.md')
}
Compress-Archive -Path (Join-Path $outputDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal
Write-Host "[package] $archivePath"
