#if !HEADLESS && !DESKTOP_PREVIEW
using UnityEngine;

namespace ReversalTower
{
    public sealed class ScenePresentation : MonoBehaviour
    {
        public ActorView player;
        public ActorView arakus;
        public BossDefinition[] bosses;
        public BoxCollider2D leftBoundary,rightBoundary,ground;

        public void Render(TowerGame game)
        {
            bool battle=game.mode==ScreenMode.Battle||game.mode==ScreenMode.Result;
            if(player)player.Render(game,battle);
            bool showArakus=game.bossIndex==0;
            if(arakus){arakus.gameObject.SetActive(showArakus);if(showArakus)arakus.Render(game,battle);}
        }
    }
}
#endif
