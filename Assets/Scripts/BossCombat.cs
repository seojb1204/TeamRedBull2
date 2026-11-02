using UnityEngine;
using System.Collections.Generic;

namespace ReversalTower
{
    public sealed partial class TowerGame
    {
        readonly Dictionary<string,float> cooldowns=new Dictionary<string,float>();
        readonly System.Random combatRandom=new System.Random();
        string previousPattern="";bool thirdPhase;
        void TickBoss(float dt)
        {
            if(bossHp<=0)return;
            if(!phaseTwo&&bossHp<spec.hp*.6f)
            {
                phaseTwo=true;phaseFlash=1;current=null;rest=2;bossStagger=0;bossState="페이즈 전환";shake=4;
                Say(bossIndex==0?"철의 맹세가 깨졌다 · 아라쿠스가 분노합니다":bossIndex==4?"일식 · 잔상이 깨어납니다":"두 번째 맹세 · 패턴이 강화됩니다",3);
                Burst(boss+Vector2.up,2,Palette.Gold,40);
            }
            if(bossIndex==4&&phaseTwo&&!thirdPhase&&bossHp<spec.hp*.3f)
            {thirdPhase=true;current=null;rest=2;phaseFlash=1;Say("마지막 잔상 · 심연의 끝",3);}
            if(bossStagger>0){bossStagger-=dt;bossState="그로기";return;}
            if(bossIndex==1)boss.y=barrier?1.5f:1.05f+Mathf.Sin(seconds*1.5f)*.18f;
            if(current==null)
            {
                rest-=dt;bossState=rest>0?"빈틈":"추적";
                if(rest>0)return;
                float distance=Mathf.Abs(boss.x-player.x);
                bossFace=player.x>=boss.x?1:-1;
                if(distance>3.1f&&bossIndex!=1){boss.x=Mathf.MoveTowards(boss.x,player.x,dt*spec.speed*(phaseTwo?1.18f:1));if(distance>5&&combatRandom.NextDouble()>.015)return;}
                if(bossIndex==1&&distance<3){boss.x=Mathf.MoveTowards(boss.x,-Mathf.Sign(player.x)*7,dt*4);}
                SelectPattern(distance);return;
            }
            patternAge+=dt;
            bossState=patternAge<windup?"공격 예고":patternAge<nextHit?"연격":"공격";
            if(patternAge<windup)
            {
                if(current.kind==AttackKind.Slash&&patternAge<windup*.5f)
                {
                    bossFace=player.x>=boss.x?1:-1;
                    if(Mathf.Abs(boss.x-player.x)>current.range*.75f)boss.x=Mathf.MoveTowards(boss.x,player.x,dt*spec.speed*.7f);
                }
                return;
            }
            if(current.kind==AttackKind.Dash&&attackStep==1)
            {
                float old=boss.x;boss.x+=bossFace*dt*(bossIndex==0?13:18);
                bool column=bossIndex==0&&((pillarLeft&&Crossed(old,boss.x,-4))||(pillarRight&&Crossed(old,boss.x,4)));
                if(column)
                {
                    if(boss.x<0)pillarLeft=false;else pillarRight=false;
                    bossStagger=3.2f;current=null;rest=.5f;shake=5;sound.Play("hit");Say("기둥 충돌 · 공격 기회!",2);Burst(boss+Vector2.up,2,Palette.Gold,30);return;
                }
                if(!dashHit&&Mathf.Abs(player.x-boss.x)<1.2f)
                {dashHit=true;PlayerHit(patternDamage,current.parryable,current.id,boss.x,player.y<2.2f);if(bossStagger>0){current=null;rest=.5f;return;}}
                if(boss.x<Left+1||boss.x>Right-1||patternAge>windup+1.1f)
                {boss.x=Mathf.Clamp(boss.x,Left+1,Right-1);if(!dashHit)PlayerHit(patternDamage,current.parryable,current.id,boss.x,false);attackStep=2;nextHit=patternAge;}
            }
            if(attackStep<patternCount&&patternAge>=nextHit)
            {
                AutoNoteHitLateness(patternAge-nextHit);
                ExecutePattern();if(ended||current==null)return;
            }
            float endTime=nextHit+recovery;
            if(attackStep>=patternCount&&patternAge>endTime)
            {
                if(bossIndex==4&&phaseTwo&&current.kind==AttackKind.Slash)
                    hazards.Add(new Hazard{x=origin.x+bossFace*2,width=4.5f,delay=.7f,life=.2f,damage=patternDamage*.7f,pattern=current.id,parry=true,color=Palette.Purple});
                previousPattern=current.id;current=null;rest=phaseTwo?.45f:.7f;bossState="빈틈";
            }
        }
        bool dashHit;
        static bool Crossed(float old,float now,float point)=>(old-point)*(now-point)<=0;
        void SelectPattern(float distance)
        {
            var candidates=new List<Pattern>();
            foreach(var p in spec.patterns)
            {
                if(cooldowns.TryGetValue(p.id,out float until)&&seconds<until)continue;
                if(p.id==previousPattern)continue;
                if(p.kind==AttackKind.Slash&&distance>p.range+1.5f)continue;
                if(p.kind==AttackKind.Barrier&&!phaseTwo)continue;
                if(p.kind==AttackKind.Summon&&minions.Count>=5)continue;
                if(p.kind==AttackKind.Balance&&seconds<12)continue;
                candidates.Add(p);
            }
            if(candidates.Count==0){rest=.2f;return;}
            Pattern selected=candidates[combatRandom.Next(candidates.Count)];
            if(seconds<4)selected=spec.patterns[0];
            BeginPattern(selected);
        }
        void BeginPattern(Pattern p)
        {
            current=p;patternAge=0;attackStep=0;dashHit=false;bossFace=player.x>=boss.x?1:-1;
            origin=boss;targetX=player.x;
            int level=Stat(p.id).level;
            float[] telegraph={1.3f,1.15f,1,.9f,.8f},damage={.8f,.9f,1,1.1f,1.2f},cooldown={1.25f,1.12f,1,.9f,.8f};
            windup=Mathf.Max(.4f,p.windup*telegraph[level+2]/(phaseTwo?1.12f:1));
            recovery=p.recovery/(phaseTwo?1.12f:1);patternDamage=p.damage*damage[level+2];patternCount=p.count;
            if(phaseTwo&&p.kind==AttackKind.Volley)patternCount++;
            if(p.count>1&&level!=0)patternCount=Mathf.Max(1,patternCount+(level==2?1:level==-2?-1:0));
            if(p.kind==AttackKind.Dash)patternCount=2;
            nextHit=windup;cooldowns[p.id]=seconds+(p.kind==AttackKind.Balance||p.kind==AttackKind.Barrier?18:4)*cooldown[level+2];
            if(p.kind==AttackKind.Balance)
            {
                int left=0,right=0;foreach(var m in minions)if(m.x<0)left++;else right++;
                safeSide=left<=right?-1:1;
                Say((safeSide<0?"왼쪽":"오른쪽")+"의 빛나는 안전지대로 이동하세요",windup);
            }
            if(p.kind==AttackKind.Barrier){Say("결계벽을 부수세요 · 실패하면 광역 피해",4);}
        }
        void ExecutePattern()
        {
            var p=current;
            if(p.kind==AttackKind.Slash)
            {
                bool radial=p.id=="spin";
                bool overlap=Mathf.Abs(player.x-boss.x)<p.range&&Mathf.Abs(player.y-boss.y)<2.2f&&(radial||(player.x-boss.x)*bossFace>-.3f);
                PlayerHit(patternDamage,p.parryable,p.id,boss.x,overlap);
                Burst(boss+new Vector2(bossFace*1.5f,1.25f),.6f,p.parryable?Palette.Gold:Palette.Red,7);
                if(bossIndex==3&&phaseTwo)hazards.Add(new Hazard{x=boss.x,width=1.5f,delay=.8f,life=.25f,damage=14,pattern=p.id,color=Palette.Purple});
            }
            else if(p.kind==AttackKind.Dash)
            {if(attackStep==0){attackStep=1;nextHit=windup+1.2f;sound.Play("dash");return;}else{attackStep=patternCount;nextHit=patternAge;return;}}
            else if(p.kind==AttackKind.Volley)
            {
                Vector2 spawn=boss+new Vector2(bossFace*.75f,1.25f);
                Vector2 aim=(player+Vector2.up*.9f-spawn).normalized;
                if(p.id=="fan")aim=Quaternion.Euler(0,0,(attackStep-(patternCount-1)*.5f)*13)*aim;
                FireShot(spawn,aim*(bossIndex==0?7:8),patternDamage,p.parryable,p.id);
            }
            else if(p.kind==AttackKind.Rain)
            {
                float x=attackStep==0?targetX:Mathf.Clamp(player.x+(attackStep%2==0?1.4f:-1.4f),Left+1,Right-1);
                hazards.Add(new Hazard{x=x,width=1.6f,delay=.85f,life=.25f,damage=patternDamage,pattern=p.id,color=Palette.Red});
            }
            else if(p.kind==AttackKind.Pillar)
            {
                hazards.Add(new Hazard{x=Mathf.Clamp(targetX+(attackStep-(patternCount-1)*.5f)*3,Left+1,Right-1),width=1.2f,delay=.75f,life=.35f,damage=patternDamage,pattern=p.id,color=bossIndex==1?Palette.Gold:Palette.Purple});
            }
            else if(p.kind==AttackKind.Wave)
            {
                FireShot(new Vector2(boss.x,.35f),new Vector2(bossFace*(6+attackStep),0),patternDamage,false,p.id);
                var last=shots[shots.Count-1];last.radius=.4f;
            }
            else if(p.kind==AttackKind.Summon)
            {if(minions.Count<6)minions.Add(new Minion{x=(attackStep%2==0?-1:1)*(7+attackStep*.5f),hp=24+bossIndex*4,pattern=p.id});}
            else if(p.kind==AttackKind.Balance)
            {hazards.Add(new Hazard{x=safeSide<0?6.3f:-6.3f,width=12.2f,delay=.03f,life=.25f,damage=patternDamage,pattern=p.id,color=Palette.Red,wide=true});}
            else if(p.kind==AttackKind.Barrier)
            {
                barrier=true;barrierHp=45;barrierTime=7;
                boss.x=player.x<0?7:-7;patternCount=1;
            }
            else if(p.kind==AttackKind.Echo)
            {hazards.Add(new Hazard{x=targetX+(attackStep-1)*2.2f,width=3.2f,delay=.6f,life=.2f,damage=patternDamage,parry=true,pattern=p.id,color=Palette.Purple});}
            attackStep++;
            float interval=p.kind==AttackKind.Slash?.32f:p.kind==AttackKind.Rain?.35f:p.kind==AttackKind.Volley?.3f:.22f;
            int l=Stat(p.id).level;interval*=l<0?1.1f:l>0?.95f:1;
            nextHit=patternAge+interval;
        }
        void FireShot(Vector2 pos,Vector2 velocity,float damage,bool parry,string pattern,bool friendly=false)
        {shots.Add(new Shot{pos=pos,vel=velocity,damage=damage,parry=parry,pattern=pattern,friendly=friendly});}
        void TickShots(float dt)
        {
            for(int i=shots.Count-1;i>=0;i--)
            {
                var s=shots[i];s.life-=dt;s.pos+=s.vel*dt;bool remove=false;
                if(s.friendly)
                {
                    Vector2 aim=(boss+Vector2.up*1.3f-s.pos).normalized;
                    s.vel=Vector2.Lerp(s.vel,aim*12,dt*9);
                    if(Vector2.Distance(s.pos,boss+Vector2.up*1.2f)<1.2f){DamageBoss(s.damage);remove=true;if(ended){shots.RemoveAt(i);return;}}
                }
                else if(s.pos.x>player.x-.5f-s.radius&&s.pos.x<player.x+.5f+s.radius&&s.pos.y>player.y-s.radius&&s.pos.y<player.y+1.65f+s.radius)
                {
                    int before=parries;PlayerHit(s.damage,s.parry,s.pattern,s.pos.x);
                    if(parries>before){s.friendly=true;s.damage*=2.5f;s.vel=(boss+Vector2.up*1.2f-s.pos).normalized*12;s.life=5;}else remove=true;
                }
                if(s.life<=0||Mathf.Abs(s.pos.x)>15||s.pos.y< -2||s.pos.y>13)
                {if(!s.friendly)PlayerHit(s.damage,s.parry,s.pattern,s.pos.x,false);remove=true;}
                if(remove)shots.RemoveAt(i);
            }
        }
        void TickHazards(float dt)
        {
            if(barrier)
            {
                barrierTime-=dt;if(barrierTime<=0)
                {barrier=false;PlayerHit(25,false,"barrier",boss.x);flash=.4f;Say("결계 폭발 · 다음에는 벽을 먼저 파괴하세요",3);}
            }
            for(int i=hazards.Count-1;i>=0;i--)
            {
                var h=hazards[i];h.delay-=dt;
                if(h.delay>0)continue;
                if(!h.hit)
                {
                    h.hit=true;bool overlaps=Mathf.Abs(player.x-h.x)<h.width*.5f+.3f;
                    PlayerHit(h.damage,h.parry,h.pattern,h.x,overlaps);
                    if(overlaps)Burst(player+Vector2.up,1,h.color,12);
                }
                h.life-=dt;if(h.life<=0)hazards.RemoveAt(i);
            }
        }
        void TickMinions(float dt)
        {
            foreach(var m in minions)
            {
                m.x=Mathf.MoveTowards(m.x,player.x,dt*(bossIndex==4?1.5f:1));m.attack-=dt;
                if(Mathf.Abs(m.x-player.x)<.85f&&m.attack<=0)
                {m.attack=1.4f;PlayerHit(9,true,m.pattern,m.x,player.y<1.5f);}
            }
        }
    }
}
