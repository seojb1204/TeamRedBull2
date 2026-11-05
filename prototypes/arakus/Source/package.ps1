$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputs=[IO.Directory]::GetParent($root).FullName
foreach($full in @($false,$true)){
 $name=if($full){'Arakus.zip'}else{'Arakus_Unity.zip'}
 $file=[IO.File]::Open((Join-Path $outputs $name),[IO.FileMode]::Create)
 $archive=[IO.Compression.ZipArchive]::new($file,[IO.Compression.ZipArchiveMode]::Create)
 $folders=@('Assets','Packages','ProjectSettings');if($full){$folders+=@('Source','Review')}
 $files=@();foreach($folder in $folders){$files+=Get-ChildItem (Join-Path $root $folder) -File -Recurse}
 $files+=Get-ChildItem $root -File
 foreach($f in $files){$rel=[IO.Path]::GetRelativePath($root,$f.FullName).Replace('\','/');$entry=$archive.CreateEntry("Arakus/$rel",[IO.Compression.CompressionLevel]::Optimal);$s=$entry.Open();$input=[IO.File]::OpenRead($f.FullName);$input.CopyTo($s);$input.Dispose();$s.Dispose()}
 $archive.Dispose();$file.Dispose()
 $check=[IO.Compression.ZipFile]::OpenRead((Join-Path $outputs $name));if($check.Entries.FullName -match '/Library/|supabase.json|/Temp/'){throw 'Unexpected archive content'};$count=$check.Entries.Count;$frames=@($check.Entries|Where-Object {$_.FullName -match '/Frames/.*\.png$'});if($frames.Count -ne 179){throw 'Archive missing frames'};$check.Dispose()
 Write-Output "$name verified: $count entries, 179 frame PNGs"
}
