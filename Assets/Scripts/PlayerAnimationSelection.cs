using System;

namespace ReversalTower
{
    // Visual sampling only: combat timers, damage, movement and cooldowns stay in TowerGame.
    public static class PlayerAnimationSelection
    {
        public struct Input
        {
            public bool dead, combat;
            public float hurt, parryAge, parryRemaining, dashRemaining;
            public string action;
            public float actionRemaining, actionTotal, height, verticalSpeed, horizontalSpeed;
            public int equippedUltimate;
        }
        public struct Pose
        {
            public string state;
            public float normalized;
            public Pose(string state, float normalized) { this.state=state; this.normalized=normalized; }
        }
        public sealed class Definition
        {
            public readonly string state, art;
            public readonly int[] milliseconds;
            public readonly bool loop;
            public readonly int impactFrame;
            public int Count => milliseconds.Length;
            public float Duration { get { int sum=0; foreach(int ms in milliseconds)sum+=ms; return sum/1000f; } }
            public Definition(string state,string art,bool loop,int impactFrame,params int[] milliseconds)
            { this.state=state;this.art=art;this.loop=loop;this.impactFrame=impactFrame;this.milliseconds=milliseconds; }
            public float FrameStart(int frame)
            { int sum=0;for(int i=0;i<frame;i++)sum+=milliseconds[i];return sum/1000f; }
        }

        public static readonly Definition[] Definitions = {
            new Definition("Idle","Idle",true,-1,150,150,150,150,150,150,150,150),
            new Definition("Run","Walk",true,-1,100,100,100,100,100,100,100,100),
            new Definition("Attack","Attack",false,4,100,80,60,50,80,70,90,130),
            new Definition("Hit","Hit",false,-1,70,50,50,60,70,90,120,140),
            new Definition("Parry","Parry",false,-1,70,55,100,100),
            new Definition("Dash","Dash",false,-1,70,50,50,100),
            new Definition("Death","Death",false,-1,100,110,100,130,150,150,300),
            new Definition("SkillQ","SkillQ",false,3,90,60,60,65,85,90,130),
            new Definition("SkillW","SkillW",false,3,80,65,65,65,65,65,65,110),
            new Definition("SkillE","SkillE",false,6,110,110,120,80,85,65,100,130,140),
            new Definition("Jump","Jump",false,-1,90,100,180),
            new Definition("Fall","Fall",true,-1,120,120,120),
            new Definition("UltimateBasic","UltimateBasic",false,3,100,100,120,65,85,130),
            new Definition("UltimateArakus","UltimateArakus",false,2,130,140,75,95,100,70,100,140),
            new Definition("UltimatePhanes","UltimatePhanes",false,2,100,120,75,70,80,75,130),
            new Definition("UltimateAdikus","UltimateAdikus",false,2,120,130,70,100,90,140,180,150),
            new Definition("UltimateAethelos","UltimateAethelos",false,4,130,140,180,75,100,140,160,160)
        };
        public static Definition Find(string state)
        { foreach(var d in Definitions)if(d.state==state)return d;throw new ArgumentException("Unknown player animation: "+state); }
        public static string Ultimate(int equipped)
        {
            switch(equipped)
            {
                case 0:return "UltimateArakus";
                case 1:return "UltimatePhanes";
                case 2:return "UltimateAdikus";
                case 3:return "UltimateAethelos";
                // Kharos has no supplied animation; retain its gameplay with the basic visual fallback.
                default:return "UltimateBasic";
            }
        }
        public static string State(Input input)
        {
            if(input.dead)return "Death";
            if(!input.combat)return "Idle";
            if(input.hurt>0)return "Hit";
            if(input.dashRemaining>0)return "Dash";
            if(input.parryAge<.21f||(input.action=="parry"&&input.parryRemaining>0))return "Parry";
            if(input.actionRemaining>0)
            {
                switch(input.action)
                {
                    case "J":return "Attack";
                    case "Q":return "SkillQ";
                    case "W":return "SkillW";
                    case "E":return "SkillE";
                    case "R":return Ultimate(input.equippedUltimate);
                    case "F":return "Idle";
                }
            }
            if(input.height>.001f)return input.verticalSpeed>=0?"Jump":"Fall";
            return Math.Abs(input.horizontalSpeed)>.3f?"Run":"Idle";
        }
        static float Clamp(float value) => Math.Max(0,Math.Min(.9999f,value));
        public static Pose Sample(Input input,float stateAge)
        {
            string state=State(input);var d=Find(state);float age=Math.Max(0,stateAge),u;
            if(state=="Dash")u=1-input.dashRemaining/.22f;
            else if(state=="Hit")u=1-input.hurt/.18f;
            else if(state=="Parry")u=(input.parryAge<.22f?input.parryAge:.22f-input.parryRemaining)/.22f;
            else if(input.combat&&input.actionRemaining>0&&d.impactFrame>=0)
            {
                float hit=input.action=="E"?.7f:input.action=="W"?.35f:input.action=="Q"?.2f:input.action=="R"?.25f:.15f;
                float total=Math.Max(.001f,input.actionTotal),elapsed=Math.Max(0,total-input.actionRemaining);
                hit=Math.Min(hit,Math.Max(.0001f,total-.0001f));
                float anchor=d.FrameStart(d.impactFrame)/d.Duration;
                // Keep the pose at the existing damage time, then use the remaining action for recovery.
                u=elapsed<=hit?elapsed/hit*anchor:anchor+(elapsed-hit)/Math.Max(.0001f,total-hit)*(1-anchor);
                if(Math.Abs(elapsed-hit)<=.00001f)u=anchor+.00001f;
                else if(elapsed>hit)u+=.00001f;
            }
            else if(d.loop)
            {
                float rate=state=="Run"?Math.Max(.1f,Math.Abs(input.horizontalSpeed)/5.5f):1;
                float cycles=age*rate/d.Duration;u=cycles-(float)Math.Floor(cycles);
            }
            else u=age/d.Duration;
            return new Pose(state,Clamp(u));
        }
    }
}
