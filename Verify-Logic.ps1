param([string]$UnityPath)
$ErrorActionPreference='Stop'
if (-not $UnityPath) {
    $preferred=Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'
    if (Test-Path -LiteralPath $preferred) {$UnityPath=$preferred} else {
        $editors=Get-ChildItem -Path "$env:ProgramFiles\Unity\Hub\Editor\*\Editor\Unity.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending
        if (-not $editors) {throw 'A Unity 6 installation is required for its C# compiler.'}
        $UnityPath=$editors[0].FullName
    }
}
$runtimeRoot=Join-Path (Split-Path $UnityPath -Parent) 'Data'
$frameworkRoot=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$testWork=Join-Path $PSScriptRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $testWork | Out-Null
$testExe=Join-Path $testWork 'Tower.HeadlessTests.exe'
$testArgs=@('-nologo','-noconfig','-nostdlib+','-define:HEADLESS','-target:exe',('-out:'+$testExe))
foreach($assembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll')) {$testArgs+='-r:'+(Join-Path $frameworkRoot $assembly)}
foreach($file in @('GameData.cs','TowerGame.cs','BossCombat.cs','SaveStore.cs','LobbyCards.cs','AutoRun.cs','LobbySmoke.cs','BossPatternSystem.cs','ArakusPatternTiming.cs','GameServices.cs')) {$testArgs+=(Join-Path $PSScriptRoot ('Assets\Scripts\'+$file))}
$testArgs+=(Join-Path $PSScriptRoot 'Tests\UnityShim.cs');$testArgs+=(Join-Path $PSScriptRoot 'Tests\HeadlessTests.cs')
& (Join-Path $runtimeRoot 'NetCoreRuntime\dotnet.exe') (Join-Path $runtimeRoot 'DotNetSdkRoslyn\csc.dll') @testArgs
if($LASTEXITCODE -ne 0){throw 'Headless test compilation failed.'}
Push-Location $testWork
try { & $testExe | Tee-Object -FilePath (Join-Path $testWork 'results.txt');if($LASTEXITCODE -ne 0){throw 'Headless tests failed.'} } finally {Pop-Location}
