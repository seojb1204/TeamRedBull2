using UnityEngine;
using System;

namespace ReversalTower
{
    // Imported CC0 recordings with synthesized fallback effects and lobby ambience.
    public sealed class Soundscape : MonoBehaviour
    {
        AudioSource music,fx;long sample;int rate;public volatile float tension;
        AudioClip hit,parry,dash,attack,heal,death,confirm;
        AudioClip battleMusic,carrier;bool externalMusic;
        public void Initialize()
        {
            rate=AudioSettings.outputSampleRate;
            music=gameObject.AddComponent<AudioSource>();music.loop=true;
            carrier=AudioClip.Create("Tower score carrier",rate,1,rate,false);music.clip=carrier;music.Play();battleMusic=Imported("battle");
            var effects=new GameObject("Tower Effects");effects.transform.SetParent(transform);fx=effects.AddComponent<AudioSource>();
            hit=Tone("Impact",100,.16f,.3f,true);parry=Tone("Parry",920,.33f,.25f,false);
            dash=Tone("Dash",180,.18f,.12f,true);attack=Tone("Blade",370,.13f,.12f,true);
            heal=Tone("Heal",520,.5f,.14f,false);death=Tone("Death",65,.8f,.3f,true);confirm=Tone("Confirm",660,.13f,.12f,false);
            attack=Imported("attack")??attack;parry=Imported("parry")??parry;hit=Imported("hit")??hit;dash=Imported("dash")??dash;
        }
        AudioClip Imported(string name){var wave=ExternalAssets.Wave(name);if(wave==null)return null;var clip=AudioClip.Create(name,wave.samples.Length/wave.channels,wave.channels,wave.rate,false);clip.SetData(wave.samples,0);return clip;}
        void Update(){bool wanted=tension>.3f&&battleMusic!=null;if(wanted!=externalMusic){externalMusic=wanted;music.Stop();music.clip=wanted?battleMusic:carrier;music.volume=wanted?.4f:1;music.Play();}}
        AudioClip Tone(string name,float hz,float length,float volume,bool noise)
        {
            int n=(int)(rate*length);var data=new float[n];var rng=new System.Random(name.GetHashCode());
            for(int i=0;i<n;i++){float t=(float)i/rate;float env=Mathf.Pow(1-(float)i/n,2);float wave=Mathf.Sin(t*hz*Mathf.PI*2*(1-t));if(noise)wave=.55f*wave+.45f*((float)rng.NextDouble()*2-1);data[i]=wave*env*volume;}
            var c=AudioClip.Create(name,n,1,rate,false);c.SetData(data,0);return c;
        }
        public void Play(string sound)
        {if(!fx)return;fx.PlayOneShot(sound=="parry"?parry:sound=="hit"?hit:sound=="dash"?dash:sound=="heal"?heal:sound=="death"?death:sound=="confirm"?confirm:attack);}
        void OnAudioFilterRead(float[] data,int channels)
        {
            if(rate==0||externalMusic)return;
            for(int i=0;i<data.Length;i+=channels)
            {
                double t=(double)sample++/rate,beat=t/.52;int step=(int)beat%16;
                int note=step==0?0:step==3?7:step==6?10:step==8?5:step==11?7:step==14?3:0;
                double freq=130.8128*Math.Pow(2,note/12.0),phase=beat-Math.Floor(beat);
                bool bell=step==0||step==3||step==6||step==8||step==11||step==14;
                double pad=(Math.Sin(t*2*Math.PI*65.4064)+.45*Math.Sin(t*2*Math.PI*98))*.028;
                double melody=bell?(Math.Sin(t*2*Math.PI*freq*2)+.3*Math.Sin(t*2*Math.PI*freq*4))*Math.Exp(-phase*5)*.038:0;
                double pulse=Math.Sin(t*2*Math.PI*(45+45*Math.Exp(-phase*18)))*Math.Exp(-phase*12)*tension*.055;
                float v=(float)(pad+melody+pulse);
                for(int c=0;c<channels;c++)data[i+c]=v;
            }
        }
    }
}
