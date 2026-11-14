using System;
using System.Collections.Generic;

namespace ReversalTower
{
    // 뒤에 붙인 다섯 종류는 아라쿠스 기획 패턴 전용 (순서를 바꾸면 BossDefinition 에셋의 kind 숫자가 어긋난다).
    public enum AttackKind { Slash, Dash, Wave, Rain, Volley, Pillar, Summon, Balance, Barrier, Echo, Step, ShieldThrow, ShieldRush, Sweep, Rage }
    [Serializable]
    public class Pattern
    {
        public string id, name;
        public AttackKind kind;
        public float windup, recovery, damage, range;
        public int count;
        public bool parryable;
        // 선택 조건 (0 = 예전 규칙). 거리 창 [minRange, maxRange], 전용 쿨타임(초), 가중치, 등장 페이즈(2 = 2페이즈부터, -1 = 무작위 선택 안 함).
        public float minRange, maxRange, cooldown, weight;
        public int minPhase;
        public Pattern(string id, string name, AttackKind kind, float windup, float recovery, float damage, float range, int count, bool parryable)
        { this.id=id; this.name=name; this.kind=kind; this.windup=windup; this.recovery=recovery; this.damage=damage; this.range=range; this.count=count; this.parryable=parryable; }
        public Pattern When(float minRange, float maxRange, float cooldown, float weight, int minPhase=0)
        { this.minRange=minRange; this.maxRange=maxRange; this.cooldown=cooldown; this.weight=weight; this.minPhase=minPhase; return this; }
    }
    [Serializable] public class BossSpec
    {
        public string id, name, title, lesson, ultimate;
        public float hp, speed;
        public float phaseThreshold=.6f,phaseSpeedScale=1.12f,dashSpeed=18,projectileSpeed=8,minionSpeed=1;
        public int minionHp=24;
        public bool airborne,hasPillars,echoBoss,afterSlashHazard;
        public Pattern[] patterns;
        public BossSpec(string id,string name,string title,string lesson,string ultimate,float hp,float speed,params Pattern[] patterns)
        {
            this.id=id;this.name=name;this.title=title;this.lesson=lesson;this.ultimate=ultimate;this.hp=hp;this.speed=speed;this.patterns=patterns;
            airborne=id=="phanes";hasPillars=id=="arakus";echoBoss=id=="kharos";afterSlashHazard=id=="aethelos";
            dashSpeed=hasPillars?13:18;projectileSpeed=hasPillars?7:8;
            if(hasPillars)phaseSpeedScale=1f; // 아라쿠스는 2페이즈에 빨라지지 않고, HP 30% 분노 뒤에 1.5배가 된다 (기획서 4.3)
            minionHp=id=="adicus"?32:id=="kharos"?40:24;
            minionSpeed=echoBoss?1.5f:1;
        }
    }
    public static class Catalog
    {
        static Pattern P(string id,string name,AttackKind kind,float w,float r,float d,float range,int n=1,bool parry=false)
            =>new Pattern(id,name,kind,w,r,d,range,n,parry);
        public static readonly BossSpec[] Bosses={
            // 아라쿠스: 팀 기획서 4.1~4.3의 시간·거리. 판정 거리는 기획 거리 + 몸 너비 0.9m. 피해는 기준 12 × 기획 계수(임시).
            new BossSpec("arakus","아라쿠스","첫 번째 문 · 철의 맹세","파랗게 빛나는 공격은 K로 패링. 돌진은 기둥 뒤로 유도하세요.","철왕의 휩쓸기",780,2.1f,
                P("cleave","거검 휘두르기",AttackKind.Slash,.85f,.75f,18,2.9f,1,true).When(0,2.9f,6,1.5f),
                P("shield","방패 내려찍기",AttackKind.Slash,.85f,1.05f,10,1.9f,1,true).When(0,1.9f,0,2),
                P("kick","발차기",AttackKind.Slash,.8f,.4f,8,1.4f).When(0,1.4f,0,2),
                P("charge","철벽 돌진",AttackKind.Dash,1.1f,.4f,18,24).When(0,99,10,1.2f,2),
                P("throw","방패 투척 후 대쉬",AttackKind.ShieldThrow,.6f,.5f,14,1.9f,1,true).When(3,7,9,1),
                P("slam","방패 던지고 내려찍기",AttackKind.ShieldThrow,.6f,.45f,19,2.4f).When(3,7,9,1),
                P("slash","거검 횡베기",AttackKind.Slash,1.05f,1.05f,12,2.4f).When(0,2.4f,0,3),
                P("rush","방패 돌진",AttackKind.ShieldRush,.5f,.35f,4,1.4f,8).When(1,5,8,1),
                P("sweep","일대 휩쓸기",AttackKind.Sweep,1f,.8f,18,6.9f,2).When(0,6.9f,12,.8f),
                P("step","퀵스텝",AttackKind.Step,.1f,.35f,0,.75f).When(0,99,3,0,-1),
                P("rage","아라쿠스의 분노",AttackKind.Rage,2.5f,2.5f,0,0).When(0,99,0,0,-1)),
            new BossSpec("phanes","파네스","두 번째 문 · 거짓된 광휘","푸른 광탄을 반사하세요. 빛 기둥은 무적 대시로 통과할 수 있습니다.","굴절의 별",930,2.5f,
                P("orb","굴절의 광탄",AttackKind.Volley,1f,.85f,17,20,2,true),
                P("fan","성운의 파편",AttackKind.Volley,.8f,.8f,10,20,5,true),
                P("beam","단죄의 빛 기둥",AttackKind.Pillar,1.05f,1f,21,20,3),
                P("rain","추락하는 별",AttackKind.Rain,1.15f,.85f,17,20,4),
                P("barrier","단절의 결계",AttackKind.Barrier,1.3f,1.3f,25,20),
                P("pulse","천공의 파동",AttackKind.Wave,.95f,.85f,18,20,2)),
            new BossSpec("adicus","아디쿠스","세 번째 문 · 눈먼 심판","죄수와 망자를 정리하고, 심판 때 빛나는 저울 쪽으로 이동하세요.","심판의 낫",1100,2.3f,
                P("scythe","심판의 낫",AttackKind.Slash,.85f,.85f,20,3.8f,1,true),
                P("cross","X자 베기",AttackKind.Slash,.75f,.9f,12,3.3f,2,true),
                P("chain","사슬낫 포획",AttackKind.Volley,.9f,.9f,17,20,1,true),
                P("fall","낫 낙하",AttackKind.Rain,1.1f,.85f,17,20,4),
                P("prisoner","죄수 소환",AttackKind.Summon,1f,.8f,10,20,3),
                P("judgement","심판의 저울",AttackKind.Balance,2.4f,1.3f,26,20),
                P("spin","낫 전체회전",AttackKind.Slash,1f,1.2f,9,4.3f,5,true)),
            new BossSpec("aethelos","에텔로스","탑의 정상 · 창조자의 몰락","연격을 패링하고, 남겨진 번개를 피하세요. 공격 뒤 빈틈을 노리세요.","천공의 심판",1320,3f,
                P("blade","빛의 연격",AttackKind.Slash,.7f,.8f,12,3.5f,3,true),
                P("lance","천공의 창",AttackKind.Volley,.8f,.7f,15,20,3,true),
                P("rift","공간 가르기",AttackKind.Dash,.95f,.95f,24,20,1,true),
                P("thunder","벼락 꽂기",AttackKind.Rain,.95f,.8f,18,20,5),
                P("creation","빛의 무기",AttackKind.Pillar,1.1f,1f,19,20,4),
                P("heaven","하늘의 분노",AttackKind.Wave,1.05f,1f,21,20,3)),
            new BossSpec("kharos","카로스","숨겨진 심연 · 먼저 추락한 자","잔상이 공격을 반복합니다. 패링 3회로 회피 봉인을 해제하세요.","심연의 잔상",1500,3.3f,
                P("twelve","망자의 연격",AttackKind.Slash,.7f,1.1f,8,3.5f,6,true),
                P("draw","심연 발도",AttackKind.Dash,.8f,.9f,23,20,1,true),
                P("echo","이름 없는 잔상",AttackKind.Echo,1.15f,1f,17,4.5f,3,true),
                P("eclipse","일식",AttackKind.Balance,2.1f,1.2f,26,20),
                P("rain","검의 무덤",AttackKind.Rain,1f,.9f,18,20,5),
                P("phantom","돌아온 망자",AttackKind.Summon,.9f,.8f,11,20,3),
                P("end","심연의 파동",AttackKind.Wave,1.05f,1.1f,20,20,3))
        };
    }
    // abandoned: ESC 포기·창 닫기·에디터 정지로 끝난 판. 기록과 골드는 남지만 DDA 학습과 DB 통계에는 쓰지 않는다.
    [Serializable] public class HistoryEntry {public string date,boss;public bool clear;public float seconds;public int hits,parries,gold;public bool dda,abandoned;}
    [Serializable] public class SaveData
    {
        public int version=2,gold,attackLevel,healthLevel,equippedUltimate=-1,unlockedBoss;
        public List<CardStack> cards=new List<CardStack>();
        public string[] cardOffer=new string[0];
        public int cardPurchases;
        public bool cardChosen;
        public bool dda=true, tutorialSeen;
        public float masterVolume=.65f;
        public bool[] cleared=new bool[5];
        public List<HistoryEntry> history=new List<HistoryEntry>();
        public List<BossLearning> learning=new List<BossLearning>();
        // DDA가 보스별로 기억한 학습(판 수·연패·패턴별 레벨·성공률·LinUCB 행렬)만 지운다. 골드·카드·클리어·전투 기록은 그대로.
        // 다음 전투는 그 보스와의 첫 만남(레벨 0, DB 통계가 있으면 그 값)으로 시작한다.
        public void ResetDdaLearning() {if(learning==null)learning=new List<BossLearning>();else learning.Clear();}
        // Repair only DDA state; progression, history and inventory are preserved.
        public void NormalizeDda()
        {
            if(learning==null)learning=new List<BossLearning>();
            var seen=new HashSet<string>();
            learning.RemoveAll(b=>b==null||string.IsNullOrEmpty(b.id)||!seen.Add(b.id));
            foreach(var b in learning)b.Normalize();
        }
        public BossLearning Learning(string id) {if(learning==null)learning=new List<BossLearning>();var b=learning.Find(x=>x!=null&&x.id==id);if(b==null){b=new BossLearning{id=id};learning.Add(b);}return b;}
        public int UpgradeCost(bool attack) => 80+(attack?attackLevel:healthLevel)*55;
    }
    [Serializable] public class PatternStats
    {
        public string id;
        public int opportunities,hits,parries,dodges,ignored,level;
        public float damage;
        public float Success => opportunities==0 ? .7f : (float)(parries+dodges)/opportunities;
    }
    // Frequency is independent of damage/timing difficulty. Only observed battle outcomes
    // are used: old persisted success rates included unthreatened misses and are not reused.
    public static class DdaPatternFrequency
    {
        public static float Multiplier(PatternStats s, bool enabled, bool pressureRelief, out string reason)
        {
            if(!enabled){reason="DDA off";return 1;}
            if(pressureRelief){reason="low HP: no extra practice pressure";return 1;}
            if(s==null||s.opportunities<4){reason="fewer than 4 valid opportunities";return 1;}
            if(s.hits<0||s.parries<0||s.dodges<0||(long)s.hits+s.parries+s.dodges!=s.opportunities)
            {reason="invalid evidence ignored";return 1;}
            // Eight neutral pseudo-observations prevent one unlucky event dominating.
            double success=((double)s.parries+s.dodges+5.6)/(s.opportunities+8.0);
            float result=(float)(1+Math.Min(.35,Math.Max(0,.7-success)*.5));
            reason=result>1?"smoothed success below 70%; practice boost capped at 35%":"success sufficient; base frequency";
            return result;
        }
    }
    public sealed class PatternSelectionEvidence
    {
        public string id,reason; public int opportunities,decision; public bool selected; public float success,weight,probability,seconds;
    }
    [Serializable] public class PatternMemory {public string id;public int level,observations;public float success=.7f;}
    [Serializable] public class BossLearning
    {
        public string id;public int battles,lossStreak;public float globalSuccess=.7f;
        public List<PatternMemory> patterns=new List<PatternMemory>();
        public BanditModel model=new BanditModel();
        public void Normalize()
        {
            battles=Math.Max(0,battles);lossStreak=Math.Max(0,lossStreak);
            globalSuccess=BanditModel.Unit(globalSuccess,.7f);
            if(patterns==null)patterns=new List<PatternMemory>();
            var seen=new HashSet<string>();
            patterns.RemoveAll(p=>p==null||string.IsNullOrEmpty(p.id)||!seen.Add(p.id));
            foreach(var p in patterns){p.level=Math.Max(-2,Math.Min(2,p.level));p.observations=Math.Max(0,p.observations);p.success=BanditModel.Unit(p.success,.7f);}
            if(model==null)model=new BanditModel();model.Init();
        }
        public PatternMemory Pattern(string key){if(patterns==null)patterns=new List<PatternMemory>();var p=patterns.Find(x=>x!=null&&x.id==key);if(p==null){p=new PatternMemory{id=key};patterns.Add(p);}return p;}
    }
    // Disjoint LinUCB: six context features, five bounded difficulty arms.
    // Models learn only after battle completion; a running pattern never changes.
    // 안전장치 규칙(성공률 45% 미만 → 한 단계 쉽게, 90% 초과 → 한 단계 어렵게)은 판 수와 관계없이 항상 먼저 적용한다.
    // LinUCB는 그 사이(45~90%)에서만 고른다. 예전처럼 초반 3판에만 규칙을 쓰면, 개인 기록이 적어 LinUCB가
    // 처음 몇 판에 정해진 레벨에 묶여 실력이 늘어도 다시 어려워지지 않았다 (2026-11-06 시뮬레이션으로 확인).
    [Serializable] public class BanditModel
    {
        public const int D=6,N=5;
        public double[] matrices=new double[N*D*D], vectors=new double[N*D];
        public bool initialized;
        public static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        public static float Unit(float value,float fallback)=>Finite(value)?Math.Max(0,Math.Min(1,value)):fallback;
        bool Healthy()
        {
            if(!initialized||matrices==null||matrices.Length!=N*D*D||vectors==null||vectors.Length!=N*D)return false;
            foreach(double v in vectors)if(!Finite(v))return false;
            foreach(double v in matrices)if(!Finite(v))return false;
            // Valid LinUCB A = I + sum(xx^T) is symmetric positive definite.
            for(int arm=0;arm<N;arm++)
            {
                var l=new double[D,D];int offset=arm*D*D;
                for(int i=0;i<D;i++)for(int j=0;j<=i;j++)
                {
                    double v=matrices[offset+i*D+j],other=matrices[offset+j*D+i];
                    if(Math.Abs(v-other)>1e-8*Math.Max(1,Math.Max(Math.Abs(v),Math.Abs(other))))return false;
                    for(int k=0;k<j;k++)v-=l[i,k]*l[j,k];
                    if(!Finite(v))return false;
                    if(i==j){if(v<=1e-12)return false;l[i,j]=Math.Sqrt(v);}else l[i,j]=v/l[j,j];
                }
            }
            return true;
        }
        public void Init(){if(Healthy())return;matrices=new double[N*D*D];vectors=new double[N*D];for(int a=0;a<N;a++)for(int i=0;i<D;i++)matrices[a*D*D+i*D+i]=1;initialized=true;}
        public double[] Context(float success,float global,int level,float power,int retries)
            => new double[]{1,1-Unit(success,.7f),1-Unit(global,.7f),Math.Max(-2,Math.Min(2,level))/2.0,Finite(power)?Math.Max(0,Math.Min(1,power/2)):0,Math.Max(0,Math.Min(1,retries/5.0))};
        public double Value(int arm,double[] x)
        {
            if(arm<0||arm>=N||!ValidContext(x))return 0;
            Init();var ax=Solve(arm,x);var b=new double[D];Array.Copy(vectors,arm*D,b,0,D);
            var theta=Solve(arm,b);double mean=0,uncertainty=0;
            for(int i=0;i<D;i++){mean+=theta[i]*x[i];uncertainty+=x[i]*ax[i];}
            return mean+.18*Math.Sqrt(Math.Max(0,uncertainty));
        }
        public int Choose(double[] context,int current,int battles,float success,int lossStreak)
        {
            current=Math.Max(-2,Math.Min(2,current));success=Unit(success,.7f);
            if(!ValidContext(context))return current;
            if(battles<=0)return 0;
            if(lossStreak>=3)return Math.Max(-2,current-1);
            if(success<.45f)return Math.Max(-2,current-1);
            if(success>.9f)return Math.Min(2,current+1);
            int best=current;double value=double.NegativeInfinity;
            for(int level=Math.Max(-2,current-1);level<=Math.Min(2,current+1);level++)
            {
                // Mild prior supports cold start; actual outcomes update LinUCB matrices.
                double prior=-.12*Math.Abs((success-.7)-level*.1);
                double score=Value(level+2,context)+prior-.012*Math.Abs(level-current);
                if(score>value){value=score;best=level;}
            }
            return best;
        }
        public static bool ValidContext(double[] x)
        {if(x==null||x.Length!=D)return false;foreach(double v in x)if(!Finite(v)||Math.Abs(v)>1)return false;return true;}
        public void Update(int level,double[] x,double reward)
        {if(level< -2||level>2||!ValidContext(x)||!Finite(reward)||reward<0||reward>1)return;Init();int a=level+2;for(int i=0;i<D;i++){vectors[a*D+i]+=reward*x[i];for(int j=0;j<D;j++)matrices[a*D*D+i*D+j]+=x[i]*x[j];}}
        double[] Solve(int arm,double[] rhs)
        {
            var a=new double[D,D+1];for(int i=0;i<D;i++){for(int j=0;j<D;j++)a[i,j]=matrices[arm*D*D+i*D+j];a[i,D]=rhs[i];}
            for(int col=0;col<D;col++)
            {
                int pivot=col;for(int row=col+1;row<D;row++)if(Math.Abs(a[row,col])>Math.Abs(a[pivot,col]))pivot=row;
                if(Math.Abs(a[pivot,col])<1e-12)throw new InvalidOperationException("Singular LinUCB matrix");
                for(int j=col;j<=D;j++){double t=a[col,j];a[col,j]=a[pivot,j];a[pivot,j]=t;}
                double divisor=a[col,col];for(int j=col;j<=D;j++)a[col,j]/=divisor;
                for(int row=0;row<D;row++)if(row!=col){double f=a[row,col];for(int j=col;j<=D;j++)a[row,j]-=f*a[col,j];}
            }
            var result=new double[D];for(int i=0;i<D;i++)result[i]=a[i,D];return result;
        }
        public static double Reward(float success){if(!Finite(success)||success<0||success>1)return 0;if(success>=.6f&&success<=.8f)return 1;return Math.Max(0,1-Math.Min(Math.Abs(success-.6f),Math.Abs(success-.8f))*2.5);}
    }
}
