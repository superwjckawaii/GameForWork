[CmdletBinding()]
param([switch]$Smoke, [switch]$Exhaustive, [ValidateRange(-1,41)][int]$CaseIndex = -1, [switch]$Harbor, [switch]$MiniRepeat)
$ErrorActionPreference = 'Stop'
if ($Smoke -and $Exhaustive) { throw 'Smoke and Exhaustive are mutually exclusive.' }
if ($MiniRepeat -and ($Smoke -or $Exhaustive -or $Harbor -or $CaseIndex -ge 0)) { throw 'MiniRepeat is a standalone three-round window regression.' }
if ($Harbor -and ($Smoke -or $Exhaustive -or $CaseIndex -ge 0)) { throw 'Harbor replay does not accept performance protocol options.' }
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
    $sourceName = if ($Harbor -and $name -eq 'Baseline.cs') { 'HarborReplay.cs' } else { $name }
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "tools\VisualBaseline\$sourceName") -Destination (Join-Path $runRoot $name)
}
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src\Game.Godot\assets') -Destination (Join-Path $runRoot 'assets') -Recurse
Invoke-NativeChecked -FilePath $dotnetBinary -Arguments @('build', (Join-Path $runRoot 'VisualBaseline.csproj'), '-c', 'Debug') -Label 'Build isolated visual baseline'
Invoke-NativeChecked -FilePath $godotBinary -Arguments @('--headless','--path',$runRoot,'--editor','--quit') -Label 'Import baseline assets' -RejectGodotErrors
$arguments = @('--path', ('"' + $runRoot + '"'), '--', '--release-stability-visible')
if ($Smoke) { $arguments += '--baseline-smoke' }
if ($Exhaustive) { $arguments += '--baseline-exhaustive' }
if ($MiniRepeat) { $arguments += '--baseline-mini-repeat' }
if ($CaseIndex -ge 0) { $arguments += "--baseline-case=$CaseIndex" }
Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber | ConvertTo-Json | Out-File (Join-Path $runRoot 'host.json') -Encoding utf8
& powercfg /getactivescheme | Out-File (Join-Path $runRoot 'power.txt') -Encoding utf8
Get-CimInstance Win32_Battery | Select-Object BatteryStatus,EstimatedChargeRemaining | ConvertTo-Json | Out-File (Join-Path $runRoot 'battery.json') -Encoding utf8
$process = Start-Process -FilePath $godotBinary -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot 'run.log') -RedirectStandardError (Join-Path $runRoot 'errors.log')
Write-Host "[visual-baseline] PID=$($process.Id); output=$runRoot"
if ($Harbor) { Write-Host '[harbor-replay] Isolated production panel and actual combat with a diagnostic build; not balance/performance acceptance.'; return }
if ($MiniRepeat) { Write-Host '[visual-baseline] Three consecutive mini-window rounds: 30s warmup + 120s capture each. Regression only, not full-matrix acceptance.'; return }
Write-Host '[visual-baseline] Full client with isolated saves. Default: 14 matrix samples plus six long samples (~21 minutes). Exhaustive: 42 long samples (~105 minutes). Smoke is tooling validation only.'
