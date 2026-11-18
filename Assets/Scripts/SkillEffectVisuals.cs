#if !HEADLESS && !DESKTOP_PREVIEW
using System.Collections.Generic;
using UnityEngine;
namespace ReversalTower
{
    // FX-only renderer. Reads game state; never changes damage, parry, cooldowns or hitboxes.
    public sealed class SkillEffectVisuals : MonoBehaviour
    {
        sealed class Sheet
        {
            public readonly string file;public readonly int width,height,count,columns,textureWidth,textureHeight;
            public readonly Vector2 pivot;public readonly float ppu;
            public Sprite[] frames;public bool loaded;
            public Sheet(string f,int w,int h,int n,int cols,int tw,int th,Vector2 p,float unit)
            {file=f;width=w;height=h;count=n;columns=cols;textureWidth=tw;textureHeight=th;pivot=p;ppu=unit;}
            public bool Load()
            {
                if(loaded)return frames!=null;loaded=true;
                var texture=Resources.Load<Texture2D>("SkillFxV1/"+file);
                if(!texture||texture.width!=textureWidth||texture.height!=textureHeight)
                {Debug.LogWarning("Skill FX unavailable or resized: "+file+"; legacy visual remains available.");return false;}
                frames=new Sprite[count];
                for(int i=0;i<count;i++)
                {
                    var rect=new Rect((i%columns)*width,textureHeight-(i/columns+1)*height,width,height);
                    frames[i]=Sprite.Create(texture,rect,pivot,ppu,0,SpriteMeshType.FullRect);
                    frames[i].name=file+"_"+i;
                }
                return true;
            }
        }
        // Match useful bright coverage to existing reach; empty transparent margins are not hitboxes.
        static readonly Sheet landing=new Sheet("arakus-landing",288,208,97,9,2592,2288,new Vector2(.5f,44f/208),44);
        static readonly Sheet spin=new Sheet("player-w-spin",208,104,25,4,832,728,new Vector2(.5f,20f/104),19);
        static readonly Sheet execution=new Sheet("player-e-execution",208,116,58,6,1248,1160,new Vector2(68f/208,20f/116),24);
        static Sheet Get(SkillFxTimeline.Kind k)=>k==SkillFxTimeline.Kind.Landing?landing:k==SkillFxTimeline.Kind.Spin?spin:execution;
        public static bool HasLanding=>landing.Load();
        public void Preload(){landing.Load();spin.Load();execution.Load();}
        sealed class Effect
        {public SkillFxTimeline.Kind kind;public float started;public int face;public Vector2 position;public bool anchored;public SpriteRenderer renderer;public Material material;public int order;public int layer;}
        readonly SkillFxTimeline timeline=new SkillFxTimeline();
        readonly SkillFxTimeline.Emission[] events=new SkillFxTimeline.Emission[2];
        readonly List<Effect> active=new List<Effect>();
        readonly List<SpriteRenderer> pool=new List<SpriteRenderer>();
        bool finishingResult;float finishClock,finishReal;
        public int VisibleCount=>active.Count;
        public int EmittedCount {get;private set;}
        SpriteRenderer Take()
        {
            foreach(var r in pool)if(!r.enabled)return r;
            var o=new GameObject("Skill FX sprite");o.transform.SetParent(transform,false);
            var created=o.AddComponent<SpriteRenderer>();created.enabled=false;pool.Add(created);return created;
        }
        void Clear(){foreach(var e in active)e.renderer.enabled=false;active.Clear();}
        void OnDisable(){Clear();timeline.Reset();finishingResult=false;}
        public void Render(TowerGame g,ActorView player,ActorView boss)
        {
            var p=g.current;
            var snap=new SkillFxTimeline.Snapshot{combat=g.mode==ScreenMode.Battle,dead=g.playerHp<=0,
                interrupted=g.stun>0||g.dashTime>0,clock=g.seconds,remaining=g.actionTime,total=g.actionTotal,action=g.playerAction,
                bossIndex=g.bossIndex,bossPattern=p!=null?p.id:null,patternToken=p,bossAge=g.patternAge,bossStruck=g.BossPatterns.struck};
            bool finishing=g.mode==ScreenMode.Result&&g.playerHp>0&&g.bossHp<=0;
            float cosmeticClock=g.seconds;int n=0;
            if(finishing)
            {
                // A killing blow transitions to Result during the same Update. Keep its impact visible.
                if(!finishingResult){finishingResult=true;finishClock=g.seconds;finishReal=g.realClock;}
                cosmeticClock=finishClock+Mathf.Max(0,g.realClock-finishReal);
            }
            else
            {
                finishingResult=false;n=timeline.Observe(snap,events);if(timeline.ResetThisFrame)Clear();
            }
            for(int i=0;i<n;i++)
            {
                var ev=events[i];var sheet=Get(ev.kind);var owner=ev.kind==SkillFxTimeline.Kind.Landing?boss:player;
                if(!owner||!owner.body||!sheet.Load())continue;
                if(active.Count>=6){active[0].renderer.enabled=false;active.RemoveAt(0);}
                bool isBoss=ev.kind==SkillFxTimeline.Kind.Landing;
                var e=new Effect{kind=ev.kind,started=g.seconds-ev.age,position=isBoss?g.boss:g.player,
                    face=isBoss?g.bossFace:g.face,anchored=isBoss,renderer=Take(),material=owner.body.sharedMaterial,
                    order=owner.body.sortingOrder+12,layer=owner.body.sortingLayerID};
                e.renderer.enabled=true;active.Add(e);EmittedCount++;
            }
            for(int i=active.Count-1;i>=0;i--)
            {
                var e=active[i];float age=cosmeticClock-e.started;bool isBoss=e.kind==SkillFxTimeline.Kind.Landing;
                bool cancelled=!finishing&&!isBoss&&age<SkillFxTimeline.Peak(e.kind)&&(!timeline.PlayerActive||timeline.PlayerAction!=(e.kind==SkillFxTimeline.Kind.Spin?"W":"E"));
                if(age<0||age>=SkillFxTimeline.Lifetime(e.kind)||cancelled){e.renderer.enabled=false;active.RemoveAt(i);continue;}
                if(!e.anchored){e.position=g.player;if(age>=SkillFxTimeline.Peak(e.kind))e.anchored=true;}
                var r=e.renderer;var sheet=Get(e.kind);r.sprite=sheet.frames[SkillFxTimeline.Frame(age,sheet.count)];
                r.sharedMaterial=e.material;r.sortingLayerID=e.layer;r.sortingOrder=e.order;
                r.color=new Color(1,1,1,SkillFxTimeline.Alpha(e.kind,age));
                r.transform.position=new Vector3(e.position.x,e.position.y,0);
                // Root parent normally has unit scale; compensate to avoid actor facing/scale double application.
                Vector3 parent=transform.lossyScale;
                r.transform.localScale=new Vector3(e.face/Mathf.Max(.0001f,Mathf.Abs(parent.x)),1/Mathf.Max(.0001f,Mathf.Abs(parent.y)),1);
                r.transform.rotation=Quaternion.identity;
            }
        }
    }
}
#endif
