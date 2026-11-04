using System;
using ReversalTower;

public static class PlayerAnimationTests
{
    static int passed;
    static void Check(bool result,string title){if(!result)throw new Exception(title);passed++;Console.WriteLine("PASS "+title);}
    static PlayerAnimationSelection.Input Ready()=>new PlayerAnimationSelection.Input{combat=true,parryAge=99,equippedUltimate=-1};
    static int Frame(PlayerAnimationSelection.Pose pose)
    {
        var d=PlayerAnimationSelection.Find(pose.state);float time=pose.normalized*d.Duration;int frame=0;
        for(int i=1;i<d.Count;i++)if(time>=d.FrameStart(i))frame=i;return frame;
    }
    public static int Main()
    {
        try
        {
            var input=Ready();Check(PlayerAnimationSelection.State(input)=="Idle","rest uses idle");
            input.horizontalSpeed=-5.5f;Check(PlayerAnimationSelection.State(input)=="Run","both movement directions use walk art");
            input.height=1;input.verticalSpeed=2;Check(PlayerAnimationSelection.State(input)=="Jump","ascending uses jump");
            input.verticalSpeed=-2;Check(PlayerAnimationSelection.State(input)=="Fall","descending uses fall");
            input.height=0;input.horizontalSpeed=0;Check(PlayerAnimationSelection.State(input)=="Idle","grounded negative velocity is not a falling pose");
            foreach(string action in new[]{"J","Q","W","E"})
            {
                input=Ready();input.action=action;input.actionRemaining=.1f;input.actionTotal=1;
                Check(PlayerAnimationSelection.State(input)==(action=="J"?"Attack":"Skill"+action),action+" selects distinct art");
            }
            string[] ultimates={"UltimateBasic","UltimateArakus","UltimatePhanes","UltimateAdikus","UltimateAethelos","UltimateBasic"};
            for(int i=-1;i<=4;i++){input=Ready();input.action="R";input.actionRemaining=.6f;input.actionTotal=.85f;input.equippedUltimate=i;Check(PlayerAnimationSelection.State(input)==ultimates[i+1],"ultimate visual slot "+i);}
            input=Ready();input.action="F";input.actionRemaining=.4f;Check(PlayerAnimationSelection.State(input)=="Idle","potion retains existing idle visual");
            input=Ready();input.parryAge=.1f;Check(PlayerAnimationSelection.State(input)=="Parry","active parry uses parry art");
            input.parryAge=99;input.action="parry";input.parryRemaining=.1f;Check(PlayerAnimationSelection.State(input)=="Parry","successful parry retains its recovery visual");
            input.dashRemaining=.15f;Check(PlayerAnimationSelection.State(input)=="Dash","dash cancels parry recovery visually");
            input.hurt=.1f;Check(PlayerAnimationSelection.State(input)=="Hit","hurt interrupts dash art");
            input.dead=true;input.combat=false;Check(PlayerAnimationSelection.State(input)=="Death","death wins even on result screen");
            input.dead=false;Check(PlayerAnimationSelection.State(input)=="Idle","victory result does not freeze leftover attack art");
            foreach(var d in PlayerAnimationSelection.Definitions)
            {
                int sum=0;foreach(int ms in d.milliseconds){Check(ms>0,d.state+" positive frame duration");sum+=ms;}
                Check(Math.Abs(d.Duration-sum/1000f)<.0001f,d.state+" complete duration");
                if(d.impactFrame<0)continue;
                input=Ready();input.action=d.state=="Attack"?"J":d.state=="SkillQ"?"Q":d.state=="SkillW"?"W":d.state=="SkillE"?"E":"R";
                input.actionTotal=input.action=="J"?.35f:input.action=="Q"?.7f:input.action=="W"?.9f:input.action=="E"?1.3f:.85f;
                input.equippedUltimate=d.state=="UltimateArakus"?0:d.state=="UltimatePhanes"?1:d.state=="UltimateAdikus"?2:d.state=="UltimateAethelos"?3:-1;
                float hit=input.action=="J"?.15f:input.action=="Q"?.2f:input.action=="W"?.35f:input.action=="E"?.7f:.25f;
                input.actionRemaining=input.actionTotal-hit;var pose=PlayerAnimationSelection.Sample(input,0);
                Check(pose.state==d.state&&Frame(pose)==d.impactFrame,d.state+" shows strike pose at unchanged combat hit time");
                input.actionRemaining+=.002f;Check(Frame(PlayerAnimationSelection.Sample(input,0))<d.impactFrame,d.state+" does not show strike pose before damage time");
            }
            input=Ready();input.dead=true;Check(Frame(PlayerAnimationSelection.Sample(input,10))==6,"death holds final lying pose");
            input=Ready();input.height=2;input.verticalSpeed=1;Check(Frame(PlayerAnimationSelection.Sample(input,10))==2,"jump holds apex pose");
            input.verticalSpeed=-1;var falling=PlayerAnimationSelection.Sample(input,1);Check(falling.normalized>=0&&falling.normalized<1,"fall loops in range");
            input=Ready();input.dashRemaining=.22f;Check(Frame(PlayerAnimationSelection.Sample(input,0))==0,"dash starts at first pose");
            input.action="E";input.dashRemaining=0;input.actionRemaining=.1f;input.actionTotal=0;
            var boundary=PlayerAnimationSelection.Sample(input,-10);Check(!float.IsNaN(boundary.normalized)&&boundary.normalized>=0&&boundary.normalized<1,"invalid duration and negative age remain finite");
            Check(PlayerAnimationSelection.Definitions.Length==17,"all 17 supplied clips are mapped");
            Console.WriteLine("PLAYER_ANIMATION_TESTS_PASSED "+passed);return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
