#if !HEADLESS && !DESKTOP_PREVIEW
using UnityEngine;

namespace ReversalTower
{
    public sealed class ScenePresentation : MonoBehaviour
    {
        SkillEffectVisuals skillEffects;
        public ActorView player;
        public ActorView arakus;
        public PhanesView phanes;
        public bool ShowsPhanes=>phanes&&phanes.gameObject.activeSelf;
        public BossDefinition[] bosses;
        public BoxCollider2D leftBoundary,rightBoundary,ground;

        public void Render(TowerGame game)
        {
            bool battle=game.mode==ScreenMode.Battle||game.mode==ScreenMode.Result;
            if(!skillEffects){skillEffects=GetComponent<SkillEffectVisuals>()??gameObject.AddComponent<SkillEffectVisuals>();skillEffects.Preload();}
            if(player)player.Render(game,battle);
            bool showArakus=game.bossIndex==0;
            if(arakus){arakus.gameObject.SetActive(showArakus);if(showArakus)arakus.Render(game,battle);}
            bool showPhanes=game.bossIndex==1&&PhanesView.HasArt;
            if(showPhanes&&!phanes){var o=new GameObject("Phanes runtime presentation");o.transform.SetParent(transform,false);phanes=o.AddComponent<PhanesView>();}
            if(phanes){phanes.gameObject.SetActive(showPhanes);if(showPhanes)phanes.Render(game,battle,arakus);}
            skillEffects.Render(game,player,arakus);
        }
    }
}
#endif
