param([string]$UnityPath)
$ErrorActionPreference='Stop'
if(-not $UnityPath){$UnityPath=Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'}
$runtime=Join-Path (Split-Path $UnityPath -Parent) 'Data'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$out=Join-Path $PSScriptRoot 'TestResults'
New-Item -ItemType Directory -Force $out | Out-Null
$exe=Join-Path $out 'DdaHistoryTests.exe'
$compile=@('-nologo','-noconfig','-nostdlib+','-define:HEADLESS','-target:exe',('-out:'+$exe))
foreach($assembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll')){$compile+='-r:'+(Join-Path $framework $assembly)}
foreach($source in @('GameData','TowerGame','BossCombat','SaveStore','LobbyCards','AutoRun','LobbySmoke','BossPatternSystem','ArakusPatternTiming','GameServices','DdaDifficultyHistory','DdaDifficultyHistoryStore','DdaHistoryRecorder')){
    $compile+=(Join-Path $PSScriptRoot ('Assets\Scripts\'+$source+'.cs'))
}
$compile+=(Join-Path $PSScriptRoot 'Tests\UnityShim.cs')
$compile+=(Join-Path $PSScriptRoot 'Tests\DdaDifficultyHistoryTests.cs')
# Explicit list excludes CloudSync, real DB configuration, runtime bootstrap and GUI.
& (Join-Path $runtime 'NetCoreRuntime\dotnet.exe') (Join-Path $runtime 'DotNetSdkRoslyn\csc.dll') @compile
if($LASTEXITCODE -ne 0){throw 'DDA history test compilation failed.'}
& $exe | Tee-Object -FilePath (Join-Path $out 'dda-history-results.txt')
if($LASTEXITCODE -ne 0){throw 'DDA history tests failed.'}
