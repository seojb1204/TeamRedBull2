// Portable desktop adapter for the same gameplay and UI source.
// This is a preview renderer, not a Unity player or an implementation of the Unity engine.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using U=UnityEngine;

namespace UnityEngine
{
    public enum TextureFormat{RGBA32}public enum FilterMode{Bilinear,Point}public enum TextureWrapMode{Clamp}
    public class Texture2D:Object
    {
        public static Texture2D whiteTexture=new Texture2D(1,1,TextureFormat.RGBA32,false);
        public int kind,width,height;public Bitmap bitmap;Color[] pixels;public FilterMode filterMode;public TextureWrapMode wrapMode;
        public Texture2D(int w,int h,TextureFormat format,bool m){width=w;height=h;}
        public bool LoadImage(byte[] bytes)
        {using(var stream=new System.IO.MemoryStream(bytes))using(var im=new Bitmap(stream)){width=im.Width;height=im.Height;var data=new Color[width*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++){var c=im.GetPixel(x,y);data[(height-1-y)*width+x]=new Color(c.R/255f,c.G/255f,c.B/255f,c.A/255f);}SetPixels(data);}return true;}
        public Color[] GetPixels()=>(Color[])pixels.Clone();
        public void SetPixels(Color[] colors)
        {
            pixels=(Color[])colors.Clone();int n=width;bool coloured=false;
            foreach(var p in colors)if(p.a>0&&(p.r<.99f||p.g<.99f||p.b<.99f)){coloured=true;break;}
            kind=coloured?3:colors[0].a>.5f&&colors[colors.Length-1].a>.5f?0:colors[(height-1)*n+n/2].a<.1f?2:1;
            if(coloured){if(bitmap!=null)bitmap.Dispose();bitmap=new Bitmap(width,height);for(int y=0;y<height;y++)for(int x=0;x<width;x++)bitmap.SetPixel(x,height-1-y,GUI.ToColor(colors[y*width+x]));}
        }
        public void Apply(){}
    }
    public class Sprite:Object{public Texture2D texture;public Rect rect;public static Sprite Create(Texture2D t,Rect r,Vector2 pivot,float units)=>new Sprite{texture=t,rect=r};}
    public class SpriteRenderer:Object {public GameObject gameObject=new GameObject();public Transform transform=>gameObject.transform;public Color color;public int sortingOrder;public Sprite sprite;}
    public struct Rect
    {
        public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
        public bool Contains(Vector2 p)=>p.x>=x&&p.y>=y&&p.x<x+width&&p.y<y+height;
    }
    public struct Matrix4x4
    {
        public float sx,sy,tx,ty;
        public static Matrix4x4 identity=>new Matrix4x4{sx=1,sy=1};
        public static Matrix4x4 TRS(Vector3 t,Quaternion q,Vector3 s)=>new Matrix4x4{sx=s.x,sy=s.y,tx=t.x,ty=t.y};
    }
    public enum TextAnchor{UpperLeft,UpperCenter,UpperRight,MiddleCenter}
    public class Font:Object {public static Font CreateDynamicFontFromOSFont(string[] names,int size)=>new Font();}
    public class GUIStyleState {public Texture2D background;public Color textColor;}
    public class GUIStyle
    {
        public static GUIStyle none=new GUIStyle();public Font font;public bool richText,wordWrap;public int fontSize=18;public TextAnchor alignment;
        public GUIStyleState normal=new GUIStyleState(),hover=new GUIStyleState(),active=new GUIStyleState(),focused=new GUIStyleState();
        public GUIStyle(){}public GUIStyle(GUIStyle other){fontSize=other.fontSize;}
    }
    public class GUISkin {public GUIStyle label=new GUIStyle(),button=new GUIStyle();}
    public class GUIContent {public static GUIContent none=new GUIContent();}
    public class Event
    {
        public static Event current=new Event();public static Vector2 rawMouse;public static float wheel;
        public Vector2 mousePosition=>new Vector2((rawMouse.x-GUI.matrix.tx)/GUI.matrix.sx,(rawMouse.y-GUI.matrix.ty)/GUI.matrix.sy);
    }
    public static class GUI
    {
        public static Graphics graphics;public static GUISkin skin=new GUISkin();public static bool enabled=true;public static Color color=new Color(1,1,1);
        static Matrix4x4 transform=Matrix4x4.identity;static Dictionary<int,System.Drawing.Font> fonts=new Dictionary<int,System.Drawing.Font>();static Stack<GraphicsState> states=new Stack<GraphicsState>();
        public static Matrix4x4 matrix {get=>transform;set{transform=value;if(graphics!=null)graphics.Transform=new Matrix(value.sx,0,0,value.sy,value.tx,value.ty);}}
        public static System.Drawing.Color ToColor(Color c)=>System.Drawing.Color.FromArgb((int)(Mathf.Clamp01(c.a)*255),(int)(Mathf.Clamp01(c.r)*255),(int)(Mathf.Clamp01(c.g)*255),(int)(Mathf.Clamp01(c.b)*255));
        public static void DrawTexture(Rect r,Texture2D texture){using(var b=new SolidBrush(ToColor(color)))graphics.FillRectangle(b,r.x,r.y,r.width,r.height);}
        static System.Drawing.Font GetFont(int size){if(!fonts.ContainsKey(size))fonts[size]=new System.Drawing.Font("Malgun Gothic",size,FontStyle.Regular,GraphicsUnit.Pixel);return fonts[size];}
        public static void Label(Rect r,string text,GUIStyle style)
        {
            using(var brush=new SolidBrush(ToColor(style.normal.textColor)))using(var format=new StringFormat())
            {
                format.Alignment=style.alignment==TextAnchor.UpperCenter||style.alignment==TextAnchor.MiddleCenter?StringAlignment.Center:style.alignment==TextAnchor.UpperRight?StringAlignment.Far:StringAlignment.Near;
                format.LineAlignment=style.alignment==TextAnchor.MiddleCenter?StringAlignment.Center:StringAlignment.Near;
                format.Trimming=StringTrimming.EllipsisCharacter;
                graphics.DrawString(text,GetFont(Math.Max(1,style.fontSize)),brush,new RectangleF(r.x,r.y,r.width,r.height),format);
            }
        }
        static bool Click(Rect r){bool hit=enabled&&Input.mouseDown.Contains(0)&&r.Contains(Event.current.mousePosition);if(hit)Input.mouseDown.Remove(0);return hit;}
        public static bool Button(Rect r,string text,GUIStyle style){var old=style.normal.textColor;if(r.Contains(Event.current.mousePosition))style.normal.textColor=style.hover.textColor;Label(r,text,style);style.normal.textColor=old;return Click(r);}
        public static bool Button(Rect r,GUIContent text,GUIStyle style)=>Click(r);
        public static float HorizontalSlider(Rect r,float value,float min,float max)
        {
            var old=color;color=new Color(.2f,.3f,.35f);DrawTexture(new Rect(r.x,r.y+9,r.width,4),Texture2D.whiteTexture);
            color=new Color(1,.76f,.37f);DrawTexture(new Rect(r.x+value*r.width-5,r.y+2,10,20),Texture2D.whiteTexture);color=old;
            if(Input.mouseHeld&&r.Contains(Event.current.mousePosition))return Mathf.Clamp((Event.current.mousePosition.x-r.x)/r.width,min,max);return value;
        }
        public static Vector2 BeginScrollView(Rect outer,Vector2 scroll,Rect view)
        {
            if(outer.Contains(Event.current.mousePosition))scroll.y=Mathf.Clamp(scroll.y-Event.wheel*.4f,0,Mathf.Max(0,view.height-outer.height));
            states.Push(graphics.Save());graphics.SetClip(new RectangleF(outer.x,outer.y,outer.width,outer.height));graphics.TranslateTransform(outer.x,outer.y-scroll.y);return scroll;
        }
        public static void EndScrollView(){graphics.Restore(states.Pop());}
    }
}

namespace ReversalTower
{
    public static class PreviewPainter
    {
        static Bitmap low;
        public static void World(Graphics graphics,int width,int height)
        {
            int scale=Math.Max(1,Math.Min(width/400,height/225)),w=Math.Max(1,width/scale),h=Math.Max(1,height/scale);
            if(low==null||low.Width!=w||low.Height!=h){if(low!=null)low.Dispose();low=new Bitmap(w,h);}
            using(var pixelGraphics=Graphics.FromImage(low)){pixelGraphics.SmoothingMode=SmoothingMode.None;pixelGraphics.InterpolationMode=InterpolationMode.NearestNeighbor;pixelGraphics.PixelOffsetMode=PixelOffsetMode.Half;DrawWorld(pixelGraphics,w,h);}
            var state=graphics.Save();graphics.InterpolationMode=InterpolationMode.NearestNeighbor;graphics.PixelOffsetMode=PixelOffsetMode.Half;
            graphics.DrawImage(low,new Rectangle((width-w*scale)/2,(height-h*scale)/2,w*scale,h*scale),0,0,w,h,GraphicsUnit.Pixel);graphics.Restore(state);
        }
        static void DrawWorld(Graphics graphics,int width,int height)
        {
            var camera=U.Camera.main;float pixels=height/(camera.orthographicSize*2);
            graphics.Clear(U.GUI.ToColor(Palette.Ink));
            var renderers=new List<U.SpriteRenderer>();foreach(var obj in U.Object.registry)if(obj is U.SpriteRenderer)renderers.Add((U.SpriteRenderer)obj);
            renderers.Sort((a,b)=>a.sortingOrder.CompareTo(b.sortingOrder));
            foreach(var r in renderers)
            {
                if(!r.gameObject.active||r.sprite==null)continue;
                var pos=r.transform.position;var size=r.transform.localScale;float x=width*.5f+(pos.x-camera.transform.position.x)*pixels,y=height*.5f-(pos.y-camera.transform.position.y)*pixels;
                float w=Math.Abs(size.x)*pixels,h=Math.Abs(size.y)*pixels;if(w<=0||h<=0)continue;
                var state=graphics.Save();graphics.TranslateTransform(x,y);graphics.RotateTransform(-r.transform.rotation.z);
                graphics.ScaleTransform(size.x<0?-1:1,size.y<0?-1:1);
                using(var brush=new SolidBrush(U.GUI.ToColor(r.color)))
                {
                    int kind=r.sprite.texture.kind;
                    if(kind==3)
                    {
                        var region=r.sprite.rect;var source=new RectangleF(region.x,r.sprite.texture.height-region.y-region.height,region.width,region.height);
                        if(r.color.r==1&&r.color.g==1&&r.color.b==1&&r.color.a==1)graphics.DrawImage(r.sprite.texture.bitmap,new RectangleF(-w*.5f,-h*.5f,w,h),source,GraphicsUnit.Pixel);
                        else using(var attributes=new System.Drawing.Imaging.ImageAttributes())
                        {var matrix=new System.Drawing.Imaging.ColorMatrix();matrix.Matrix00=r.color.r;matrix.Matrix11=r.color.g;matrix.Matrix22=r.color.b;matrix.Matrix33=r.color.a;attributes.SetColorMatrix(matrix);graphics.DrawImage(r.sprite.texture.bitmap,new Rectangle((int)(-w*.5f),(int)(-h*.5f),Math.Max(1,(int)w),Math.Max(1,(int)h)),source.X,source.Y,source.Width,source.Height,GraphicsUnit.Pixel,attributes);}
                    }
                    else if(kind==1)graphics.FillEllipse(brush,-w*.5f,-h*.5f,w,h);
                    else if(kind==2)graphics.FillPolygon(brush,new[]{new PointF(-w*.5f,h*.5f),new PointF(0,-h*.5f),new PointF(w*.5f,h*.5f)});
                    else graphics.FillRectangle(brush,-w*.5f,-h*.5f,w,h);
                }
                graphics.Restore(state);
            }
        }
    }
    public static class PreviewAudio
    {
        static readonly Dictionary<string,byte[]> cache=new Dictionary<string,byte[]>();static System.Media.SoundPlayer player;
        public static void Play(string name)
        {
            if(U.AudioListener.volume<.01)return;
            if(NativeMixer.TryPlay(name))return;
            string key=name+"_"+(int)(U.AudioListener.volume*10);
            if(!cache.ContainsKey(key))
            {
                int rate=22050,n=5000;double hz=name=="parry"?920:name=="hit"?100:name=="heal"?520:name=="death"?65:name=="confirm"?660:240;
                using(var stream=new System.IO.MemoryStream())using(var w=new System.IO.BinaryWriter(stream))
                {
                    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+n*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(n*2);
                    for(int i=0;i<n;i++){double e=Math.Pow(1-(double)i/n,3);w.Write((short)(Math.Sin(i*hz*Math.PI*2/rate)*e*6000*U.AudioListener.volume));}cache[key]=stream.ToArray();
                }
            }
            try{if(player!=null)player.Dispose();player=new System.Media.SoundPlayer(new System.IO.MemoryStream(cache[key]));player.Play();}catch{}
        }
    }
}
