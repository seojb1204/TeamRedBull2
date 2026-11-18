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
            new Definition("NormalAttack_R","캐릭터",192,false,250,350,450,100,150,250,250,300),
            new Definition("NormalAttack_L","캐릭터",192,false,200,250,400,120,200,250,230,250),
            new Definition("NormalAttack_F","캐릭터",192,false,200,200,300,100,90,90,100,120),
            new Definition("QuickStep","캐릭터",192,false,100,60,70,80,90,100,100,150),
            new Definition("Sword_Swinging","캐릭터",192,false,150,200,250,250,120,250,180,200),
            new Definition("ShieldThrowing_DashAttack","캐릭터",192,false,200,200,200,200,150,250,200,120,180,200),
            new Definition("ShieldThrowing_DashAttack_Caught","캐릭터",192,false,200,200,200,200,150,250,200,120,180,200),
            new Definition("ShieldDash","캐릭터",192,false,200,300,200,200,300,300,300,350),
            new Definition("Shield_Throwing_Fly_Attack","캐릭터",192,false,200,200,200,750,150,100,100,150,150,150,200,250),
            new Definition("Aggressive_Attack","캐릭터",192,false,300,350,350,100,200,300,400,100,100,200,200,200),
            new Definition("Of_Wrath","캐릭터",192,false,400,600,750,750,200,800,750,750),
            new Definition("Charge","캐릭터",192,false,300,350,450,150,300,300,350,400),
            new Definition("Walk","캐릭터",192,true,100,100,100,100,100,100,100,100),
            new Definition("Stagger","캐릭터",192,false,150,300,1750,300),
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
        // Presentation only. Ground-contact centroids measured from the approved eight walk frames.
        public sealed class WalkCycle
        {
            public const float BodyScale=ArakusGeometry.VisualScale*88f/124f;
            public const float Stride=103.5f*BodyScale/56f;
            // Switch near the midpoint between measured foot-contact poses.
            static readonly float[] contactTravelPixels={12.25f,18f,13f,8.5f,10f,17.75f,17f,7f};
            float previousX,previousTime,travel;
            bool ready;
            int facing;
            public bool Moving { get; private set; }
            public float Elapsed { get; private set; }
            public void Reset(){ready=false;travel=Elapsed=0;Moving=false;facing=0;}
            public void Observe(float x,float time,bool eligible,int direction=0)
            {
                if(!ready||time<previousTime){Reset();previousX=x;previousTime=time;ready=true;return;}
                float dt=time-previousTime;
                if(dt<=0)return; // Hit-stop and repeated renders keep the current pose.
                float signedDx=x-previousX;
                float dx=Math.Abs(signedDx);
                if(direction==0)direction=Math.Sign(signedDx);
                bool turning=facing!=0&&direction!=0&&facing!=direction;
                Moving=eligible&&signedDx*direction>0.0001f&&dx<=Math.Max(.5f,dt*12);
                if(!Moving||turning)travel=0;
                if(Moving&&!turning)travel=(travel+dx)%Stride;
                if(direction!=0)facing=direction;
                previousX=x;previousTime=time;
                float pixels=travel*56f/BodyScale;
                Elapsed=0;
                for(int frame=0;frame<contactTravelPixels.Length;frame++)
                {
                    float span=contactTravelPixels[frame];
                    if(pixels<span){Elapsed=(frame+pixels/span)*.1f;break;}
                    pixels-=span;
                }
            }
        }
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
                case "cleave":return "Sword_Swinging";case "throw":return i.shieldCaught?"ShieldThrowing_DashAttack_Caught":"ShieldThrowing_DashAttack";
                case "slam":return "Shield_Throwing_Fly_Attack";case "rush":return "ShieldDash";
                case "charge":return "Charge";case "sweep":return "Aggressive_Attack";
                case "step":return "QuickStep";case "rage":return "Of_Wrath";
            }
            if(i.hurt>0)return "Stagger";
            return i.walking?"Walk":"Idle";
        }
        static int Segment(float u,int a,int b)=>Math.Min(b,a+(int)(Clamp(u)*(b-a+1)));
        public static Pose Sample(Input i,float elapsed)
        {
            string state=State(i);float t=i.age,w=Math.Max(.001f,i.windup),r=Math.Max(.001f,i.recovery),T=1/Tempo(i);int f=0;
            if(state=="Death")
            {
                int first=i.shieldOut?2:0;
                float start=Find(state).Start(first);
                return new Pose(state,Math.Max(first,NativeFrame(state,start+Math.Max(0,elapsed))));
            }
            if(state=="Idle"||state=="Walk")return new Pose(state,NativeFrame(state,elapsed,true));
            if(state=="Stagger")return new Pose(state,Math.Min(3,NativeFrame(state,elapsed)));
            if(state=="Groggy")return new Pose(state,elapsed<.15f?0:elapsed<.5f?1:2+(int)((elapsed-.5f)/.35f)%2);
            if(EmptyHandFallback(i))return new Pose("ShieldThrowing_DashAttack",4);
            if(state=="ShieldThrowing_DashAttack"||state=="ShieldThrowing_DashAttack_Caught")
            {
                if(t<w)f=Segment(t/w,0,2);
                else if(t<w+.2f*T)f=3;
                else if(!i.struck)f=Segment((t-w-.2f*T)/(.6f*T),4,6);
                else if(t<w+.8f*T+.12f*T)f=7;
                else if(t<w+.8f*T+.25f*T)f=8;
                else f=i.shieldCaught||!i.shieldOut?9:8;
            }
            else if(state=="Shield_Throwing_Fly_Attack")
            {
                if(t<w)f=Segment(t/w,0,1);
                else if(i.struck)f=Segment((t-i.leapStart)/r,9,11);
                else if(i.leaping)f=Segment((t-i.leapStart)/(.5f*T),6,8);
                else f=i.shieldCaught?5:2+(int)((t-w)/(.2f*T))%2;
            }
            else if(state=="Of_Wrath")f=t<w?Segment(t/w,0,3):t<w+.12f*T?4:Segment((t-w-.12f*T)/Math.Max(.001f,r-.12f*T),5,7);
            else if(state=="QuickStep")f=t<w?Segment(t/w,0,1):t<w+.3f*T?Segment((t-w)/(.3f*T),2,5):Segment((t-w-.3f*T)/r,6,7);
            else if(state=="ShieldDash")f=t<w?Segment(t/w,0,1):t<w+ArakusPatternTiming.ShieldRushMoveDuration*T?2+(int)((t-w)/(.12f*T))%4:Segment((t-w-ArakusPatternTiming.ShieldRushMoveDuration*T)/r,6,7);
            else if(state=="Charge")f=t<w?Segment(t/w,0,1):i.step<2?2+(int)((t-w)/(.1f*T))%4:Segment((t-w-1.1f*T)/r,6,7);
            else if(state=="Aggressive_Attack")f=t<w?Segment(t/w,0,2):t<w+.3f*T?Segment((t-w)/(.3f*T),3,5):t<w+T?Segment((t-w-.3f*T)/(.7f*T),6,7):t<w+1.3f*T?Segment((t-w-T)/(.3f*T),8,9):Segment((t-w-1.3f*T)/Math.Max(.001f,r-.3f*T),10,11);
            else
            {
                int impact=4; // reviewed contact: sword downstroke, shield impact, kick extension
                if(state=="NormalAttack_R")impact=3;
                if(t<w)f=Segment(t/w,0,impact-1);
                else if(t<w+.08f*T)f=impact;
                else f=Segment((t-w-.08f*T)/Math.Max(.001f,r-.08f*T),impact+1,Find(state).ms.Length-1);
            }
            return new Pose(state,f);
        }
    }
}
