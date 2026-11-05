using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower
{
    public static class Palette
    {
        public static readonly Color Ink=new Color(.035f,.05f,.075f),Slate=new Color(.12f,.16f,.21f),Stone=new Color(.22f,.28f,.33f),Teal=new Color(.33f,.89f,.83f),Gold=new Color(1,.76f,.37f),Red=new Color(1,.30f,.32f),Purple=new Color(.69f,.49f,1),White=new Color(.91f,.92f,.87f);
        // 패링 신호 (파란 빛): 테두리 빛 / 몸 위 빛·별빛 중심
        public static readonly Color ParryRim=new Color(.28f,.62f,1f),ParryCore=new Color(.72f,.88f,1f);
        public static Color Alpha(Color c,float a){c.a=a;return c;}
    }
    public sealed class WorldArtist : MonoBehaviour
    {
        TowerGame g;Sprite box,disk,triangle;readonly List<SpriteRenderer> pool=new List<SpriteRenderer>();int cursor;
        bool glowing;int layerBoost;   // 가산(발광) 재질로 그리기 / 아라쿠스 그림(정렬 1000) 위에 그리기
#if !DESKTOP_PREVIEW
        Material spriteMaterial;
#endif
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
            var r=pool[cursor];r.gameObject.SetActive(true);r.sprite=sprite;r.color=color;r.sortingOrder=layerBoost+cursor++;
#if !DESKTOP_PREVIEW
            if(spriteMaterial==null)spriteMaterial=r.sharedMaterial;
            var material=glowing&&ParryGlow.Material!=null?ParryGlow.Material:spriteMaterial;
            if(r.sharedMaterial!=material)r.sharedMaterial=material;
#endif
            r.transform.position=new Vector3(x,y,0);r.transform.localScale=new Vector3(w,h,1);r.transform.rotation=Quaternion.Euler(0,0,angle);
        }
        void Rect(float x,float y,float w,float h,Color c,float a=0)=>Shape(box,x,y,w,h,c,a);
        void Disc(float x,float y,float w,float h,Color c)=>Shape(disk,x,y,w,h,c);
        void Tri(float x,float y,float w,float h,Color c,float a=0)=>Shape(triangle,x,y,w,h,c,a);
        void GlowShape(Sprite sprite,float x,float y,float w,float h,Color c,float a=0){glowing=true;Shape(sprite,x,y,w,h,c,a);glowing=false;}
        void Line(Vector2 a,Vector2 b,float width,Color c)
        {var d=b-a;Rect((a.x+b.x)*.5f,(a.y+b.y)*.5f,d.magnitude,width,c,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);}
        public void Render()
        {
            bool spriteArakus=false;
#if !DESKTOP_PREVIEW
            spriteArakus=g.bossIndex==0&&g.scenePresentation&&g.scenePresentation.arakus&&g.scenePresentation.arakus.UsesArakusArt;
#endif
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
                if(!spriteArakus){if(g.pillarLeft)Pillar(-4);if(g.pillarRight)Pillar(4);}
                if(g.current!=null)Telegraph();
                if(g.current!=null&&g.current.kind>=AttackKind.Step)ArakusCues(time);
                if(!spriteArakus&&(g.BossPatterns.rageActive||(g.current!=null&&g.current.kind==AttackKind.Rage)))RageAura(time);
                foreach(var h in g.hazards)
                {
                    Color hc=h.parry?Palette.ParryRim:h.color;   // 패링 가능한 잔상은 파란 예고
                    if(h.delay>0){Rect(h.x,.04f,h.width,.07f,hc);Rect(h.x,3,h.width,6,Palette.Alpha(hc,.055f));for(int i=0;i<3;i++)Tri(h.x,1.2f+i*.8f,.2f,.22f,Palette.Alpha(hc,.3f),180);}
                    else{Rect(h.x,4,h.width,8,Palette.Alpha(hc,.7f));Rect(h.x,4,h.width*.35f,8,Palette.White);}
                }
                if(g.barrier)
                {Rect(0,2.6f,.24f,5.2f,Palette.Alpha(Palette.Gold,.4f));for(int i=0;i<7;i++)Line(new Vector2(-.45f,i*.75f),new Vector2(.45f,i*.75f+.4f),.05f,Palette.Gold);Rect(0,5.65f,1.5f,.08f,Palette.Slate);Rect(-.75f+.75f*Mathf.Max(0,g.barrierHp)/45,5.65f,1.5f*Mathf.Max(0,g.barrierHp)/45,.08f,Palette.Gold);}
                foreach(var m in g.minions)Minion(m.x,time);
                Character(g.player,1,g.face,g.stun>0?"hit":g.dashTime>0?"dash":g.attackAnim>0?g.playerAction:Mathf.Abs(g.pvY)>1?"jump":Mathf.Abs(g.horizontalSpeed)>.3f?"run":"idle",false,time,g.hitFlash>0?Palette.Red:Palette.Teal);
                ParryAura();
                Character(g.boss,g.bossIndex==0?1.5f:g.bossIndex==3?1.45f:1.3f,g.bossFace,g.bossStagger>0?"hit":g.current!=null?(g.patternAge>g.windup?"attack":"windup"):g.bossState=="추적"?"run":"idle",true,time,g.bossFlash>0?Palette.White:accent);
                foreach(var s in g.shots)
                {
                    if(s.returns){if(!spriteArakus)Shield(s,time);continue;}
                    Color c=s.friendly?Palette.Teal:s.parry?Palette.ParryRim:Palette.Red;Disc(s.pos.x,s.pos.y,s.radius*5,s.radius*5,Palette.Alpha(c,.12f));Line(s.pos-s.vel.normalized*.65f,s.pos,.08f,Palette.Alpha(c,.4f));Disc(s.pos.x,s.pos.y,s.radius*2,s.radius*2,c);Disc(s.pos.x,s.pos.y,s.radius,s.radius,Palette.White);}
                if(g.parryAge<.21f)
                {float a=Mathf.Clamp01(1-g.parryAge/.24f);Disc(g.player.x+g.face*.6f,g.player.y+1.1f,.8f,1.7f,Palette.Alpha(Palette.Teal,a*.25f));Rect(g.player.x+g.face*.65f,g.player.y+1.1f,.07f,1.6f,Palette.Alpha(Palette.White,a));}
                if(g.dashTime>0)for(int i=1;i<4;i++)Rect(g.player.x-g.face*i*.5f,g.player.y+.9f,.4f,1.4f,Palette.Alpha(Palette.Teal,.14f/i));
                ParryGlints();
                Impacts();
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
            Color c=p.parryable?Palette.ParryRim:Palette.Red;float opacity=.08f+.05f*Mathf.Sin(g.realClock*14);
            if(p.kind==AttackKind.Slash)
            {bool radial=p.id=="spin";float x=radial?g.boss.x:g.boss.x+g.bossFace*p.range*.5f;Rect(x,.8f,radial?p.range*2:p.range,1.6f,Palette.Alpha(c,opacity));Rect(x,.05f,radial?p.range*2:p.range,.04f,c);}
            if(p.kind==AttackKind.Dash){Rect(0,.06f,24,.05f,Palette.Red);for(int i=0;i<8;i++)Tri(-10+i*3,.4f,.35f,.45f,Palette.Alpha(c,.4f),g.bossFace>0?-90:90);}
            if(p.kind==AttackKind.Balance)
            {Rect(g.safeSide*6,.04f,11.2f,.1f,Palette.Teal);Rect(g.safeSide*6,1,11.2f,2,Palette.Alpha(Palette.Teal,.08f));}
        }
        // ── 아라쿠스 기획 패턴 예고·효과: 파란빛 = 패링 가능, 붉은빛 = 피하거나 거리 벌리기 ──
        void ArakusCues(float time)
        {
            var bp=g.BossPatterns;var p=g.current;float t=g.patternAge,w=g.windup,pulse=.5f+.5f*Mathf.Sin(time*16);
            float face=g.bossFace,bx=g.boss.x;
            if(p.kind==AttackKind.ShieldThrow)
            {
                if(t<w)
                {
                    // 두 패턴이 같은 예고 (이지선다): 방패가 날아갈 길
                    Vector2 hand=g.boss+new Vector2(face*.75f,1.25f);Vector2 aim=(g.player+Vector2.up*.9f-hand).normalized;
                    for(int i=1;i<=7;i++){Vector2 q=hand+aim*i;Disc(q.x,q.y,.18f,.18f,Palette.Alpha(Palette.ParryRim,.18f+.25f*pulse*(i/7f)));}
                    return;
                }
                if(p.id=="throw")
                {
                    if(!bp.struck){float x=bp.moveTo+face*p.range*.5f;Rect(x,.8f,p.range,1.6f,Palette.Alpha(Palette.ParryRim,.1f+.08f*pulse));Rect(x,.05f,p.range,.05f,Palette.ParryRim);}
                    else if(t<w+1.1f){float x=bx+face*1.2f;Line(new Vector2(x-face*.9f,2.1f),new Vector2(x+face*.5f,.3f),.12f,Palette.Alpha(Palette.White,.8f));}
                    return;
                }
                // 내려찍기: 도약 중 착지점(반경 1.5m), 착지 뒤 충격파
                if(bp.leaping||!bp.struck)
                {
                    float x=bp.leaping?bp.leapTo:g.player.x,a=bp.leaping?.16f+.12f*pulse:.05f;
                    Rect(x,.06f,3.6f,.08f,Palette.Alpha(Palette.Red,bp.leaping?1:.4f));Rect(x,.9f,3.6f,1.8f,Palette.Alpha(Palette.Red,a));
                    if(bp.leaping)for(int i=0;i<3;i++)Tri(x,2.4f-i*.6f,.3f,.3f,Palette.Alpha(Palette.Red,.5f),180);
                }
                else
                {
                    float age=t-bp.leapStart,r=1.8f+age*9;
                    if(age<.35f){Rect(bx,.12f,r*2,.12f,Palette.Alpha(Palette.Red,.8f*(1-age/.35f)));Disc(bx,.1f,r*1.2f,.45f,Palette.Alpha(Palette.Red,.25f*(1-age/.35f)));}
                }
                return;
            }
            if(p.kind==AttackKind.ShieldRush)
            {
                if(t<w){float x=bx+face*3;Rect(x,.9f,6.4f,1.8f,Palette.Alpha(Palette.Red,.07f+.06f*pulse));Rect(x,.05f,6.4f,.05f,Palette.Red);for(int i=0;i<4;i++)Tri(bx+face*(1+i*1.4f),1,.35f,.45f,Palette.Alpha(Palette.Red,.45f),face>0?-90:90);}
                else if(!bp.struck)for(int i=1;i<4;i++)Rect(bx-face*i*.45f,1.2f,.35f,2,Palette.Alpha(Palette.Red,.14f/i));
                return;
            }
            if(p.kind==AttackKind.Sweep)
            {
                float second=w+1f/bp.tempo;
                if(t<w){Rect(bx,.5f,p.range*2,1f,Palette.Alpha(Palette.Red,.07f+.07f*pulse));Rect(bx,.05f,p.range*2,.05f,Palette.Red);Rect(bx,1.02f,p.range*2,.04f,Palette.Alpha(Palette.Red,.7f));}
                else if(t<second)
                {
                    float k=Mathf.Clamp01((t-w)/(second-w));
                    Rect(bx,1.25f,p.range*2,2.5f,Palette.Alpha(Palette.Red,.04f+.12f*k+.05f*pulse));Rect(bx,2.52f,p.range*2,.04f,Palette.Alpha(Palette.Red,.4f+.5f*k));
                    if(t<w+.25f)Rect(bx,.45f,p.range*2*Mathf.Clamp01((t-w)/.12f),.14f,Palette.Alpha(Palette.White,.8f));
                }
                else if(t<second+.25f)Rect(bx,1.3f,.14f+p.range*2*Mathf.Clamp01((t-second)/.12f),2.4f,Palette.Alpha(Palette.White,.55f));
                return;
            }
            if(p.kind==AttackKind.Step&&t>w)for(int i=1;i<4;i++)Rect(bx+face*i*.4f,1.2f,.3f,2,Palette.Alpha(Palette.Stone,.25f/i));
        }
        void RageAura(float time)
        {
            var bp=g.BossPatterns;bool casting=g.current!=null&&g.current.kind==AttackKind.Rage&&g.patternAge<g.windup;
            float pulse=.5f+.5f*Mathf.Sin(time*(casting?18:6)),x=g.boss.x,y=g.boss.y;
            if(casting)
            {
                float k=Mathf.Clamp01(g.patternAge/Mathf.Max(.01f,g.windup));
                Rect(x,3+y,1.2f+k*1.6f,6,Palette.Alpha(Palette.Red,.08f+.1f*pulse));
                Disc(x,1.4f+y,2.6f+k*1.5f,3.4f+k*1.5f,Palette.Alpha(Palette.Red,.06f+.06f*pulse));
                for(int i=0;i<6;i++){float a=time*3+i*1.047f;Disc(x+Mathf.Cos(a)*(1.8f-k),1.4f+y+Mathf.Sin(a)*(1.8f-k),.14f,.14f,Palette.Alpha(Palette.Red,.7f));}
            }
            else if(bp.rageActive)
            {
                Disc(x,1.3f+y,2.4f,3.2f,Palette.Alpha(Palette.Red,.04f+.03f*pulse));
                for(int i=0;i<3;i++){float a=time*1.7f+i*2.1f;Disc(x+Mathf.Sin(a)*.7f,.6f+y+Mathf.Repeat(time*1.2f+i*.33f,1)*2.4f,.08f,.08f,Palette.Alpha(Palette.Red,.6f));}
            }
        }
        void Shield(TowerGame.Shot s,float time)
        {
            Color glow=s.friendly?Palette.Teal:Palette.ParryRim;
            Disc(s.pos.x,s.pos.y,1.1f,1.1f,Palette.Alpha(glow,.14f));
            var pic=ExternalAssets.Picture("Art/Arakus/Arakus_Shield_Projectile.png");
            if(pic!=null)Shape(pic,s.pos.x,s.pos.y,1.25f,1.25f,Palette.White,time*720);
            else{Disc(s.pos.x,s.pos.y,.8f,.8f,Palette.Stone);Disc(s.pos.x,s.pos.y,.35f,.35f,glow);}
            Line(s.pos-s.vel.normalized*.9f,s.pos-s.vel.normalized*.3f,.1f,Palette.Alpha(glow,.35f));
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
            var bp=g.BossPatterns;bool battle=enemy&&g.mode==ScreenMode.Battle;
            if(battle)pos.x+=g.bossRecoil;   // 맞으면 살짝 밀려 보임
            var sprite=PixelSprites.Get(actor,action,frame);float cy=pos.y+1.4f*scale,w=3.36f*scale*face,h=3.36f*scale;
            // 패링 신호: 같은 그림을 8방향으로 살짝 어긋나게 가산 → 테두리 빛, 몸 위에 한 장 더 → 몸이 밝아짐
            float level=battle?bp.parryGlow:0,flash=battle?Mathf.Clamp01(1-bp.parryFlash/BossPatternSystem.ParryFlashTime):0;
            float k=Mathf.Max(level*(.85f+.15f*Mathf.Sin(t*22)),flash);
            if(level>.01f||flash>0)for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/4,reach=(i<8?.06f:.15f)*(1+.6f*flash);
                GlowShape(sprite,pos.x+Mathf.Cos(a)*reach,cy+Mathf.Sin(a)*reach,w,h,Palette.Alpha(Palette.ParryRim,(i<8?.42f:.16f)*k));
            }
            Shape(sprite,pos.x,cy,w,h,Palette.White);
            if(level>.01f||flash>0)GlowShape(sprite,pos.x,cy,w,h,Palette.Alpha(Palette.ParryCore,.16f*level+.45f*flash));
            float hit=battle?Mathf.Clamp01(g.bossFlash/.1f):0;   // 맞았을 때 하얗게 번쩍
            if(hit>0)GlowShape(sprite,pos.x,cy,w,h,Palette.Alpha(Palette.White,.8f*hit));
        }
        // ── 타격감: 충돌 불꽃 (칼자국·패링 충돌·피격) + 화면 하얀 번쩍임. 주인공·아라쿠스 그림(정렬 1000)보다 위에 그린다. ──
        void Impacts()
        {
            layerBoost=2000;
            foreach(var im in g.impacts)
            {
                float u=Mathf.Clamp01(im.age/im.life),k=1-u,s=im.size;Vector2 p=im.pos;
                if(im.kind==2)
                {
                    // 패링 충돌: 둥근 빛 → 긴 가로 섬광 + 세로줄 + X자 별 → 퍼지는 충격파 고리와 방사형 선
                    GlowShape(disk,p.x,p.y,(1.4f+3.4f*u)*s,(1.4f+3.4f*u)*s,Palette.Alpha(Palette.Gold,.28f*k));
                    GlowShape(box,p.x,p.y,(2+13*k)*s,.1f*s,Palette.Alpha(Palette.White,k));
                    GlowShape(box,p.x,p.y,(1+6*k)*s,.05f*s,Palette.Alpha(Palette.Gold,k));
                    GlowShape(box,p.x,p.y,.07f*s,(1+5*k)*s,Palette.Alpha(Palette.Gold,.85f*k));
                    float star=(1.2f+1.2f*k)*s;
                    GlowShape(box,p.x,p.y,star,.16f*s,Palette.Alpha(Palette.White,k),45);GlowShape(box,p.x,p.y,star,.16f*s,Palette.Alpha(Palette.White,k),-45);
                    GlowShape(disk,p.x,p.y,.7f*s*(.4f+.6f*k),.7f*s*(.4f+.6f*k),Palette.Alpha(Palette.White,k));
                    float ring=(.4f+2.6f*u)*s;
                    for(int i=0;i<28;i++){float a=i*Mathf.PI*2/28;GlowShape(box,p.x+Mathf.Cos(a)*ring,p.y+Mathf.Sin(a)*ring,.34f*s,.07f*s*(.3f+.7f*k),Palette.Alpha(Palette.Gold,.9f*k),a*Mathf.Rad2Deg+90);}
                    for(int i=0;i<12;i++){float a=i*Mathf.PI/6+.26f,r0=(.7f+1.8f*u)*s,len=.9f*k*s;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Vector2 q=p+d*(r0+len*.5f);GlowShape(box,q.x,q.y,len,.05f*s,Palette.Alpha(Palette.White,.8f*k),a*Mathf.Rad2Deg);}
                }
                else if(im.kind==3)
                {
                    // 플레이어 피격: 붉은 고리 + X자
                    float ring=(.3f+1.1f*u)*s;
                    for(int i=0;i<16;i++){float a=i*Mathf.PI/8;GlowShape(box,p.x+Mathf.Cos(a)*ring,p.y+Mathf.Sin(a)*ring,.22f,.06f,Palette.Alpha(Palette.Red,.8f*k),a*Mathf.Rad2Deg+90);}
                    GlowShape(box,p.x,p.y,1.2f*s*(.5f+.5f*k),.1f,Palette.Alpha(Palette.Red,k),40);GlowShape(box,p.x,p.y,1.2f*s*(.5f+.5f*k),.1f,Palette.Alpha(Palette.Red,k),-40);
                }
                else
                {
                    // 칼자국: 비스듬한 하얀 베기 줄(처음엔 길고 굵다가 가늘어짐) + 금빛 테두리 + 작은 별 + 튀는 불티
                    bool heavy=im.kind==1;float len=(heavy?2.6f:1.8f)*s*(.55f+.45f*k),th=(heavy?.2f:.13f)*s*k;
                    GlowShape(box,p.x,p.y,len*1.08f,th*2.2f,Palette.Alpha(Palette.Gold,.55f*k),im.angle);
                    GlowShape(box,p.x,p.y,len,th,Palette.Alpha(Palette.White,k),im.angle);
                    float star=(heavy?1.1f:.7f)*s*k;
                    GlowShape(box,p.x,p.y,star,.07f,Palette.Alpha(Palette.White,k),im.angle+90);
                    GlowShape(disk,p.x,p.y,(heavy?1.5f:1)*s*(.5f+u),(heavy?1.5f:1)*s*(.5f+u),Palette.Alpha(Palette.Gold,.22f*k));
                    for(int i=0;i<(heavy?10:6);i++){float a=i*Mathf.PI*2/(heavy?10:6)+im.angle*.02f,r0=(.25f+(heavy?1.2f:.8f)*u)*s;Vector2 q=p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r0;GlowShape(box,q.x,q.y,.28f*s*k,.04f,Palette.Alpha(Palette.Gold,.9f*k),a*Mathf.Rad2Deg);}
                }
            }
            if(g.whiteFlash>0){layerBoost=3000;Rect(0,5,90,50,Palette.Alpha(Palette.White,g.whiteFlash*.45f));}
            layerBoost=0;
        }
        // 패링 신호: 보스 뒤 푸른 빛 번짐 (몸 테두리 빛과 함께 '이 공격은 받아칠 수 있다'를 알림)
        void ParryAura()
        {
            var bp=g.BossPatterns;float level=bp.parryGlow;if(level<=.01f)return;
            float x=g.boss.x,y=g.boss.y+1.6f,pulse=.8f+.2f*Mathf.Sin(g.realClock*9);
            GlowShape(disk,x,y,4.6f,5.4f,Palette.Alpha(Palette.ParryRim,.07f*level*pulse));
            GlowShape(disk,x,y,2.8f,3.6f,Palette.Alpha(Palette.ParryRim,.09f*level*pulse));
            for(int i=0;i<5;i++)
            {
                float u=Mathf.Repeat(g.realClock*.9f+i*.2f,1);
                GlowShape(disk,x+Mathf.Sin(i*2.4f+g.realClock*2)*.9f,g.boss.y+.3f+u*3.2f,.09f,.09f,Palette.Alpha(Palette.ParryCore,.7f*level*(1-u)));
            }
        }
        // 패링 신호: 받아칠 순간 직전 푸른 별빛 (근접 = 보스 무기 앞, 투사체·잔상 = 그 자리). 아라쿠스 그림보다 위에 그린다.
        void ParryGlints()
        {
            var bp=g.BossPatterns;float span=BossPatternSystem.ParryFlashTime;
            layerBoost=2000;
            float melee=Mathf.Clamp01(1-bp.parryFlash/span);
            if(melee>0)Glint(g.boss.x+g.bossFace*1.3f,g.boss.y+1.8f,melee,1);
            foreach(var s in g.shots)if(s.cueAt>=0&&!s.friendly){float k=Mathf.Clamp01(1-(g.seconds-s.cueAt)/span);if(k>0)Glint(s.pos.x,s.pos.y,k,.7f);}
            foreach(var hz in g.hazards)if(hz.cueAt>=0){float k=Mathf.Clamp01(1-(g.seconds-hz.cueAt)/span);if(k>0)Glint(hz.x,1.3f,k,.9f);}
            layerBoost=0;
        }
        // 푸른 별빛 한 번 (k: 막 터졌을 때 1 → 0). 긴 가로줄 + 세로줄 + X자 별 + 퍼지는 고리
        void Glint(float x,float y,float k,float size)
        {
            float pop=Mathf.Clamp01((1-k)/.18f),grow=size*(.6f+.4f*pop);
            GlowShape(disk,x,y,2.6f*grow,2.6f*grow,Palette.Alpha(Palette.ParryRim,.22f*k));
            GlowShape(box,x,y,1+9*grow*k,.07f,Palette.Alpha(Palette.ParryRim,.9f*k));
            GlowShape(box,x,y,.5f+4*grow*k,.035f,Palette.Alpha(Palette.ParryCore,.9f*k));
            GlowShape(box,x,y,.05f,.4f+2.6f*grow*k,Palette.Alpha(Palette.ParryRim,.8f*k));
            GlowShape(box,x,y,1.1f*grow,.13f,Palette.Alpha(Palette.ParryCore,k),45);
            GlowShape(box,x,y,1.1f*grow,.13f,Palette.Alpha(Palette.ParryCore,k),-45);
            GlowShape(disk,x,y,.42f*grow,.42f*grow,Palette.Alpha(Palette.White,k));
            float ring=(.55f+1.1f*(1-k))*size;
            for(int i=0;i<20;i++){float a=i*Mathf.PI/10;GlowShape(box,x+Mathf.Cos(a)*ring,y+Mathf.Sin(a)*ring,.22f*size,.05f,Palette.Alpha(Palette.ParryRim,.55f*k),a*Mathf.Rad2Deg+90);}
        }
    }
}
