[CmdletBinding()]
param([string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$pattern = '(?i)(?<![a-z0-9])p(?:0|[1-9][0-9]?)(?![0-9])|(?-i:P)(?:0|[1-9][0-9]?)(?![0-9])|[pP][xX]{2}'
$textExtensions = '.cs','.csproj','.sln','.md','.txt','.json','.ps1','.py','.yml','.yaml','.toml','.cfg','.godot','.tscn','.tres','.uid','.import'
$paths = @(& git -C $RepositoryRoot -c core.quotepath=false ls-files --cached --others --exclude-standard | Sort-Object -Unique)
if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate repository files.' }
$checked = 0
foreach ($relative in $paths) {
    $path = Join-Path $RepositoryRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    if ($relative -match $pattern) { throw "Retired numeric development name in path: $relative" }
    if ([IO.Path]::GetExtension($path) -notin $textExtensions -and $relative -notin '.gitattributes','.gitignore') { continue }
    $content = [IO.File]::ReadAllText($path)
    if ($content -match $pattern) { throw "Retired numeric development name in text: $relative" }
    $checked++
}
Write-Host "[domain-names] PASS: $checked text files and all active repository paths use domain names."
