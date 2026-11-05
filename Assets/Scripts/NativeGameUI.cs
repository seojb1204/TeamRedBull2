#if !HEADLESS && !DESKTOP_PREVIEW
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
namespace ReversalTower
{
    /// <summary>
    /// 게임 화면(uGUI). 씬에 저장된 Canvas(FoundationSetup이 만든 것)를 그대로 쓰고,
    /// 모양·배치·추가 요소는 Awake에서 코드로 입힌다 → 씬 파일을 고치지 않아 다른 팀원의 씬 작업과 충돌하지 않는다.
    ///
    /// 원칙: 한 화면에 크게 보이는 것은 하나. 나머지는 작게. 글은 짧게.
    /// 모양(폰트·테두리·색)은 UiKit.cs 한 곳에서 바뀐다. 도움말·기록·설정 창은 아직 IMGUI(GameUI.cs).
    ///
    /// 씬에 연결된 필드 이름과 public 메서드(버튼 이벤트)는 바꾸지 말 것 — LobbySmoke 검사와 씬 이벤트가 이름으로 찾는다.
    /// </summary>
    public sealed class NativeGameUI : MonoBehaviour
    {
        public TowerGame game;
        public GameObject titlePage,lobbyPage,battlePage,resultPage,endingPage,cardsPage,pausePage;
        public CanvasGroup inputGroup;
        public Text lobbyStats,bossDescription,battleStats,resultStats,notice,endingText;
        public Text[] cardLabels;
        public Button[] cardButtons,bossButtons;
        public Button upgradeButton,retryButton,endingButton;
        public Text upgradeLabel;
        public Image playerHealth,bossHealth;

        const float BarWidth=470;   // LobbySmoke가 이 폭을 기준으로 체력 바를 검사한다

        bool built;
        ScreenMode lastMode=(ScreenMode)(-1);
        float pageClock;
        CanvasGroup titleGroup,lobbyGroup,battleGroup,resultGroup,endingGroup,cardsGroup;

        // 타이틀
        Text titleEnterLabel;
        // 로비
        Text goldText,lobbyLine,focusTitle,focusName,focusReward,ultName,ultHint;
        Text[] bossNumbers,bossNames,bossStates;
        Button ultButton;
        float shownGold=-1;
        // 카드
        Text cardSub;
        Text[] cardGlyph,cardAmount,cardDetail,cardOwned;
        CanvasGroup[] cardGroups;
        RectTransform[] cardRects;
        Vector2[] cardHome;
        float[] cardLift;
        Button cardsClose;
        float cardsClock,outro=-1;
        int chosen=-1;
        bool cardsWereOpen;
        // 전투
        sealed class Slot{public RectTransform root;public Image frame,shade;public Text key,name,badge;public string letter;public float pop,last;}
        Slot[] slots;
        Text playerHpText,dashLabel,sealText,bossName,timer;
        Image[] dashPips,ticks;
        Image playerTrail,bossTrail;
        float playerTrailValue=1,bossTrailValue=1;
        bool ultWasReady;
        // 결과
        Text resSub,resSub2,resCompare,resGold;
        Text[] resValues;
        Image resBarFrame,resBarFill;
        Button resultLobby;
        float resultClock;
        // 엔딩
        Text endingOver,endingBody,endingNote;

        void Awake()
        {
            UiKit.Init();
            foreach(Text text in GetComponentsInChildren<Text>(true))text.font=UiKit.Regular;
            Build();
        }

        // ═══════════════════════════ 만들기 (한 번) ═══════════════════════════

        void Build()
        {
            if(built)return;built=true;
            titleGroup=Group(titlePage);lobbyGroup=Group(lobbyPage);battleGroup=Group(battlePage);
            resultGroup=Group(resultPage);endingGroup=Group(endingPage);cardsGroup=Group(cardsPage);
            BuildTitle();BuildLobby();BuildCards();BuildBattle();BuildPause();BuildResult();BuildEnding();
            if(notice)UiKit.Dress(notice,UiKit.Body,Palette.Gold,TextAnchor.MiddleCenter,false,true);
        }

        void BuildTitle()
        {
            var t=titlePage.transform;
            UiKit.Picture(t,"Shade",0,0,880,900,UiKit.FadeRight,Alpha(UiKit.Ink,.92f)).transform.SetAsFirstSibling();
            UiKit.Picture(t,"Accent",104,182,56,6,UiKit.Solid,Palette.Gold);
            var heading=Grab<Text>(titlePage,"Title");
            if(heading){UiKit.Place(heading,96,200,760,120);UiKit.Dress(heading,UiKit.Giant,Palette.White,TextAnchor.UpperLeft,true,true);heading.text="역전의 탑";}
            var sub=Grab<Text>(titlePage,"Subtitle");
            if(sub){UiKit.Place(sub,104,332,700,40);UiKit.Dress(sub,UiKit.Body,UiKit.Soft,TextAnchor.UpperLeft,false,true);sub.text="검을 받아내고, 쓰러뜨린 자의 힘을 이어받아라.";}
            var enter=Grab<Button>(titlePage,"탑에 입장");
            if(enter){UiKit.Place(enter,100,420,380,64);UiKit.Skin(enter,UiKit.Primary);titleEnterLabel=UiKit.Caption(enter,"탑에 들어가기",UiKit.Body,UiKit.Ink,"Enter");}
            Small(titlePage,"조작 안내",100,500,182,52);
            Small(titlePage,"설정",298,500,182,52);
            UiKit.Label(t,"Credit",104,836,600,30,UiKit.Body,UiKit.Muted).text="팀 레드불";
        }

        void BuildLobby()
        {
            var t=lobbyPage.transform;
            UiKit.Picture(t,"Shade",0,0,1600,240,UiKit.FadeDown,Alpha(UiKit.Ink,.85f)).transform.SetAsFirstSibling();
            var heading=Grab<Text>(lobbyPage,"Title");
            if(heading){UiKit.Place(heading,72,34,640,60);UiKit.Dress(heading,UiKit.Head,Palette.White,TextAnchor.UpperLeft,true,true);heading.text="맹세의 회랑";}
            lobbyLine=UiKit.Label(t,"Last Run",74,98,940,30,UiKit.Body,UiKit.Muted,TextAnchor.UpperLeft,false,true);
            goldText=UiKit.Label(t,"Gold",1028,34,500,60,UiKit.Head,Palette.Gold,TextAnchor.UpperRight,true,true);

            // 왼쪽: 다섯 개의 문
            int n=bossButtons.Length;bossNumbers=new Text[n];bossNames=new Text[n];bossStates=new Text[n];
            for(int i=0;i<n;i++)
            {
                var b=bossButtons[i];UiKit.Place(b,72,160+i*80,380,68);UiKit.Skin(b,UiKit.Button);
                bossNames[i]=b.GetComponentInChildren<Text>(true);
                UiKit.Place(bossNames[i],72,4,290,40);UiKit.Dress(bossNames[i],UiKit.Sub,Palette.White);
                bossNumbers[i]=UiKit.Label(b.transform,"Number",20,0,48,68,UiKit.Body,Palette.Gold,TextAnchor.MiddleLeft);
                bossNumbers[i].text=(i+1).ToString("00");
                bossStates[i]=UiKit.Label(b.transform,"State",74,40,290,26,UiKit.Body,UiKit.Muted);
            }

            // 가운데: 고른 문 하나
            UiKit.Picture(t,"Focus",480,160,620,420,UiKit.Panel,Color.white);
            focusTitle=UiKit.Label(t,"Focus Title",512,186,560,30,UiKit.Body,Palette.Gold);
            focusName=UiKit.Label(t,"Focus Name",508,220,560,90,UiKit.Huge,Palette.White,TextAnchor.UpperLeft,true);
            UiKit.Picture(t,"Focus Rule",512,316,556,2,UiKit.Solid,Alpha(Palette.Gold,.35f));
            if(bossDescription){bossDescription.transform.SetAsLastSibling();UiKit.Place(bossDescription,512,338,556,150);UiKit.Dress(bossDescription,UiKit.Body,UiKit.Soft);}
            focusReward=UiKit.Label(t,"Focus Reward",512,524,556,30,UiKit.Body,Palette.Gold);
            var enter=Grab<Button>(lobbyPage,"강화 없이 입장 [ENTER]");
            if(enter){enter.transform.SetAsLastSibling();UiKit.Place(enter,480,600,620,72);UiKit.Skin(enter,UiKit.Primary);UiKit.Caption(enter,"도전하기",UiKit.Sub,UiKit.Ink,"Enter");}
            UiKit.Label(t,"Hint",480,688,620,30,UiKit.Body,UiKit.Muted,TextAnchor.UpperCenter).text="← →  문 고르기";

            // 오른쪽 위: 궁극기
            UiKit.Picture(t,"Ultimate Panel",1128,160,400,170,UiKit.Panel,Color.white);
            UiKit.Label(t,"Ultimate Caption",1156,184,344,30,UiKit.Body,Palette.Gold).text="R  ·  계승한 기술";
            ultName=UiKit.Label(t,"Ultimate Name",1156,220,344,46,UiKit.Sub,Palette.White);
            ultButton=MakeButton(t,"Ultimate Change",1156,272,150,40,UiKit.Button,"바꾸기 ↻",UiKit.Body,Palette.White,CycleUltimate);
            ultHint=UiKit.Label(t,"Ultimate Hint",1156,278,344,30,UiKit.Body,UiKit.Muted);
            ultHint.text="보스를 쓰러뜨리면 늘어난다";

            // 오른쪽 아래: 영구 강화(인장)
            UiKit.Picture(t,"Upgrade Panel",1128,350,400,230,UiKit.Panel,Color.white);
            UiKit.Label(t,"Upgrade Caption",1156,374,344,30,UiKit.Body,Palette.Gold).text="C  ·  맹세의 인장";
            if(lobbyStats){lobbyStats.transform.SetAsLastSibling();UiKit.Place(lobbyStats,1156,412,344,70);UiKit.Dress(lobbyStats,UiKit.Body,Palette.White);}
            if(upgradeButton)
            {
                upgradeButton.transform.SetAsLastSibling();UiKit.Place(upgradeButton,1156,500,344,56);UiKit.Skin(upgradeButton,UiKit.Button);
                upgradeLabel=UiKit.Caption(upgradeButton,"인장 고르기",UiKit.Body,Palette.White);
            }

            // 작은 메뉴
            string[] menu={"기록","설정","조작 안내"};
            for(int i=0;i<menu.Length;i++)Small(lobbyPage,menu[i],1128+i*136,606,128,52);
        }

        void BuildCards()
        {
            var t=cardsPage.transform;
            var bg=cardsPage.GetComponent<Image>();
            if(bg){UiKit.Paint(bg,UiKit.Solid,new Color(.01f,.015f,.025f,.88f));bg.raycastTarget=true;}
            var title=Grab<Text>(cardsPage,"Title");
            if(title){UiKit.Place(title,0,140,1600,60);UiKit.Dress(title,UiKit.Head,Palette.White,TextAnchor.UpperCenter,true,true);title.text="인장을 하나 고르세요";}
            cardSub=UiKit.Label(t,"Subtitle",0,206,1600,30,UiKit.Body,UiKit.Muted,TextAnchor.UpperCenter);
            int n=cardButtons.Length;
            cardGlyph=new Text[n];cardAmount=new Text[n];cardDetail=new Text[n];cardOwned=new Text[n];
            cardGroups=new CanvasGroup[n];cardRects=new RectTransform[n];cardHome=new Vector2[n];cardLift=new float[n];
            for(int i=0;i<n;i++)
            {
                var b=cardButtons[i];
                cardRects[i]=UiKit.Place(b,250+i*380,262,340,400,true);cardHome[i]=cardRects[i].anchoredPosition;
                UiKit.Skin(b,UiKit.Card);
                cardGroups[i]=Group(b.gameObject);
                cardGlyph[i]=UiKit.Label(b.transform,"Glyph",0,28,340,80,UiKit.Huge,Palette.Gold,TextAnchor.UpperCenter,true);
                cardAmount[i]=UiKit.Label(b.transform,"Amount",0,116,340,90,UiKit.Huge,Palette.White,TextAnchor.UpperCenter,true,true);
                var stat=cardLabels[i];UiKit.Place(stat,10,206,320,44);UiKit.Dress(stat,UiKit.Sub,Palette.White,TextAnchor.UpperCenter);
                cardDetail[i]=UiKit.Label(b.transform,"Detail",28,258,284,64,UiKit.Body,UiKit.Muted,TextAnchor.UpperCenter);
                cardOwned[i]=UiKit.Label(b.transform,"Owned",0,344,340,30,UiKit.Body,Palette.Teal,TextAnchor.UpperCenter);
            }
            cardsClose=Grab<Button>(cardsPage,"선택하지 않고 돌아가기");
            if(cardsClose){UiKit.Place(cardsClose,640,700,320,56);UiKit.Skin(cardsClose,UiKit.Button);UiKit.Caption(cardsClose,"이번엔 넘어가기",UiKit.Body,Palette.White,"Esc");}
        }

        void BuildBattle()
        {
            var t=battlePage.transform;
            var bg=battlePage.GetComponent<Image>();
            if(bg)UiKit.Paint(bg,UiKit.FadeDown,Alpha(UiKit.Ink,.9f));
            UiKit.Picture(t,"Shade Bottom",0,720,1600,180,UiKit.FadeUp,Alpha(UiKit.Ink,.9f)).transform.SetAsFirstSibling();

            // 왼쪽 위: 내 체력
            playerTrail=Bar(t,playerHealth,60,48,"Player");
            playerHpText=UiKit.Label(t,"Player HP",60,72,300,30,UiKit.Body,Palette.White,TextAnchor.UpperLeft,false,true);
            dashLabel=UiKit.Label(t,"Dash",330,72,150,30,UiKit.Body,UiKit.Muted,TextAnchor.UpperRight,false,true);dashLabel.text="대시";
            dashPips=new Image[2];
            for(int i=0;i<2;i++)dashPips[i]=UiKit.Picture(t,"Dash Pip",492+i*22,80,14,14,UiKit.Pip,Palette.Teal);
            sealText=UiKit.Label(t,"Seal",200,72,330,30,UiKit.Body,Palette.Purple,TextAnchor.UpperRight,false,true);

            // 오른쪽 위: 보스
            bossName=UiKit.Label(t,"Boss Name",940,2,600,46,UiKit.Sub,Palette.Gold,TextAnchor.UpperRight,true,true);
            bossTrail=Bar(t,bossHealth,1070,48,"Boss");
            ticks=new Image[2];
            for(int i=0;i<2;i++){ticks[i]=UiKit.Picture(bossHealth.transform.parent,"Phase Mark",0,0,2,14,UiKit.Solid,Alpha(UiKit.Ink,.95f));ticks[i].transform.SetAsLastSibling();}
            if(battleStats){UiKit.Place(battleStats,840,72,700,30);UiKit.Dress(battleStats,UiKit.Body,UiKit.Soft,TextAnchor.UpperRight,false,true);battleStats.supportRichText=true;}
            timer=UiKit.Label(t,"Timer",700,12,200,30,UiKit.Body,UiKit.Muted,TextAnchor.UpperCenter,false,true);

            // 아래: 기술 칸
            string[] keys={"J","K","Q","W","E","F","R"};
            string[] names={"검격","패링","돌진","회전","처형","회복약","궁극기"};
            slots=new Slot[keys.Length];
            for(int i=0;i<keys.Length;i++)
            {
                var obj=new GameObject("Slot "+keys[i],typeof(RectTransform));obj.transform.SetParent(t,false);
                var s=new Slot{letter=keys[i]};
                s.root=UiKit.Place(obj.transform,60+i*84,788,64,64,true);
                s.frame=UiKit.Picture(obj.transform,"Frame",0,0,64,64,UiKit.Slot,Color.white);
                s.shade=UiKit.Picture(obj.transform,"Cooldown",6,6,52,52,UiKit.Solid,new Color(0,0,0,.62f));
                s.shade.type=Image.Type.Filled;s.shade.fillMethod=Image.FillMethod.Vertical;s.shade.fillOrigin=(int)Image.OriginVertical.Top;s.shade.fillAmount=0;
                s.key=UiKit.Label(obj.transform,"Key",0,0,64,64,UiKit.Sub,Palette.White,TextAnchor.MiddleCenter,true,true);s.key.text=keys[i];
                s.badge=UiKit.Label(obj.transform,"Badge",28,2,32,30,UiKit.Body,Palette.Teal,TextAnchor.UpperRight,false,true);
                s.name=UiKit.Label(obj.transform,"Name",-24,68,112,30,UiKit.Body,UiKit.Muted,TextAnchor.UpperCenter,false,true);s.name.text=names[i];
                slots[i]=s;
            }
            var ult=slots[slots.Length-1].shade;   // 궁극기 칸은 아래부터 금빛으로 차오름
            ult.color=Alpha(Palette.Gold,.45f);ult.fillOrigin=(int)Image.OriginVertical.Bottom;
        }

        /// <summary>체력 바에 도트 테두리와 깎인 만큼 늦게 줄어드는 붉은 잔상을 붙인다</summary>
        Image Bar(Transform page,Image fill,float x,float y,string who)
        {
            var back=(RectTransform)fill.transform.parent;
            var frame=UiKit.Picture(page,who+" Bar Frame",x-6,y-6,BarWidth+12,26,UiKit.BarFrame,Color.white);
            frame.transform.SetSiblingIndex(back.GetSiblingIndex());
            UiKit.Place(back,x,y,BarWidth,14);
            var backImage=back.GetComponent<Image>();if(backImage)backImage.color=new Color(.02f,.03f,.045f,1);
            var trail=UiKit.Picture(back,"Trail",0,0,BarWidth,14,UiKit.Solid,new Color(1,.45f,.42f,.9f));
            trail.transform.SetSiblingIndex(fill.transform.GetSiblingIndex());
            return trail;
        }

        void BuildPause()
        {
            UiKit.Place(pausePage.transform,0,0,1600,900);
            var bg=pausePage.GetComponent<Image>();
            if(bg){UiKit.Paint(bg,UiKit.Solid,new Color(0,0,0,.62f));bg.raycastTarget=true;}
            var t=pausePage.transform;
            UiKit.Picture(t,"Frame",500,200,600,470,UiKit.Panel,Color.white).transform.SetAsFirstSibling();
            var title=Grab<Text>(pausePage,"Title");
            if(title){UiKit.Place(title,540,234,520,60);UiKit.Dress(title,UiKit.Head,Palette.White,TextAnchor.UpperLeft,true);title.text="잠시 검을 내려놓고";}
            var resume=Grab<Button>(pausePage,"계속하기");
            if(resume){UiKit.Place(resume,540,316,520,64);UiKit.Skin(resume,UiKit.Primary);UiKit.Caption(resume,"계속 싸우기",UiKit.Body,UiKit.Ink,"Esc");}
            Small(pausePage,"설정",540,396,520,56);
            var quit=Grab<Button>(pausePage,"전투 중단");
            if(quit){UiKit.Place(quit,540,468,520,56);UiKit.Skin(quit,UiKit.Button);UiKit.Caption(quit,"포기하고 회랑으로",UiKit.Body,Palette.White);}
            UiKit.Label(t,"Note",540,544,520,90,UiKit.Body,UiKit.Muted).text="포기해도 이번 전투의 기록과 골드는 남는다.";
        }

        void BuildResult()
        {
            UiKit.Place(resultPage.transform,0,0,1600,900);
            var bg=resultPage.GetComponent<Image>();
            if(bg){UiKit.Paint(bg,UiKit.Solid,new Color(.012f,.02f,.035f,.84f));bg.raycastTarget=true;}
            var t=resultPage.transform;
            if(resultStats){UiKit.Place(resultStats,0,148,1600,100);UiKit.Dress(resultStats,UiKit.Huge,Palette.White,TextAnchor.UpperCenter,true,true);}
            resSub=UiKit.Label(t,"Sub",0,250,1600,46,UiKit.Sub,Palette.White,TextAnchor.UpperCenter,false,true);
            resBarFrame=UiKit.Picture(t,"Boss Left Frame",494,306,612,30,UiKit.BarFrame,Color.white);
            resBarFill=UiKit.Picture(t,"Boss Left",500,312,600,18,UiKit.Solid,Palette.Red);
            resSub2=UiKit.Label(t,"Sub 2",0,302,1600,30,UiKit.Body,UiKit.Muted,TextAnchor.UpperCenter);
            string[] labels={"걸린 시간","패링","맞은 횟수"};
            resValues=new Text[labels.Length];
            for(int i=0;i<labels.Length;i++)
            {
                float cx=620+i*180;
                UiKit.Label(t,"Stat Label",cx-100,384,200,30,UiKit.Body,UiKit.Muted,TextAnchor.UpperCenter).text=labels[i];
                resValues[i]=UiKit.Label(t,"Stat Value",cx-100,414,200,46,UiKit.Sub,Palette.White,TextAnchor.UpperCenter);
            }
            resCompare=UiKit.Label(t,"Compare",0,474,1600,30,UiKit.Body,Palette.Teal,TextAnchor.UpperCenter);
            resGold=UiKit.Label(t,"Gold",0,512,1600,60,UiKit.Head,Palette.Gold,TextAnchor.UpperCenter,true,true);
            resultLobby=Grab<Button>(resultPage,"로비로 돌아가기");
        }

        void BuildEnding()
        {
            UiKit.Place(endingPage.transform,0,0,1600,900);
            var bg=endingPage.GetComponent<Image>();
            if(bg){UiKit.Paint(bg,UiKit.Solid,new Color(.01f,.015f,.025f,.94f));bg.raycastTarget=true;}
            var t=endingPage.transform;
            endingOver=UiKit.Label(t,"Over",0,178,1600,30,UiKit.Body,Palette.Gold,TextAnchor.UpperCenter);
            if(endingText){UiKit.Place(endingText,0,218,1600,100);UiKit.Dress(endingText,UiKit.Huge,Palette.White,TextAnchor.UpperCenter,true,true);}
            endingBody=UiKit.Label(t,"Body",400,340,800,200,UiKit.Body,UiKit.Soft,TextAnchor.UpperCenter);endingBody.lineSpacing=1.25f;
            endingNote=UiKit.Label(t,"Note",0,560,1600,34,UiKit.Body,Palette.Gold,TextAnchor.UpperCenter);
            var back=Grab<Button>(endingPage,"로비로 돌아가기");
            if(back){UiKit.Place(back,640,630,320,64);UiKit.Skin(back,UiKit.Primary);UiKit.Caption(back,"회랑으로 돌아가기",UiKit.Body,UiKit.Ink);}
        }

        // ═══════════════════════════ 매 프레임 ═══════════════════════════

        public void Refresh()
        {
            if(!game||game.save==null)return;
            if(!built){UiKit.Init();Build();}
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            ScreenMode mode=game.mode;
            if(mode!=lastMode){EnterMode(mode);lastMode=mode;}
            pageClock+=dt;

            bool cardsVisible=mode==ScreenMode.Lobby&&(game.showCards||outro>=0);
            titlePage.SetActive(mode==ScreenMode.Title);lobbyPage.SetActive(mode==ScreenMode.Lobby);
            battlePage.SetActive(mode==ScreenMode.Battle);resultPage.SetActive(mode==ScreenMode.Result);
            endingPage.SetActive(mode==ScreenMode.Ending);cardsPage.SetActive(cardsVisible);
            pausePage.SetActive(mode==ScreenMode.Battle&&game.paused);
            float fade=UiKit.EaseOut(pageClock/.2f);   // 화면이 바뀔 때 0.2초 동안 스며듦
            titleGroup.alpha=lobbyGroup.alpha=battleGroup.alpha=resultGroup.alpha=endingGroup.alpha=fade;
            inputGroup.interactable=!(game.showHelp||game.showSettings||game.showHistory);
            Set(notice,game.noticeTime>0?game.notice:"");

            switch(mode)
            {
                case ScreenMode.Title:Set(titleEnterLabel,game.save.history.Count>0?"이어서 오르기":"탑에 들어가기");break;
                case ScreenMode.Lobby:RefreshLobby(dt);RefreshCards(dt);break;
                case ScreenMode.Battle:RefreshBattle(dt);break;
                case ScreenMode.Result:RefreshResult(dt);break;
            }
        }

        void EnterMode(ScreenMode mode)
        {
            pageClock=0;
            if(notice)
            {
                if(mode==ScreenMode.Battle)UiKit.Place(notice,300,180,1000,36);
                else UiKit.Place(notice,300,836,1000,36);
            }
            if(mode==ScreenMode.Lobby)
            {
                if(shownGold<0)shownGold=game.save.gold;   // 타이틀에서 처음 들어올 땐 굴리지 않음, 전투 뒤엔 늘어나는 게 보임
                cardsWereOpen=false;outro=-1;chosen=-1;
                Set(lobbyLine,LastRunLine(game.save));
            }
            if(mode==ScreenMode.Battle&&game.spec!=null)
            {
                playerTrailValue=bossTrailValue=1;ultWasReady=false;
                PlaceTick(ticks[0],game.spec.phaseThreshold,true);
                PlaceTick(ticks[1],.3f,game.spec.echoBoss||game.spec.hasPillars);
                foreach(var s in slots){s.pop=0;s.last=0;}
            }
            if(mode==ScreenMode.Result)EnterResult();
            if(mode==ScreenMode.Ending)EnterEnding();
        }

        void RefreshLobby(float dt)
        {
            SaveData save=game.save;
            shownGold+=(save.gold-shownGold)*Mathf.Min(1,dt*7);
            if(Mathf.Abs(save.gold-shownGold)<.5f)shownGold=save.gold;
            Set(goldText,UiKit.Gold(Mathf.RoundToInt(shownGold)));

            for(int i=0;i<bossButtons.Length;i++)
            {
                bool locked=i>save.unlockedBoss,selected=i==game.bossIndex,cleared=Cleared(save,i);
                bossButtons[i].interactable=!locked;
                Swap(bossButtons[i].image,selected?UiKit.Selected:UiKit.Button);
                Set(bossNames[i],Catalog.Bosses[i].name);
                Tint(bossNames[i],locked?UiKit.Muted:Palette.White);
                Tint(bossNumbers[i],locked?UiKit.Muted:Palette.Gold);
                Set(bossStates[i],locked?"잠긴 문":cleared?"★ 넘은 문":i==4?"숨겨진 문":"열린 문");
                Tint(bossStates[i],cleared?Palette.Teal:UiKit.Muted);
            }

            BossSpec boss=Catalog.Bosses[game.bossIndex];bool done=Cleared(save,game.bossIndex);
            Set(focusTitle,boss.title);Set(focusName,boss.name);Set(bossDescription,boss.lesson);
            Set(focusReward,(done?"계승함  ·  ":"넘으면 얻는 기술  ·  ")+boss.ultimate);
            Tint(focusReward,done?Palette.Teal:Palette.Gold);

            bool any=false;for(int i=0;i<Catalog.Bosses.Length;i++)any|=Cleared(save,i);
            int u=save.equippedUltimate;
            Set(ultName,u<0||u>=Catalog.Bosses.Length?"반역의 불꽃":Catalog.Bosses[u].ultimate);
            if(ultButton.gameObject.activeSelf!=any)ultButton.gameObject.SetActive(any);
            if(ultHint.gameObject.activeSelf==any)ultHint.gameObject.SetActive(!any);

            Set(lobbyStats,"공격 "+Num(LobbyCards.Attack(save))+"  ·  체력 "+Num(LobbyCards.Health(save))+"\n회복약 "+Num(LobbyCards.Heal(save))+"  ·  인장 "+save.cardPurchases+"장");
            int cost=LobbyCards.Cost(save);
            upgradeButton.interactable=!save.cardChosen;
            Set(upgradeLabel,save.cardChosen?"다음 전투가 끝나면 다시":save.gold<cost?"골드 부족  ·  "+UiKit.Gold(cost):"인장 고르기  ·  "+UiKit.Gold(cost));
        }

        void RefreshCards(float dt)
        {
            SaveData save=game.save;
            if(game.showCards&&!cardsWereOpen){cardsClock=0;outro=-1;chosen=-1;for(int i=0;i<cardLift.Length;i++)cardLift[i]=0;}
            cardsWereOpen=game.showCards;
            if(!cardsPage.activeSelf)return;
            cardsClock+=dt;if(outro>=0)outro+=dt;

            int cost=LobbyCards.Cost(save);bool afford=save.gold>=cost;
            Set(cardSub,afford?"한 장에 "+UiKit.Gold(cost)+"   ·   가진 골드 "+UiKit.Gold(save.gold)+"   ·   쓰러져도 사라지지 않는다"
                              :"골드가 모자라다   ·   "+UiKit.Gold(cost)+" 필요, 지금 "+UiKit.Gold(save.gold));
            Tint(cardSub,afford||outro>=0?UiKit.Muted:Palette.Red);

            bool offer=save.cardOffer!=null&&save.cardOffer.Length==cardButtons.Length;
            bool live=outro<0;
            for(int i=0;i<cardButtons.Length;i++)
            {
                UpgradeCard card=offer?LobbyCards.Find(save.cardOffer[i]):null;
                if(cardButtons[i].gameObject.activeSelf!=(card!=null))cardButtons[i].gameObject.SetActive(card!=null);
                if(card==null)continue;
                int owned=LobbyCards.Count(save,card.id);
                Set(cardGlyph[i],card.glyph);Set(cardAmount[i],card.amount);Set(cardLabels[i],card.stat);Set(cardDetail[i],card.detail);
                Set(cardOwned[i],!live&&i==chosen?"지금 "+owned+"장":"지금 "+owned+"장 → "+(owned+1)+"장");
                cardButtons[i].interactable=live?!save.cardChosen&&afford:true;   // 사라지는 동안 흐려지지 않게

                // 등장: 한 장씩 아래에서 올라옴
                float t=(cardsClock-i*.07f)/.32f;
                float rise=(1-UiKit.EaseOutBack(t))*56,alpha=Mathf.Clamp01(t*1.8f),scale=1;
                // 마우스를 올리면 들림
                bool hover=live&&cardButtons[i].interactable&&RectTransformUtility.RectangleContainsScreenPoint(cardRects[i],Input.mousePosition,null);
                cardLift[i]=Mathf.MoveTowards(cardLift[i],hover?12:0,dt*120);
                // 고른 뒤: 고른 카드는 커졌다 돌아오고, 나머지는 가라앉으며 사라짐
                if(!live)
                {
                    if(i==chosen){scale=1+.1f*UiKit.EaseOut(outro/.16f)-.06f*UiKit.EaseOut((outro-.16f)/.3f);cardLift[i]=12;}
                    else{alpha*=1-Mathf.Clamp01(outro/.22f);rise+=outro*120;}
                }
                Swap(cardButtons[i].image,hover||(!live&&i==chosen)?UiKit.CardHover:UiKit.Card);
                cardRects[i].anchoredPosition=cardHome[i]+new Vector2(0,cardLift[i]-rise);
                cardRects[i].localScale=Vector3.one*scale;
                cardGroups[i].alpha=alpha;
            }
            if(cardsClose&&cardsClose.gameObject.activeSelf!=live)cardsClose.gameObject.SetActive(live);
            cardsGroup.alpha=live?UiKit.EaseOut(cardsClock/.18f):1-Mathf.Clamp01((outro-.42f)/.22f);
            cardsGroup.blocksRaycasts=live;
            if(outro>.66f){outro=-1;chosen=-1;cardsPage.SetActive(false);}
        }

        void RefreshBattle(float dt)
        {
            if(game.spec==null)return;
            float pf=Mathf.Clamp01(game.playerHp/Mathf.Max(1,game.maxHp)),bf=Mathf.Clamp01(game.bossHp/Mathf.Max(1,game.spec.hp));
            playerHealth.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,BarWidth*pf);
            bossHealth.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,BarWidth*bf);
            Trail(playerTrail,pf,ref playerTrailValue,dt);Trail(bossTrail,bf,ref bossTrailValue,dt);
            Set(playerHpText,Mathf.CeilToInt(game.playerHp)+" / "+Mathf.CeilToInt(game.maxHp));

            bool seal=game.seal;
            if(sealText.gameObject.activeSelf!=seal){sealText.gameObject.SetActive(seal);dashLabel.gameObject.SetActive(!seal);foreach(var p in dashPips)p.gameObject.SetActive(!seal);}
            if(seal)Set(sealText,"회피 봉인  ·  패링 "+game.sealParries+" / 3");
            else for(int i=0;i<dashPips.Length;i++){var c=i<game.dashes?Palette.Teal:Alpha(UiKit.Muted,.3f);if(dashPips[i].color!=c)dashPips[i].color=c;}

            Set(bossName,game.spec.name);
            Set(battleStats,PatternLine());
            Set(timer,UiKit.Clock(game.seconds));

            UpdateSlot(slots[0],0,1,false,false,dt);
            UpdateSlot(slots[1],game.parryLock,.55f,false,false,dt);
            UpdateSlot(slots[2],game.skillQ,6,true,true,dt);
            UpdateSlot(slots[3],game.skillW,10,true,true,dt);
            UpdateSlot(slots[4],game.skillE,15,true,true,dt);
            var f=slots[5];int potions=game.PotionCount;
            UpdateSlot(f,0,1,false,false,dt);Set(f.badge,potions.ToString());Tint(f.key,potions>0?Palette.White:UiKit.Muted);Tint(f.badge,potions>0?Palette.Teal:UiKit.Muted);
            var r=slots[6];bool ready=game.ult>=100;
            r.shade.fillAmount=Mathf.Clamp01(game.ult/100);
            Tint(r.key,ready?Palette.Gold:UiKit.Soft);
            Set(r.name,ready?"궁극기":Mathf.FloorToInt(game.ult)+"%");Tint(r.name,ready?Palette.Gold:UiKit.Muted);
            if(ready&&!ultWasReady)r.pop=1;ultWasReady=ready;
            Pop(r,dt);
        }

        string PatternLine()
        {
            if(game.bossStagger>0)return "<color=#54e3d4>그로기  ·  지금 몰아쳐라</color>";
            var p=game.current;
            if(p==null)return "";
            return p.name+"   "+(p.parryable?"<color=#479eff>◆ 받아치기 K</color>":"<color=#ff5a5e>피하기</color>");
        }

        void UpdateSlot(Slot s,float remaining,float max,bool number,bool popWhenReady,float dt)
        {
            bool cooling=remaining>.01f;
            float fill=cooling?Mathf.Clamp01(remaining/max):0;
            if(!Mathf.Approximately(s.shade.fillAmount,fill))s.shade.fillAmount=fill;
            Set(s.key,cooling&&number?Mathf.CeilToInt(remaining).ToString():s.letter);
            Tint(s.key,cooling?UiKit.Muted:Palette.White);
            if(popWhenReady&&s.last>.01f&&!cooling)s.pop=1;   // 쿨타임이 끝나는 순간 칸이 톡 튐
            s.last=remaining;
            Pop(s,dt);
        }

        static void Pop(Slot s,float dt)
        {
            s.pop=Mathf.MoveTowards(s.pop,0,dt*4);
            float k=1+.18f*UiKit.EaseOut(s.pop);
            if(!Mathf.Approximately(s.root.localScale.x,k))s.root.localScale=new Vector3(k,k,1);
            Swap(s.frame,s.pop>0?UiKit.Selected:UiKit.Slot);
        }

        void Trail(Image trail,float value,ref float shown,float dt)
        {
            shown=value>shown?value:Mathf.MoveTowards(shown,value,dt*.45f);   // 회복은 바로, 피해는 천천히 따라 줄어듦
            trail.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,BarWidth*shown);
        }

        void PlaceTick(Image tick,float at,bool show)
        {
            tick.gameObject.SetActive(show);
            UiKit.Place(tick,BarWidth*at-1,0,2,14);
        }

        void EnterResult()
        {
            var r=game.result;var spec=game.spec;
            if(r==null||spec==null)return;
            resultClock=0;
            string name=spec.name;
            float left=Mathf.Clamp01(game.bossHp/Mathf.Max(1,spec.hp));
            if(r.clear)
            {
                resultStats.text=UiKit.Josa(name,"을","를")+" 쓰러뜨렸다";resultStats.color=Palette.Gold;
                if(game.wasFirstClear){resSub.text="새 기술  ·  "+spec.ultimate;resSub.color=Palette.Gold;resSub2.text="회랑에서 R 기술로 장착됐다.";}
                else{resSub.text="이미 넘은 문을 다시 넘었다";resSub.color=UiKit.Soft;resSub2.text="";}
            }
            else
            {
                resultStats.text=UiKit.Josa(name,"이","가")+" 버텨냈다";resultStats.color=Palette.White;
                resSub.text="남은 체력 "+Mathf.CeilToInt(left*100)+"%";resSub.color=Palette.White;resSub2.text="";
            }
            resBarFrame.gameObject.SetActive(!r.clear);resBarFill.gameObject.SetActive(!r.clear);
            resSub2.gameObject.SetActive(r.clear);
            resBarFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,600*left);
            resValues[0].text=UiKit.Clock(r.seconds);resValues[1].text=r.parries+"번";resValues[2].text=r.hits+"번";
            resCompare.text=Compare(r);

            // 버튼: 졌으면 다시 도전이 먼저, 이겼으면 회랑으로가 먼저 (Enter는 언제나 회랑으로)
            bool ending=r.clear&&game.bossIndex>=3;
            retryButton.gameObject.SetActive(!r.clear);endingButton.gameObject.SetActive(ending);
            if(!r.clear){ResultButton(retryButton,520,"다시 도전",true,null);ResultButton(resultLobby,810,"회랑으로",false,"Enter");}
            else if(ending){ResultButton(endingButton,520,"엔딩 보기",true,null);ResultButton(resultLobby,810,"회랑으로",false,"Enter");}
            else ResultButton(resultLobby,665,"회랑으로",true,"Enter");
        }

        void ResultButton(Button b,float x,string text,bool primary,string key)
        {
            if(!b)return;
            UiKit.Place(b,x,604,270,64);UiKit.Skin(b,primary?UiKit.Primary:UiKit.Button);
            UiKit.Caption(b,text,UiKit.Body,primary?UiKit.Ink:Palette.White,key);
        }

        /// <summary>같은 보스의 바로 전 도전과 비교해 나아진 것만 말한다 (DDA 보정값은 절대 보여 주지 않는다)</summary>
        string Compare(HistoryEntry now)
        {
            var history=game.save.history;HistoryEntry before=null;
            for(int i=1;i<history.Count;i++)if(history[i]!=null&&history[i].boss==now.boss){before=history[i];break;}
            if(before==null)return "";
            var better=new List<string>();
            if(now.parries>before.parries)better.Add("패링 +"+(now.parries-before.parries));
            if(now.hits<before.hits)better.Add("맞은 횟수 -"+(before.hits-now.hits));
            if(now.clear&&!before.clear)better.Add("처음으로 넘었다");
            else if(now.clear&&before.clear&&now.seconds<before.seconds-1)better.Add(UiKit.Clock(before.seconds-now.seconds)+" 더 빨리");
            return better.Count==0?"":"지난번보다   "+string.Join("   ·   ",better.ToArray());
        }

        void RefreshResult(float dt)
        {
            if(game.result==null)return;
            resultClock+=dt;
            int shown=Mathf.RoundToInt(game.result.gold*UiKit.EaseOut((resultClock-.3f)/.9f));
            Set(resGold,"+"+UiKit.Gold(shown));
        }

        void EnterEnding()
        {
            bool abyss=game.bossIndex==4;
            endingOver.text=abyss?"숨겨진 끝":"탑의 정상";
            endingText.text=abyss?"남겨진 맹세":"신이 무너진 자리";
            endingBody.text=abyss?"당신보다 먼저 이 길을 걸었던 자는\n마침내 검을 내려놓았다.\n\n탑의 끝에는 왕좌 대신, 다음 날의 빛이 있었다."
                                 :"당신을 가로막던 힘이, 당신의 힘이 되었다.\n탑 위의 하늘이 처음으로 열렸다.\n\n그러나 심연에는 아직 이름 없는 맹세가 남아 있다.";
            endingNote.text=abyss?"다섯 개의 문을 모두 넘었다.":"숨겨진 문, 카로스가 열렸다.";
        }

        static string LastRunLine(SaveData save)
        {
            if(save.history==null||save.history.Count==0||save.history[0]==null)return "첫 번째 문이 열려 있다.";
            var h=save.history[0];var b=Array.Find(Catalog.Bosses,x=>x.id==h.boss);string n=b==null?h.boss:b.name;
            return "지난 도전  ·  "+(h.clear?UiKit.Josa(n,"을","를")+" 쓰러뜨렸다":n+" 앞에서 쓰러졌다")+"  ("+UiKit.Clock(h.seconds)+")";
        }

        // ═══════════════════════════ 버튼 이벤트 (씬에 이름으로 연결됨) ═══════════════════════════

        public void EnterLobby(){game.ReturnLobby();}
        public void SelectBoss(int index){if(index<=game.save.unlockedBoss)game.bossIndex=index;}
        public void EnterBattle(){game.StartBattle(game.bossIndex);}
        public void OpenCards(){game.OpenCards();}
        public void ChooseCard(int index)
        {
            if(game.save.cardOffer.Length!=3)return;
            bool before=game.save.cardChosen;
            game.ChooseCard(game.save.cardOffer[index]);
            if(!before&&game.save.cardChosen){chosen=index;outro=0;game.PlaySound("heal");}   // 결제는 즉시, 연출은 그 뒤에
        }
        public void CloseCards(){game.showCards=false;}
        public void Retry(){game.StartBattle(game.bossIndex);}
        public void OpenSettings(){game.showSettings=true;}
        public void OpenHelp(){game.showHelp=true;}
        public void OpenHistory(){game.showHistory=true;}
        public void Resume(){game.paused=false;}
        public void Abandon(){game.Abandon();}
        public void ShowEnding(){game.mode=ScreenMode.Ending;}
        public void CycleUltimate(){game.CycleUltimate();}

        // ═══════════════════════════ 작은 도우미 ═══════════════════════════

        static CanvasGroup Group(GameObject page){var g=page.GetComponent<CanvasGroup>();return g?g:page.AddComponent<CanvasGroup>();}
        static T Grab<T>(GameObject page,string name) where T:Component{var t=page?page.transform.Find(name):null;return t?t.GetComponent<T>():null;}

        static void Small(GameObject page,string name,float x,float y,float w,float h)
        {
            var b=Grab<Button>(page,name);if(!b)return;
            b.transform.SetAsLastSibling();UiKit.Place(b,x,y,w,h);UiKit.Skin(b,UiKit.Button);UiKit.Caption(b,name,UiKit.Body,Palette.White);
        }

        static Button MakeButton(Transform parent,string name,float x,float y,float w,float h,Sprite look,string text,int size,Color color,UnityEngine.Events.UnityAction action)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));obj.transform.SetParent(parent,false);
            var b=obj.GetComponent<Button>();UiKit.Place(b,x,y,w,h);UiKit.Skin(b,look);UiKit.Caption(b,text,size,color);
            if(action!=null)b.onClick.AddListener(action);
            return b;
        }

        static bool Cleared(SaveData save,int i)=>save.cleared!=null&&i>=0&&i<save.cleared.Length&&save.cleared[i];
        static string Num(float v)=>v.ToString(Mathf.Approximately(v,Mathf.Round(v))?"0":"0.0",CultureInfo.InvariantCulture);
        static Color Alpha(Color c,float a){c.a=a;return c;}
        static void Set(Text t,string s){if(t&&t.text!=s)t.text=s;}
        static void Tint(Text t,Color c){if(t&&t.color!=c)t.color=c;}
        static void Swap(Image image,Sprite sprite){if(image&&image.sprite!=sprite)image.sprite=sprite;}
    }
}
#endif
