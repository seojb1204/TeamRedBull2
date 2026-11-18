#if !HEADLESS && !DESKTOP_PREVIEW
using UnityEngine;

namespace ReversalTower
{
    // Read-only projection of SweepEcho. No combat events, timers or hitboxes are created here.
    public sealed class ArakusEchoView : MonoBehaviour
    {
        SpriteRenderer body,low,high;
        void Hide(){if(body)body.enabled=false;if(low)low.enabled=false;if(high)high.enabled=false;}
        void OnDisable()=>Hide();
        SpriteRenderer Create(string label)
        {var o=new GameObject(label);o.transform.SetParent(transform,false);var r=o.AddComponent<SpriteRenderer>();r.enabled=false;return r;}
        void Draw(SpriteRenderer r,string id,int frame,Vector2 pos,int face,float scale,Color color,ActorView player)
        {
            var sprites=ArakusVisuals.Sprites(id);if(sprites.Length==0)return;
            r.sprite=sprites[Mathf.Clamp(frame,0,sprites.Length-1)];r.sharedMaterial=player.body.sharedMaterial;
            r.sortingLayerID=player.body.sortingLayerID;r.sortingOrder=player.body.sortingOrder-1;r.color=color;
            r.flipX=false;r.flipY=false;r.transform.position=new Vector3(pos.x,pos.y,0);
            var parent=transform.lossyScale;
            // Divide by signed scale so a mirrored scene parent cannot reverse the echo's facing.
            float x=Mathf.Abs(parent.x)<.0001f?.0001f:parent.x,y=Mathf.Abs(parent.y)<.0001f?.0001f:parent.y;
            r.transform.localScale=new Vector3(face*scale/x,scale/y,1);r.transform.rotation=Quaternion.identity;r.enabled=true;
        }
        void Effect(SpriteRenderer r,string id,float elapsed,Vector2 pos,int face,ActorView player)
        {
            if(elapsed<0||elapsed>=.6f)return;
            int frame=ArakusAnimationSelection.NativeFrame(id,elapsed/.6f*ArakusAnimationSelection.Find(id).Duration);
            Draw(r,id,frame,pos,face,ArakusGeometry.LegacyEffectScale,new Color(1,.25f,.25f,.7f),player);
        }
        public void Render(TowerGame g,ActorView player)
        {
            Hide();
            if(g==null||g.echo==null||g.mode!=ScreenMode.Battle||!player||!player.body||!ArakusVisuals.HasArt)return;
            if(!body)body=Create("Arakus ultimate echo");if(!low)low=Create("Echo low sweep");if(!high)high=Create("Echo high sweep");
            var e=g.echo;float age=e.age;
            var pose=ArakusAnimationSelection.Sample(new ArakusAnimationSelection.Input{combat=true,pattern="sweep",age=age,
                windup=TowerGame.EchoWindup,recovery=TowerGame.EchoRecovery,tempo=1f/TowerGame.EchoGap},age);
            float alpha=.55f*Mathf.Min(Mathf.Clamp01(age/.15f),Mathf.Clamp01((TowerGame.EchoLife-age)/.25f));
            var pos=new Vector2(e.pos.x,0);
            Draw(body,pose.state,pose.frame,pos,e.face,ArakusGeometry.VisualScale,new Color(1,.25f,.25f,alpha),player);
            Effect(low,"FX_WideHorizontal",age-TowerGame.EchoWindup,e.pos,e.face,player);
            Effect(high,"FX_WideVertical",age-TowerGame.EchoWindup-TowerGame.EchoGap,e.pos+new Vector2(e.face*.7f,0),e.face,player);
        }
    }
}
#endif
