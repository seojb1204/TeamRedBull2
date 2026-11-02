using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower
{
    public static class Palette
    {
        public static readonly Color Ink=new Color(.035f,.05f,.075f),Slate=new Color(.12f,.16f,.21f),Stone=new Color(.22f,.28f,.33f),Teal=new Color(.33f,.89f,.83f),Gold=new Color(1,.76f,.37f),Red=new Color(1,.30f,.32f),Purple=new Color(.69f,.49f,1),White=new Color(.91f,.92f,.87f);
        public static Color Alpha(Color c,float a){c.a=a;return c;}
    }
    public sealed class WorldArtist : MonoBehaviour
    {
        TowerGame g;Sprite box,disk,triangle;readonly List<SpriteRenderer> pool=new List<SpriteRenderer>();int cursor;
        public void Initialize(TowerGame game)
        {
            g=game;box=Make(0);disk=Make(1);triangle=Make(2);
        }
        Sprite Make(int type)
        {
            int n=32;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float nx=(x+.5f)/n*2-1,ny=(y+.5f)/n*2-1;
                float a=type==0?1:type==1?(nx*nx+ny*ny<1?1:0):(ny<1-2*Mathf.Abs(nx)?1:0);
                pixels[y*n+x]=new Color(1,1,1,a);
            }
            texture.SetPixels(pixels);texture.Apply();return Sprite.Create(texture,new Rect(0,0,n,n),new Vector2(.5f,.5f),n);
        }
        void Shape(Sprite sprite,float x,float y,float w,float h,Color color,float angle=0)
        {
            if(cursor>=pool.Count){var obj=new GameObject("Ink "+cursor);obj.transform.SetParent(transform);pool.Add(obj.AddComponent<SpriteRenderer>());}
            var r=pool[cursor];r.gameObject.SetActive(true);r.sprite=sprite;r.color=color;r.sortingOrder=cursor++;
            r.transform.position=new Vector3(x,y,0);r.transform.localScale=new Vector3(w,h,1);r.transform.rotation=Quaternion.Euler(0,0,angle);
        }
        void Rect(float x,float y,float w,float h,Color c,float a=0)=>Shape(box,x,y,w,h,c,a);
        void Disc(float x,float y,float w,float h,Color c)=>Shape(disk,x,y,w,h,c);
        void Tri(float x,float y,float w,float h,Color c,float a=0)=>Shape(triangle,x,y,w,h,c,a);
        void Line(Vector2 a,Vector2 b,float width,Color c)
        {var d=b-a;Rect((a.x+b.x)*.5f,(a.y+b.y)*.5f,d.magnitude,width,c,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);}
        public void Render()
        {
            cursor=0;bool fight=g.mode==ScreenMode.Battle||g.mode==ScreenMode.Result;float time=g.realClock;
            Rect(0,5,70,40,Palette.Ink);
            Disc(4,7.5f,19,19,new Color(.055f,.09f,.125f));Disc(4,7.5f,14,14,new Color(.075f,.12f,.15f));
            // Repeated arches, a broken celestial dial, and a deep lower vault.
            for(int i=-3;i<=3;i++)
            {
                float x=i*4.8f;Rect(x,4.5f,3.5f,9,new Color(.045f,.07f,.095f));
                Rect(x-1.8f,4.7f,.28f,10,Palette.Alpha(Palette.Stone,.28f));Rect(x+1.8f,4.7f,.28f,10,Palette.Alpha(Palette.Stone,.28f));
                Line(new Vector2(x-1.8f,9.7f),new Vector2(x,11.5f),.28f,Palette.Alpha(Palette.Stone,.28f));
                Line(new Vector2(x,11.5f),new Vector2(x+1.8f,9.7f),.28f,Palette.Alpha(Palette.Stone,.28f));
                Rect(x,7.2f,.08f,6,Palette.Alpha(Palette.Gold,.06f));
            }
            var castle=ExternalAssets.Picture("Art/Castle/parallaxBG-interior1.png");
            var pillars=ExternalAssets.Picture("Art/Castle/parallaxBG-interior2.png");
            if(pillars!=null)for(int i=-1;i<=1;i++)Shape(pillars,i*11.2f,6.7f,11.2f,14,new Color(.65f,.7f,.8f,.75f));
            if(castle!=null)for(int i=-1;i<=1;i++)Shape(castle,i*14,7,14,14,new Color(.8f,.85f,.95f,.75f));
            Color accent=g.bossIndex==4?Palette.Purple:g.bossIndex==1?Palette.Teal:Palette.Gold;
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI*2/48,rad=3.3f;
                Vector2 p=new Vector2(4+Mathf.Cos(a)*rad,6.4f+Mathf.Sin(a)*rad);
                Vector2 q=new Vector2(4+Mathf.Cos(a+.07f)*rad,6.4f+Mathf.Sin(a+.07f)*rad);
                Line(p,q,.035f,Palette.Alpha(accent,.18f));
                if(i%4==0)Line(p,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.25f,.04f,Palette.Alpha(accent,.25f));
            }
            Tri(4,6.4f,3,2.6f,Palette.Alpha(accent,.035f),180);
            for(int i=0;i<22;i++)
            {float x=-14+(i*3.771f)%28,y=1+Mathf.Repeat(i*1.37f+time*.13f,11);Disc(x+Mathf.Sin(time*.2f+i)*.2f,y,.035f,.06f,Palette.Alpha(accent,.22f));}
            Rect(0,-.45f,30,.9f,new Color(.12f,.16f,.19f));Rect(0,-.04f,30,.09f,Palette.Stone);Rect(0,-1.05f,30,.25f,new Color(.045f,.065f,.09f));
            for(int i=-15;i<15;i++){Rect(i*1.1f,-.45f,.018f,.7f,new Color(.07f,.10f,.13f));Rect(i*1.7f,-.15f,.7f,.018f,Palette.Alpha(accent,.16f));}
            var floor=ExternalAssets.Picture("Art/Castle/twilight-tiles.png",0,240,48,16);
            if(floor!=null)for(int i=-5;i<=5;i++)Shape(floor,i*3,-.48f,3,1,Palette.White);
            if(fight)
            {
                if(g.pillarLeft)Pillar(-4);if(g.pillarRight)Pillar(4);
                if(g.current!=null)Telegraph();
                foreach(var h in g.hazards)
                {
                    if(h.delay>0){Rect(h.x,.04f,h.width,.07f,h.color);Rect(h.x,3,h.width,6,Palette.Alpha(h.color,.055f));for(int i=0;i<3;i++)Tri(h.x,1.2f+i*.8f,.2f,.22f,Palette.Alpha(h.color,.3f),180);}
                    else{Rect(h.x,4,h.width,8,Palette.Alpha(h.color,.7f));Rect(h.x,4,h.width*.35f,8,Palette.White);}
                }
                if(g.barrier)
                {Rect(0,2.6f,.24f,5.2f,Palette.Alpha(Palette.Gold,.4f));for(int i=0;i<7;i++)Line(new Vector2(-.45f,i*.75f),new Vector2(.45f,i*.75f+.4f),.05f,Palette.Gold);Rect(0,5.65f,1.5f,.08f,Palette.Slate);Rect(-.75f+.75f*Mathf.Max(0,g.barrierHp)/45,5.65f,1.5f*Mathf.Max(0,g.barrierHp)/45,.08f,Palette.Gold);}
                foreach(var m in g.minions)Minion(m.x,time);
                Character(g.player,1,g.face,g.stun>0?"hit":g.dashTime>0?"dash":g.attackAnim>0?g.playerAction:Mathf.Abs(g.pvY)>1?"jump":Mathf.Abs(g.horizontalSpeed)>.3f?"run":"idle",false,time,g.hitFlash>0?Palette.Red:Palette.Teal);
                Character(g.boss,g.bossIndex==0?1.5f:g.bossIndex==3?1.45f:1.3f,g.bossFace,g.bossStagger>0?"hit":g.current!=null?(g.patternAge>g.windup?"attack":"windup"):g.bossState=="추적"?"run":"idle",true,time,g.bossFlash>0?Palette.White:accent);
                foreach(var s in g.shots)
                {Color c=s.friendly?Palette.Teal:s.parry?Palette.Gold:Palette.Red;Disc(s.pos.x,s.pos.y,s.radius*5,s.radius*5,Palette.Alpha(c,.12f));Line(s.pos-s.vel.normalized*.65f,s.pos,.08f,Palette.Alpha(c,.4f));Disc(s.pos.x,s.pos.y,s.radius*2,s.radius*2,c);Disc(s.pos.x,s.pos.y,s.radius,s.radius,Palette.White);}
                if(g.parryAge<.21f)
                {float a=Mathf.Clamp01(1-g.parryAge/.24f);Disc(g.player.x+g.face*.6f,g.player.y+1.1f,.8f,1.7f,Palette.Alpha(Palette.Teal,a*.25f));Rect(g.player.x+g.face*.65f,g.player.y+1.1f,.07f,1.6f,Palette.Alpha(Palette.White,a));}
                if(g.dashTime>0)for(int i=1;i<4;i++)Rect(g.player.x-g.face*i*.5f,g.player.y+.9f,.4f,1.4f,Palette.Alpha(Palette.Teal,.14f/i));
            }
            else
            {
                Character(new Vector2(-5.3f,0),1,1,"idle",false,time,Palette.Teal);
                Character(new Vector2(6,0),2,-1,"idle",true,time,accent);
                Rect(6,-.05f,4,.04f,Palette.Alpha(accent,.5f));
            }
            foreach(var m in g.motes)Rect(m.pos.x,m.pos.y,m.size,m.size,Palette.Alpha(m.color,Mathf.Clamp01(m.life*3)),time*300);
            // Low drifting foreground mist.
            for(int i=0;i<6;i++)Disc(-15+i*6+Mathf.Sin(time*.07f+i)*2,-.6f,12,.55f,new Color(.12f,.22f,.25f,.12f));
            for(int i=cursor;i<pool.Count;i++)pool[i].gameObject.SetActive(false);
        }
        void Pillar(float x)
        {Rect(x,.85f,.65f,1.7f,Palette.Stone);Rect(x,1.75f,.9f,.18f,Palette.Slate);Rect(x,.15f,.9f,.3f,Palette.Slate);Rect(x-.18f,.95f,.07f,1.3f,Palette.Alpha(Palette.Gold,.5f));}
        void Telegraph()
        {
            var p=g.current;float remaining=g.windup-g.patternAge;
            if(remaining<=0)return;
            Color c=p.parryable?Palette.Gold:Palette.Red;float opacity=.08f+.05f*Mathf.Sin(g.realClock*14);
            if(p.kind==AttackKind.Slash)
            {bool radial=p.id=="spin";float x=radial?g.boss.x:g.boss.x+g.bossFace*p.range*.5f;Rect(x,.8f,radial?p.range*2:p.range,1.6f,Palette.Alpha(c,opacity));Rect(x,.05f,radial?p.range*2:p.range,.04f,c);}
            if(p.kind==AttackKind.Dash){Rect(0,.06f,24,.05f,Palette.Red);for(int i=0;i<8;i++)Tri(-10+i*3,.4f,.35f,.45f,Palette.Alpha(c,.4f),g.bossFace>0?-90:90);}
            if(p.kind==AttackKind.Balance)
            {Rect(g.safeSide*6,.04f,11.2f,.1f,Palette.Teal);Rect(g.safeSide*6,1,11.2f,2,Palette.Alpha(Palette.Teal,.08f));}
            if(p.parryable&&remaining<.3f)
            {float x=g.boss.x,y=g.boss.y+3.3f;Disc(x,y,.7f,.7f,Palette.Alpha(Palette.Gold,.2f));Rect(x,y,.3f,.3f,Palette.Gold,45);Line(new Vector2(x-.6f,y),new Vector2(x+.6f,y),.035f,Palette.White);}
        }
        void Minion(float x,float t)
        {
            var sprite=PixelSprites.Get(6,"run",(int)(t*7)%4);
            Shape(sprite,x,1.05f,2.52f,2.52f,Palette.White);
        }
        void Character(Vector2 pos,float scale,int face,string action,bool enemy,float t,Color accent)
        {
#if !DESKTOP_PREVIEW
            if(g.scenePresentation&&(!enemy?g.scenePresentation.player!=null:g.bossIndex==0&&g.scenePresentation.arakus!=null))return;
#endif
            int frame=action=="run"?(int)(t*9)%4:(int)(t*3)%2;
            bool attack=action=="attack"||action=="J"||action=="Q"||action=="W"||action=="E"||action=="R";
            if(attack)frame=enemy?(int)(t*10)%4:Mathf.Clamp((int)((1-g.actionTime/Mathf.Max(.01f,g.actionTotal))*4),0,3);
            int actor=enemy?g.bossIndex:-1;
            if(enemy&&g.bossIndex==1)pos.y+=.12f+Mathf.Sin(t*2)*.08f;
            Disc(pos.x,pos.y+.03f,1.35f*scale,.15f*scale,new Color(0,0,0,.4f));
            if(!enemy)
            {
                float progress=1-g.actionTime/Mathf.Max(.01f,g.actionTotal);
                var hero=ExternalAssets.Hero(action,t,progress,g.pvY);
                if(hero!=null){Shape(hero,pos.x,pos.y+.89f*scale,6.7f*scale*face,6.7f*scale,Palette.White);return;}
            }
            Shape(PixelSprites.Get(actor,action,frame),pos.x,pos.y+1.4f*scale,3.36f*scale*face,3.36f*scale,Palette.White);
        }
    }
}
