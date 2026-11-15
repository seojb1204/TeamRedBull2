using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class Object { public bool destroyed; public static void Destroy(Object x){if(x!=null)x.destroyed=true;} public static implicit operator bool(Object x)=>x!=null&&!x.destroyed; }
 public class Transform {public void SetParent(Transform t){} }
 public class GameObject:Object {public Transform transform=new Transform();public GameObject(string s=""){}public T AddComponent<T>() where T:new()=>new T();}
 public class MonoBehaviour:Object {public GameObject gameObject=new GameObject();public Transform transform=new Transform();}
 public class AudioClip:Object {public string name; public static AudioClip Create(string n,int size,int channels,int rate,bool stream)=>new AudioClip{name=n};public void SetData(float[] a,int offset){} }
 public class AudioSource:Object {public bool loop,isPlaying;public AudioClip clip;public float volume=1;public int plays,stops;public void Play(){plays++;isPlaying=clip!=null;}public void Stop(){stops++;isPlaying=false;}public void PlayOneShot(AudioClip c){} }
 public static class AudioSettings {public static int outputSampleRate=1000;}
 public static class Time {public static float unscaledDeltaTime=.05f;}
 public static class Mathf {public const float PI=(float)Math.PI;public static int Clamp(int x,int a,int b)=>Math.Clamp(x,a,b);public static float Clamp01(float x)=>Math.Clamp(x,0,1);public static float Pow(float x,float y)=>(float)Math.Pow(x,y);public static float Sin(float x)=>(float)Math.Sin(x);public static float Exp(float x)=>(float)Math.Exp(x);public static float MoveTowards(float x,float target,float delta)=>Math.Abs(target-x)<=delta?target:x+Math.Sign(target-x)*delta;}
}
namespace ReversalTower {public class WaveAsset {public float[] samples=new float[10];public int channels=1,rate=1000;}public static class ExternalAssets {public static HashSet<string> missing=new HashSet<string>();public static List<string> calls=new List<string>();public static WaveAsset Wave(string n,bool cache=true){calls.Add(n+":"+cache);return missing.Contains(n)?null:new WaveAsset();}}}
