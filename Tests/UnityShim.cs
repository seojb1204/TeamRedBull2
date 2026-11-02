// Minimal headless test doubles. This file is outside Assets and never ships in the game.
// It exercises the actual combat source without pretending to emulate Unity rendering/audio.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
namespace UnityEngine
{
    public class Object
    {
        public static List<Object> registry=new List<Object>();
        public static T FindAnyObjectByType<T>() where T:class {return registry.Find(o=>o is T) as T;}
        public static implicit operator bool(Object o)=>o!=null;
    }
    public class MonoBehaviour:Object {public GameObject gameObject=new GameObject();public Transform transform=>gameObject.transform;}
    public class GameObject:Object
    {
        public Transform transform=new Transform();public bool active=true;public string tag;
        public GameObject(string name=""){}public void SetActive(bool value){active=value;}
        public T AddComponent<T>() where T:new(){var value=new T();var obj=value as Object;if(obj!=null)registry.Add(obj);var mono=value as MonoBehaviour;if(mono!=null)mono.gameObject=this;var camera=value as Camera;if(camera!=null){camera.gameObject=this;Camera.main=camera;}
#if DESKTOP_PREVIEW
            var sprite=value as SpriteRenderer;if(sprite!=null)sprite.gameObject=this;
#endif
            return value;}
    }
    public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
    public enum RuntimeInitializeLoadType{AfterSceneLoad}
    public struct Vector2
    {
        public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
        public static Vector2 up=>new Vector2(0,1);public static Vector2 down=>new Vector2(0,-1);
        public float magnitude=>(float)Math.Sqrt(x*x+y*y);public Vector2 normalized=>magnitude<.00001f?new Vector2():this*(1/magnitude);
        public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
        public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
        public static Vector2 operator*(Vector2 a,float s)=>new Vector2(a.x*s,a.y*s);
        public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude;
        public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>a+(b-a)*Mathf.Clamp01(t);
    }
    public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
    public struct Quaternion {public float z;public static Quaternion identity=>new Quaternion();public static Quaternion Euler(float x,float y,float z)=>new Quaternion{z=z};public static Vector2 operator*(Quaternion q,Vector2 v){double a=q.z*Math.PI/180;return new Vector2((float)(v.x*Math.Cos(a)-v.y*Math.Sin(a)),(float)(v.x*Math.Sin(a)+v.y*Math.Cos(a)));}}
    public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
    public class Transform {public Vector3 position,localScale;public Quaternion rotation;public void SetParent(Transform parent){}}
    public class Camera:Object {public static Camera main;public bool orthographic;public float orthographicSize;public Transform transform=new Transform();public string tag;public Color backgroundColor;public GameObject gameObject=new GameObject();}
    public class AudioListener:Object {public static float volume;}
    public enum RuntimePlatform{WindowsPlayer,WindowsEditor,LinuxPlayer,OSXPlayer}
    public static class Application {public static int targetFrameRate;public static string unityVersion="headless-shim";public static RuntimePlatform platform=RuntimePlatform.WindowsPlayer;public static bool isBatchMode=true;public static void Quit(int code=0){}public static string persistentDataPath=Path.GetFullPath("work/headless-save");public static string streamingAssetsPath=>Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets");}
    public static class QualitySettings {public static int vSyncCount;}
    public static class Time {public static float unscaledDeltaTime=1f/60;}
    public static class Screen {public static int width=1600,height=900;}
    public static class ScreenCapture {public static Action<string> capture;public static void CaptureScreenshot(string path){if(capture!=null)capture(path);}}
    public static class Debug {public static void Log(object text){Console.WriteLine(text);}public static void LogWarning(object text){Console.WriteLine(text);}public static void LogError(object text){Console.WriteLine(text);}}
    public enum KeyCode {F12,F1,Escape,Return,LeftArrow,RightArrow,A,D,Space,LeftShift,RightShift,J,Z,K,X,Q,W,E,R,F,C}
    public static class Input
    {
        public static HashSet<KeyCode> held=new HashSet<KeyCode>(),down=new HashSet<KeyCode>();
        public static HashSet<int> mouseDown=new HashSet<int>();public static bool mouseHeld;
        public static bool GetKey(KeyCode key)=>held.Contains(key);public static bool GetKeyDown(KeyCode key)=>down.Contains(key);public static bool GetMouseButtonDown(int button)=>mouseDown.Contains(button);
        public static void Clear(){held.Clear();down.Clear();mouseDown.Clear();}
    }
    public static class Random {static System.Random r=new System.Random(17);public static float value=>(float)r.NextDouble();public static float Range(float a,float b)=>a+(b-a)*value;}
    public static class Mathf
    {
        public const float PI=(float)Math.PI,Rad2Deg=180f/(float)Math.PI;
        public static float Min(float a,float b)=>Math.Min(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
        public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Abs(float a)=>Math.Abs(a);public static float Sign(float a)=>a>=0?1:-1;
        public static float Sin(float a)=>(float)Math.Sin(a);public static float Cos(float a)=>(float)Math.Cos(a);
        public static float Pow(float a,float b)=>(float)Math.Pow(a,b);public static float Sqrt(float a)=>(float)Math.Sqrt(a);public static float Atan2(float y,float x)=>(float)Math.Atan2(y,x);
        public static int CeilToInt(float a)=>(int)Math.Ceiling(a);public static float Repeat(float t,float length)=>t-(float)Math.Floor(t/length)*length;
        public static float Clamp(float a,float low,float high)=>Math.Max(low,Math.Min(high,a));public static int Clamp(int a,int low,int high)=>Math.Max(low,Math.Min(high,a));
        public static float Clamp01(float a)=>Clamp(a,0,1);public static int RoundToInt(float a)=>(int)Math.Round(a);public static int FloorToInt(float a)=>(int)Math.Floor(a);
        public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
        public static float MoveTowards(float a,float b,float max)=>Math.Abs(b-a)<=max?b:a+Math.Sign(b-a)*max;
    }
    public static class JsonUtility
    {
        public static string ToJson(object value,bool pretty=false){using(var stream=new MemoryStream()){new DataContractJsonSerializer(value.GetType()).WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray());}}
        public static T FromJson<T>(string text){using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(text)))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
    }
}
namespace ReversalTower
{
    public sealed class Soundscape:UnityEngine.MonoBehaviour {public float tension;public void Initialize(){}public void Play(string s){
#if DESKTOP_PREVIEW
        PreviewAudio.Play(s);
#endif
    }}
#if !DESKTOP_PREVIEW
    public sealed class PixelPresentation:UnityEngine.MonoBehaviour {}
    public sealed class WorldArtist:UnityEngine.MonoBehaviour {public void Initialize(TowerGame g){}public void Render(){}}
    public static class Palette {public static UnityEngine.Color Teal=new UnityEngine.Color(),Gold=new UnityEngine.Color(),Red=new UnityEngine.Color(),Purple=new UnityEngine.Color();}
    public sealed partial class TowerGame {void InitGui(){} }
#endif
}
