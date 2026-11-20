param([Parameter(Mandatory=$true)][string]$WindowsDir)
$ErrorActionPreference='Stop'
# Unity 6000.4 Windows x64 Mono player dependencies. Fail before making an incomplete ZIP.
$requiredFiles=@('ReversalTower.exe','UnityPlayer.dll','dstorage.dll','dstoragecore.dll','MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll','ReversalTower_Data/globalgamemanagers')
foreach($relative in $requiredFiles){
 $path=Join-Path $WindowsDir $relative
 if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw ('Missing player file: '+$relative)}
 if((Get-Item -LiteralPath $path).Length -eq 0){throw ('Empty player file: '+$relative)}
}
$includeNames=@('ReversalTower.exe','ReversalTower_Data','UnityPlayer.dll','MonoBleedingEdge','dstorage.dll','dstoragecore.dll','UnityCrashHandler64.exe','WinPixEventRuntime.dll','GameAssembly.dll')
foreach($name in $includeNames){$path=Join-Path $WindowsDir $name;if(Test-Path -LiteralPath $path){$path}}
