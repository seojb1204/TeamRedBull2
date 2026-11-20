$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$root=Join-Path ([IO.Path]::GetTempPath()) ('tower-package-'+[guid]::NewGuid())
New-Item -ItemType Directory $root | Out-Null
$build=Join-Path $root 'Windows';New-Item -ItemType Directory $build | Out-Null
$required=@('ReversalTower.exe','UnityPlayer.dll','dstorage.dll','dstoragecore.dll','MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll','ReversalTower_Data/globalgamemanagers')
$count=0
try {
 foreach($name in $required){$p=Join-Path $build $name;New-Item -ItemType Directory (Split-Path $p -Parent) -Force | Out-Null;[IO.File]::WriteAllText($p,('fixture:'+ $name))}
 foreach($name in $required){
  $p=Join-Path $build $name;$saved=[IO.File]::ReadAllBytes($p);Remove-Item -LiteralPath $p
  $failed=$false;try{& (Join-Path $repo 'Get-WindowsReleaseFiles.ps1') -WindowsDir $build | Out-Null}catch{$failed=$true}
  if(!$failed){throw ('Missing dependency accepted: '+$name)};$count++
  [IO.File]::WriteAllBytes($p,[byte[]]@());$failed=$false
  try{& (Join-Path $repo 'Get-WindowsReleaseFiles.ps1') -WindowsDir $build | Out-Null}catch{$failed=$true}
  if(!$failed){throw ('Empty dependency accepted: '+$name)};$count++;[IO.File]::WriteAllBytes($p,$saved)
 }
 [IO.File]::WriteAllText((Join-Path $build 'unrelated-debug.txt'),'exclude')
 $files=@(& (Join-Path $repo 'Get-WindowsReleaseFiles.ps1') -WindowsDir $build)
 $zip=Join-Path $root 'release.zip';Compress-Archive -LiteralPath $files -DestinationPath $zip
 $unpack=Join-Path $root 'Extracted';Expand-Archive -LiteralPath $zip -DestinationPath $unpack
 foreach($name in $required){if((Get-FileHash -LiteralPath (Join-Path $build $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $unpack $name)).Hash){throw ('ZIP mismatch: '+$name)};$count++}
 if(Test-Path -LiteralPath (Join-Path $unpack 'unrelated-debug.txt')){throw 'Unexpected file packaged'};$count++
 Write-Output ('WINDOWS_PACKAGE_PASS '+$count+' checks; synthetic files, no Unity execution')
} finally {
 $resolved=[IO.Path]::GetFullPath($root);$tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
 if(!$resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -or !(Split-Path $resolved -Leaf).StartsWith('tower-package-')){throw 'Unsafe cleanup path'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
