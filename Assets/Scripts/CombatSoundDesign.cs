using System;
namespace ReversalTower
{
    // Original deterministic synthesis, not recordings or third-party samples.
    // Dry transients, filtered air and damped inharmonic resonances keep cues readable.
    public static class CombatSoundDesign
    {
        public static float[] Render(string key,int rate)
        {
            if(rate<8000||rate>192000)throw new ArgumentOutOfRangeException(nameof(rate));
            uint seed=2166136261;foreach(char c in key)seed=(seed^c)*16777619;
            bool cue=key=="cue",metal=key=="clang"||key=="parry"||key.Contains("Slash")||key.Contains("Shield"),
                 air=key=="dash"||key=="attack"||key.Contains("Dash")||key.Contains("Volley"),
                 heavy=key=="thud"||key=="hit"||key.Contains("landing")||key.Contains("Pillar")||key.Contains("Wave"),
                 spell=key.StartsWith("boss_1")||key.StartsWith("boss_3")||key.Contains("Barrier")||key.Contains("Balance"),
                 dark=key.StartsWith("boss_2")||key.StartsWith("boss_4"),gentle=key=="heal"||key=="confirm";
            double length=cue?.22:gentle?.42:key=="death"?.85:heavy?.43:metal?.38:air?.25:.55;
            var data=new float[(int)(rate*length)];double lp=0,phase=0;
            double baseHz=cue?2093:gentle?440:metal?620:spell?330:dark?110:heavy?78:240;
            // Stable per-pattern resonator voicing, bounded so attack identity is not just louder.
            if(key.StartsWith("boss_"))baseHz*=.9+(seed%25)/100.0;
            for(int i=0;i<data.Length;i++)
            {
                double t=(double)i/rate,u=t/length;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;
                double noise=(seed/(double)uint.MaxValue)*2-1;lp+=.055*(noise-lp);
                double env=Math.Min(1,t*800)*Math.Pow(1-u,2);
                phase+=2*Math.PI*baseHz*(heavy?1-.55*u:air?1-.3*u:1)/rate;
                double v;
                if(cue)v=(Math.Sin(phase)+.25*Math.Sin(phase*1.5))*Math.Exp(-t*20)*.16;
                else if(gentle)v=(Math.Sin(phase)+.4*Math.Sin(phase*1.25)+.25*Math.Sin(phase*1.5))*env*.10;
                else if(metal)v=(Math.Sin(phase)*.3+Math.Sin(phase*2.31)*.22+Math.Sin(phase*3.73)*.12)*Math.Exp(-t*12)+noise*Math.Exp(-t*100)*.2;
                else if(heavy)v=Math.Sin(phase)*Math.Exp(-t*15)*.34+lp*Math.Exp(-t*8)*.7+noise*Math.Exp(-t*80)*.15;
                else if(air)v=(noise-lp)*Math.Sin(Math.PI*u)*env*.20+lp*env*.35;
                else v=(Math.Sin(phase)+.3*Math.Sin(phase*1.49))*env*.16+lp*env*.4;
                if(spell)v+=Math.Sin(phase*3)*env*.06;
                if(dark)v+=Math.Sin(phase*.5)*env*.08;
                double fade=Math.Min(1,t*1000)*Math.Min(1,(length-t)*250);
                data[i]=(float)Math.Max(-.7,Math.Min(.7,v*fade));
            }
            return data;
        }
    }
}
