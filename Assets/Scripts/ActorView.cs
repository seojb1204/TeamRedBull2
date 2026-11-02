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
            transform.position=new Vector3(position.x,position.y,0);
            transform.localScale=new Vector3(1.4f*facing,1.4f,1);
            body.color=(bossActor?game.bossFlash:game.hitFlash)>0?Color.white:new Color(.99f,.99f,.99f,1);
            string state="Idle";
            float normalized=Mathf.Repeat(game.realClock*.25f,1);
            if (battle && bossActor)
            {
                if(game.bossHp<=0){state="Death";normalized=.8f;}
                else if(game.bossState=="페이즈 전환"){state="Phase";normalized=Mathf.Clamp01(1-game.rest/2);}
                else if(game.bossStagger>0){state="Hit";normalized=.4f;}
                else if(game.current!=null)
                {
                    state=PoseForPattern(game.current.id);
                    normalized=game.patternAge<game.windup?0:game.attackStep<game.patternCount?.38f:.72f;
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
        }

        static string PoseForPattern(string id)
        {
            switch(id)
            {
                case "cleave": return "SwordSwing";
                case "shield": return "ShieldStrike";
                case "kick": return "Kick";
                case "charge": return "Charge";
                case "throw": return "ShieldThrow";
                case "quake": return "Sweep";
                default: return "Attack";
            }
        }
    }
}
#endif
