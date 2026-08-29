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
. (Join-Path $PSScriptRoot 'native-tools.ps1')
$dotnetBinary = Resolve-DotnetBinary
Set-DotnetEnvironment -DotnetBinary $dotnetBinary
$GodotPath = Resolve-GodotBinary -RequestedPath $GodotPath

& (Join-Path $repositoryRoot 'scripts\verify_p21_assets.ps1') -RepositoryRoot $repositoryRoot
Invoke-NativeChecked -FilePath $GodotPath -Arguments @('--headless', '--path', $projectPath, '--editor', '--quit') `
    -Label 'Godot asset import before release export' -RejectGodotErrors

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

Invoke-NativeChecked -FilePath $GodotPath -Arguments @('--headless', '--path', $projectPath, '--export-release',
    'Windows Portable x64', (Join-Path $outputDirectory 'GameForWork.exe')) `
    -Label 'Godot Windows release export' -RejectGodotErrors
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
$archive = Get-Item -LiteralPath $archivePath
if ($archive.Length -lt 50MB) { throw "Portable ZIP is unexpectedly small: $($archive.Length) bytes." }
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
Write-Host "[package] $archivePath"
Write-Host "[package] bytes=$($archive.Length) sha256=$archiveHash"
