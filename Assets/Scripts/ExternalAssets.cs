using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ReversalTower
{
    public static class ExternalAssets
    {
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        static readonly Dictionary<string,WaveAsset> waves=new Dictionary<string,WaveAsset>();
        public static string Root=>Application.streamingAssetsPath;
        static Texture2D Texture(string name)
        {
            if(textures.TryGetValue(name,out var t))return t;
            string path=Path.Combine(Root,name);if(!File.Exists(path))return null;
            t=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!t.LoadImage(File.ReadAllBytes(path)))return null;
            t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;
            if(name.StartsWith("Art/Castle/"))
            {
                var colors=t.GetPixels();var key=colors[0];
                for(int i=0;i<colors.Length;i++)if(Math.Abs(colors[i].r-key.r)<.001&&Math.Abs(colors[i].g-key.g)<.001&&Math.Abs(colors[i].b-key.b)<.001)colors[i].a=0;
                t.SetPixels(colors);t.Apply();
            }
            textures[name]=t;return t;
        }
        public static Sprite Picture(string name,int x=0,int y=0,int width=0,int height=0)
        {
            var t=Texture(name);if(t==null)return null;
            if(width==0)width=t.width;if(height==0)height=t.height;
            string key=name+"/"+x+"/"+y+"/"+width+"/"+height;
            if(sprites.TryGetValue(key,out var sprite))return sprite;
            sprite=Sprite.Create(t,new Rect(x,y,width,height),new Vector2(.5f,.5f),width);sprites[key]=sprite;return sprite;
        }
        public static Sprite Hero(string action,float time,float progress,float verticalSpeed)
        {
            string name=action=="run"||action=="dash"?"Run":action=="hit"?"Take Hit":action=="jump"?(verticalSpeed>0?"Jump":"Fall"):action=="Q"||action=="W"||action=="R"?"Attack2":action=="J"||action=="E"||action=="windup"||action=="parry"?"Attack1":"Idle";
            string file="Art/Hero/"+name+".png";var t=Texture(file);if(t==null)return null;
            int count=t.width/180;bool attack=name.StartsWith("Attack");
            int frame=attack?Mathf.Clamp((int)(progress*count),0,count-1):(int)(time*(name=="Run"?12:10))%count;
            if(action=="parry"||action=="windup")frame=1;
            return Picture(file,frame*180,0,180,180);
        }
        public static WaveAsset Wave(string name)
        {
            if(waves.TryGetValue(name,out var result))return result;
            string path=Path.Combine(Root,"Audio",name+".wav");if(!File.Exists(path))return null;
            try
            {
                using(var reader=new BinaryReader(File.OpenRead(path)))
                {
                    if(new string(reader.ReadChars(4))!="RIFF")return null;reader.ReadInt32();if(new string(reader.ReadChars(4))!="WAVE")return null;
                    int channels=1,rate=22050,bits=16,format=1;byte[] bytes=null;
                    while(reader.BaseStream.Position+8<=reader.BaseStream.Length)
                    {
                        string chunk=new string(reader.ReadChars(4));int size=reader.ReadInt32();long next=reader.BaseStream.Position+size+(size%2);
                        if(size<0||next>reader.BaseStream.Length+1)return null;
                        if(chunk=="fmt "){format=reader.ReadInt16();channels=reader.ReadInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();}
                        else if(chunk=="data")bytes=reader.ReadBytes(size);
                        reader.BaseStream.Position=next;
                    }
                    if(format!=1||bits!=16||bytes==null)return null;
                    var samples=new float[bytes.Length/2];for(int i=0;i<samples.Length;i++)samples[i]=BitConverter.ToInt16(bytes,i*2)/32768f;
                    result=new WaveAsset{samples=samples,channels=channels,rate=rate};waves[name]=result;return result;
                }
            }
            catch(Exception e){Debug.LogWarning("Audio resource unavailable: "+e.Message);return null;}
        }
    }
    public sealed class WaveAsset {public float[] samples;public int channels,rate;}
}
