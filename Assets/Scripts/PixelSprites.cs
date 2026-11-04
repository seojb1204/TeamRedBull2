using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower
{
    // Original 48 x 48 pixel drawings. Each pose is drawn on the pixel grid;
    // colour ramps and one-pixel outlines are shared across animation frames.
    public static class PixelSprites
    {
        public const int Size=48;
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        public static Sprite Get(int actor,string pose,int frame)
        {
            string key=actor+"/"+pose+"/"+frame;
            if(sprites.TryGetValue(key,out var sprite))return sprite;
            var canvas=new Canvas();Draw(canvas,actor,pose,frame);
            var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels(canvas.Outlined());texture.Apply();
            sprite=Sprite.Create(texture,new Rect(0,0,Size,Size),new Vector2(.5f,.5f),Size);sprites[key]=sprite;return sprite;
        }
        static Color C(int r,int g,int b)=>new Color(r/255f,g/255f,b/255f);
        static void Draw(Canvas c,int actor,string pose,int frame)
        {
            bool enemy=actor>=0,ghost=actor==6,mage=actor==1,hood=actor==2||actor==4||ghost;
            Color dark=C(13,22,35),shadow=C(38,48,65),steel=C(82,103,121),light=C(151,176,184),edge=C(221,232,216);
            Color trim=actor==4?C(184,137,242):actor==1?C(112,221,220):enemy?C(239,184,89):C(82,207,182);
            Color cloth=actor==4?C(75,48,115):actor==1?C(41,89,113):enemy?C(88,62,53):C(25,88,91);
            Color clothLight=actor==4?C(113,73,151):actor==1?C(58,137,157):enemy?C(139,95,62):C(39,137,132);
            int bob=pose=="idle"?frame%2:pose=="run"?(frame%2):0;
            int lean=pose=="dash"?3:pose=="hit"?-2:0;
            int step=pose=="run"?(frame==0?2:frame==2?-2:0):0;
            bool attack=pose=="attack"||pose=="J"||pose=="Q"||pose=="W"||pose=="E"||pose=="R";
            bool ready=pose=="windup"||(attack&&frame==0);
            // Cloak is stepped, shaded, and has a moving torn hem.
            c.Box(16+lean,23+bob,8,13,cloth);c.Box(13+lean,29+bob,7,10,cloth);
            c.Box(11+lean-(frame%2),36,8,5,cloth);c.Box(11+lean,40,3,2,cloth);
            c.Box(16+lean,27+bob,2,12,clothLight);c.Box(13+lean,35,2,5,clothLight);
            c.Box(20+lean,30,2,8,dark);c.Box(16+lean,40,2,3,dark);
            // Separate leg poses avoid rotating or stretching a bitmap.
            c.Box(20-step,33,4,9,shadow);c.Box(21-step,34,2,6,steel);
            c.Box(26+step,33,4,9,steel);c.Box(27+step,34,2,6,light);
            c.Box(18-step,41,7,3,dark);c.Box(25+step,41,7,3,dark);
            c.Box(20-step,41,4,1,light);c.Box(27+step,41,4,1,steel);
            int bx=21+lean,by=23+bob;
            c.Box(bx,by,9,10,steel);c.Box(bx+1,by,3,8,light);c.Box(bx+6,by+2,2,7,shadow);
            c.Box(bx+3,by+3,3,4,trim);c.Dot(bx+4,by+4,edge);c.Box(bx-1,by+9,11,2,dark);c.Box(bx+4,by+9,2,2,trim);
            // Overlapping shoulder plates and a steel glove.
            c.Box(18+lean,22+bob,7,4,shadow);c.Box(19+lean,21+bob,6,3,steel);c.Box(20+lean,21+bob,4,1,trim);
            c.Box(29+lean,24+bob,3,7,steel);c.Box(30+lean,25+bob,2,3,light);c.Box(30+lean,30+bob,4,3,dark);
            // Helmet, scarf and a two-pixel emissive visor.
            int hy=14+bob;
            c.Box(22+lean,hy,8,9,shadow);c.Box(23+lean,hy-1,6,1,steel);c.Box(23+lean,hy,5,4,light);
            c.Box(22+lean,hy+4,9,3,dark);c.Box(26+lean,hy+4,5,1,trim);c.Box(29+lean,hy+5,2,1,edge);
            c.Box(23+lean,hy+7,7,2,steel);c.Dot(24+lean,hy+7,light);
            if(!enemy)
            {
                c.Box(19+lean,21+bob,8,2,trim);c.Box(15+lean,22+bob,6,2,clothLight);c.Box(10+lean-(frame%2),23+bob,7,2,trim);
                c.Dot(9+lean,24+bob,trim);c.Box(23+lean,hy-3,3,3,clothLight);c.Dot(24+lean,hy-4,trim);
            }
            if(actor==0)
            {
                c.Box(20+lean,hy-4,2,6,trim);c.Box(18+lean,hy-6,2,4,trim);c.Dot(18+lean,hy-7,edge);
                c.Box(29+lean,hy-4,2,6,trim);c.Box(31+lean,hy-6,2,4,trim);c.Dot(32+lean,hy-7,edge);
                c.Box(16+lean,26,5,12,shadow);c.Box(16+lean,26,4,1,light);c.Box(17+lean,28,1,8,trim);c.Box(16+lean,37,3,1,trim);
            }
            if(hood)
            {
                c.Box(21+lean,hy-1,10,2,cloth);c.Box(20+lean,hy+1,3,9,cloth);c.Box(22+lean,hy-3,6,2,clothLight);
                c.Box(23+lean,hy+2,7,5,dark);c.Box(27+lean,hy+4,4,1,actor==4?C(241,86,103):trim);
                if(actor==2){c.Box(22+lean,hy+3,9,2,trim);c.Box(23+lean,hy+3,4,1,edge);}
            }
            if(actor==1||actor==3)
            {
                for(int i=0;i<3;i++){int xx=22+lean+i*3;c.Box(xx,hy-4-(i==1?2:0),2,5+(i==1?2:0),trim);c.Dot(xx,hy-5-(i==1?2:0),edge);}
                if(actor==3){c.Box(18+lean,23,4,5,trim);c.Box(29+lean,23,4,5,trim);c.Dot(19+lean,23,edge);}
            }
            if(mage)
            {
                c.Line(34,35,36,15,trim,1);c.Box(33,14,7,2,steel);c.Box(34,10,5,4,trim);c.Box(35,11,3,2,edge);c.Dot(36,8,trim);
                c.Box(23,34,7,7,cloth);c.Box(25,35,2,5,clothLight);
            }
            else
            {
                int hx=32+lean,handY=31+bob,tx=44,ty=18;
                if(ready){hx=30+lean;handY=25;tx=21;ty=4;}
                else if(attack&&frame==1){hx=32+lean;handY=26;tx=45;ty=16;}
                else if(attack&&frame==2){hx=33+lean;handY=29;tx=46;ty=34;}
                else if(attack&&frame==3){hx=32+lean;handY=31;tx=40;ty=39;}
                if(pose=="parry"){hx=34;handY=24;tx=35;ty=10;}
                c.Line(29+lean,25+bob,hx,handY,steel,2);c.Box(hx-1,handY-1,3,3,dark);
                c.Line(hx,handY,tx,ty,shadow,enemy?3:2);c.Line(hx,handY-1,tx,ty-1,edge,enemy?2:1);c.Line(hx-1,handY+1,hx+2,handY-2,trim,1);
                if(actor==2){c.Line(tx,ty,tx-9,ty+1,edge,2);c.Line(tx-9,ty+1,tx-11,ty+4,steel,1);}
                if(attack&&frame==2){c.Line(38,17,45,24,trim,1);c.Line(45,24,46,31,edge,1);c.Dot(44,34,trim);}
            }
            if(pose=="parry"){c.Box(35,19,1,12,trim);c.Box(36,22,1,6,edge);}
        }
        sealed class Canvas
        {
            Color[] pixels=new Color[Size*Size];
            public void Dot(int x,int y,Color c){if(x>=0&&x<Size&&y>=0&&y<Size)pixels[(Size-1-y)*Size+x]=c;}
            public void Box(int x,int y,int w,int h,Color c){for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)Dot(xx,yy,c);}
            public void Line(int x,int y,int tx,int ty,Color c,int width)
            {int dx=System.Math.Abs(tx-x),dy=-System.Math.Abs(ty-y),sx=x<tx?1:-1,sy=y<ty?1:-1,err=dx+dy;while(true){Box(x,y,width,width,c);if(x==tx&&y==ty)break;int e=2*err;if(e>=dy){err+=dy;x+=sx;}if(e<=dx){err+=dx;y+=sy;}}}
            public Color[] Outlined()
            {
                var result=(Color[])pixels.Clone();var outline=C(8,14,24);
                for(int y=1;y<Size-1;y++)for(int x=1;x<Size-1;x++){int i=y*Size+x;if(pixels[i].a>0)continue;if(pixels[i-1].a>0||pixels[i+1].a>0||pixels[i-Size].a>0||pixels[i+Size].a>0)result[i]=outline;}
                return result;
            }
        }
    }
}
