using System;using System.Reflection;using ReversalTower;using UnityEngine;
class Program {
 static int checks;static void Assert(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
 static T Field<T>(Soundscape s,string name)=>(T)typeof(Soundscape).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);
 static void Tick(Soundscape s,int n=40){for(int i=0;i<n;i++)typeof(Soundscape).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);}
 static Soundscape New(){var s=new Soundscape();s.Initialize();s.SetBoss(0);s.tension=.08f;Tick(s);return s;}
 static void Main(){
 var s=New();var m=Field<AudioSource>(s,"music");Assert(m.clip.name=="Tower score carrier"&&m.volume==1,"lobby carrier");
 s.tension=.65f;Tick(s);Assert(m.clip.name=="boss_arakus"&&Math.Abs(m.volume-.29f)<.001,"boss enter");int plays=m.plays;
 s.tension=.95f;Tick(s);Assert(m.plays==plays&&Math.Abs(m.volume-.34f)<.001,"phase two no restart");
 var old=m.clip;s.SetBoss(1);Tick(s,1);Assert(m.clip==old&&m.volume<.34f,"fade before switch");Tick(s);Assert(m.clip.name=="boss_phanes"&&old.destroyed,"boss change destroys old");
 s.tension=.08f;Tick(s);Assert(m.clip.name=="Tower score carrier"&&!Field<bool>(s,"externalMusic"),"lobby returns synthesis");
 ExternalAssets.missing.Add("boss_adicus");s.SetBoss(2);Tick(s);s.tension=.65f;Tick(s);Assert(m.clip.name=="battle","missing boss falls back battle");
 s.SetBoss(99);Tick(s);Assert(m.clip.name=="boss_kharos","clamp high");s.SetBoss(-20);Tick(s);Assert(m.clip.name=="boss_arakus","clamp low");
 Assert(ExternalAssets.calls.Contains("boss_arakus:False"),"no decoded cache");
 ExternalAssets.missing.Add("battle");ExternalAssets.missing.Add("boss_arakus");var empty=New();empty.tension=.65f;Tick(empty);var em=Field<AudioSource>(empty,"music");Assert(em.clip!=null&&em.clip.name=="Tower score carrier"&&!Field<bool>(empty,"externalMusic")&&em.volume==1,"both missing preserve synth");
 Console.WriteLine("PASS "+checks+" lifecycle assertions (STUB, not Unity runtime)");
 }
}
