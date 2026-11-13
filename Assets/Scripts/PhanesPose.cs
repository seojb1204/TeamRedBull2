using System;
namespace ReversalTower
{
    // Presentation only. The six patterns in main remain the gameplay authority.
    public static class PhanesPose
    {
        public struct Frame {public string sheet;public int index,rows;public float bodyPixels,baseline;public Frame(string s,int i){sheet=s;index=i;rows=s=="idle-cast"?2:4;bodyPixels=rows==2?395:300;baseline=rows==2?470:335;}}
        public static Frame Sample(string pattern,float age,float windup,float recovery,float elapsed,bool dead,bool stagger,bool hurt,bool barrier,bool phase,int downStage=0,float downAge=0)
        {
            if(dead)return new Frame("basic",12+Math.Min(3,(int)(Math.Max(0,elapsed)/.25f)));
            if(downStage==1)return new Frame("basic",downAge<.12f?8:9);
            if(downStage==2)return new Frame("basic",10);
            if(downStage==3)return new Frame("basic",11);
            if(downStage==4)return new Frame("idle-cast",0);
            if(stagger)return new Frame("basic",6);
            if(hurt)return new Frame("basic",5+Math.Min(2,(int)(Math.Max(0,elapsed)/.08f)));
            if(barrier||phase)return new Frame("spells-b",barrier?11:8+Math.Min(3,(int)(Math.Max(0,elapsed)/.3f)));
            if(string.IsNullOrEmpty(pattern))return new Frame("idle-cast",(int)(Math.Max(0,elapsed)/.25f)%4);
            int f=age<windup?Math.Min(1,(int)(Math.Max(0,age)/Math.Max(.001f,windup)*2)):2+Math.Min(1,(int)(Math.Max(0,age-windup)/Math.Max(.001f,recovery)*2));
            switch(pattern){case "orb":return new Frame("spells-a",4+f);case "fan":return new Frame("spells-b",8+f);case "beam":return new Frame("spells-a",8+f);case "rain":return new Frame("spells-b",4+f);case "barrier":return new Frame("spells-b",8+f);case "pulse":return new Frame("spells-a",12+f);default:return new Frame("idle-cast",0);}
        }
    }
}
