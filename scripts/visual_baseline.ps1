[CmdletBinding()]
param([switch]$Smoke, [switch]$Exhaustive, [ValidateRange(-1,41)][int]$CaseIndex = -1)
$ErrorActionPreference = 'Stop'
if ($Smoke -and $Exhaustive) { throw 'Smoke and Exhaustive are mutually exclusive.' }
$caseCount = if ($Exhaustive) { 42 } elseif ($Smoke) { 14 } else { 20 }
if ($CaseIndex -ge $caseCount) { throw "CaseIndex must be below $caseCount for this protocol." }
$repositoryRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
. (Join-Path $PSScriptRoot 'native-tools.ps1')
$dotnetBinary = Resolve-DotnetBinary
Set-DotnetEnvironment -DotnetBinary $dotnetBinary
$godotBinary = Resolve-GodotBinary
if (Get-Process -Name '*Godot*' -ErrorAction SilentlyContinue) { throw 'Close the running game or Godot editor before baseline measurement.' }
$runRoot = Join-Path $repositoryRoot ('artifacts\visual-baseline-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot | Out-Null
foreach ($name in @('Baseline.cs','Baseline.tscn','project.godot','VisualBaseline.csproj')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "tools\VisualBaseline\$name") -Destination $runRoot
}
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src\Game.Godot\assets') -Destination (Join-Path $runRoot 'assets') -Recurse
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('build', (Join-Path $runRoot 'VisualBaseline.csproj'), '-c', 'Debug') -Label 'Build isolated visual baseline'
Invoke-NativeChecked -FilePath $godotBinary -Arguments @('--headless','--path',$runRoot,'--editor','--quit') -Label 'Import baseline assets' -RejectGodotErrors
$arguments = @('--path', ('"' + $runRoot + '"'), '--', '--release-stability-visible')
if ($Smoke) { $arguments += '--baseline-smoke' }
if ($Exhaustive) { $arguments += '--baseline-exhaustive' }
if ($CaseIndex -ge 0) { $arguments += "--baseline-case=$CaseIndex" }
Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber | ConvertTo-Json | Out-File (Join-Path $runRoot 'host.json') -Encoding utf8
& powercfg /getactivescheme | Out-File (Join-Path $runRoot 'power.txt') -Encoding utf8
Get-CimInstance Win32_Battery | Select-Object BatteryStatus,EstimatedChargeRemaining | ConvertTo-Json | Out-File (Join-Path $runRoot 'battery.json') -Encoding utf8
$process = Start-Process -FilePath $godotBinary -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot 'run.log') -RedirectStandardError (Join-Path $runRoot 'errors.log')
Write-Host "[visual-baseline] PID=$($process.Id); output=$runRoot"
Write-Host '[visual-baseline] Full client with isolated saves. Default: 14 matrix samples plus six long samples (~21 minutes). Exhaustive: 42 long samples (~105 minutes). Smoke is tooling validation only.'
