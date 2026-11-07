using UnityEngine;
using System.Collections.Generic;

namespace ReversalTower
{
    // Geometry only: authored pattern data, DDA multipliers and motion timings stay unchanged.
    public static class ArakusGeometry
    {
        public const float VisualScale=2.2f, LegacyEffectScale=1.4f, PixelsPerUnit=56f;
        // Idle alpha bounds: pivot(96,160), max horizontal extent33px and height89px.
        public const float HalfWidth=33f*VisualScale/PixelsPerUnit, Height=89f*VisualScale/PixelsPerUnit;
        public static float Reach(BossSpec boss,Pattern p)
        {
            if(boss==null||boss.id!="arakus")return p.range;
            switch(p.id)
            {
                case "cleave":return p.range*(4f/2.9f);
                case "shield":return p.range*(2.5f/1.9f);
                case "kick":return p.range*1.5f;
                case "slash":return p.range*(3.2f/2.4f);
                case "rush":return p.range*(2f/1.4f);
                default:return p.range;
            }
        }
        public static float AttackHeight(BossSpec boss,Pattern p)
        {
            if(boss==null||boss.id!="arakus")return 2.2f;
            // Active-frame alpha height informs the vertical reach; kick keeps its low profile.
            switch(p.id){case "cleave":return 3.3f;case "shield":return 2.7f;case "slash":return 3.4f;case "rush":return 2.7f;default:return 2.2f;}
        }
        public static float SelectionMax(BossSpec boss,Pattern p)
        {
            if(boss==null||boss.id!="arakus")return p.maxRange;
            float reach=Reach(boss,p);if(reach==p.range||p.maxRange<=0)return p.maxRange;
            return p.maxRange+reach-p.range;
        }
    }
    public sealed class BossPatternSystem
    {
        readonly TowerGame g;
        public Pattern current;
        public float patternAge,windup,recovery,rest,patternDamage,targetX,safeSide,nextHit,bossStagger,barrierHp,barrierTime;
        public int patternCount,attackStep;
        public Vector2 origin;
        public bool phaseTwo,thirdPhase,pillarLeft,pillarRight,barrier;
        // 아라쿠스 기획 패턴 상태 (다른 보스는 쓰지 않음)
        public const float RageTempo=1.5f, RageDamageTaken=.5f, RageThreshold=.3f, BehindLimit=1.5f;
        public float tempo=1;            // 이번 패턴의 속도 배율 (분노 뒤 1.5)
        public bool rageDone,rageActive; // 분노 시작 / 분노 시전 완료(이후 1.5배)
        public int poseFrame=-1;         // 아라쿠스 그림 프레임 (-1 = 기본 규칙)
        public float behindTime;         // 플레이어가 등 뒤에 있던 시간 (퀵스텝 조건)
        public bool shieldOut,shieldCaught,leaping,struck,touched;
        public float leapStart,leapFrom,leapTo,moveFrom,moveTo;
        public Pattern forceNext;        // 개발용: 다음에 바로 실행할 패턴 (TowerGame.DevKeys)
        // 패링 신호 (파란 빛): 패링 가능한 공격을 시전하기 전 보스 몸이 파랗게 빛나고,
        // 받아칠 순간 ParryFlashLead초 전에 푸른 별빛이 번쩍인다. 화면은 ActorView·WorldArtist가 그린다.
        public const float ParryCueLead=1.2f, ParryFlashLead=.35f, ParryFlashTime=.3f;
        public float parryGlow;          // 0~1 보스 몸의 파란 빛 (부드럽게 따라감)
        public float parryFlash=99;      // 보스 무기 앞 별빛이 터진 뒤 지난 시간
        int patternSerial,cueKey=-1;float cueSoundAt=-9;
        public float DamageTakenScale => current!=null&&current.kind==AttackKind.Rage?RageDamageTaken:1f;
        public BossPatternSystem(TowerGame game) { g = game; }
        public void Reset()
        {
            cooldowns.Clear(); previousPattern=""; dashHit=false;
            tempo=1; rageDone=rageActive=false; poseFrame=-1; behindTime=0;
            shieldOut=shieldCaught=leaping=struck=touched=false;forceNext=null;
            parryGlow=0;parryFlash=99;cueKey=-1;cueSoundAt=-9;
        }
        readonly Dictionary<string,float> cooldowns=new Dictionary<string,float>();
        readonly System.Random combatRandom=new System.Random();
        string previousPattern="";
        public void TickBoss(float dt)
        {
            if(g.bossHp<=0)return;
            if(!phaseTwo&&g.bossHp<g.spec.hp*g.spec.phaseThreshold)
            {
                phaseTwo=true;g.phaseFlash=1;current=null;rest=2;bossStagger=0;g.bossState="페이즈 전환";g.shake=4;
                g.Say(g.spec.hasPillars?"철의 맹세가 흔들린다 · 돌진을 기둥으로 유도하세요":g.spec.echoBoss?"일식 · 잔상이 깨어납니다":"두 번째 맹세 · 패턴이 강화됩니다",3);
                g.Burst(g.boss+Vector2.up,2,Palette.Gold,40);
            }
            if(g.spec.echoBoss&&phaseTwo&&!thirdPhase&&g.bossHp<g.spec.hp*.3f)
            {thirdPhase=true;current=null;rest=2;g.phaseFlash=1;g.Say("마지막 잔상 · 심연의 끝",3);}
            if(g.spec.hasPillars)
            {
                bool behind=(g.player.x-g.boss.x)*g.bossFace< -.3f;
                behindTime=behind?behindTime+dt:0;
            }
            if(bossStagger>0){bossStagger-=dt;g.bossState="그로기";if(!g.spec.airborne){g.boss.y=0;leaping=false;}poseFrame=-1;return;}
            if(g.spec.airborne)g.boss.y=barrier?1.5f:1.05f+Mathf.Sin(g.seconds*1.5f)*.18f;
            if(current==null)
            {
                if(!g.spec.airborne){g.boss.y=0;leaping=false;}
                poseFrame=-1;
                if(forceNext!=null){var f=forceNext;forceNext=null;if(f.kind==AttackKind.Rage)rageDone=true;BeginPattern(f);return;}
                // 분노: HP 30% 아래로 내려가면 지금 패턴이 끝난 뒤 한 번 (기획서 4.3)
                if(g.spec.hasPillars&&!rageDone&&g.bossHp<g.spec.hp*RageThreshold)
                {var rage=Find(AttackKind.Rage);if(rage!=null){rageDone=true;BeginPattern(rage);return;}}
                // 퀵스텝: 플레이어가 등 뒤에 1.5초 있으면 돌아서며 물러남 (기획서 4.2)
                if(g.spec.hasPillars&&behindTime>=BehindLimit)
                {var step=Find(AttackKind.Step);if(step!=null&&Ready(step)){BeginPattern(step);return;}}
                rest-=dt;g.bossState=rest>0?"빈틈":"추적";
                if(rest>0)return;
                float distance=Mathf.Abs(g.boss.x-g.player.x);
                g.bossFace=g.player.x>=g.boss.x?1:-1;
                float chase=g.spec.speed*(g.spec.hasPillars?(rageActive?RageTempo:1):(phaseTwo?1.18f:1));
                if(distance>3.1f&&!g.spec.airborne){g.boss.x=Mathf.MoveTowards(g.boss.x,g.player.x,dt*chase);if(distance>5&&combatRandom.NextDouble()>.015)return;}
                if(g.spec.airborne&&distance<3){g.boss.x=Mathf.MoveTowards(g.boss.x,-Mathf.Sign(g.player.x)*7,dt*4);}
                SelectPattern(distance);return;
            }
            patternAge+=dt;
            g.bossState=patternAge<windup?"공격 예고":patternAge<nextHit?"연격":"공격";
            if(current.kind>=AttackKind.Step){TickArakus(dt);return;}
            if(patternAge<windup)
            {
                if(current.kind==AttackKind.Slash&&patternAge<windup*.5f)
                {
                    g.bossFace=g.player.x>=g.boss.x?1:-1;
                    if(Mathf.Abs(g.boss.x-g.player.x)>ArakusGeometry.Reach(g.spec,current)*.75f)g.boss.x=Mathf.MoveTowards(g.boss.x,g.player.x,dt*g.spec.speed*.7f*tempo);
                }
                if(g.spec.hasPillars)poseFrame=0;
                return;
            }
            if(g.spec.hasPillars)poseFrame=attackStep<patternCount?1:2;
            if(current.kind==AttackKind.Dash&&attackStep==1)
            {
                float old=g.boss.x;g.boss.x+=g.bossFace*dt*g.spec.dashSpeed*tempo;
                bool column=g.spec.hasPillars&&((pillarLeft&&Crossed(old,g.boss.x,-4))||(pillarRight&&Crossed(old,g.boss.x,4)));
                if(column)
                {
                    if(g.boss.x<0)pillarLeft=false;else pillarRight=false;
                    bossStagger=3.2f;current=null;rest=.5f;g.shake=5;g.PlaySound("hit");g.Say("기둥 충돌 · 공격 기회!",2);g.Burst(g.boss+Vector2.up,2,Palette.Gold,30);return;
                }
                if(!dashHit&&Mathf.Abs(g.player.x-g.boss.x)<1.2f)
                {dashHit=true;g.PlayerHit(patternDamage,current.parryable,current.id,g.boss.x,g.player.y<2.2f);if(bossStagger>0){current=null;rest=.5f;return;}}
                if(g.boss.x<TowerGame.Left+1||g.boss.x>TowerGame.Right-1||patternAge>windup+1.1f)
                {g.boss.x=Mathf.Clamp(g.boss.x,TowerGame.Left+1,TowerGame.Right-1);if(!dashHit)g.PlayerHit(patternDamage,current.parryable,current.id,g.boss.x,false);attackStep=2;nextHit=patternAge;}
            }
            if(attackStep<patternCount&&patternAge>=nextHit)
            {
                g.AutoNoteHitLateness(patternAge-nextHit);
                ExecutePattern();if(g.BattleEnded||current==null)return;
            }
            float endTime=nextHit+recovery;
            if(attackStep>=patternCount&&patternAge>endTime)
            {
                if(g.spec.echoBoss&&phaseTwo&&current.kind==AttackKind.Slash)
                    g.hazards.Add(new TowerGame.Hazard{x=origin.x+g.bossFace*2,width=4.5f,delay=.7f,life=.2f,damage=patternDamage*.7f,pattern=current.id,parry=true,color=Palette.Purple});
                previousPattern=current.id;current=null;rest=(phaseTwo?.45f:.7f)/tempo;g.bossState="빈틈";
            }
        }
        bool dashHit;
        static bool Crossed(float old,float now,float point)=>(old-point)*(now-point)<=0;
        bool Ready(Pattern p)=>!(cooldowns.TryGetValue(p.id,out float until)&&g.seconds<until);
        Pattern Find(AttackKind kind){foreach(var p in g.spec.patterns)if(p.kind==kind)return p;return null;}
        void Finish()
        {
            previousPattern=current.id;current=null;rest=(phaseTwo?.45f:.7f)/tempo;g.bossState="빈틈";
            poseFrame=-1;leaping=false;if(!g.spec.airborne)g.boss.y=0;
        }
        bool InFront(float reach)=>Mathf.Abs(g.player.x-g.boss.x)<reach&&(g.player.x-g.boss.x)*g.bossFace>-.3f;

        // ── 아라쿠스 기획 패턴 (기획서 4.1~4.3). 시간은 모두 tempo(분노 뒤 1.5)로 나눈다. ──
        void TickArakus(float dt)
        {
            var p=current;float t=patternAge,T=1/tempo;
            if(p.kind==AttackKind.Step)
            {
                // 선딜 0.1 → 0.3초 동안 0.75m 뒤로 → 후딜
                float u=Mathf.Clamp01((t-windup)/(.3f*T));
                if(t<windup)poseFrame=0;
                else{poseFrame=u<1?1:2;g.boss.x=Mathf.Lerp(moveFrom,moveTo,u);}
                if(t>=windup+.3f*T+recovery)Finish();
                return;
            }
            if(p.kind==AttackKind.Rage)
            {
                // 시전 2.5초(받는 피해 절반) → 분노 완료, 이후 모든 속도 1.5배 → 후딜 2.5초
                if(t<windup){poseFrame=0;g.bossState="분노 시전";return;}
                if(!rageActive)
                {
                    rageActive=true;g.shake=6;g.flash=.35f;g.PlaySound("parry");
                    g.Burst(g.boss+Vector2.up*1.5f,3,Palette.Red,50);
                    g.Say("아라쿠스가 분노했다 · 이제부터 모든 동작이 1.5배 빠릅니다",3);
                }
                poseFrame=t<windup+.6f?1:2;g.bossState="분노";
                if(t>=windup+recovery)Finish();
                return;
            }
            if(p.kind==AttackKind.ShieldThrow)
            {
                bool slam=p.id=="slam";
                if(t<windup){poseFrame=0;return;}
                if(attackStep==0)
                {
                    // 두 패턴 모두 같은 자세로 방패를 던진다 (이지선다)
                    attackStep=1;shieldOut=true;shieldCaught=false;g.PlaySound("attack");
                    Vector2 hand=g.boss+new Vector2(g.bossFace*.75f,1.25f);
                    Vector2 aim=(g.player+Vector2.up*.9f-hand).normalized;
                    var shot=new TowerGame.Shot{pos=hand,vel=aim*12*tempo,damage=7*patternDamage/Mathf.Max(1,p.damage),parry=true,pattern=p.id,
                        returns=true,maxTravel=7,radius=.35f,life=6};
                    g.shots.Add(shot);
                    moveFrom=g.boss.x;
                    float goal=g.player.x-g.bossFace*1.1f;
                    moveTo=Mathf.Clamp(moveFrom+g.bossFace*Mathf.Clamp((goal-moveFrom)*g.bossFace,0,6),TowerGame.Left+1,TowerGame.Right-1);
                }
                if(!slam)
                {
                    // 투척 후 대쉬: 0.2초 뒤 6m를 0.6초에 달려 끝에서 베기(패링 가능)
                    float dashStart=windup+.2f*T,dashEnd=dashStart+.6f*T;
                    if(t<dashStart){poseFrame=1;return;}
                    if(t<dashEnd){poseFrame=1;g.boss.x=Mathf.Lerp(moveFrom,moveTo,(t-dashStart)/(.6f*T));g.bossState="돌진";return;}
                    if(!struck)
                    {
                        struck=true;g.boss.x=moveTo;
                        g.PlayerHit(patternDamage,true,p.id,g.boss.x,InFront(p.range)&&g.player.y<2.2f);
                        g.Burst(g.boss+new Vector2(g.bossFace*1.3f,1.2f),.8f,Palette.ParryRim,10);g.shake=Mathf.Max(g.shake,2.5f);
                    }
                    poseFrame=2;
                    if(t>=dashEnd+recovery)Finish();
                    return;
                }
                // 방패 던지고 내려찍기: 방패를 돌려받은 '뒤에' 도약 → 착지 반경 1.5m 내려찍기(패링 불가)
                if(!leaping&&!struck)
                {
                    poseFrame=1;g.bossState="방패 회수 대기";
                    if(!shieldCaught&&t<windup+3.5f*T)return;     // 안전장치: 방패가 사라져도 3.5초 뒤엔 진행
                    leaping=true;leapStart=t;leapFrom=g.boss.x;
                    g.bossFace=g.player.x>=g.boss.x?1:-1;
                    leapTo=Mathf.Clamp(Mathf.Clamp(g.player.x,leapFrom-7,leapFrom+7),TowerGame.Left+1,TowerGame.Right-1);
                    targetX=leapTo;g.PlaySound("dash");
                }
                if(leaping)
                {
                    float u=Mathf.Clamp01((t-leapStart)/(.5f*T));
                    g.boss.x=Mathf.Lerp(leapFrom,leapTo,u);g.boss.y=4*2.2f*u*(1-u);
                    poseFrame=u<.5f?2:3;g.bossState="도약";
                    if(u<1)return;
                    leaping=false;struck=true;g.boss.y=0;leapStart=t;
                    bool hit=Mathf.Abs(g.player.x-g.boss.x)<1.5f+.3f&&g.player.y<1.2f;
                    g.PlayerHit(patternDamage,false,p.id,g.boss.x,hit);
                    g.shake=6;g.Burst(g.boss+new Vector2(0,.2f),2.2f,Palette.Red,30);
                }
                poseFrame=4;g.bossState="내려찍기";
                if(t>=leapStart+recovery)Finish();
                return;
            }
            if(p.kind==AttackKind.ShieldRush)
            {
                // 방패 돌진: 선딜 0.5 → 1.75초 동안 직선 5m, 0.25초마다 판정 (피격 무적 0.35초라 실제로는 약 4타)
                if(t<windup){poseFrame=0;moveFrom=g.boss.x;moveTo=Mathf.Clamp(g.boss.x+g.bossFace*5,TowerGame.Left+1,TowerGame.Right-1);return;}
                float span=1.75f*T,u=Mathf.Clamp01((t-windup)/span);
                if(u<1)
                {
                    poseFrame=1;g.bossState="방패 돌진";g.boss.x=Mathf.Lerp(moveFrom,moveTo,u);
                    while(attackStep<patternCount&&t>=windup+attackStep*.25f*T)
                    {
                        attackStep++;
                        if(InFront(g.spec.id=="arakus"?ArakusGeometry.Reach(g.spec,p):1.4f)&&g.player.y<ArakusGeometry.AttackHeight(g.spec,p)){touched=true;g.PlayerHit(patternDamage,false,p.id,g.boss.x);g.Burst(g.boss+new Vector2(g.bossFace*1,1),.5f,Palette.Red,5);}
                    }
                    return;
                }
                if(!struck){struck=true;if(!touched)g.PlayerHit(patternDamage,false,p.id,g.boss.x,false);}
                poseFrame=2;
                if(t>=windup+span+recovery)Finish();
                return;
            }
            if(p.kind==AttackKind.Sweep)
            {
                // 일대 휩쓸기: 1초 횡베기(낮게 — 점프로 넘김) → 2초 종베기(높게 — 거리로만 피함). 대시 무적 무시.
                float second=windup+1f*T;
                if(t<windup){poseFrame=0;return;}
                if(attackStep==0)
                {
                    attackStep=1;
                    bool hit=Mathf.Abs(g.player.x-g.boss.x)<p.range&&g.player.y<1f;
                    g.PlayerHit(patternDamage,false,p.id,g.boss.x,hit,true);
                    g.shake=Mathf.Max(g.shake,3);g.Burst(g.boss+new Vector2(0,.6f),2.5f,Palette.Red,24);
                }
                if(t<second){poseFrame=t<windup+.3f*T?1:2;return;}
                if(attackStep==1)
                {
                    attackStep=2;
                    bool hit=Mathf.Abs(g.player.x-g.boss.x)<p.range&&g.player.y<2.5f;
                    g.PlayerHit(patternDamage,false,p.id,g.boss.x,hit,true);
                    g.shake=Mathf.Max(g.shake,4.5f);g.Burst(g.boss+new Vector2(0,1.4f),2.5f,Palette.Red,24);
                }
                poseFrame=t<second+.3f*T?1:3;
                if(t>=second+recovery)Finish();
                return;
            }
            Finish();
        }
        public void SelectPattern(float distance)
        {
            var candidates=new List<Pattern>();
            foreach(var p in g.spec.patterns)
            {
                if(!Ready(p))continue;
                if(p.id==previousPattern)continue;
                if(p.minPhase<0)continue;                      // 분노·퀵스텝은 조건으로만 나옴
                if(p.minPhase>=2&&!phaseTwo)continue;
                if(p.kind==AttackKind.Slash&&distance>ArakusGeometry.Reach(g.spec,p)+1.5f)continue;
                if(p.maxRange>0&&(distance<p.minRange||distance>ArakusGeometry.SelectionMax(g.spec,p)+(p.kind==AttackKind.Slash?1.5f:0)))continue;
                if(p.kind==AttackKind.Barrier&&!phaseTwo)continue;
                if(p.kind==AttackKind.Summon&&g.minions.Count>=5)continue;
                if(p.kind==AttackKind.Balance&&g.seconds<12)continue;
                candidates.Add(p);
            }
            if(candidates.Count==0){rest=.2f;return;}
            float total=0;foreach(var p in candidates)total+=p.weight>0?p.weight:1;
            double roll=combatRandom.NextDouble()*total;Pattern selected=candidates[candidates.Count-1];
            foreach(var p in candidates){roll-=p.weight>0?p.weight:1;if(roll<0){selected=p;break;}}
            if(g.seconds<4)selected=g.spec.patterns[0];
            BeginPattern(selected);
        }
        public void BeginPattern(Pattern p)
        {
            current=p;patternAge=0;attackStep=0;dashHit=false;patternSerial++;g.bossFace=g.player.x>=g.boss.x?1:-1;
            origin=g.boss;targetX=g.player.x;
            int level=g.Stat(p.id).level;
            float[] telegraph={1.3f,1.15f,1,.9f,.8f},damage={.8f,.9f,1,1.1f,1.2f},cooldown={1.25f,g.spec.phaseSpeedScale,1,.9f,.8f};
            tempo=rageActive?RageTempo:1;
            bool special=p.kind==AttackKind.Step||p.kind==AttackKind.Rage;
            windup=special?p.windup:Mathf.Max(.4f,p.windup*telegraph[level+2]/(phaseTwo?g.spec.phaseSpeedScale:1));
            windup/=tempo;
            recovery=p.recovery/(phaseTwo?g.spec.phaseSpeedScale:1)/tempo;patternDamage=p.damage*damage[level+2];patternCount=p.count;
            if(phaseTwo&&p.kind==AttackKind.Volley)patternCount++;
            if(p.count>1&&level!=0&&p.kind<AttackKind.Step)patternCount=Mathf.Max(1,patternCount+(level==2?1:level==-2?-1:0));
            if(p.kind==AttackKind.Dash)patternCount=2;
            float baseCooldown=p.cooldown>0?p.cooldown:(p.kind==AttackKind.Balance||p.kind==AttackKind.Barrier?18:4);
            nextHit=windup;cooldowns[p.id]=g.seconds+baseCooldown*cooldown[level+2];
            shieldCaught=leaping=struck=touched=false;poseFrame=g.spec.hasPillars?0:-1;
            if(p.kind==AttackKind.Step){behindTime=0;moveFrom=g.boss.x;moveTo=Mathf.Clamp(g.boss.x-g.bossFace*p.range,TowerGame.Left+1,TowerGame.Right-1);}
            if(p.kind==AttackKind.Rage)g.Say("아라쿠스의 분노 · 시전하는 동안 받는 피해 절반",windup);
            if(p.kind==AttackKind.Balance)
            {
                int left=0,right=0;foreach(var m in g.minions)if(m.x<0)left++;else right++;
                safeSide=left<=right?-1:1;
                g.Say((safeSide<0?"왼쪽":"오른쪽")+"의 빛나는 안전지대로 이동하세요",windup);
            }
            if(p.kind==AttackKind.Barrier){g.Say("결계벽을 부수세요 · 실패하면 광역 피해",4);}
        }
        public void ExecutePattern()
        {
            var p=current;
            if(p.kind==AttackKind.Slash)
            {
                bool radial=p.id=="spin";
                bool overlap=Mathf.Abs(g.player.x-g.boss.x)<ArakusGeometry.Reach(g.spec,p)&&Mathf.Abs(g.player.y-g.boss.y)<ArakusGeometry.AttackHeight(g.spec,p)&&(radial||(g.player.x-g.boss.x)*g.bossFace>-.3f);
                int hitsBefore=g.hits;
                g.PlayerHit(patternDamage,p.parryable,p.id,g.boss.x,overlap);
                // 거검 휘두르기: 맞으면 1m 날아감 (PlayerHit 기본 0.45m + 0.55m)
                if(g.spec.hasPillars&&p.id=="cleave"&&g.hits>hitsBefore)g.player.x=Mathf.Clamp(g.player.x+g.bossFace*.55f,TowerGame.Left+.4f,TowerGame.Right-.4f);
                if(g.spec.hasPillars)g.shake=Mathf.Max(g.shake,p.id=="shield"?3.5f:2);
                g.Burst(g.boss+new Vector2(g.bossFace*1.5f,1.25f),.6f,p.parryable?Palette.ParryRim:Palette.Red,7);
                if(g.spec.afterSlashHazard&&phaseTwo)g.hazards.Add(new TowerGame.Hazard{x=g.boss.x,width=1.5f,delay=.8f,life=.25f,damage=14,pattern=p.id,color=Palette.Purple});
            }
            else if(p.kind==AttackKind.Dash)
            {if(attackStep==0){attackStep=1;nextHit=windup+1.2f;g.PlaySound("dash");return;}else{attackStep=patternCount;nextHit=patternAge;return;}}
            else if(p.kind==AttackKind.Volley)
            {
                Vector2 spawn=g.boss+new Vector2(g.bossFace*.75f,1.25f);
                Vector2 aim=(g.player+Vector2.up*.9f-spawn).normalized;
                if(p.id=="fan")aim=Quaternion.Euler(0,0,(attackStep-(patternCount-1)*.5f)*13)*aim;
                FireShot(spawn,aim*g.spec.projectileSpeed,patternDamage,p.parryable,p.id);
            }
            else if(p.kind==AttackKind.Rain)
            {
                float x=attackStep==0?targetX:Mathf.Clamp(g.player.x+(attackStep%2==0?1.4f:-1.4f),TowerGame.Left+1,TowerGame.Right-1);
                g.hazards.Add(new TowerGame.Hazard{x=x,width=1.6f,delay=.85f,life=.25f,damage=patternDamage,pattern=p.id,color=Palette.Red});
            }
            else if(p.kind==AttackKind.Pillar)
            {
                g.hazards.Add(new TowerGame.Hazard{x=Mathf.Clamp(targetX+(attackStep-(patternCount-1)*.5f)*3,TowerGame.Left+1,TowerGame.Right-1),width=1.2f,delay=.75f,life=.35f,damage=patternDamage,pattern=p.id,color=g.spec.airborne?Palette.Gold:Palette.Purple});
            }
            else if(p.kind==AttackKind.Wave)
            {
                FireShot(new Vector2(g.boss.x,.35f),new Vector2(g.bossFace*(6+attackStep),0),patternDamage,false,p.id);
                var last=g.shots[g.shots.Count-1];last.radius=.4f;
            }
            else if(p.kind==AttackKind.Summon)
            {if(g.minions.Count<6)g.minions.Add(new TowerGame.Minion{x=(attackStep%2==0?-1:1)*(7+attackStep*.5f),hp=g.spec.minionHp,pattern=p.id});}
            else if(p.kind==AttackKind.Balance)
            {g.hazards.Add(new TowerGame.Hazard{x=safeSide<0?6.3f:-6.3f,width=12.2f,delay=.03f,life=.25f,damage=patternDamage,pattern=p.id,color=Palette.Red,wide=true});}
            else if(p.kind==AttackKind.Barrier)
            {
                barrier=true;barrierHp=45;barrierTime=7;
                g.boss.x=g.player.x<0?7:-7;patternCount=1;
            }
            else if(p.kind==AttackKind.Echo)
            {g.hazards.Add(new TowerGame.Hazard{x=targetX+(attackStep-1)*2.2f,width=3.2f,delay=.6f,life=.2f,damage=patternDamage,parry=true,pattern=p.id,color=Palette.Purple});}
            attackStep++;
            float interval=p.kind==AttackKind.Slash?.32f:p.kind==AttackKind.Rain?.35f:p.kind==AttackKind.Volley?.3f:.22f;
            int l=g.Stat(p.id).level;interval*=l<0?1.1f:l>0?.95f:1;
            nextHit=patternAge+interval;
        }
        // ── 패링 신호 ──
        // 보스가 다음에 '패링 가능한' 동작을 하기까지 남은 시간 (-1 = 없음). melee = 보스 무기로 직접 치는 타격.
        // 투사체·잔상은 던지는 순간까지만 보스가 빛나고, 받아칠 순간의 별빛은 투사체 쪽에서 터진다.
        public float ParryCueTime(out bool melee)
        {
            melee=false;var p=current;
            if(p==null||bossStagger>0||g.BattleEnded)return -1;
            if(p.kind==AttackKind.ShieldThrow)
            {
                if(attackStep==0)return Mathf.Max(0,windup-patternAge);                    // 방패 투척
                if(p.id=="throw"&&!struck){melee=true;return Mathf.Max(0,windup+.8f/tempo-patternAge);} // 대쉬 끝 베기
                return -1;
            }
            if(p.kind>=AttackKind.Step||!p.parryable||attackStep>=patternCount)return -1;
            if(p.kind==AttackKind.Dash)
            {
                melee=true;float reach=Mathf.Abs(g.player.x-g.boss.x)/Mathf.Max(1,g.spec.dashSpeed*tempo);
                if(attackStep==0)return Mathf.Max(0,nextHit-patternAge)+reach;
                return dashHit?-1:reach;
            }
            melee=p.kind==AttackKind.Slash;
            return Mathf.Max(0,nextHit-patternAge);
        }
        // 투사체가 플레이어에게 닿기까지 남은 시간 (멀어지는 중이면 99)
        public float ShotEta(TowerGame.Shot s)
        {
            float dx=g.player.x-s.pos.x;
            if(Mathf.Abs(s.vel.x)<.01f||dx*s.vel.x<=0)return 99;
            return Mathf.Max(0,Mathf.Abs(dx)-.5f-s.radius)/Mathf.Abs(s.vel.x);
        }
        public void TickParryCue(float dt)
        {
            float t=ParryCueTime(out bool melee);
            float target=t<0||t>ParryCueLead?0:.55f+.45f*(1-t/ParryCueLead);
            parryGlow=Mathf.MoveTowards(parryGlow,target,dt*(target>parryGlow?6:3));
            parryFlash+=dt;
            int key=patternSerial*32+attackStep+(struck?16:0);
            if(melee&&t>=0&&t<=ParryFlashLead&&key!=cueKey){cueKey=key;parryFlash=0;Ting();}
            foreach(var s in g.shots)
                if(s.parry&&!s.friendly&&!s.hitDone&&s.cueAt<0&&ShotEta(s)<=ParryFlashLead){s.cueAt=g.seconds;Ting();}
            foreach(var h in g.hazards)
                if(h.parry&&h.cueAt<0&&h.delay>0&&h.delay<=ParryFlashLead){h.cueAt=g.seconds;Ting();}
        }
        void Ting(){if(g.seconds-cueSoundAt>.12f){cueSoundAt=g.seconds;g.PlaySound("cue");}}
        public void FireShot(Vector2 pos,Vector2 velocity,float damage,bool parry,string pattern,bool friendly=false)
        {g.shots.Add(new TowerGame.Shot{pos=pos,vel=velocity,damage=damage,parry=parry,pattern=pattern,friendly=friendly});}
        public void TickShots(float dt)
        {
            for(int i=g.shots.Count-1;i>=0;i--)
            {
                var s=g.shots[i];s.life-=dt;s.pos+=s.vel*dt;bool remove=false;
                if(s.returns&&!s.friendly)
                {
                    // 아라쿠스 방패: 7m 날아갔다가 보스 손으로 돌아옴. 손에 닿으면 회수.
                    s.travelled+=s.vel.magnitude*dt;
                    Vector2 hand=g.boss+new Vector2(g.bossFace*.6f,1.25f);
                    if(!s.returning&&s.travelled>=s.maxTravel)s.returning=true;
                    if(s.returning)
                    {
                        s.vel=(hand-s.pos).normalized*Mathf.Max(10,s.vel.magnitude);
                        if(Vector2.Distance(s.pos,hand)<.7f)
                        {
                            if(!s.hitDone)g.PlayerHit(s.damage,s.parry,s.pattern,s.pos.x,false);
                            shieldCaught=true;shieldOut=false;g.shots.RemoveAt(i);continue;
                        }
                    }
                }
                if(s.friendly)
                {
                    Vector2 aim=(g.boss+Vector2.up*1.3f-s.pos).normalized;
                    s.vel=Vector2.Lerp(s.vel,aim*12,dt*9);
                    if(g.BossShotOverlap(s.pos,s.radius)){if(s.returns){shieldCaught=true;shieldOut=false;}g.DamageBoss(s.damage);remove=true;if(g.BattleEnded){g.shots.RemoveAt(i);return;}}
                }
                else if(!s.hitDone&&s.pos.x>g.player.x-.5f-s.radius&&s.pos.x<g.player.x+.5f+s.radius&&s.pos.y>g.player.y-s.radius&&s.pos.y<g.player.y+1.65f+s.radius)
                {
                    int before=g.parries;g.PlayerHit(s.damage,s.parry,s.pattern,s.pos.x);
                    if(g.parries>before){s.friendly=true;s.damage*=2.5f;s.vel=(g.boss+Vector2.up*1.2f-s.pos).normalized*12;s.life=5;}
                    else if(s.returns){s.hitDone=true;s.returning=true;}
                    else remove=true;
                }
                if(s.life<=0||Mathf.Abs(s.pos.x)>15||s.pos.y< -2||s.pos.y>13)
                {if(!s.friendly&&!s.hitDone)g.PlayerHit(s.damage,s.parry,s.pattern,s.pos.x,false);if(s.returns){shieldCaught=true;shieldOut=false;}remove=true;}
                if(remove)g.shots.RemoveAt(i);
            }
        }
        public void TickHazards(float dt)
        {
            if(barrier)
            {
                barrierTime-=dt;if(barrierTime<=0)
                {barrier=false;g.PlayerHit(25,false,"barrier",g.boss.x);g.flash=.4f;g.Say("결계 폭발 · 다음에는 벽을 먼저 파괴하세요",3);}
            }
            for(int i=g.hazards.Count-1;i>=0;i--)
            {
                var h=g.hazards[i];h.delay-=dt;
                if(h.delay>0)continue;
                if(!h.hit)
                {
                    h.hit=true;bool overlaps=Mathf.Abs(g.player.x-h.x)<h.width*.5f+.3f;
                    g.PlayerHit(h.damage,h.parry,h.pattern,h.x,overlaps);
                    if(overlaps)g.Burst(g.player+Vector2.up,1,h.color,12);
                }
                h.life-=dt;if(h.life<=0)g.hazards.RemoveAt(i);
            }
        }
        public void TickMinions(float dt)
        {
            foreach(var m in g.minions)
            {
                m.x=Mathf.MoveTowards(m.x,g.player.x,dt*g.spec.minionSpeed);m.attack-=dt;
                if(Mathf.Abs(m.x-g.player.x)<.85f&&m.attack<=0)
                {m.attack=1.4f;g.PlayerHit(9,true,m.pattern,m.x,g.player.y<1.5f);}
            }
        }
    }
}


