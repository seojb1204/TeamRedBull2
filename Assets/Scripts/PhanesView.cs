#if !HEADLESS && !DESKTOP_PREVIEW
using System.Collections.Generic;
using UnityEngine;
namespace ReversalTower
{
    public sealed class PhanesView : MonoBehaviour
    {
        public const float BodyWorldHeight=2.2f;
        static readonly Dictionary<string,Sprite[]> cache=new Dictionary<string,Sprite[]>();
        public SpriteRenderer body;
        SpriteRenderer[] rim;
        string previous;float started,lastClock=-1,lastAge=-1;Pattern lastPattern;
        public static bool HasArt=>Resources.Load<Texture2D>("PhanesV1/idle-cast")!=null;
        public static Sprite[] Frames(string name)
        {
            if(cache.TryGetValue(name,out var frames))return frames;
            var tex=Resources.Load<Texture2D>("PhanesV1/"+name);if(!tex)return null;
            int rows=name=="idle-cast"?2:4;float ratio=tex.width/1536f,baseline=(rows==2?470:335)*ratio,bodyPx=(rows==2?395:300)*ratio;
            int w=tex.width/4,h=tex.height/rows;frames=new Sprite[4*rows];
            for(int i=0;i<frames.Length;i++)
            {
                // Audited opaque contact rows in the current 1254px basic sheet.
                // Align the seated body/staff and recovery feet to logical ground, not the standing pivot.
                float contact=baseline;
                if(name=="basic"&&i>=8&&i<=11)contact=(i==8?290:i==11?278:288)*(tex.width/1254f);
                frames[i]=Sprite.Create(tex,new Rect((i%4)*w,tex.height-(i/4+1)*h,w,h),new Vector2(.5f,(h-contact)/h),bodyPx*1.4f/BodyWorldHeight,0,SpriteMeshType.FullRect);
                frames[i].name=name+"_"+i.ToString("D2");
            }
            cache[name]=frames;return frames;
        }
        public void Render(TowerGame g,bool battle,ActorView reference)
        {
            if(!body){body=gameObject.AddComponent<SpriteRenderer>();body.sortingOrder=reference&&reference.body?reference.body.sortingOrder:20;if(reference&&reference.body){body.sharedMaterial=reference.body.sharedMaterial;body.sortingLayerID=reference.body.sortingLayerID;}}
            float clock=battle&&g.mode==ScreenMode.Battle&&g.bossHp>0?g.seconds:g.realClock;
            string state=g.bossHp<=0&&battle?"death":g.bossStagger>0?"groggy":g.bossFlash>0?"hurt":g.barrier?"barrier":g.bossState=="페이즈 전환"?"phase":g.current!=null?g.current.id:"idle";
            if(state!=previous||clock<lastClock||g.current!=lastPattern||g.patternAge<lastAge){previous=state;started=clock;}
            var pose=PhanesPose.Sample(battle&&g.current!=null?g.current.id:null,g.patternAge,g.windup,g.recovery,clock-started,battle&&g.bossHp<=0,battle&&g.bossStagger>0,battle&&g.bossFlash>0,battle&&g.barrier,battle&&g.bossState=="페이즈 전환",battle?g.BossPatterns.phanesDownStage:0,g.BossPatterns.phanesDownAge);
            var sprites=Frames(pose.sheet);if(sprites==null){body.enabled=false;return;}body.enabled=true;body.sprite=sprites[pose.index];
            Vector2 pos=battle?g.boss:new Vector2(6,1.05f);int face=battle?g.bossFace:-1;
            transform.position=new Vector3(pos.x+(battle?g.bossRecoil:0),pos.y,0);transform.localScale=new Vector3(1.4f*face,1.4f,1);
            body.color=battle&&g.bossFlash>0?Color.white:new Color(.99f,.99f,.99f,1);
            Glow(g,battle);lastClock=clock;lastPattern=g.current;lastAge=g.patternAge;
        }
        void Glow(TowerGame g,bool battle)
        {
            float level=battle?Mathf.Max(g.BossPatterns.parryGlow,Mathf.Clamp01(1-g.BossPatterns.parryFlash/BossPatternSystem.ParryFlashTime)):0;
            if(rim==null&&level>.01f&&ParryGlow.Material){rim=new SpriteRenderer[8];for(int i=0;i<8;i++){var o=new GameObject("Phanes parry rim");o.transform.SetParent(transform,false);rim[i]=o.AddComponent<SpriteRenderer>();rim[i].sharedMaterial=ParryGlow.Material;rim[i].sortingOrder=body.sortingOrder-1;rim[i].sortingLayerID=body.sortingLayerID;}}
            if(rim==null)return;
            for(int i=0;i<rim.Length;i++){var r=rim[i];r.enabled=level>.01f;r.sprite=body.sprite;r.color=Palette.Alpha(Palette.ParryRim,.45f*level);float a=i*Mathf.PI/4;r.transform.localPosition=new Vector3(Mathf.Cos(a)*.045f,Mathf.Sin(a)*.045f,0);}
        }
    }
}
#endif
