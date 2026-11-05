param()
$ErrorActionPreference='Stop'
$runtime='C:/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Data'
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$testRoot=Join-Path $PSScriptRoot TestResults
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$exe=Join-Path $testRoot Arakus.Animation.Tests.exe
$args=@('-nologo','-noconfig','-nostdlib+','-target:exe',('-out:'+$exe))
foreach($dll in @('mscorlib.dll','System.dll','System.Core.dll')){$args+='-r:'+(Join-Path $framework $dll)}
$args+=@((Join-Path $PSScriptRoot 'Assets/Scripts/ArakusAnimationSelection.cs'),(Join-Path $PSScriptRoot 'Tests/ArakusAnimationTests.cs'))
& "$runtime/NetCoreRuntime/dotnet.exe" "$runtime/DotNetSdkRoslyn/csc.dll" @args
if($LASTEXITCODE -ne 0){throw 'Arakus animation test compilation failed'}
& $exe | Tee-Object -FilePath (Join-Path $testRoot arakus-animation.txt)
if($LASTEXITCODE -ne 0){throw 'Arakus animation tests failed'}
