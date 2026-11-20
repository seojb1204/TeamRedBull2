param([Parameter(Mandatory=$true)][string]$DotNetPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'TestResults/PresentationNet8'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$sdkRoot=Split-Path $DotNetPath -Parent
$sdk=Get-ChildItem (Join-Path $sdkRoot 'sdk') -Directory | Where-Object Name -Like '8.*' | Sort-Object Name -Descending | Select-Object -First 1
$pack=Get-ChildItem (Join-Path $sdkRoot 'packs/Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '8.*' | Sort-Object Name -Descending | Select-Object -First 1
if(!$sdk -or !$pack){throw '.NET 8 SDK and reference pack required'}
$base=@('TutorialSession','TowerGame.Tutorial','GameData','TowerGame','BossCombat','SaveStore','LobbyCards','AutoRun','LobbySmoke','BossPatternSystem','GameServices','ArakusPatternTiming','ArakusAnimationSelection','PhanesPose')
$suites=@('PresentationReleaseTests','ProfileArchiveTests','UltimateSystemTests','AdaptationColdStartTests','TutorialPersistenceTests','TutorialDdaIntegrationTests','SelectiveIntegrationTests','DdaMonitorTests','DdaFrequencyTests','DdaRobustnessTests','PhanesTests','PlaytestAgentTests')
$results=@()
foreach($suite in $suites){
 $dll=Join-Path $out ($suite+'.dll')
 $argsList=@('-nologo','-noconfig','-nostdlib+','-define:HEADLESS','-target:exe',('-out:'+$dll),('-main:'+$suite))
 foreach($a in Get-ChildItem (Join-Path $pack.FullName 'ref/net8.0') -Filter '*.dll'){$argsList+='-r:'+$a.FullName}
 foreach($f in $base){$argsList+=(Join-Path $root ('Assets/Scripts/'+$f+'.cs'))}
 $argsList+=(Join-Path $PSScriptRoot 'UnityShim.cs');$argsList+=(Join-Path $PSScriptRoot ($suite+'.cs'))
 $log=Join-Path $out ($suite+'.log')
 & $DotNetPath (Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll') @argsList *> $log
 $code=$LASTEXITCODE
 if($code -eq 0){
  '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content (Join-Path $out ($suite+'.runtimeconfig.json')) -Encoding UTF8
  Push-Location $out;try{& $DotNetPath $dll *>> $log;$code=$LASTEXITCODE}finally{Pop-Location}
 }
 $results+=@{suite=$suite;exit=$code};Write-Output ($suite+' exit='+$code);Get-Content $log -Tail 2
}
$results|ConvertTo-Json|Set-Content (Join-Path $out 'summary.json') -Encoding UTF8
if(@($results|Where-Object {$_.exit -ne 0}).Count){exit 1}
