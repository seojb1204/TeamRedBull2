using System;
namespace ReversalTower
{
    // Visual-only mapping. Attack timers, damage, movement and recovery remain owned by BossPatternSystem.
    public static class ArakusAnimationSelection
    {
        public sealed class Definition
        {
            public readonly string id,group;public readonly int size;public readonly bool loop;public readonly int[] ms;
            public Definition(string id,string group,int size,bool loop,params int[] ms){this.id=id;this.group=group;this.size=size;this.loop=loop;this.ms=ms;}
            public float Duration{get{int n=0;foreach(var t in ms)n+=t;return n/1000f;}}
            public float Start(int frame){int n=0;for(int i=0;i<frame;i++)n+=ms[i];return n/1000f;}
            public float Normalized(int frame)=>Math.Min(.999f,(Start(frame)+ms[frame]/2000f)/Duration);
        }
        public static readonly Definition[] Definitions={
            new Definition("Idle","캐릭터",192,true,300,300,300,300),
            new Definition("NormalAttack_R","캐릭터",192,false,600,450,250,400,400),
            new Definition("NormalAttack_L","캐릭터",192,false,500,350,250,400,400),
            new Definition("NormalAttack_F","캐릭터",192,false,600,250,150,200),
            new Definition("QuickStep","캐릭터",192,false,150,200,150,200,250),
            new Definition("Sword_Swinging","캐릭터",192,false,350,500,200,300,100,150),
            new Definition("ShieldThrowing_DashAttack","캐릭터",192,false,600,200,200,400,200,200),
            new Definition("ShieldDash","캐릭터",192,false,500,950,950,100,100),
            new Definition("Shield_Throwing_Fly_Attack","캐릭터",192,false,600,150,100,250,250,450),
            new Definition("Aggressive_Attack","캐릭터",192,false,700,300,300,700,500,500),
            new Definition("Of_Wrath","캐릭터",192,false,400,2100,500,2000),
            new Definition("Charge","캐릭터",192,false,1100,150,180,180,400),
            new Definition("Walk","캐릭터",192,true,200,200,200,200),
            new Definition("Stagger","캐릭터",192,false,150,2000,350),
            new Definition("Groggy","캐릭터",192,false,150,350,2250,2250),
            new Definition("Death","캐릭터",192,false,120,200,200,250,400),
            new Definition("Shield_Spin","소품",192,true,90,90,90,90),
            new Definition("Pillar","소품",192,false,1000,1000),
            new Definition("FX_SwordAttack","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_ShieldAttack","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_SwordSwing","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_ArmWind","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_Dirt","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_ShieldThrow","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_ShieldDash","이펙트",384,true,70,70,90,100,120,150),
            new Definition("FX_LeapingSlam","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_WideHorizontal","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_WideVertical","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_BladeCharge","이펙트",384,true,70,70,90,100,120,150),
            new Definition("FX_WrathWhite","이펙트",384,true,70,70,90,100,120,150),
            new Definition("FX_WrathRing","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_WrathRed","이펙트",384,true,70,70,90,100,120,150),
            new Definition("FX_Charge","이펙트",384,false,70,70,90,100,120,150),
            new Definition("FX_PillarImpact","이펙트",384,false,70,70,90,100,120,150),
        };
        public static Definition Find(string id){foreach(var d in Definitions)if(d.id==id)return d;throw new ArgumentException(id);}
        public struct Input
        {
            public bool combat,dead,groggy,shieldOut,shieldCaught,leaping,struck,walking,phase;
            public string pattern;public float age,windup,recovery,tempo,leapStart,stagger,hurt;
            public int step;
        }
        public struct Pose
        {
            public string state;public int frame;public float normalized;
            public Pose(string state,int frame){this.state=state;this.frame=frame;normalized=Find(state).Normalized(frame);}
        }
        static float Clamp(float t)=>Math.Max(0,Math.Min(1,t));
        static float Tempo(Input i)=>Math.Max(.01f,i.tempo);
        public static int NativeFrame(string id,float elapsed,bool loop=false)
        {
            var d=Find(id);float t=Math.Max(0,elapsed);if(loop)t%=d.Duration;int n=0;
            for(int k=0;k<d.ms.Length;k++){n+=d.ms[k];if(t<n/1000f)return k;}return d.ms.Length-1;
        }
        static bool EmptyHandFallback(Input i)=>i.shieldOut&&!i.shieldCaught&&((i.pattern!="throw"&&i.pattern!="slam")||(i.pattern=="slam"&&(i.leaping||i.struck)));
        public static string State(Input i)
        {
            if(i.dead)return "Death";
            if(!i.combat)return "Idle";
            // No empty-hand stagger/idle art was supplied; keep a shield-free pose while the real projectile is away.
            if(EmptyHandFallback(i))return "ShieldThrowing_DashAttack";
            if(i.stagger>0||i.phase)return i.groggy?"Groggy":"Stagger";
            switch(i.pattern)
            {
                case "slash":return "NormalAttack_R";case "shield":return "NormalAttack_L";case "kick":return "NormalAttack_F";
                case "cleave":return "Sword_Swinging";case "throw":return "ShieldThrowing_DashAttack";
                case "slam":return "Shield_Throwing_Fly_Attack";case "rush":return "ShieldDash";
                case "charge":return "Charge";case "sweep":return "Aggressive_Attack";
                case "step":return "QuickStep";case "rage":return "Of_Wrath";
            }
            if(i.hurt>0)return "Stagger";
            return i.walking?"Walk":"Idle";
        }
        public static Pose Sample(Input i,float elapsed)
        {
            string state=State(i);float t=i.age,w=Math.Max(.001f,i.windup),r=Math.Max(.001f,i.recovery),T=1/Tempo(i);int frame=0;
            if(state=="Death"){frame=NativeFrame(state,elapsed);if(i.shieldOut&&!i.shieldCaught)frame=Math.Max(2,frame);return new Pose(state,frame);}
            if(state=="Idle"||state=="Walk")return new Pose(state,NativeFrame(state,elapsed,true));
            if(state=="Stagger")return new Pose(state,Math.Min(2,(int)(Clamp(elapsed/Math.Max(.01f,elapsed+Math.Max(i.stagger,i.hurt)))*3)));
            if(state=="Groggy")return new Pose(state,elapsed<.15f?0:elapsed<.35f?1:2+(int)((elapsed-.35f)/.35f)%2);
            if(EmptyHandFallback(i))return new Pose(state,1);
            if(state=="ShieldThrowing_DashAttack")
            {
                if(t<w)frame=0;else if(t<w+.2f*T)frame=1;
                else if(t<w+.8f*T&&!i.struck)frame=2+(int)((t-w-.2f*T)/(.12f*T))%2;
                else frame=i.shieldCaught||!i.shieldOut?5:4;
            }
            else if(state=="Shield_Throwing_Fly_Attack")
            {
                if(t<w)frame=0;else if(i.struck)frame=5;
                else if(i.leaping){float u=(t-i.leapStart)/T;frame=u<.06f?2:u<.15f?3:4;}
                else frame=i.shieldCaught?2:1;
            }
            else if(state=="Of_Wrath")frame=t<w?(t<Math.Min(.4f*T,w*.4f)?0:1):t<w+.6f*T?2:3;
            else if(state=="QuickStep")frame=t<w?(t<w*.4f?0:1):t<w+.3f*T?2:t<w+.3f*T+r*.5f?3:4;
            else if(state=="ShieldDash")frame=t<w?0:t<w+ArakusPatternTiming.ShieldRushMoveDuration*T?1+(int)((t-w)/(.12f*T))%2:t<w+ArakusPatternTiming.ShieldRushMoveDuration*T+r*.5f?3:4;
            else if(state=="Charge")frame=t<w?0:i.step<2?(t<w+.12f*T?1:2+(int)((t-w)/(.1f*T))%2):4;
            else if(state=="Aggressive_Attack")frame=t<w?(t<w*.7f?0:1):t<w+.3f*T?2:t<w+1*T?3:t<w+1*T+r*.65f?4:5;
            else
            {
                var d=Find(state);const int impact=2;
                if(t<w)frame=Math.Min(impact-1,(int)(Clamp(t/w)*impact));
                else frame=Math.Min(d.ms.Length-1,impact+(int)(Clamp((t-w)/r)*(d.ms.Length-impact)));
                // Cleave's shield stays on the back through recovery; the last pose re-equips it.
            }
            return new Pose(state,frame);
        }
    }
}
