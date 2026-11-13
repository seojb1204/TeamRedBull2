using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower
{
    public enum ScreenMode { Title, Lobby, Battle, Result, Ending }
    public sealed partial class TowerGame : MonoBehaviour
    {
        public const float Left=-12,Right=12;
        public SaveData save; public ScreenMode mode=ScreenMode.Title;
        public int bossIndex; public BossSpec spec;
        public Vector2 player,boss;public float playerHp,maxHp,bossHp,ult,seconds;
        public int face=1,bossFace=-1,dashes=2,parries,hits,sealParries;
        public float horizontalSpeed,pvY,playerInv,stun,parryAge=99,parryLock,dashTime,dashRecharge,actionTime,actionTotal;
        public float attackAnim,hitFlash,bossFlash,shake,flash,phaseFlash,healing,skillQ,skillW,skillE;
        public string playerAction="",bossState="",notice="";public float noticeTime;
        public bool paused,showHelp,showHistory,showSettings,seal;
        public readonly List<Hazard> hazards=new List<Hazard>();
        public readonly List<Shot> shots=new List<Shot>();
        public readonly List<Minion> minions=new List<Minion>();
        public readonly List<Mote> motes=new List<Mote>();
        // ── 타격감 ── 숫자를 바꿔 세기를 조절한다.
        // hitStop: 이 시간 동안 전투가 멈춘다(역경직). slowMo: 이 시간 동안 SlowMoScale 배속.
        // zoomPunch: 카메라가 zoomFocus 쪽으로 잠깐 다가감. whiteFlash: 화면 하얀 번쩍임. bossRecoil: 보스 그림을 밀어 보이는 거리(보기용).
        public const float HitStopLight=.05f,HitStopParry=.14f,HitStopHurt=.06f,SlowMoScale=.3f,ParrySlowMo=.3f,ShakeScale=.045f;
        public float hitStop,slowMo,zoomPunch,whiteFlash,bossRecoil;public Vector2 zoomFocus;
        public readonly List<Impact> impacts=new List<Impact>();
        int hitCount;
        public void HitStop(float t){hitStop=Mathf.Max(hitStop,t);}
        public void AddImpact(Vector2 pos,int kind,float size,float angle){impacts.Add(new Impact{pos=pos,kind=kind,size=size,angle=angle,life=kind==2?.42f:kind==1?.24f:.17f});if(impacts.Count>24)impacts.RemoveAt(0);}
        public HistoryEntry result;public bool wasFirstClear;public float realClock;
        float comboGap;int combo;bool skillFired,ended;float dodgeBuffer,coyote;
        bool abandoning;   // Abandon()·OnApplicationQuit()이 EndBattle 직전에 켠다 (끝까지 싸우지 않은 판 표시)
        readonly DdaBattleSession dda=new DdaBattleSession();
        readonly RewardLedger rewards=new RewardLedger();
        public List<PatternStats> stats => dda.Stats;
        public Dictionary<string,double[]> contexts => dda.Contexts;
        bool battleDda => dda.Enabled;
        Soundscape sound;WorldArtist artist;Camera cam;
#if !HEADLESS && !DESKTOP_PREVIEW
        public ScenePresentation scenePresentation;
        public NativeGameUI nativeUI;
#endif
        bool demo;float demoAction;string capturePath;bool captureDone;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(FindAnyObjectByType<TowerGame>()==null)new GameObject("Reversal Tower").AddComponent<TowerGame>();}
        void Awake()
        {
            // 저장을 읽기 전에 파싱해야 테스트 데이터 격리가 의미를 가진다.
            AutoRunArgs.Parse();
            // vSync 가 켜져 있으면 Unity 는 targetFrameRate 를 무시한다. 둘을 같이 지정하면
            // 의도가 모호해지므로 기본은 vSync 에 맡기고, 측정할 때만 고정 프레임을 쓴다.
            if(AutoRunArgs.ForcedFrameRate>0){QualitySettings.vSyncCount=0;Application.targetFrameRate=AutoRunArgs.ForcedFrameRate;}
            else{QualitySettings.vSyncCount=1;Application.targetFrameRate=-1;}
            save=SaveStore.Load();AudioListener.volume=save.masterVolume;
#if !HEADLESS && !DESKTOP_PREVIEW
            if(!scenePresentation)scenePresentation=FindAnyObjectByType<ScenePresentation>();
            if(!nativeUI)nativeUI=FindAnyObjectByType<NativeGameUI>();
#endif
            cam=Camera.main;if(!cam){cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";}
            cam.orthographic=true;cam.orthographicSize=8.2f;cam.transform.position=new Vector3(0,5,-20);cam.backgroundColor=new Color(.025f,.035f,.055f);
            if(!FindAnyObjectByType<AudioListener>())cam.gameObject.AddComponent<AudioListener>();
#if !DESKTOP_PREVIEW
            cam.gameObject.AddComponent<PixelPresentation>();
#endif
            sound=gameObject.AddComponent<Soundscape>();sound.Initialize();
            artist=gameObject.AddComponent<WorldArtist>();artist.Initialize(this);
            InitGui();
            var args=Environment.GetCommandLineArgs();demo=Array.IndexOf(args,"-towerDemo")>=0;
            capturePath=AutoRunArgs.CapturePath;
            if(demo)StartBattle(0);
        }
        void Update()
        {
            float raw=Mathf.Min(Time.unscaledDeltaTime,.05f);realClock+=raw;
            noticeTime=Mathf.Max(0,noticeTime-raw);flash=Mathf.Max(0,flash-raw*2);phaseFlash=Mathf.Max(0,phaseFlash-raw*.8f);
            shake=Mathf.Max(0,shake-raw*12);
            if(Input.GetKeyDown(KeyCode.F12))Capture();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevKeys();
#endif
            HandleScreenInput(Input.GetKeyDown(KeyCode.F1),Input.GetKeyDown(KeyCode.Escape),Input.GetKeyDown(KeyCode.Return),
                Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.A),Input.GetKeyDown(KeyCode.RightArrow)||Input.GetKeyDown(KeyCode.D),Input.GetKeyDown(KeyCode.C));
            // 역경직 동안에는 전투 시간을 0으로 흘린다 (입력은 계속 읽어서 그 사이 누른 키를 놓치지 않음)
            float battleDt=raw;bool fighting=mode==ScreenMode.Battle&&!paused&&!showHelp&&!showSettings;
            if(fighting){if(hitStop>0){hitStop=Mathf.Max(0,hitStop-raw);battleDt=0;}else if(slowMo>0){slowMo=Mathf.Max(0,slowMo-raw);battleDt=raw*SlowMoScale;}}
            if(fighting)TickBattle(battleDt);
            float fx=fighting?battleDt:raw;   // 불꽃·파편도 같이 멈췄다가 움직인다
            zoomPunch=Mathf.MoveTowards(zoomPunch,0,raw*3.5f);whiteFlash=Mathf.Max(0,whiteFlash-raw*5);bossRecoil=Mathf.MoveTowards(bossRecoil,0,fx*3.5f);
            for(int i=impacts.Count-1;i>=0;i--){impacts[i].age+=fx;if(impacts[i].age>=impacts[i].life)impacts.RemoveAt(i);}
            for(int i=motes.Count-1;i>=0;i--){var m=motes[i];m.life-=fx;m.pos+=m.velocity*fx;m.velocity+=Vector2.down*fx*3;if(m.life<=0)motes.RemoveAt(i);}
            sound.tension=mode==ScreenMode.Battle&&!paused?(phaseTwo?.95f:.65f):.08f;
            if(!captureDone&&!string.IsNullOrEmpty(capturePath)&&realClock>5){ScreenCapture.CaptureScreenshot(capturePath);captureDone=true;}
            AutoRunTick(Time.unscaledDeltaTime);
        }
        void LateUpdate()
        {
            float aspect=(float)Screen.width/Mathf.Max(1,Screen.height);cam.orthographicSize=Mathf.Max(8.2f,14.3f/aspect)-zoomPunch;
            float zx=zoomFocus.x*zoomPunch*.12f,zy=(zoomFocus.y-5)*zoomPunch*.12f;   // 부딪힌 곳 쪽으로 살짝
            cam.transform.position=new Vector3(zx+Mathf.Sin(realClock*93)*shake*ShakeScale,5+zy+Mathf.Cos(realClock*87)*shake*ShakeScale*.8f,-20);
            artist.Render();
#if !HEADLESS && !DESKTOP_PREVIEW
            if(scenePresentation)scenePresentation.Render(this);
            if(nativeUI)nativeUI.Refresh();
#endif
        }
        void Capture(){string path=System.IO.Path.Combine(SaveStore.Root,"Screenshot_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".png");ScreenCapture.CaptureScreenshot(path);Say("스크린샷을 저장했습니다",2);}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 개발용 단축키 (에디터·개발 빌드에서만): 발표 시연과 패턴 확인용
        // F2 무적  F3 보스 HP 59%(2페이즈)  F4 보스 HP 29%(분노)  F5/F6 이전/다음 패턴 바로 실행  F7 패링 성공 연출 미리보기
        bool devGod;int devPattern=-1;
        void DevKeys()
        {
            if(mode!=ScreenMode.Battle||spec==null)return;
            if(Input.GetKeyDown(KeyCode.F2)){devGod=!devGod;Say(devGod?"[DEV] 무적 켜짐":"[DEV] 무적 꺼짐",1.5f);}
            if(Input.GetKeyDown(KeyCode.F3)){bossHp=Mathf.Min(bossHp,spec.hp*.59f);Say("[DEV] 보스 HP 59%",1.5f);}
            if(Input.GetKeyDown(KeyCode.F4)){bossHp=Mathf.Min(bossHp,spec.hp*.29f);Say("[DEV] 보스 HP 29%",1.5f);}
            int step=Input.GetKeyDown(KeyCode.F6)?1:Input.GetKeyDown(KeyCode.F5)?-1:0;
            if(step!=0&&spec.patterns.Length>0)
            {
                devPattern=(devPattern+step+spec.patterns.Length)%spec.patterns.Length;
                var p=spec.patterns[devPattern];BossPatterns.forceNext=p;
                Say("[DEV] 다음 패턴 "+(devPattern+1)+"/"+spec.patterns.Length+" · "+p.name,2);
            }
            if(Input.GetKeyDown(KeyCode.F7)){bossStagger=Mathf.Max(bossStagger,.45f);ParryFeedback(boss.x);}   // 패링 성공 연출만 미리보기
            if(devGod){playerHp=maxHp;}
        }
#endif
        public void Say(string text,float duration=2.5f){notice=text;noticeTime=duration;}
        bool CardOutroBlocksInput
        {
            get
            {
#if !HEADLESS && !DESKTOP_PREVIEW
                return nativeUI&&nativeUI.CardOutroBlocksInput;
#else
                return false;
#endif
            }
        }
        bool CardsBlockLobbyInput => showCards||CardOutroBlocksInput;
        void HandleScreenInput(bool helpDown,bool escapeDown,bool enterDown,bool leftDown,bool rightDown,bool cardsDown)
        {
            // Keep a card-close event from activating the lobby later in this same input batch.
            bool cardsBlocked=CardsBlockLobbyInput,animating=CardOutroBlocksInput;
            if(helpDown&&!animating)showHelp=!showHelp;
            if(escapeDown&&!animating)
            {
                if(showHelp)showHelp=false;else if(showHistory)showHistory=false;else if(showSettings){showSettings=false;SaveStore.Save(save);}else if(showCards)showCards=false;else if(mode==ScreenMode.Battle)paused=!paused;else if(mode==ScreenMode.Lobby)mode=ScreenMode.Title;
            }
            if(mode==ScreenMode.Title&&enterDown){mode=ScreenMode.Lobby;sound.Play("confirm");}
            else if(mode==ScreenMode.Lobby&&!showHistory&&!showSettings&&!showHelp&&!cardsBlocked&&!CardsBlockLobbyInput)
                LobbyKeys(leftDown,rightDown,cardsDown,enterDown);
            if(mode==ScreenMode.Result&&enterDown)ReturnLobby();
        }
        void LobbyKeys(bool leftDown,bool rightDown,bool cardsDown,bool enterDown)
        {
            if(CardsBlockLobbyInput)return;
            if(leftDown)bossIndex=Mathf.Max(0,bossIndex-1);
            if(rightDown)bossIndex=Mathf.Min(save.unlockedBoss,bossIndex+1);
            if(cardsDown)OpenCards();
            if(enterDown&&!CardsBlockLobbyInput)StartBattle(bossIndex);
        }
        public void StartBattle(int index)
        {
            if(index<0||index>save.unlockedBoss)return;
            bossIndex=index;spec=Catalog.Bosses[index];mode=ScreenMode.Battle;paused=false;showHelp=false;showSettings=false;showHistory=false;showCards=false;ended=false;
#if !HEADLESS && !DESKTOP_PREVIEW
            if(scenePresentation&&scenePresentation.bosses!=null&&index<scenePresentation.bosses.Length&&scenePresentation.bosses[index])spec=scenePresentation.bosses[index].CreateRuntime();
#endif
            player=new Vector2(-6,0);boss=new Vector2(5,index==1?1.1f:0);face=1;bossFace=-1;
            maxHp=LobbyCards.Health(save);playerHp=maxHp;bossHp=spec.hp;ult=0;seconds=0;hits=parries=sealParries=0;
            horizontalSpeed=0;pvY=0;playerInv=stun=parryLock=dashTime=dashRecharge=actionTime=attackAnim=healing=skillQ=skillW=skillE=0;parryAge=99;
            phaseTwo=false;thirdPhase=false;seal=index==4;dashes=2;potions=3;combo=0;comboGap=0;bossStagger=0;rest=1.8f;current=null;bossState="입장";
            BossPatterns.Reset();rewards.Reset();playerAction="";healing=0;
            barrier=false;barrierTime=0;pillarLeft=pillarRight=index==0;hazards.Clear();shots.Clear();minions.Clear();motes.Clear();impacts.Clear();hitStop=slowMo=zoomPunch=whiteFlash=bossRecoil=0;
            dda.Prepare(save,spec,AutoRunArgs.DisableDda);
            Say(spec.name+" — "+spec.lesson,5);sound.Play("confirm");
            if(!save.tutorialSeen&&!demo)showHelp=true;
        }
        public PatternStats Stat(string id)=>stats.Find(s=>s.id==id);
        void TickBattle(float dt)
        {
            seconds+=dt;
            playerInv-=dt;stun-=dt;parryLock-=dt;parryAge+=dt;attackAnim-=dt;hitFlash-=dt;bossFlash-=dt;skillQ-=dt;skillW-=dt;skillE-=dt;comboGap-=dt;
            if(seconds>30)ult=Mathf.Min(100,ult+dt*.25f);
            if(player.y<=.001f){coyote=.1f;if(dashes<2){dashRecharge+=dt;if(dashRecharge>=2.5f){dashes++;dashRecharge=0;}}}else coyote-=dt;
            bool left=Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow),right=Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow);
            bool jump=Input.GetKeyDown(KeyCode.Space),dash=Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift);
            bool panelMouse=false;
#if !HEADLESS && !DESKTOP_PREVIEW
            panelMouse=DdaPanelConsumesMouse();
#endif
            bool attack=Input.GetKeyDown(KeyCode.J)||Input.GetKeyDown(KeyCode.Z)||(!panelMouse&&Input.GetMouseButtonDown(0));
            bool parry=Input.GetKeyDown(KeyCode.K)||Input.GetKeyDown(KeyCode.X)||(!panelMouse&&Input.GetMouseButtonDown(1));
            bool q=Input.GetKeyDown(KeyCode.Q),w=Input.GetKeyDown(KeyCode.W),e=Input.GetKeyDown(KeyCode.E),r=Input.GetKeyDown(KeyCode.R),f=Input.GetKeyDown(KeyCode.F);
            float move=(right?1:0)-(left?1:0);
            if(demo)DemoInput(ref move,ref jump,ref dash,ref attack,ref parry,ref q,ref w,ref e,ref r,ref f,dt);
            if(jump)dodgeBuffer=.12f;else dodgeBuffer-=dt;
            if(dash&&stun<=0&&dashes>0&&!seal&&(actionTime<=0||healing>0||skillFired))
            {dashes--;dashTime=.22f;playerInv=.18f;actionTime=0;healing=0;parryAge=99;sound.Play("dash");Burst(player+Vector2.up,.5f,Palette.Teal,9);}
            if(parry&&stun<=0&&parryLock<=0&&actionTime<=0&&dashTime<=0)
            {parryAge=0;parryLock=.55f;attackAnim=.22f;playerAction="parry";}
            if(stun<=0&&dashTime<=0)
            {
                if(move!=0&&actionTime<=0)face=move>0?1:-1;
                if(dodgeBuffer>0&&coyote>0&&actionTime<=0){pvY=9f;dodgeBuffer=0;coyote=0;sound.Play("dash");}
                if(actionTime<=0&&parryLock<=0)
                {
                    if(r&&ult>=100)BeginAction("R",.85f);
                    else if(f&&potions>0&&playerHp<maxHp){potions--;healing=.9f;BeginAction("F",.9f);}
                    else if(q&&skillQ<=0){skillQ=6;BeginAction("Q",.7f);}
                    else if(w&&skillW<=0){skillW=10;BeginAction("W",.9f);}
                    else if(e&&skillE<=0){skillE=15;BeginAction("E",1.3f);}
                    else if(attack){combo=comboGap>0?1-combo:0;comboGap=.75f;BeginAction("J",.35f);}
                }
            }
            float velocity=dashTime>0?face*18.18f:stun>0?0:actionTime>0?(playerAction=="Q"?face*4.3f:0):move*5.5f;
            float before=player.x;player.x=Mathf.Clamp(player.x+velocity*dt,Left+.4f,Right-.4f);
            horizontalSpeed=(player.x-before)/Mathf.Max(.001f,dt);
            if(barrier&&Mathf.Sign(before)!=Mathf.Sign(player.x))player.x=before<0?-.65f:.65f;
            dashTime-=dt;
            pvY-=22.5f*dt;player.y+=pvY*dt;if(player.y<0){player.y=0;pvY=0;}
            if(actionTime>0)TickAction(dt);if(ended)return;
            TickBoss(dt);if(ended)return;BossPatterns.TickParryCue(dt);
            TickShots(dt);if(ended)return;TickHazards(dt);if(ended)return;TickMinions(dt);
            if(playerHp<=0)EndBattle(false);
        }
        int potions=3;
        public int PotionCount=>potions;
        void BeginAction(string name,float duration)
        {
            playerAction=name;actionTime=actionTotal=duration;skillFired=false;attackAnim=duration;
            if(name=="R"){ult=0;playerInv=.5f;flash=.5f;sound.Play("parry");}
            else if(name!="F")sound.Play("attack");
        }
        void TickAction(float dt)
        {
            actionTime-=dt;float elapsed=actionTotal-actionTime;
            float at=playerAction=="E"?.7f:playerAction=="W"?.35f:playerAction=="Q"?.2f:playerAction=="F"?.6f:playerAction=="R"?.25f:.15f;
            if(skillFired||elapsed<at)return;skillFired=true;
            if(playerAction=="F"){playerHp=Mathf.Min(maxHp,playerHp+LobbyCards.Heal(save));healing=0;sound.Play("heal");Burst(player+Vector2.up,1,Palette.Teal,18);return;}
            float attack=LobbyCards.Attack(save);
            float damage=attack*(playerAction=="E"?3:playerAction=="Q"?1.8f:playerAction=="W"?1.2f:playerAction=="R"?10:1)*LobbyCards.SkillMultiplier(save,playerAction);
            float range=playerAction=="E"?2.8f:playerAction=="Q"?2.5f:playerAction=="W"?2.9f:playerAction=="R"?8:2.1f;
            bool radial=playerAction=="W"||playerAction=="R";
            if(playerAction=="R")
            {
                // Inherited attacks retain a distinct behaviour, beyond a damage multiplier.
                int u=save.equippedUltimate;
                if(u==0){range=6;damage*=1.2f;bossStagger=Mathf.Max(bossStagger,.8f);}
                if(u==1){for(int i=0;i<5;i++)FireShot(player+new Vector2(0,1+i*.12f),new Vector2(face*(7+i),.3f),attack*2,true,"ultimate",true);damage=0;}
                if(u==2){range=4;damage*=1.4f;playerHp=Mathf.Min(maxHp,playerHp+12);}
                if(u==3){range=30;damage*=1.7f;}
                if(u==4){range=30;damage*=2;playerInv=1;}
                Burst(player+Vector2.up,3,Palette.Gold,45);shake=4;
            }
            bool bossIn=Mathf.Abs(player.x-boss.x)<range+BossWidth&&BossAttackHeightOverlap(player.y)&&(radial||(boss.x-player.x)*face>-.5f);
            if(bossIn&&damage>0)DamageBoss(damage,playerAction=="R"?2.6f:playerAction=="E"?2.2f:playerAction=="Q"?1.5f:playerAction=="W"?1.3f:1);
            for(int i=minions.Count-1;i>=0;i--)
            {var m=minions[i];if(Mathf.Abs(player.x-m.x)<range&&(radial||(m.x-player.x)*face>-.5f)){m.hp-=damage;if(m.hp<=0){Burst(new Vector2(m.x,1),.8f,Palette.Gold,12);minions.RemoveAt(i);}}}
            if(barrier&&Mathf.Abs(player.x)<range&&((0-player.x)*face>0||radial)){barrierHp-=damage;Burst(new Vector2(0,1.2f),.7f,Palette.Gold,8);if(barrierHp<=0){barrier=false;bossStagger=3;Say("결계 파괴 · 파네스가 추락했습니다",2);}}
            Burst(player+new Vector2(face*.9f,1),.25f,Palette.Teal,4);
        }
        public float BossWidth=>bossIndex==0?ArakusGeometry.HalfWidth:bossIndex==3?.8f:.65f;
        public float BossHeight=>bossIndex==0?ArakusGeometry.Height:2.2f;
        public bool BossAttackHeightOverlap(float feetY)
            =>bossIndex==0?feetY<boss.y+BossHeight&&feetY+2.2f>boss.y:Mathf.Abs(feetY-boss.y)<2.2f;
        public bool BossShotOverlap(Vector2 point,float radius)
            =>bossIndex==0?Mathf.Abs(point.x-boss.x)<BossWidth+radius&&point.y+radius>boss.y&&point.y-radius<boss.y+BossHeight
                :Vector2.Distance(point,boss+Vector2.up*1.2f)<1.2f;
        // 패링 성공 연출: 멈춤 → 슬로모션, 화면 번쩍임, 부딪힌 곳으로 카메라 펀치, 큰 충돌 불꽃, 보스가 크게 밀려 보임
        public void ParryFeedback(float attackerX)
        {
            float toward=Mathf.Sign(attackerX-player.x);Vector2 clash=player+new Vector2(toward*.8f,1.25f);
            flash=.3f;shake=6;sound.Play("parry");sound.Play("clang");Burst(clash,2.4f,Palette.Gold,32);
            HitStop(HitStopParry);slowMo=ParrySlowMo;whiteFlash=.55f;zoomPunch=.8f;zoomFocus=clash;bossFlash=.14f;
            bossRecoil=toward*.45f;AddImpact(clash,2,1.2f,0);
        }
        // weight: 타격의 무게 (일반 공격 1, 스킬 1.3~2.6) → 멈춤·흔들림·밀림·불꽃 크기가 커진다
        public void DamageBoss(float damage,float weight=1)
        {
            if(ended||mode!=ScreenMode.Battle)return;
            if(barrier)damage*=.15f;
            damage*=BossPatterns.DamageTakenScale; // 아라쿠스 분노 시전 중에만 절반
            bossHp=Mathf.Max(0,bossHp-damage);bossFlash=.1f;ult=Mathf.Min(100,ult+damage*.03f);sound.Play("hit");if(weight>=2)sound.Play("thud");
            float side=Mathf.Sign(boss.x-player.x);if(side==0)side=1;
            shake=Mathf.Max(shake,1.4f+weight*1.1f);HitStop(HitStopLight*(.8f+.4f*weight));bossRecoil=side*(.1f+.07f*weight);
            Vector2 at=new Vector2(boss.x-side*BossWidth*.55f,boss.y+1.25f+(hitCount%2==0?.15f:-.1f));hitCount++;
            AddImpact(at,weight>=2?1:0,.85f+.25f*weight,(hitCount%2==0?35:-30)*side);
            Burst(at,.9f+.3f*weight,Palette.Gold,(int)(8+5*weight));
            if(bossHp<=0)EndBattle(true);
        }
        // ignoreDash: 대시 무적을 무시하는 공격 (아라쿠스 일대 휩쓸기). 맞은 뒤의 무적은 그대로 지킨다.
        public void PlayerHit(float damage,bool canParry,string pattern,float attackerX,bool overlap=true,bool ignoreDash=false)
        {
            if(ended)return;var s=Stat(pattern);if(s==null)return;
            if(!overlap){s.opportunities++;s.dodges++;return;}
            // Intentional parry can override post-hit invulnerability; dash invulnerability cannot.
            bool activeParry=canParry&&parryAge>=.05f&&parryAge<=.2f&&dashTime<=0;
            if(activeParry)
            {
                s.opportunities++;s.parries++;parries++;ult=Mathf.Min(100,ult+15);parryLock=.05f;parryAge=99;bossStagger=Mathf.Max(bossStagger,.45f);
                ParryFeedback(attackerX);
                if(seal&&++sealParries>=3){seal=false;Say("회피 봉인 해제",2);}
                return;
            }
            if(playerInv>0&&!(ignoreDash&&dashTime>0)){if(dashTime>0){s.opportunities++;s.dodges++;}else s.ignored++;return;}
            s.opportunities++;s.hits++;damage=Mathf.Min(damage,maxHp*.3f);s.damage+=damage;
            playerHp=Mathf.Max(0,playerHp-damage);hits++;playerInv=.35f;stun=.15f;hitFlash=.18f;shake=5;
            HitStop(HitStopHurt);AddImpact(player+new Vector2(0,1.1f),3,1,0);
            if(playerAction=="E"&&actionTime>.6f)skillE=7.5f;
            actionTime=0;healing=0;parryAge=99;player.x=Mathf.Clamp(player.x+Mathf.Sign(player.x-attackerX)*.45f,Left+.4f,Right-.4f);
            sound.Play("hit");Burst(player+Vector2.up,.9f,Palette.Red,15);
        }
        public void Burst(Vector2 position,float force,Color color,int count)
        {for(int i=0;i<count;i++){float a=UnityEngine.Random.value*Mathf.PI*2;motes.Add(new Mote{pos=position,velocity=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*UnityEngine.Random.Range(.4f,3f)*force,life=UnityEngine.Random.Range(.18f,.6f),color=color,size=UnityEngine.Random.Range(.035f,.12f)});}if(motes.Count>350)motes.RemoveRange(0,motes.Count-350);}
        void EndBattle(bool clear)
        {
            bool abandoned=abandoning;abandoning=false;
            if(ended)return;
            if(!rewards.TrySettle(save,spec,bossIndex,clear,bossHp,seconds,hits,parries,battleDda,out result,out wasFirstClear))return;
            ended=true;result.abandoned=abandoned;
            dda.Complete(save,spec,clear,abandoned);
            SaveStore.Log(result,stats,save);SaveStore.Save(save);mode=ScreenMode.Result;paused=false;flash=clear?.5f:.2f;
            sound.Play(clear?"parry":"death");
        }
        public void ReturnLobby(){mode=ScreenMode.Lobby;paused=false;showHelp=showSettings=showHistory=showCards=false;current=null;barrier=false;potions=3;hazards.Clear();shots.Clear();minions.Clear();SaveStore.Save(save);}
        public void Abandon(){abandoning=true;EndBattle(false);Say("전투를 포기했다. 기록과 골드는 남았다.");}
        void OnApplicationQuit(){if(mode==ScreenMode.Battle&&!ended){abandoning=true;EndBattle(false);}if(save!=null)SaveStore.Save(save);}
        void DemoInput(ref float move,ref bool jump,ref bool dash,ref bool attack,ref bool parry,ref bool q,ref bool w,ref bool e,ref bool r,ref bool f,float dt)
        {
            float distance=boss.x-player.x;move=Mathf.Abs(distance)>2?Mathf.Sign(distance):0;demoAction-=dt;
            attack=demoAction<=0;if(attack)demoAction=.37f;q=skillQ<=0&&Mathf.Abs(distance)>4;w=skillW<=0&&Mathf.Abs(distance)<3;e=skillE<=0&&bossStagger>1;
            parry=current!=null&&current.parryable&&patternAge>windup-.14f&&patternAge<windup-.09f;
            jump=current!=null&&current.kind==AttackKind.Wave&&patternAge>windup-.2f;dash=false;r=ult>=100;f=playerHp<maxHp*.5f;
        }
        public sealed class Hazard{public float x,width,delay,life,damage;public string pattern;public bool parry,hit,wide;public float cueAt=-1;public Color color;}
        public sealed class Shot{public Vector2 pos,vel;public float damage,life=6,radius=.22f,maxTravel,travelled;public bool parry,friendly,returns,returning,hitDone;public float cueAt=-1;public string pattern;}
        public sealed class Minion{public float x,hp=25,attack;public string pattern;}
        public sealed class Mote{public Vector2 pos,velocity;public float life,size;public Color color;}
        // 충돌 불꽃 한 개. kind 0 = 일반 타격, 1 = 강한 타격, 2 = 패링 충돌, 3 = 플레이어 피격
        public sealed class Impact{public Vector2 pos;public float age,life,size,angle;public int kind;}
    }
}
