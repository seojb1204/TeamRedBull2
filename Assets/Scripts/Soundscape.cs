using UnityEngine;
using System;

namespace ReversalTower
{
    // Attributed boss recordings with preserved CC0 fallback effects and synthesized ambience.
    // Boss combat effects: sfx_*.wav built from CC0 recordings by Tools/sfx/generate_boss_sfx.py.
    // Game code only names events (tell_0_sweep, boss_0_Slash_sweep_low, erupt_3_thunder ...);
    // this class decides which file, how loud, and how much the music steps back.
    public sealed class Soundscape : MonoBehaviour
    {
        readonly System.Collections.Generic.Dictionary<string,AudioClip> designed=new System.Collections.Generic.Dictionary<string,AudioClip>();
        readonly System.Collections.Generic.Dictionary<string,float> lastPlayed=new System.Collections.Generic.Dictionary<string,float>();
        AudioSource music,fx;long sample;int rate;public volatile float tension;
        AudioClip hit,parry,dash,attack,heal,death,confirm,cue,clang,thud;
        AudioClip battleMusic,carrier,bossMusic,lobbyMusic;volatile bool externalMusic;
        int requestedBoss=-1,loadedBoss=-1;
        volatile bool pauseRequested;bool sourcePaused;
        public void SetPaused(bool paused){pauseRequested=paused;}
        static readonly string[] BossTracks={"boss_arakus","boss_phanes","boss_adicus","boss_aethelos","boss_kharos"};
        // Identity only: no combat/DDA values are changed by presentation.
        public void SetBoss(int index){requestedBoss=Mathf.Clamp(index,0,BossTracks.Length-1);}

        // ── 보스 전투 효과음 ──
        // volume: PlayOneShot 배율. duck: 이 소리가 나는 동안 배경음을 줄이는 시간(초).
        sealed class Sfx{public string[] files;public float volume,duck;}
        readonly System.Collections.Generic.Dictionary<string,Sfx> sfxMap=new System.Collections.Generic.Dictionary<string,Sfx>();
        readonly System.Collections.Generic.Dictionary<string,Sfx> sfxResolved=new System.Collections.Generic.Dictionary<string,Sfx>();
        readonly System.Collections.Generic.Dictionary<string,AudioClip> sfxClips=new System.Collections.Generic.Dictionary<string,AudioClip>();
        static readonly Sfx Silent=new Sfx{files=new string[0]};
        float duckUntil;
        // 0 = 여유, 1 = 빈사. TowerGame이 체력 35% 아래에서 올린다. 심장 박동 간격과 크기를 정한다.
        public float danger;float heartAt;
        const float DuckLevel=.5f;
        // 효과음 전체 여유: 보스 강타와 피격음이 같은 프레임에 겹쳐도 출력이 1을 넘지 않게.
        // 개별 볼륨은 Tools/sfx 측정값(A가중 음량)으로 맞춘 상대값이다.
        const float SfxGain=.72f;

        public void Initialize()
        {
            rate=AudioSettings.outputSampleRate;
            music=gameObject.AddComponent<AudioSource>();music.loop=true;
            carrier=AudioClip.Create("Tower score carrier",rate,1,rate,false);lobbyMusic=Imported("lobby",false);music.clip=lobbyMusic??carrier;externalMusic=lobbyMusic!=null;music.volume=0;music.Play();battleMusic=Imported("battle");
            var effects=new GameObject("Tower Effects");effects.transform.SetParent(transform);fx=effects.AddComponent<AudioSource>();
            hit=Tone("Impact",100,.16f,.3f,true);parry=Tone("Parry",920,.33f,.25f,false);
            dash=Tone("Dash",180,.18f,.12f,true);attack=Tone("Blade",370,.13f,.12f,true);
            heal=Tone("Heal",520,.5f,.14f,false);death=Tone("Death",65,.8f,.3f,true);confirm=Tone("Confirm",660,.13f,.12f,false);
            cue=Glint();clang=Clang();thud=Thud();
            attack=Imported("attack")??attack;parry=Imported("parry")??parry;hit=Imported("hit")??hit;dash=Imported("dash")??dash;
            BuildSfxMap();
            // Decode every combat effect once here, never on the frame a boss attacks.
            foreach(var entry in sfxMap.Values)
                foreach(string file in entry.files)
                    if(!sfxClips.ContainsKey(file))sfxClips[file]=Imported("sfx_"+file,false);
        }

        void Map(string key,float volume,float duck,params string[] files){sfxMap[key]=new Sfx{files=files,volume=volume,duck=duck};}
        void BuildSfxMap()
        {
            // 공통 위험 신호: 낮은 금관 경고음(braam)이 들리면 패링이 안 되는 큰 공격이다.
            // 예고음(tell_보스번호_패턴) — BossPatternSystem.BeginPattern에서 선딜이 시작될 때.
            Map("tell_0_cleave",.52f,0,"tell_heavy");Map("tell_0_slash",.52f,0,"tell_heavy");
            Map("tell_0_shield",.29f,0,"tell_shield");Map("tell_0_kick",.26f,0,"tell_quick");Map("tell_0_step",.26f,0,"tell_quick");
            Map("tell_0_charge",.62f,1.2f,"tell_charge");Map("tell_0_throw",.31f,0,"tell_throw");Map("tell_0_slam",.31f,0,"tell_throw");
            Map("tell_0_rush",.77f,0,"tell_rush");Map("tell_0_sweep",.50f,2.4f,"tell_sweep");Map("tell_0_rage",.92f,2.6f,"tell_rage");
            Map("tell_1",.13f,0,"tell_spell");Map("tell_1_beam",.21f,0,"tell_holy");Map("tell_1_rain",.21f,0,"tell_holy");Map("tell_1_barrier",.36f,1.4f,"tell_barrier");
            Map("tell_2",.29f,0,"tell_dark");Map("tell_2_prisoner",.42f,0,"tell_summon");Map("tell_2_judgement",.36f,2.4f,"tell_barrier");
            Map("tell_3",.21f,0,"tell_holy");Map("tell_3_blade",.52f,0,"tell_heavy");Map("tell_3_lance",.13f,0,"tell_spell");Map("tell_3_rift",.29f,0,"tell_dark");Map("tell_3_heaven",.36f,1.2f,"tell_barrier");
            Map("tell_4",.29f,0,"tell_dark");Map("tell_4_draw",.62f,0,"tell_charge");Map("tell_4_phantom",.42f,0,"tell_summon");Map("tell_4_eclipse",.36f,2.1f,"tell_barrier");Map("tell_4_end",.36f,1.2f,"tell_barrier");
            // 실행음(boss_보스번호_종류_패턴) — 기존 PlaySound 호출 그대로. 없으면 boss_종류로 내려간다.
            Map("boss_Slash",.34f,0,"swing");Map("boss_Dash",1.00f,.6f,"charge_go");Map("boss_Volley",.29f,0,"zap");
            Map("boss_Rain",.12f,0,"hazard_warn");Map("boss_Pillar",.12f,0,"hazard_warn");Map("boss_Wave",.79f,0,"wave");
            Map("boss_Summon",.91f,0,"summon");Map("boss_Balance",.87f,1.2f,"judgement");Map("boss_Barrier",.59f,.8f,"barrier_rise");Map("boss_Echo",.34f,0,"swing");
            Map("boss_0_Slash_cleave",.84f,0,"swing_heavy");Map("boss_0_Slash_slash",.84f,0,"swing_heavy");Map("boss_0_Slash_throw",.84f,0,"swing_heavy");
            Map("boss_0_Slash_shield",.99f,.3f,"impact_shield");Map("boss_0_Slash_kick",.98f,0,"kick");
            Map("boss_0_Dash_charge",1.00f,1f,"charge_go");Map("boss_0_ShieldThrow",.58f,0,"shield_throw");Map("boss_0_landing",1f,.8f,"landing");
            Map("boss_0_ShieldRush",1f,.6f,"rush");Map("boss_0_Slash_sweep_low",.84f,1.2f,"sweep_low");Map("boss_0_Slash_sweep_high",1f,1.6f,"sweep_high");
            Map("boss_0_Rage",1f,1.8f,"rage_roar");Map("pillar_crash",1f,1.4f,"charge_crash");
            Map("boss_3_Slash",.84f,0,"swing_heavy");
            // 장판 폭발(erupt_보스번호_패턴) — 경고 장판이 실제로 터지는 순간.
            Map("erupt",1f,0,"erupt_fall");Map("erupt_1",.92f,0,"erupt_holy");Map("erupt_1_rain",1f,0,"erupt_fall");
            Map("erupt_3_thunder",1f,.3f,"thunder");Map("erupt_3_creation",.92f,0,"erupt_holy");
            Map("erupt_4",.34f,0,"swing");Map("erupt_4_rain",1f,0,"erupt_fall");
            sfxMap["erupt_2_judgement"]=Silent;sfxMap["erupt_4_eclipse"]=Silent;   // 판정음은 boss_Balance가 이미 냈다
            // 전투 사건
            Map("phase_change",1f,2.2f,"phase_change");Map("boss_death",1f,3.5f,"boss_death");Map("player_hurt",.6f,.25f,"player_hurt");
            Map("barrier_break",1f,1f,"barrier_break");Map("barrier_blast",1f,1.8f,"barrier_blast");Map("heartbeat",1f,0,"heartbeat");
        }
        // 정확한 키 → 뒤 조각을 하나씩 떼며 찾는다. boss_0_Slash 다음에는 보스 공통 boss_Slash도 본다.
        Sfx Resolve(string key)
        {
            if(sfxResolved.TryGetValue(key,out var found))return found;
            string k=key;Sfx s=null;
            while(true)
            {
                if(sfxMap.TryGetValue(k,out s))break;
                int cut=k.LastIndexOf('_');if(cut<=0){s=null;break;}
                k=k.Substring(0,cut);
                if(k.StartsWith("boss_")){var parts=k.Split('_');if(parts.Length==3&&sfxMap.TryGetValue("boss_"+parts[2],out s))break;s=null;}
            }
            sfxResolved[key]=s;return s;
        }
        bool PlaySfx(Sfx s,float scale=1)
        {
            bool played=false;
            foreach(string file in s.files)
                if(sfxClips.TryGetValue(file,out var clip)&&clip){fx.PlayOneShot(clip,s.volume*scale*SfxGain);played=true;}
            if(played&&s.duck>0)duckUntil=Math.Max(duckUntil,Time.unscaledTime+s.duck);
            return played||s.files.Length==0;
        }
        // 새 사건 이름은 파일이 없으면 조용히 넘어간다. 예전 이름(hit, cue ...)과 보스 실행음만 합성음으로 대신한다.
        static bool NewEvent(string sound)=>sound.StartsWith("tell_")||sound.StartsWith("erupt")||sound=="phase_change"||sound=="boss_death"||sound=="player_hurt"||sound=="heartbeat"||sound.StartsWith("barrier_")||sound=="pillar_crash";

        AudioClip Imported(string name,bool cache=true){var wave=ExternalAssets.Wave(name,cache);if(wave==null)return null;var clip=AudioClip.Create(name,wave.samples.Length/wave.channels,wave.channels,wave.rate,false);clip.SetData(wave.samples,0);return clip;}
        void Update()
        {
            // Pause preserves the clip and sample cursor. Never treat ESC as a scene change.
            if(pauseRequested)
            {
                if(!sourcePaused){music.Pause();sourcePaused=true;}
                return;
            }
            if(sourcePaused){music.UnPause();sourcePaused=false;}
            bool battle=tension>.3f;
            bool needsBoss=requestedBoss!=loadedBoss;
            AudioClip wanted=battle?(bossMusic??battleMusic??carrier):(lobbyMusic??carrier);
            bool switching=music.clip!=wanted || (needsBoss&&battle);
            float level=switching?0:(wanted==carrier?1:(!battle?.24f:(tension>.8f?.34f:.29f)));
            // 큰 예고·강타가 울리는 동안 배경음이 물러나서 그 소리가 묻히지 않게 한다.
            if(battle&&!switching&&Time.unscaledTime<duckUntil)level*=DuckLevel;
            music.volume=Mathf.MoveTowards(music.volume,level,Time.unscaledDeltaTime*1.8f);
            // 빈사 심장 박동: 위험할수록 빠르고 크게.
            if(battle&&danger>.01f&&fx&&Time.unscaledTime>=heartAt)
            {
                var beat=Resolve("heartbeat");
                if(beat!=null)PlaySfx(beat,.55f+.45f*danger);
                heartAt=Time.unscaledTime+(1.05f-.45f*Mathf.Clamp01(danger));
            }
            if(switching&&music.volume>.001f)return;
            // Read only the selected boss recording, never retain five decoded PCM copies.
            if(needsBoss&&requestedBoss>=0)
            {
                if(music.clip==bossMusic)music.Stop();
                if(bossMusic!=null)Destroy(bossMusic);
                bossMusic=Imported(BossTracks[requestedBoss],false);
                loadedBoss=requestedBoss;
                wanted=battle?(bossMusic??battleMusic??carrier):(lobbyMusic??carrier);
            }
            if(music.clip!=wanted||!music.isPlaying)
            {
                music.Stop();externalMusic=wanted!=carrier;
                music.clip=wanted;music.volume=0;music.Play();
            }
        }
        void OnDestroy()
        {
            if(bossMusic!=null)Destroy(bossMusic);if(lobbyMusic!=null)Destroy(lobbyMusic);
            foreach(var c in designed.Values)if(c)Destroy(c);designed.Clear();
            foreach(var c in sfxClips.Values)if(c)Destroy(c);sfxClips.Clear();
        }
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
        public void Play(string sound)
        {
            if(!fx)return;
            float now=Time.unscaledTime;
            if(lastPlayed.TryGetValue(sound,out float previous)&&now-previous<.035f)return;
            lastPlayed[sound]=now;
            var sfx=Resolve(sound);
            if(sfx!=null&&PlaySfx(sfx))return;
            if(NewEvent(sound))return;
            if(!designed.TryGetValue(sound,out var clip))
            {
                // This vocabulary is authored game events, never user or network strings.
                if(designed.Count>=96)return;
                var samples=CombatSoundDesign.Render(sound,rate);
                clip=AudioClip.Create("Combat "+sound,samples.Length,1,rate,false);clip.SetData(samples,0);designed[sound]=clip;
            }
            fx.PlayOneShot(clip,sound=="cue"?1f:sound=="parry"?.65f:.85f);
        }
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
