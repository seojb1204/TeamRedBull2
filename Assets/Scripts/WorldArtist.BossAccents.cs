using System.Collections.Generic;
using UnityEngine;
namespace ReversalTower
{
    public sealed partial class WorldArtist
    {
        sealed class Accent {public Vector2 position;public float at,size;public int boss,variant;}
        readonly List<Accent> accents=new List<Accent>();
        readonly HashSet<TowerGame.Hazard> activeHazards=new HashSet<TowerGame.Hazard>();
        readonly List<TowerGame.Hazard> expiredHazards=new List<TowerGame.Hazard>();
        Pattern accentPattern;int accentStep,accentBoss=-1;float accentClock=-1;bool accentStruck;
        void AddAccent(Vector2 p,float size,string id)
        {
            if(accents.Count>=12)accents.RemoveAt(0);
            int variant=0;foreach(char c in id??string.Empty)variant=(variant*31+c)&255;
            accents.Add(new Accent{position=p,at=g.seconds,size=Mathf.Clamp(size,.3f,1.6f),boss=g.bossIndex,variant=variant});
        }
        // Cosmetic events observe the simulation. They never change timings, bounds, colors of
        // warning/unsafe zones, blue parry stars, movement, damage, or the DDA combat statistics. RGB evaluation builds must remain frozen.
        void BossPatternAccents()
        {
            if(g.mode!=ScreenMode.Battle){accents.Clear();activeHazards.Clear();accentClock=-1;return;}
            if(accentClock<0||g.seconds<accentClock||g.bossIndex!=accentBoss)
            {accents.Clear();activeHazards.Clear();accentPattern=g.current;accentStep=g.attackStep;accentStruck=g.BossPatterns.struck;}
            bool next=g.current!=accentPattern;
            if(next){accentStep=0;accentStruck=false;}
            if(g.current!=null&&g.patternAge>=g.windup&&g.attackStep>accentStep)
            {
                // This is a weapon strike or casting discharge, NOT the impact of delayed hazards.
                float size=g.current.kind==AttackKind.Slash?.8f:.5f;
                AddAccent(g.boss+new Vector2(g.bossFace*.75f,1.25f),size,g.current.id);
            }
            if(g.current!=null&&g.BossPatterns.struck&&!accentStruck&&g.current.id=="slam")
                AddAccent(g.boss+Vector2.up*.12f,1.5f,"slam-impact");
            foreach(var h in g.hazards)
                if(h.delay<=0&&!activeHazards.Contains(h))
                {activeHazards.Add(h);AddAccent(new Vector2(h.x,.12f),Mathf.Min(1.6f,h.width*.5f),h.pattern);}
            expiredHazards.Clear();foreach(var h in activeHazards)if(!g.hazards.Contains(h))expiredHazards.Add(h);
            foreach(var h in expiredHazards)activeHazards.Remove(h);
            accentPattern=g.current;accentStep=g.attackStep;accentStruck=g.BossPatterns.struck;accentClock=g.seconds;accentBoss=g.bossIndex;
            for(int k=accents.Count-1;k>=0;k--)
            {
                var a=accents[k];float u=(g.seconds-a.at)/.28f;
                if(u>=1){accents.RemoveAt(k);continue;}
                float alpha=(1-u)*.5f,r=a.size*(.3f+.8f*u),rot=(a.variant%8)*Mathf.PI/16;
                Color c=Palette.Alpha(a.boss==0?Palette.Gold:a.boss==1?new Color(.95f,.82f,.55f):a.boss==2?new Color(.7f,.55f,.45f):a.boss==3?Palette.White:Palette.Purple,alpha);
                Vector2 p=a.position;
                if(a.boss==0) // Bronze flakes: short directional metal sparks.
                    for(int j=0;j<5;j++){float angle=rot+j*1.25f;var d=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));Line(p+d*r*.4f,p+d*r,.035f*(1-u),c);}
                else if(a.boss==1) // Observatory: thin crystalline diamond, no blue parry coloring.
                    for(int j=0;j<4;j++){float x=rot+j*Mathf.PI*.5f,y=x+Mathf.PI*.5f;Line(p+new Vector2(Mathf.Cos(x),Mathf.Sin(x))*r,p+new Vector2(Mathf.Cos(y),Mathf.Sin(y))*r,.025f,c);}
                else if(a.boss==2) // Judgment: broken chain links disperse rather than fill the hit region.
                    for(int j=0;j<4;j++){float x=p.x+(j-1.5f)*r*.45f;Line(new Vector2(x-.07f,p.y-r*.2f),new Vector2(x+.07f,p.y+r*.2f),.035f,c);Line(new Vector2(x+.07f,p.y-r*.2f),new Vector2(x+.16f,p.y+r*.1f),.025f,c);}
                else if(a.boss==3) // Sky forge: branched angular discharge.
                    for(int j=-1;j<=1;j++){var q=p+new Vector2(j*r*.6f,r*.35f);Line(p,q,.03f,c);Line(q,q+new Vector2(-j*r*.25f,r*.45f),.02f,c);}
                else // Abyss: offset double crescents, deliberately unlike the parry star.
                    for(int j=0;j<7;j++){float angle=rot+j*.35f;var d=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));Line(p+d*r,p+new Vector2(Mathf.Cos(angle+.25f),Mathf.Sin(angle+.25f))*r,.025f,c);Line(p+d*r*.7f+Vector2.right*.1f,p+new Vector2(Mathf.Cos(angle+.25f),Mathf.Sin(angle+.25f))*r*.7f+Vector2.right*.1f,.02f,c);}
            }
        }
    }
}
