param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$refs=@(Get-ChildItem "$PSHOME/ref/*.dll" | ForEach-Object FullName)+@([System.Drawing.Bitmap].Assembly.Location)+@(Get-ChildItem "$PSHOME/System.Private.Windows*.dll" | ForEach-Object FullName)
Add-Type -ReferencedAssemblies $refs -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Linq;
public static class ArakusPixels {
 static Color Ink=Color.FromArgb(255,30,23,26);
 static bool Empty(Bitmap b,int x) {for(int y=0;y<b.Height;y++)if(b.GetPixel(x,y).A>0)return false;return true;}
 static Bitmap Clean(string file){
  var b=new Bitmap(file);int w=b.Width,h=b.Height;
  // Only edge-connected pale neutral background is removed. Preserve enclosed metal highlights.
  var seen=new bool[w*h];var q=new Queue<int>();
  Action<int,int> add=(x,y)=>{int k=y*w+x;if(seen[k])return;seen[k]=true;var p=b.GetPixel(x,y);int hi=Math.Max(p.R,Math.Max(p.G,p.B)),lo=Math.Min(p.R,Math.Min(p.G,p.B));if(p.A<200||(lo>215&&hi-lo<22)){q.Enqueue(k);}};
  for(int x=0;x<w;x++){add(x,0);add(x,h-1);}for(int y=0;y<h;y++){add(0,y);add(w-1,y);}
  while(q.Count>0){int k=q.Dequeue(),x=k%w,y=k/w;b.SetPixel(x,y,Color.Transparent);if(x>0)add(x-1,y);if(x+1<w)add(x+1,y);if(y>0)add(x,y-1);if(y+1<h)add(x,y+1);}
  for(int y=0;y<h;y++)for(int x=0;x<w;x++){var p=b.GetPixel(x,y);b.SetPixel(x,y,p.A>=200?Color.FromArgb(255,p.R,p.G,p.B):Color.Transparent);}
  return b;
 }
 public static void Extract(string file,string root,string id,int count,int targetHeight,int size,int baseline){
  using(var b=Clean(file)){
   int w=b.Width,h=b.Height;bool[] seen=new bool[w*h];var all=new List<List<int>>();
   for(int yy=0;yy<h;yy++)for(int xx=0;xx<w;xx++){int start=yy*w+xx;if(seen[start]||b.GetPixel(xx,yy).A==0)continue;var pixels=new List<int>();var q=new Queue<int>();q.Enqueue(start);seen[start]=true;while(q.Count>0){int k=q.Dequeue();pixels.Add(k);int x=k%w,y=k/w;foreach(int n in new[]{x>0?k-1:-1,x+1<w?k+1:-1,y>0?k-w:-1,y+1<h?k+w:-1})if(n>=0&&!seen[n]){seen[n]=true;if(b.GetPixel(n%w,n/w).A>0)q.Enqueue(n);}}if(pixels.Count>=12)all.Add(pixels);}
   var main=all.OrderByDescending(a=>a.Count).Take(count).OrderBy(a=>a.Average(k=>k%w)).ToList();
   if(main.Count!=count||main.Min(a=>a.Count)<main.Max(a=>a.Count)*.12)throw new Exception(id+" connected figures count mismatch");
   var groups=main.Select(a=>new List<int>(a)).ToList();
   foreach(var part in all){if(main.Contains(part))continue;double cx=part.Average(k=>k%w);int closest=0;double best=double.MaxValue;for(int n=0;n<count;n++){double dx=Math.Abs(main[n].Average(k=>k%w)-cx);if(dx<best){best=dx;closest=n;}}if(part.Count>24)groups[closest].AddRange(part);}
   var boxes=new List<Rectangle>();var masks=new List<Bitmap>();
   for(int c=0;c<count;c++){var g=groups[c];int minX=g.Min(k=>k%w),maxX=g.Max(k=>k%w),minY=g.Min(k=>k/w),maxY=g.Max(k=>k/w);if(minX<1||maxX>=w-1||minY<1||maxY>=h-1)throw new Exception(id+" source clipped "+c);boxes.Add(Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1));var mask=new Bitmap(w,h,PixelFormat.Format32bppArgb);foreach(int k in g)mask.SetPixel(k%w,k/w,b.GetPixel(k%w,k/w));masks.Add(mask);}
   double scale=targetHeight/(double)boxes[0].Height;if(id=="Of_Wrath")scale=88/(double)boxes[count-1].Height;
   var dir=Path.Combine(root,"Assets/Arakus/Frames");Directory.CreateDirectory(dir);
   for(int c=0;c<count;c++){
    var box=boxes[c];int dw=(int)Math.Round(box.Width*scale),dh=(int)Math.Round(box.Height*scale);
    if(dw>size-8||dh>baseline-4)throw new Exception(id+" target overflow "+c+" "+dw+"x"+dh);
    using(var o=new Bitmap(size,size,PixelFormat.Format32bppArgb)){
     using(var g=Graphics.FromImage(o)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;int footBand=Math.Max(3,box.Height/20);var foot=groups[c].Where(k=>k/w>=box.Bottom-footBand).ToArray();double anchor=foot.Average(k=>k%w);int dx=size/2-(int)Math.Round((anchor-box.X)*scale);g.DrawImage(masks[c],new Rectangle(dx,baseline-dh,dw,dh),box,GraphicsUnit.Pixel);}
     o.Save(Path.Combine(dir,id+"_"+c.ToString("00")+".png"),ImageFormat.Png);
    }
   }
  }
 }

 public static void FX(string file,string root,string id,int target,bool vertical){
  using(var b=Clean(file)){
   int n=6;int[] cut=new int[7];cut[0]=0;cut[6]=b.Width;
   for(int c=1;c<6;c++){int expected=b.Width*c/6,best=expected,min=int.MaxValue;for(int x=expected-b.Width/30;x<=expected+b.Width/30;x++){int occupied=0;for(int y=0;y<b.Height;y++)if(b.GetPixel(x,y).A>0)occupied++;if(occupied<min){min=occupied;best=x;}if(occupied==0){best=x;break;}}cut[c]=best;}
   var boxes=new List<Rectangle>();
   for(int c=0;c<n;c++){int minX=cut[c+1],maxX=-1,minY=b.Height,maxY=-1;for(int y=0;y<b.Height;y++)for(int x=cut[c];x<cut[c+1];x++)if(b.GetPixel(x,y).A>0){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}boxes.Add(maxX<0?Rectangle.Empty:Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1));}
   int dimension=vertical?boxes.Max(a=>a.Height):boxes.Max(a=>a.Width);if(dimension<1)throw new Exception(id+" no FX content");double scale=target/(double)dimension;
   for(int c=0;c<n;c++)using(var o=new Bitmap(384,384,PixelFormat.Format32bppArgb)){var box=boxes[c];if(!box.IsEmpty){int dw=(int)Math.Round(box.Width*scale),dh=(int)Math.Round(box.Height*scale);if(dw>376||dh>312)throw new Exception(id+" FX overflow");using(var g=Graphics.FromImage(o)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(b,new Rectangle((384-dw)/2,320-dh,dw,dh),box,GraphicsUnit.Pixel);}}o.Save(Path.Combine(root,"Assets/Arakus/Frames",id+"_"+c.ToString("00")+".png"),ImageFormat.Png);}
  }
 }
 // Weighted median-cut palette shared by all character frames; no dithering.
 public static Color[] Palette(string[] files,int max){
  var hist=new Dictionary<int,int>();foreach(var f in files)using(var b=new Bitmap(f))for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var p=b.GetPixel(x,y);if(p.A==0)continue;int k=(p.R/8<<10)|(p.G/8<<5)|p.B/8;hist[k]=hist.ContainsKey(k)?hist[k]+1:1;}
  var groups=new List<List<int>>{hist.Keys.ToList()};
  Func<int,int,int> chan=(k,ch)=>ch==0?((k>>10)&31):ch==1?((k>>5)&31):k&31;
  while(groups.Count<max){int pick=-1,best=-1,channel=0;for(int i=0;i<groups.Count;i++){var a=groups[i];if(a.Count<2)continue;for(int ch=0;ch<3;ch++){int range=a.Max(k=>chan(k,ch))-a.Min(k=>chan(k,ch));int score=range*(int)Math.Sqrt(a.Sum(k=>hist[k]));if(score>best){best=score;pick=i;channel=ch;}}}if(pick<0)break;var list=groups[pick].OrderBy(k=>chan(k,channel)).ToList();int half=list.Sum(k=>hist[k])/2,acc=0,split=1;for(int n=0;n<list.Count-1;n++){acc+=hist[list[n]];split=n+1;if(acc>=half)break;}groups[pick]=list.Take(split).ToList();groups.Add(list.Skip(split).ToList());}
  return groups.Select(a=>{long n=a.Sum(k=>(long)hist[k]);return Color.FromArgb(255,(int)(a.Sum(k=>(long)(chan(k,0)*8+4)*hist[k])/n),(int)(a.Sum(k=>(long)(chan(k,1)*8+4)*hist[k])/n),(int)(a.Sum(k=>(long)(chan(k,2)*8+4)*hist[k])/n));}).ToArray();
 }
 public static void Quantize(string[] files,Color[] palette){foreach(var f in files)using(var b=new Bitmap(f)){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var p=b.GetPixel(x,y);if(p.A==0)continue;Color best=palette[0];int distance=int.MaxValue;foreach(var c in palette){int dr=p.R-c.R,dg=p.G-c.G,db=p.B-c.B;int d=dr*dr+dg*dg+db*db;if(d<distance){distance=d;best=c;}}b.SetPixel(x,y,best);}var tmp=f+".new.png";b.Save(tmp,ImageFormat.Png);b.Dispose();File.Move(tmp,f,true);}}
 public static void Sheet(string root,string id,int count,int size){using(var o=new Bitmap(count*size,size,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(o)){g.CompositingMode=CompositingMode.SourceCopy;for(int i=0;i<count;i++)using(var b=new Bitmap(Path.Combine(root,"Assets/Arakus/Frames",id+"_"+i.ToString("00")+".png")))g.DrawImageUnscaled(b,i*size,0);}o.Save(Path.Combine(root,"Assets/Arakus",id+".png"),ImageFormat.Png);}}
}
'@
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$jobs=Get-Content (Join-Path $PSScriptRoot jobs.json) -Raw | ConvertFrom-Json
foreach($j in $jobs){
 $id=[string]$j[0];$inputFile=Join-Path $PSScriptRoot "Generated/$id.png"
 if(!(Test-Path $inputFile)){continue}
 $height=88;if($id -eq 'Aggressive_Attack'){$height=100};if($id -eq 'Of_Wrath'){$height=60};if($id -eq 'Charge'){$height=60};if($id -eq 'Shield_Spin'){$height=36};if($id -eq 'Pillar'){$height=120}
 [ArakusPixels]::Extract($inputFile,$root,$id,[int]$j[2],$height,192,160)
 Write-Output "EXTRACTED $id"
}
# Re-sequence generated shield-slam poses so contact is visible at 0.85s.
$frameDir=Join-Path $root 'Assets/Arakus/Frames'
$n0=[IO.File]::ReadAllBytes("$frameDir/NormalAttack_L_00.png")
$n2=[IO.File]::ReadAllBytes("$frameDir/NormalAttack_L_02.png")
$n3=[IO.File]::ReadAllBytes("$frameDir/NormalAttack_L_03.png")
[IO.File]::WriteAllBytes("$frameDir/NormalAttack_L_01.png",$n2)
[IO.File]::WriteAllBytes("$frameDir/NormalAttack_L_02.png",$n3)
[IO.File]::WriteAllBytes("$frameDir/NormalAttack_L_03.png",$n3)
[IO.File]::WriteAllBytes("$frameDir/NormalAttack_L_04.png",$n0)
foreach($n in 0..1){Copy-Item "$frameDir/ShieldThrowing_DashAttack_$($n.ToString('00')).png" "$frameDir/Shield_Throwing_Fly_Attack_$($n.ToString('00')).png"}
$files=@(Get-ChildItem "$root/Assets/Arakus/Frames/*.png" | Where-Object {$_.Name -notmatch '^FX_|^Pillar_|^Shield_Spin_'} | ForEach-Object FullName)
if($files.Count -gt 0){$palette=[ArakusPixels]::Palette($files,32);[ArakusPixels]::Quantize($files,$palette);$palette | ForEach-Object { '#{0:X2}{1:X2}{2:X2}' -f $_.R,$_.G,$_.B } | ConvertTo-Json | Set-Content "$root/Source/palette.json"}
$shieldFiles=@(Get-ChildItem "$root/Assets/Arakus/Frames/Shield_Spin_*.png" | ForEach-Object FullName)
if($shieldFiles.Count -gt 0){[ArakusPixels]::Quantize($shieldFiles,$palette)}
$pillarFiles=@(Get-ChildItem "$root/Assets/Arakus/Frames/Pillar_*.png" | ForEach-Object FullName)
if($pillarFiles.Count -gt 0){[ArakusPixels]::Quantize($pillarFiles,[ArakusPixels]::Palette($pillarFiles,32))}
foreach($j in $jobs){if(Test-Path "$root/Assets/Arakus/Frames/$($j[0])_00.png"){[ArakusPixels]::Sheet($root,[string]$j[0],[int]$j[2],192)}}

$fx=Get-Content (Join-Path $PSScriptRoot fx_jobs.json) -Raw | ConvertFrom-Json
foreach($j in $fx){$f=Join-Path $PSScriptRoot "Generated/$($j[0]).png";if(Test-Path $f){$vertical=$j[0] -in @('FX_WideVertical','FX_BladeCharge','FX_WrathWhite','FX_WrathRed');[ArakusPixels]::FX($f,$root,[string]$j[0],[int]$j[2],$vertical);Write-Output "EXTRACTED $($j[0])"}}
$fxfiles=@(Get-ChildItem "$root/Assets/Arakus/Frames/FX_*.png" | ForEach-Object FullName)
if($fxfiles.Count -gt 0){$fxpalette=[ArakusPixels]::Palette($fxfiles,32);[ArakusPixels]::Quantize($fxfiles,$fxpalette)}
foreach($j in $fx){if(Test-Path "$root/Assets/Arakus/Frames/$($j[0])_00.png"){[ArakusPixels]::Sheet($root,[string]$j[0],6,384)}}
