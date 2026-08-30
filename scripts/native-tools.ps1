$script:KnownGodotLocations = @(
    'D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe',
    'D:\OtherTools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
)
$script:KnownDotnetLocations = @(
    'D:\dotnet\dotnet.exe',
    'C:\Program Files\dotnet\dotnet.exe'
)

function Initialize-NativeConsoleEncoding {
    # Windows PowerShell 5.1 otherwise decodes UTF-8 output from modern .NET
    # tools with the active legacy code page, which turns Chinese into mojibake.
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try {
        [Console]::InputEncoding = $utf8
        [Console]::OutputEncoding = $utf8
    }
    catch {
        # A redirected host can reject Console encoding changes. OutputEncoding
        # below still keeps native-process pipes on UTF-8 in that environment.
    }
    $script:OutputEncoding = $utf8

    if ($env:OS -eq 'Windows_NT') {
        $changeCodePage = Join-Path $env:SystemRoot 'System32\chcp.com'
        if (Test-Path -LiteralPath $changeCodePage) {
            & $changeCodePage 65001 | Out-Null
        }
    }
}

function Resolve-DotnetBinary {
    param([string]$RequestedPath = $env:DOTNET_BIN)
    if ($RequestedPath -and (Test-Path -LiteralPath $RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }
    foreach ($candidate in $script:KnownDotnetLocations) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    $command = Get-Command 'dotnet' -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw '.NET 8 SDK executable was not found. Set DOTNET_BIN to the full path of dotnet.exe.'
}

function Resolve-GodotBinary {
    param([string]$RequestedPath = $env:GODOT_BIN)
    if ($RequestedPath -and (Test-Path -LiteralPath $RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }
    foreach ($candidate in $script:KnownGodotLocations) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    foreach ($name in @('godot', 'godot4', 'Godot_v4.7.2-stable_mono_win64_console')) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) { return $command.Source }
    }
    throw 'Godot 4.7.2 Mono console executable was not found. Set GODOT_BIN to its full path.'
}

function Set-DotnetEnvironment {
    param([Parameter(Mandatory)] [string]$DotnetBinary)
    $dotnetRoot = Split-Path -Parent $DotnetBinary
    $env:DOTNET_ROOT = $dotnetRoot
    if (($env:PATH -split ';') -notcontains $dotnetRoot) { $env:PATH = "$dotnetRoot;$env:PATH" }
}

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [Parameter(Mandatory)] [string]$Label,
        [switch]$RejectGodotErrors
    )
    Write-Host "[gate] $Label"
    $captured = [System.Collections.Generic.List[string]]::new()
    & $FilePath @Arguments 2>&1 | ForEach-Object {
        $line = $_.ToString()
        $captured.Add($line)
        Write-Host $line
    }
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) { throw "$Label failed with exit code $exitCode." }
    if ($RejectGodotErrors) {
        $text = $captured -join [Environment]::NewLine
        if ($text -match '(?m)^(ERROR|SCRIPT ERROR):' -or
            $text -match '"level"\s*:\s*"(Error|Critical)"' -or
            $text -match '(?m)^Unhandled exception') {
            throw "$Label emitted a Godot or unhandled runtime error despite exit code 0."
        }
    }
}
