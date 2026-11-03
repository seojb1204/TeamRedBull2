#if !HEADLESS && !DESKTOP_PREVIEW
using UnityEngine;

namespace ReversalTower
{
    /// <summary>
    /// 패링 신호(파란 빛)에 쓰는 가산 재질 (색은 Palette.ParryRim/ParryCore). 시점 계산은 BossPatternSystem.TickParryCue,
    /// 아라쿠스 그림 발광은 ActorView, 다른 보스·별빛 효과는 WorldArtist가 그린다.
    /// </summary>
    public static class ParryGlow
    {
        static Material material;static bool tried;
        public static Material Material
        {
            get
            {
                if(material==null&&!tried)
                {
                    tried=true;
                    var shader=Resources.Load<Shader>("ReversalTower/SpriteGlow")??Shader.Find("ReversalTower/SpriteGlow");
                    if(shader!=null)material=new Material(shader){name="Parry Glow (runtime)"};
                }
                return material;
            }
        }
    }
}
#endif
