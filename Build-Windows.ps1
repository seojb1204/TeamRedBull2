param([string]$UnityPath)
$ErrorActionPreference = 'Stop'
if (-not $UnityPath) {
    $preferred=Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'
    if (Test-Path -LiteralPath $preferred) {$UnityPath=$preferred}
}
if (-not $UnityPath) {
    $editors = Get-ChildItem -Path "$env:ProgramFiles\Unity\Hub\Editor\*\Editor\Unity.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending
    if (-not $editors) { throw 'Unity 6 Editor with Windows Build Support is required.' }
    $UnityPath = $editors[0].FullName
}
$buildLog = Join-Path $PSScriptRoot 'build.log'
$buildArgs = @('-batchmode','-nographics','-projectPath',('"'+$PSScriptRoot+'"'),'-buildTarget','Win64','-executeMethod','BuildProject.Build','-logFile',('"'+$buildLog+'"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $buildArgs -PassThru -Wait -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Unity build failed. See $buildLog" }
$game = Join-Path (Split-Path $PSScriptRoot -Parent) 'Windows\ReversalTower.exe'
if (-not (Test-Path -LiteralPath $game)) { throw "Build output was not found. See $buildLog" }
Write-Host "Built game: $game"
