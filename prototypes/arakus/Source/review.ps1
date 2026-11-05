Add-Type -AssemblyName System.Drawing
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$jobs=Get-Content "$PSScriptRoot/jobs.json" -Raw | ConvertFrom-Json
for($page=0;$page -lt 5;$page++){
 $bmp=[Drawing.Bitmap]::new(1536,960);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(31,35,43));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 for($r=0;$r -lt 4;$r++){$ix=$page*4+$r;if($ix -ge $jobs.Count){break};$j=$jobs[$ix];$id=$j[0];$font=[Drawing.Font]::new('Arial',14);$g.DrawString($id,$font,[Drawing.Brushes]::White,8,$r*240+5);$font.Dispose();for($c=0;$c -lt [int]$j[2];$c++){$f="$root/Assets/Arakus/Frames/${id}_$($c.ToString('00')).png";$im=[Drawing.Bitmap]::new($f);$g.DrawImage($im,[Drawing.Rectangle]::new($c*256,$r*240+24,256,256),[Drawing.Rectangle]::new(32,48,128,128),[Drawing.GraphicsUnit]::Pixel);$im.Dispose()}}
 $g.Dispose();$bmp.Save("$root/Review/Characters_$page.png");$bmp.Dispose()
}
