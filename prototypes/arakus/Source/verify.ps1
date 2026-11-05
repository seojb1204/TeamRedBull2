$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$m=Get-Content "$root/Assets/Arakus/clip_manifest.json" -Raw | ConvertFrom-Json
$refs=@(Get-ChildItem "$PSHOME/ref/*.dll" | ForEach-Object FullName)+@([System.Drawing.Bitmap].Assembly.Location)+@(Get-ChildItem "$PSHOME/System.Private.Windows*.dll" | ForEach-Object FullName)
Add-Type -ReferencedAssemblies $refs -TypeDefinition @'
using System;using System.Drawing;using System.Collections.Generic;using System.IO;
public static class PixelCheck {
 public static HashSet<int> CharacterColors=new HashSet<int>();
 public static int[] Check(string file,string sheet,int index,int size,bool character){using(var b=new Bitmap(file))using(var s=new Bitmap(sheet)){
  if(b.Width!=size||b.Height!=size)throw new Exception(file+" size");int count=0,minX=size,minY=size,maxX=-1,maxY=-1;var colors=new HashSet<int>();
  for(int y=0;y<size;y++)for(int x=0;x<size;x++){var p=b.GetPixel(x,y);if(p.A!=0&&p.A!=255)throw new Exception(file+" alpha");if(p.ToArgb()!=s.GetPixel(index*size+x,y).ToArgb())throw new Exception(file+" sheet differs");if(p.A==0)continue;count++;colors.Add(p.ToArgb());if(character)CharacterColors.Add(p.ToArgb());minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
  if(count>0&&(minX<2||minY<2||maxX>=size-2||maxY>=size-2))throw new Exception(file+" clipping "+minX+","+minY+","+maxX+","+maxY);
  if(character&&(count<100||colors.Count>32))throw new Exception(file+" character content/colors");return new[]{count,colors.Count,minX,minY,maxX,maxY};
 }}
}
'@
$records=@();$total=0
foreach($d in $m.clips){
 if($d.ms.Count -ne $d.count -or ($d.ms | Where-Object {$_ -le 0})){throw "Invalid timing $($d.id)"}
 $sheet="$root/Assets/Arakus/$($d.id).png";$b=[Drawing.Bitmap]::new($sheet);if($b.Width -ne $d.size*$d.count -or $b.Height -ne $d.size){throw "Sheet size $($d.id)"};$b.Dispose()
 $stats=@();for($i=0;$i -lt $d.count;$i++){$f="$root/Assets/Arakus/Frames/$($d.id)_$($i.ToString('00')).png";$a=[PixelCheck]::Check($f,$sheet,$i,$d.size,($d.group -eq '캐릭터'));$stats+=,@{index=$i;opaquePixels=$a[0];colors=$a[1];bounds=@($a[2..5])};$total++}
 $records+=@{id=$d.id;frames=$d.count;durationMs=($d.ms|Measure-Object -Sum).Sum;framesVerified=$stats}
}
if([PixelCheck]::CharacterColors.Count -gt 32){throw 'Global character palette exceeds 32'}
foreach($i in 0..1){$n=$i.ToString('00');$a=(Get-FileHash "$root/Assets/Arakus/Frames/ShieldThrowing_DashAttack_$n.png").Hash;$b=(Get-FileHash "$root/Assets/Arakus/Frames/Shield_Throwing_Fly_Attack_$n.png").Hash;if($a -ne $b){throw 'Shared throw frames differ'}}
$report=@{passed=$true;clips=$m.clips.Count;totalFrames=$total;characterColors=[PixelCheck]::CharacterColors.Count;binaryAlpha=$true;sheetPixelsMatchFrames=$true;noCanvasClipping=$true;sharedThrowOpeningIdentical=$true;checks=$records;notVerified=@('Manual full game play','Combat integration','Build executable','Measured 3D camera angle')}
$report | ConvertTo-Json -Depth 9 | Set-Content "$root/Validation.json"
Write-Output "ARAKUS_PIXEL_CHECK_PASSED clips=$($m.clips.Count) frames=$total colors=$([PixelCheck]::CharacterColors.Count)"
