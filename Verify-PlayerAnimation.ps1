param([string]$UnityPath)
$ErrorActionPreference='Stop'
if(-not $UnityPath){$UnityPath=Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'}
$runtime=Join-Path (Split-Path $UnityPath -Parent) 'Data'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$out=Join-Path $PSScriptRoot 'TestResults'
New-Item -ItemType Directory -Force $out | Out-Null
$exe=Join-Path $out 'PlayerAnimationTests.exe'
$compile=@('-nologo','-noconfig','-nostdlib+','-target:exe',('-out:'+$exe))
foreach($assembly in @('mscorlib.dll','System.dll','System.Core.dll')){$compile+='-r:'+(Join-Path $framework $assembly)}
$compile+=(Join-Path $PSScriptRoot 'Assets\Scripts\PlayerAnimationSelection.cs')
$compile+=(Join-Path $PSScriptRoot 'Tests\PlayerAnimationTests.cs')
& (Join-Path $runtime 'NetCoreRuntime\dotnet.exe') (Join-Path $runtime 'DotNetSdkRoslyn\csc.dll') @compile
if($LASTEXITCODE -ne 0){throw 'Player animation test compilation failed.'}
& $exe | Tee-Object -FilePath (Join-Path $out 'player-animation-results.txt')
if($LASTEXITCODE -ne 0){throw 'Player animation tests failed.'}
