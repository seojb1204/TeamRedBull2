#if !HEADLESS && !DESKTOP_PREVIEW
using UnityEngine;

namespace ReversalTower
{
    public sealed class ActorView : MonoBehaviour
    {
        public SpriteRenderer body;
        public Animator animator;
        public bool bossActor;
        public string sourcePack;

        public void Render(TowerGame game, bool battle)
        {
            Vector2 position = bossActor ? game.boss : game.player;
            int facing = bossActor ? game.bossFace : game.face;
            if (!battle) { position=new Vector2(bossActor?6:-5.3f,0); facing=bossActor?-1:1; }
            transform.position=new Vector3(position.x+(battle&&bossActor?game.bossRecoil:0),position.y,0);   // 맞으면 살짝 밀려 보임
            transform.localScale=new Vector3(1.4f*facing,1.4f,1);
            body.color=(bossActor?game.bossFlash:game.hitFlash)>0?Color.white:new Color(.99f,.99f,.99f,1);
            if(bossActor&&battle&&game.bossFlash<=0)
            {
                // 아라쿠스 분노: 시전 중 붉게 맥동, 분노 뒤 계속 붉은 기
                var bp=game.BossPatterns;bool casting=game.current!=null&&game.current.kind==AttackKind.Rage&&game.patternAge<game.windup;
                if(casting){float k=.5f+.5f*Mathf.Sin(game.realClock*18);body.color=new Color(1,.55f+.25f*k,.5f+.25f*k,1);}
                else if(bp.rageActive)body.color=new Color(1,.78f,.74f,1);
            }
            string state="Idle";
            float normalized=Mathf.Repeat(game.realClock*.25f,1);
            if (battle && bossActor)
            {
                if(game.bossHp<=0){state="Death";normalized=.8f;}
                else if(game.bossState=="페이즈 전환"){state=game.spec!=null&&game.spec.hasPillars?"Hit":"Phase";normalized=game.spec!=null&&game.spec.hasPillars?.4f:Mathf.Clamp01(1-game.rest/2);}
                else if(game.bossStagger>0){state="Hit";normalized=.4f;}
                else if(game.current!=null)
                {
                    state=PoseForPattern(game.current.id);
                    int frame=game.BossPatterns.poseFrame;
                    // 패턴이 고른 프레임 (클립은 프레임 N개를 같은 간격으로 놓고 마지막을 한 번 더 둔다 → 중앙 = (k+0.5)/(N+1))
                    normalized=frame>=0?(frame+.5f)/(FramesOf(state)+1):game.patternAge<game.windup?0:game.attackStep<game.patternCount?.38f:.72f;
                }
                else if(game.bossState=="추적")state="Run";
            }
            else if(battle)
            {
                if(game.playerHp<=0){state="Death";normalized=.9f;}
                else if(game.stun>0){state="Hit";normalized=.35f;}
                else if(game.parryAge<.21f){state="Parry";normalized=.15f;}
                else if(game.dashTime>0)state="Dash";
                else if(game.actionTime>0)
                {
                    state=game.playerAction=="F"?"Idle":game.playerAction=="J"||game.playerAction=="E"?"Attack":"Skill";
                    normalized=Mathf.Clamp01(1-game.actionTime/Mathf.Max(.01f,game.actionTotal));
                }
                else if(game.pvY>1)state="Jump";
                else if(game.pvY< -1)state="Fall";
                else if(Mathf.Abs(game.horizontalSpeed)>.3f)state="Run";
            }
            if(animator&&animator.runtimeAnimatorController)
            {
                if(state=="Run"||state=="Dash")normalized=Mathf.Repeat(game.realClock*1.5f,1);
                animator.Play(state,0,Mathf.Min(.999f,normalized));
                animator.Update(0);
            }
            RenderGlow(game,battle);
        }

        // ── 빛 겹치기 (가산 재질) ──
        // 테두리 빛: 같은 그림을 8방향으로 살짝 어긋나게 겹침 (보스의 파란 패링 신호)
        // 몸 위 빛: 한 장 더 얹어 몸 전체를 밝힘 (패링 신호 / 맞았을 때 하얀 번쩍임 / 주인공 피격 붉은 번쩍임)
        SpriteRenderer[] glow;
        static readonly Vector2[] GlowDirections={new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1),new Vector2(.7f,.7f),new Vector2(-.7f,.7f),new Vector2(.7f,-.7f),new Vector2(-.7f,-.7f)};
        static readonly Color HurtRed=new Color(1f,.28f,.22f);
        void RenderGlow(TowerGame game,bool battle)
        {
            float level=0,flash=0,shine=0;Color shineColor=Palette.ParryCore;
            if(battle&&bossActor)
            {
                var bp=game.BossPatterns;level=bp.parryGlow;flash=Mathf.Clamp01(1-bp.parryFlash/BossPatternSystem.ParryFlashTime);
                shine=.16f*level+.45f*flash;
                float hit=Mathf.Clamp01(game.bossFlash/.1f);
                if(hit>0){shineColor=Color.Lerp(shineColor,Color.white,hit);shine=Mathf.Max(shine,.8f*hit);}
            }
            else if(battle)
            {
                float hurt=Mathf.Clamp01(game.hitFlash/.18f);
                if(hurt>0){shineColor=HurtRed;shine=.65f*hurt;}
            }
            bool rim=level>.01f||flash>0,on=rim||shine>.01f;
            if(glow==null)
            {
                if(!on||ParryGlow.Material==null)return;
                glow=new SpriteRenderer[GlowDirections.Length*2+1];
                for(int i=0;i<glow.Length;i++)
                {
                    bool overlay=i==glow.Length-1;
                    var child=new GameObject(overlay?"Glow Shine":"Glow Rim");child.transform.SetParent(body.transform,false);
                    var r=child.AddComponent<SpriteRenderer>();r.sharedMaterial=ParryGlow.Material;
                    r.sortingLayerID=body.sortingLayerID;r.sortingOrder=body.sortingOrder+(overlay?1:-1);glow[i]=r;
                }
            }
            int n=GlowDirections.Length;float k=Mathf.Max(level*(.85f+.15f*Mathf.Sin(game.realClock*22)),flash);
            for(int i=0;i<glow.Length;i++)
            {
                var r=glow[i];bool overlay=i==glow.Length-1;
                r.enabled=overlay?shine>.01f:rim;if(!r.enabled)continue;
                r.sprite=body.sprite;r.flipX=body.flipX;r.flipY=body.flipY;
                if(overlay){r.transform.localPosition=Vector3.zero;r.color=Palette.Alpha(shineColor,shine);continue;}
                // 안쪽 고리(약 2px)는 진하게, 바깥 고리(약 5px)는 옅게 → 부드러운 빛 번짐
                bool outer=i>=n;float reach=(outer?.11f:.045f)*(1+.6f*flash);
                r.transform.localPosition=(Vector3)(GlowDirections[i%n]*reach);
                r.color=Palette.Alpha(Palette.ParryRim,(outer?.16f:.42f)*k);
            }
        }

        static string PoseForPattern(string id)
        {
            switch(id)
            {
                case "cleave": return "SwordSwing";   // 거검 휘두르기
                case "shield": return "ShieldStrike"; // 방패 내려찍기 (일반 공격 2)
                case "kick": return "Kick";           // 발차기 (일반 공격 3)
                case "slash": return "Attack";        // 거검 횡베기 (일반 공격 1)
                case "charge": return "Charge";
                case "throw": return "ShieldThrow";   // 방패 투척 후 대쉬
                case "slam": return "ShieldSlam";     // 방패 던지고 내려찍기
                case "rush": return "ShieldDash";     // 방패 돌진
                case "sweep": return "Sweep";         // 일대 휩쓸기
                case "step": return "QuickStep";
                case "rage": return "Phase";          // 아라쿠스의 분노
                case "quake": return "Sweep";
                default: return "Attack";
            }
        }
        static int FramesOf(string state)=>state=="ShieldSlam"?5:state=="Sweep"?4:3;
    }
}
#endif
