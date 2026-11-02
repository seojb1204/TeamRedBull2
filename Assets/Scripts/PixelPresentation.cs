#if !DESKTOP_PREVIEW
using UnityEngine;
namespace ReversalTower
{
    // Built-in rendering pipeline: world at low resolution, IMGUI remains full resolution.
    public sealed class PixelPresentation:MonoBehaviour
    {
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            int scale=Mathf.Max(1,Mathf.Min(Screen.width/400,Screen.height/225));
            var low=RenderTexture.GetTemporary(Mathf.Max(1,Screen.width/scale),Mathf.Max(1,Screen.height/scale),0,source.format);
            low.filterMode=FilterMode.Point;
            Graphics.Blit(source,low);Graphics.Blit(low,destination);RenderTexture.ReleaseTemporary(low);
        }
    }
}
#endif
