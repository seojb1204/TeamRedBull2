#if !HEADLESS && !DESKTOP_PREVIEW
using System;
using System.Collections.Generic;
using UnityEngine;
namespace ReversalTower
{
    // Read-only presentation of the existing simulation. Never writes combat fields or creates hitboxes.
    public sealed class ArakusVisuals : MonoBehaviour
    {
        static readonly Dictionary<string,Sprite[]> cache=new Dictionary<string,Sprite[]>();
        public static Sprite[] Sprites(string id)
        {
            if(cache.TryGetValue(id,out var a))return a;
            a=Resources.LoadAll<Sprite>("ArakusV1/"+id);Array.Sort(a,(x,y)=>string.CompareOrdinal(x.name,y.name));
            if(a.Length>0)cache[id]=a;return a;
        }
        public static bool HasArt=>Sprites("Idle").Length==4;
        public static void ClearCache()=>cache.Clear();
        sealed class Effect {public string id;public Vector2 pos;public float start,duration;public int face;}
        readonly List<Effect> effects=new List<Effect>();
        readonly List<SpriteRenderer> pool=new List<SpriteRenderer>();
        readonly Dictionary<TowerGame.Shot,bool> previousShots=new Dictionary<TowerGame.Shot,bool>();
        readonly List<TowerGame.Shot> stale=new List<TowerGame.Shot>();
        Pattern previousPattern;float previousAge=-1,lastClock=-1,poseStarted,pillarLeftAt=-99,pillarRightAt=-99;
        bool initialized,lastBattle,left,right,lastRage,lastStruck,pillarStun;
        string poseState;int cursor,leftImpactFace=1,rightImpactFace=1;float clock;
        public bool PillarStun=>pillarStun;
        public int VisibleSprites{get{int n=0;foreach(var r in pool)if(r.enabled)n++;return n;}}
        void OnDisable(){foreach(var r in pool)if(r)r.enabled=false;effects.Clear();previousShots.Clear();initialized=false;}
        public ArakusAnimationSelection.Pose Sample(TowerGame g,bool battle)
        {
            bool combat=battle&&g.mode==ScreenMode.Battle;
            clock=combat?g.seconds:g.realClock;
            bool restart=!initialized||battle!=lastBattle||clock<lastClock;
            if(restart){effects.Clear();previousShots.Clear();left=g.pillarLeft;right=g.pillarRight;lastRage=g.BossPatterns.rageActive;pillarStun=false;pillarLeftAt=pillarRightAt=-99;poseState=null;previousPattern=null;previousAge=-1;lastStruck=false;}
            initialized=true;lastBattle=battle;lastClock=clock;
            if(combat)
            {
                if(left&&!g.pillarLeft){pillarLeftAt=clock;leftImpactFace=g.bossFace;pillarStun=true;Emit("FX_PillarImpact",new Vector2(-4,0),g.bossFace);}
                if(right&&!g.pillarRight){pillarRightAt=clock;rightImpactFace=g.bossFace;pillarStun=true;Emit("FX_PillarImpact",new Vector2(4,0),g.bossFace);}
                if(g.bossStagger<=0)pillarStun=false;
                Observe(g);
            }
            left=g.pillarLeft;right=g.pillarRight;
            var bp=g.BossPatterns;
            var i=new ArakusAnimationSelection.Input{combat=combat,dead=battle&&g.bossHp<=0,groggy=pillarStun,
                shieldOut=bp.shieldOut,shieldCaught=bp.shieldCaught,leaping=bp.leaping,struck=bp.struck,
                walking=g.bossState=="추적",phase=g.bossState=="페이즈 전환",pattern=g.current!=null?g.current.id:null,
                age=g.patternAge,windup=g.windup,recovery=g.recovery,tempo=bp.tempo,leapStart=bp.leapStart,
                stagger=Mathf.Max(g.bossStagger,g.bossState=="페이즈 전환"?g.rest:0),hurt=g.bossFlash,step=g.attackStep};
            string state=ArakusAnimationSelection.State(i);
            if(state!=poseState||restart||g.current!=previousPattern||g.patternAge<previousAge){poseState=state;poseStarted=clock;}
            var pose=ArakusAnimationSelection.Sample(i,clock-poseStarted);
            previousPattern=g.current;previousAge=g.patternAge;return pose;
        }
        void Emit(string id,Vector2 pos,int face,float duration=.6f)
        {if(effects.Count>=32)effects.RemoveAt(0);effects.Add(new Effect{id=id,pos=pos,face=face,start=clock,duration=duration});}
        void Observe(TowerGame g)
        {
            var bp=g.BossPatterns;var p=g.current;
            bool newPattern=p!=previousPattern||g.patternAge<previousAge;
            float before=newPattern?-1:previousAge,t=g.patternAge,w=g.windup,T=1/Mathf.Max(.01f,bp.tempo);
            if(p!=null)
            {
                if(before<w&&t>=w)
                {
                    switch(p.id)
                    {
                        case "slash":Emit("FX_SwordAttack",g.boss+new Vector2(g.bossFace*.7f,.25f),g.bossFace);break;
                        case "shield":Emit("FX_ShieldAttack",g.boss+new Vector2(g.bossFace*.8f,0),g.bossFace);break;
                        case "cleave":Emit("FX_SwordSwing",g.boss+new Vector2(g.bossFace*.7f,.5f),g.bossFace);Emit("FX_ArmWind",g.boss+Vector2.up*.5f,g.bossFace);break;
                        case "step":Emit("FX_Dirt",g.boss,-g.bossFace);break;
                        case "charge":Emit("FX_Charge",g.boss,-g.bossFace);break;
                        case "sweep":Emit("FX_WideHorizontal",g.boss,g.bossFace,1.5f);break;
                    }
                }
                if(p.id=="sweep"&&before<w+T&&t>=w+T)Emit("FX_WideVertical",g.boss+new Vector2(g.bossFace*.7f,0),g.bossFace,1.5f);
                if(bp.struck&&(newPattern||!lastStruck))
                {
                    if(p.id=="throw")Emit("FX_SwordAttack",g.boss+new Vector2(g.bossFace*.7f,.25f),g.bossFace);
                    if(p.id=="slam")Emit("FX_LeapingSlam",g.boss,g.bossFace);
                }
            }
            if(bp.rageActive&&!lastRage)Emit("FX_WrathRing",g.boss,g.bossFace);
            lastRage=bp.rageActive;lastStruck=bp.struck;
            foreach(var s in g.shots)if(s.returns)
            {
                bool had=previousShots.TryGetValue(s,out bool hit);
                if((s.hitDone||s.friendly)&&(!had||!hit))Emit("FX_ShieldThrow",s.pos,g.bossFace);
                previousShots[s]=s.hitDone||s.friendly;
            }
            stale.Clear();foreach(var s in previousShots.Keys)if(!g.shots.Contains(s))stale.Add(s);foreach(var s in stale)previousShots.Remove(s);
        }
        void Draw(string id,int frame,Vector2 pos,int face,ActorView owner,Color? color=null,int order=10)
        {
            var sprites=Sprites(id);if(sprites.Length==0)return;
            if(cursor==pool.Count){var child=new GameObject("Arakus sprite effect "+cursor);child.transform.SetParent(transform,false);pool.Add(child.AddComponent<SpriteRenderer>());}
            var r=pool[cursor++];r.enabled=true;r.sprite=sprites[Mathf.Clamp(frame,0,sprites.Length-1)];r.sharedMaterial=owner.body.sharedMaterial;
            r.sortingLayerID=owner.body.sortingLayerID;r.sortingOrder=owner.body.sortingOrder+order;r.color=color??Color.white;
            r.transform.position=new Vector3(pos.x,pos.y,0);r.transform.localScale=new Vector3(face*(transform.localScale.x<0?-1:1),1,1);r.transform.localRotation=Quaternion.identity;
        }
        void Loop(string id,Vector2 pos,int face,ActorView owner,int order=10)
        {Draw(id,ArakusAnimationSelection.NativeFrame(id,clock,true),pos,face,owner,null,order);}
        public void Render(TowerGame g,bool battle,ActorView owner)
        {
            cursor=0;
            if(battle)
            {
                if(g.pillarLeft)Draw("Pillar",0,new Vector2(-4,0),1,owner,null,-30);
                else if(clock-pillarLeftAt<.6f)Draw("Pillar",1,new Vector2(-4,0),leftImpactFace,owner,new Color(1,1,1,Mathf.Clamp01(1-(clock-pillarLeftAt)/.6f)),-30);
                if(g.pillarRight)Draw("Pillar",0,new Vector2(4,0),1,owner,null,-30);
                else if(clock-pillarRightAt<.6f)Draw("Pillar",1,new Vector2(4,0),rightImpactFace,owner,new Color(1,1,1,Mathf.Clamp01(1-(clock-pillarRightAt)/.6f)),-30);
                foreach(var s in g.shots)if(s.returns)Draw("Shield_Spin",ArakusAnimationSelection.NativeFrame("Shield_Spin",clock,true),s.pos,1,owner,s.friendly?Palette.Teal:Color.white);
                if(g.bossHp<=0&&!g.BossPatterns.shieldOut&&poseState=="Death")
                {
                    float drop=clock-poseStarted-ArakusAnimationSelection.Find("Death").Start(2);
                    if(drop>=0)Draw("Shield_Spin",drop<.2f?((int)(drop/.05f)%4):0,g.boss+new Vector2(g.bossFace*.65f,Mathf.Lerp(1.1f,.45f,Mathf.Clamp01(drop/.2f))),1,owner);
                }
                var p=g.current;
                if(g.bossHp>0&&p!=null)
                {
                    if(p.id=="rage"&&g.patternAge<g.windup)Loop("FX_WrathWhite",g.boss,1,owner,-5);
                    if(p.id=="sweep"&&g.patternAge<g.windup)Loop("FX_BladeCharge",g.boss+new Vector2(g.bossFace*.4f,.6f),g.bossFace,owner);
                    if(p.id=="rush"&&g.patternAge>=g.windup&&!g.BossPatterns.struck)Loop("FX_ShieldDash",g.boss,-g.bossFace,owner,-5);
                }
                if(g.bossHp>0&&g.BossPatterns.rageActive)Loop("FX_WrathRed",g.boss,1,owner,-5);
                for(int n=effects.Count-1;n>=0;n--){var e=effects[n];float age=clock-e.start;if(age<0||age>=e.duration){effects.RemoveAt(n);continue;}var d=ArakusAnimationSelection.Find(e.id);Draw(e.id,ArakusAnimationSelection.NativeFrame(e.id,age/e.duration*d.Duration),e.pos,e.face,owner);}
            }
            for(int n=cursor;n<pool.Count;n++)pool[n].enabled=false;
        }
    }
}
#endif
