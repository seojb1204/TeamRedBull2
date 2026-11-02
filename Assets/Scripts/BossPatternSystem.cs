using UnityEngine;
using System.Collections.Generic;

namespace ReversalTower
{
    public sealed class BossPatternSystem
    {
        readonly TowerGame g;
        public Pattern current;
        public float patternAge,windup,recovery,rest,patternDamage,targetX,safeSide,nextHit,bossStagger,barrierHp,barrierTime;
        public int patternCount,attackStep;
        public Vector2 origin;
        public bool phaseTwo,thirdPhase,pillarLeft,pillarRight,barrier;
        public BossPatternSystem(TowerGame game) { g = game; }
        public void Reset() { cooldowns.Clear(); previousPattern=""; dashHit=false; }
        readonly Dictionary<string,float> cooldowns=new Dictionary<string,float>();
        readonly System.Random combatRandom=new System.Random();
        string previousPattern="";
        public void TickBoss(float dt)
        {
            if(g.bossHp<=0)return;
            if(!phaseTwo&&g.bossHp<g.spec.hp*g.spec.phaseThreshold)
            {
                phaseTwo=true;g.phaseFlash=1;current=null;rest=2;bossStagger=0;g.bossState="페이즈 전환";g.shake=4;
                g.Say(g.spec.hasPillars?"철의 맹세가 깨졌다 · 아라쿠스가 분노합니다":g.spec.echoBoss?"일식 · 잔상이 깨어납니다":"두 번째 맹세 · 패턴이 강화됩니다",3);
                g.Burst(g.boss+Vector2.up,2,Palette.Gold,40);
            }
            if(g.spec.echoBoss&&phaseTwo&&!thirdPhase&&g.bossHp<g.spec.hp*.3f)
            {thirdPhase=true;current=null;rest=2;g.phaseFlash=1;g.Say("마지막 잔상 · 심연의 끝",3);}
            if(bossStagger>0){bossStagger-=dt;g.bossState="그로기";return;}
            if(g.spec.airborne)g.boss.y=barrier?1.5f:1.05f+Mathf.Sin(g.seconds*1.5f)*.18f;
            if(current==null)
            {
                rest-=dt;g.bossState=rest>0?"빈틈":"추적";
                if(rest>0)return;
                float distance=Mathf.Abs(g.boss.x-g.player.x);
                g.bossFace=g.player.x>=g.boss.x?1:-1;
                if(distance>3.1f&&!g.spec.airborne){g.boss.x=Mathf.MoveTowards(g.boss.x,g.player.x,dt*g.spec.speed*(phaseTwo?1.18f:1));if(distance>5&&combatRandom.NextDouble()>.015)return;}
                if(g.spec.airborne&&distance<3){g.boss.x=Mathf.MoveTowards(g.boss.x,-Mathf.Sign(g.player.x)*7,dt*4);}
                SelectPattern(distance);return;
            }
            patternAge+=dt;
            g.bossState=patternAge<windup?"공격 예고":patternAge<nextHit?"연격":"공격";
            if(patternAge<windup)
            {
                if(current.kind==AttackKind.Slash&&patternAge<windup*.5f)
                {
                    g.bossFace=g.player.x>=g.boss.x?1:-1;
                    if(Mathf.Abs(g.boss.x-g.player.x)>current.range*.75f)g.boss.x=Mathf.MoveTowards(g.boss.x,g.player.x,dt*g.spec.speed*.7f);
                }
                return;
            }
            if(current.kind==AttackKind.Dash&&attackStep==1)
            {
                float old=g.boss.x;g.boss.x+=g.bossFace*dt*g.spec.dashSpeed;
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
                previousPattern=current.id;current=null;rest=phaseTwo?.45f:.7f;g.bossState="빈틈";
            }
        }
        bool dashHit;
        static bool Crossed(float old,float now,float point)=>(old-point)*(now-point)<=0;
        public void SelectPattern(float distance)
        {
            var candidates=new List<Pattern>();
            foreach(var p in g.spec.patterns)
            {
                if(cooldowns.TryGetValue(p.id,out float until)&&g.seconds<until)continue;
                if(p.id==previousPattern)continue;
                if(p.kind==AttackKind.Slash&&distance>p.range+1.5f)continue;
                if(p.kind==AttackKind.Barrier&&!phaseTwo)continue;
                if(p.kind==AttackKind.Summon&&g.minions.Count>=5)continue;
                if(p.kind==AttackKind.Balance&&g.seconds<12)continue;
                candidates.Add(p);
            }
            if(candidates.Count==0){rest=.2f;return;}
            Pattern selected=candidates[combatRandom.Next(candidates.Count)];
            if(g.seconds<4)selected=g.spec.patterns[0];
            BeginPattern(selected);
        }
        public void BeginPattern(Pattern p)
        {
            current=p;patternAge=0;attackStep=0;dashHit=false;g.bossFace=g.player.x>=g.boss.x?1:-1;
            origin=g.boss;targetX=g.player.x;
            int level=g.Stat(p.id).level;
            float[] telegraph={1.3f,1.15f,1,.9f,.8f},damage={.8f,.9f,1,1.1f,1.2f},cooldown={1.25f,g.spec.phaseSpeedScale,1,.9f,.8f};
            windup=Mathf.Max(.4f,p.windup*telegraph[level+2]/(phaseTwo?g.spec.phaseSpeedScale:1));
            recovery=p.recovery/(phaseTwo?g.spec.phaseSpeedScale:1);patternDamage=p.damage*damage[level+2];patternCount=p.count;
            if(phaseTwo&&p.kind==AttackKind.Volley)patternCount++;
            if(p.count>1&&level!=0)patternCount=Mathf.Max(1,patternCount+(level==2?1:level==-2?-1:0));
            if(p.kind==AttackKind.Dash)patternCount=2;
            nextHit=windup;cooldowns[p.id]=g.seconds+(p.kind==AttackKind.Balance||p.kind==AttackKind.Barrier?18:4)*cooldown[level+2];
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
                bool overlap=Mathf.Abs(g.player.x-g.boss.x)<p.range&&Mathf.Abs(g.player.y-g.boss.y)<2.2f&&(radial||(g.player.x-g.boss.x)*g.bossFace>-.3f);
                g.PlayerHit(patternDamage,p.parryable,p.id,g.boss.x,overlap);
                g.Burst(g.boss+new Vector2(g.bossFace*1.5f,1.25f),.6f,p.parryable?Palette.Gold:Palette.Red,7);
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
        public void FireShot(Vector2 pos,Vector2 velocity,float damage,bool parry,string pattern,bool friendly=false)
        {g.shots.Add(new TowerGame.Shot{pos=pos,vel=velocity,damage=damage,parry=parry,pattern=pattern,friendly=friendly});}
        public void TickShots(float dt)
        {
            for(int i=g.shots.Count-1;i>=0;i--)
            {
                var s=g.shots[i];s.life-=dt;s.pos+=s.vel*dt;bool remove=false;
                if(s.friendly)
                {
                    Vector2 aim=(g.boss+Vector2.up*1.3f-s.pos).normalized;
                    s.vel=Vector2.Lerp(s.vel,aim*12,dt*9);
                    if(Vector2.Distance(s.pos,g.boss+Vector2.up*1.2f)<1.2f){g.DamageBoss(s.damage);remove=true;if(g.BattleEnded){g.shots.RemoveAt(i);return;}}
                }
                else if(s.pos.x>g.player.x-.5f-s.radius&&s.pos.x<g.player.x+.5f+s.radius&&s.pos.y>g.player.y-s.radius&&s.pos.y<g.player.y+1.65f+s.radius)
                {
                    int before=g.parries;g.PlayerHit(s.damage,s.parry,s.pattern,s.pos.x);
                    if(g.parries>before){s.friendly=true;s.damage*=2.5f;s.vel=(g.boss+Vector2.up*1.2f-s.pos).normalized*12;s.life=5;}else remove=true;
                }
                if(s.life<=0||Mathf.Abs(s.pos.x)>15||s.pos.y< -2||s.pos.y>13)
                {if(!s.friendly)g.PlayerHit(s.damage,s.parry,s.pattern,s.pos.x,false);remove=true;}
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


