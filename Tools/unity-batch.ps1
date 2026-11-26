<#
  Headless Unity jobs for Null Record, each with a hard time limit.
  Needs the unpackaged Unity Hub running (see CLAUDE.md > Licensing) or the editor can't get a license.

  Usage (from the repo root, Windows PowerShell):
    powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 test       # build scenes, lighting shots, WAVs, EditMode + PlayMode tests
    powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 web        # build scenes + WebGL player into Builds/WebGL
    powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 scenes     # regenerate MainMenu + Greybox_Intake only
  Logs and test results go to Logs/batch/ (git-ignored). Screenshots go to Screenshots/.
#>
param(
  [Parameter(Mandatory = $true)][ValidateSet('test', 'web', 'scenes')] [string]$Job,
  [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
)

$root = (Resolve-Path "$PSScriptRoot\..").Path
$out = Join-Path $root "Logs\batch"
New-Item -ItemType Directory -Force $out | Out-Null
$proj = '"' + $root + '"'

function Run([string[]]$extra, [string]$name, [int]$limitSec) {
  $argList = @('-batchmode', '-projectPath', $proj, '-logFile', "`"$out\$name.log`"") + $extra
  $p = Start-Process $Unity -ArgumentList $argList -PassThru
  if (-not $p.WaitForExit($limitSec * 1000)) { $p | Stop-Process -Force; $code = 'KILLED (time limit)' } else { $code = $p.ExitCode }
  "{0,-8} {1}" -f "${name}:", $code
}

function Summary([string]$name) {
  $f = "$out\$name.xml"
  if (Test-Path $f) {
    $m = Select-String -Path $f -Pattern 'testcasecount="\d+" result="\w+"[^>]*passed="\d+" failed="\d+"' | Select-Object -First 1
    if ($m) { "  $name results: " + $m.Matches[0].Value }
  }
}

Run @('-executeMethod', 'Game.EditorTools.GreyboxLevelBuilder.BuildAll', '-quit') 'scenes' 420

if ($Job -eq 'test') {
  Remove-Item "$out\edit.xml", "$out\play.xml" -ErrorAction SilentlyContinue
  Run @('-executeMethod', 'Game.EditorTools.GreyboxLevelBuilder.CaptureLightingShots', '-quit') 'shots' 300
  Run @('-executeMethod', 'Game.EditorTools.SoundExport.ExportAll', '-quit') 'wav' 300
  Run @('-runTests', '-testPlatform', 'EditMode', '-testResults', "`"$out\edit.xml`"") 'edit' 420
  Run @('-runTests', '-testPlatform', 'PlayMode', '-testResults', "`"$out\play.xml`"") 'play' 600
  Summary 'edit'
  Summary 'play'
}

if ($Job -eq 'web') {
  Run @('-buildTarget', 'WebGL', '-executeMethod', 'Game.EditorTools.Builds.WebGL', '-quit') 'webgl' 3000
  Select-String -Path "$out\webgl.log" -Pattern '\[Null Record\] WebGL build' | Select-Object -First 1 | ForEach-Object { '  ' + $_.Line.Trim() }
}
