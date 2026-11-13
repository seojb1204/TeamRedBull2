using System;
namespace ReversalTower
{
    // Presentation-only observer. Never mutates gameplay or consumes randomness.
    public sealed class SkillFxTimeline
    {
        public enum Kind { Landing, Spin, Execution }
        public struct Snapshot
        {
            public bool combat, dead, interrupted, bossStruck;
            public float clock, remaining, total, bossAge;
            public string action, bossPattern;
            public object patternToken;
            public int bossIndex;
        }
        public struct Emission { public Kind kind; public float age; public Emission(Kind k,float a){kind=k;age=a;} }
        public bool ResetThisFrame { get; private set; }
        public bool PlayerActive { get; private set; }
        public string PlayerAction { get; private set; }
        float lastClock=-1,lastElapsed=-1;
        string castAction;
        bool castEmitted,lastStruck;
        object pattern;
        float patternAge=-1;
        public static float Offset(Kind k)=>k==Kind.Spin?.35f-Peak(k):k==Kind.Execution?.7f-Peak(k):0;
        public static float Peak(Kind k)=>k==Kind.Spin?6f/30:k==Kind.Execution?11f/30:0;
        public static float Lifetime(Kind k)=>k==Kind.Landing?1.05f:k==Kind.Spin?.65f:1.2f;
        public static int Frame(float age,int count)=>Math.Max(0,Math.Min(count-1,(int)Math.Floor(Math.Max(0,age)*30+0.0001)));
        public static float Alpha(Kind k,float age)
        {float fade=k==Kind.Landing?.3f:.2f;return Math.Max(0,Math.Min(1,(Lifetime(k)-age)/fade));}
        public void Reset()
        {lastClock=-1;lastElapsed=-1;castAction=null;castEmitted=false;lastStruck=false;pattern=null;patternAge=-1;PlayerActive=false;PlayerAction=null;}
        // At most two cosmetic emissions per snapshot; caller provides reusable storage.
        public int Observe(Snapshot s,Emission[] emissions)
        {
            ResetThisFrame=!s.combat||s.dead||(lastClock>=0&&s.clock<lastClock-.001f);
            if(ResetThisFrame)Reset();
            if(!s.combat||s.dead)return 0;
            lastClock=s.clock;
            int count=0;
            PlayerActive=s.remaining>0&&!s.interrupted&&(s.action=="W"||s.action=="E");
            PlayerAction=PlayerActive?s.action:null;
            if(PlayerActive)
            {
                float elapsed=Math.Max(0,s.total-s.remaining);
                if(castAction!=s.action||elapsed<lastElapsed-.001f){castAction=s.action;castEmitted=false;}
                var kind=s.action=="W"?Kind.Spin:Kind.Execution;
                if(!castEmitted&&elapsed>=Offset(kind)){castEmitted=true;emissions[count++]=new Emission(kind,elapsed-Offset(kind));}
                lastElapsed=elapsed;
            }
            else{castAction=null;lastElapsed=-1;castEmitted=false;}
            bool newPattern=!ReferenceEquals(pattern,s.patternToken)||s.bossAge<patternAge-.001f;
            if(newPattern)lastStruck=false;
            bool landing=s.bossIndex==0&&s.bossPattern=="slam"&&s.bossStruck;
            if(landing&&!lastStruck)emissions[count++]=new Emission(Kind.Landing,0);
            lastStruck=landing;pattern=s.patternToken;patternAge=s.bossAge;
            return count;
        }
    }
}
