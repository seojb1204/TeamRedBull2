using UnityEngine;
using System;

namespace ReversalTower
{
    // Imported CC0 recordings with synthesized fallback effects and lobby ambience.
    public sealed class Soundscape : MonoBehaviour
    {
        AudioSource music,fx;long sample;int rate;public volatile float tension;
        AudioClip hit,parry,dash,attack,heal,death,confirm,cue,clang,thud,step;
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
            cue=Glint();clang=Clang();thud=Thud();step=Step();
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
        // 패링 신호 소리: 맑게 울리는 짧은 금속음 (두 배음 + 빠른 감쇠)
        AudioClip Glint()
        {
            int n=(int)(rate*.32f);var data=new float[n];
            for(int i=0;i<n;i++){float t=(float)i/rate;float env=Mathf.Exp(-t*14)*Mathf.Clamp01(t*400);data[i]=(Mathf.Sin(t*2093*Mathf.PI*2)*.6f+Mathf.Sin(t*3136*Mathf.PI*2)*.3f+Mathf.Sin(t*4186*Mathf.PI*2)*.1f)*env*.16f;}
            var c=AudioClip.Create("Parry Cue",n,1,rate,false);c.SetData(data,0);return c;
        }
        // 패링 성공 금속음: 맞부딪힌 쇠가 울리는 비조화 배음 + 짧은 충격 잡음
        AudioClip Clang()
        {
            int n=(int)(rate*.7f);var data=new float[n];var rng=new System.Random(7);
            float[] hz={587,1361,2133,3017,4219};float[] amp={.5f,.32f,.22f,.14f,.08f};
            for(int i=0;i<n;i++)
            {
                float t=(float)i/rate,v=0;
                for(int k=0;k<hz.Length;k++)v+=Mathf.Sin(t*hz[k]*Mathf.PI*2)*amp[k]*Mathf.Exp(-t*(5+k*2.5f));
                v+=((float)rng.NextDouble()*2-1)*Mathf.Exp(-t*60)*.6f;
                data[i]=v*Mathf.Clamp01(t*600)*.32f;
            }
            var c=AudioClip.Create("Parry Clang",n,1,rate,false);c.SetData(data,0);return c;
        }
        // 강한 타격 저음: 아래로 떨어지는 저음 + 잡음
        AudioClip Thud()
        {
            int n=(int)(rate*.28f);var data=new float[n];var rng=new System.Random(11);double ph=0;
            for(int i=0;i<n;i++)
            {
                float t=(float)i/rate;ph+=2*Math.PI*(95-55*Mathf.Clamp01(t*5))/rate;
                data[i]=((float)Math.Sin(ph)*.8f+((float)rng.NextDouble()*2-1)*Mathf.Exp(-t*40)*.5f)*Mathf.Exp(-t*11)*.4f;
            }
            var c=AudioClip.Create("Heavy Thud",n,1,rate,false);c.SetData(data,0);return c;
        }
        // 보스 발소리: 낮게 울리는 짧은 쿵 (흙먼지 잡음 조금)
        AudioClip Step()
        {
            int n=(int)(rate*.22f);var data=new float[n];var rng=new System.Random(23);double ph=0;
            for(int i=0;i<n;i++)
            {
                float t=(float)i/rate;ph+=2*Math.PI*(62-24*Mathf.Clamp01(t*6))/rate;
                data[i]=((float)Math.Sin(ph)*.9f+((float)rng.NextDouble()*2-1)*Mathf.Exp(-t*55)*.35f)*Mathf.Exp(-t*16)*Mathf.Clamp01(t*500)*.3f;
            }
            var c=AudioClip.Create("Boss Step",n,1,rate,false);c.SetData(data,0);return c;
        }
        public void Play(string sound)
        {if(!fx)return;fx.PlayOneShot(sound=="step"?step:sound=="clang"?clang:sound=="thud"?thud:sound=="cue"?cue:sound=="parry"?parry:sound=="hit"?hit:sound=="dash"?dash:sound=="heal"?heal:sound=="death"?death:sound=="confirm"?confirm:attack);}
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
