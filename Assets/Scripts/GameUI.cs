using UnityEngine;
using System;

namespace ReversalTower
{
    public sealed partial class TowerGame
    {
        Font font;GUIStyle label,button;Texture2D white;Vector2 historyScroll;
        Color muted=new Color(.55f,.64f,.69f),panel=new Color(.035f,.055f,.08f,.94f);
        // 게임 화면이 uGUI(NativeGameUI)일 때 남은 IMGUI 창(도움말·설정·기록)도 같은 도트 폰트·테두리로 그린다 (UiKit.cs)
        bool pixelUi;
        void InitGui()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","맑은 고딕","Arial"},22);
            white=Texture2D.whiteTexture;
#if !HEADLESS && !DESKTOP_PREVIEW
            if(nativeUI){UiKit.Init();font=UiKit.Regular;pixelUi=true;}
#endif
        }
        int FontSize(int size)
        {
#if !HEADLESS && !DESKTOP_PREVIEW
            if(pixelUi)return UiKit.Snap(size);
#endif
            return size;
        }
        void EnsureStyles()
        {
            if(label!=null)return;
            label=new GUIStyle(GUI.skin.label){font=font,richText=false,wordWrap=true};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=FontSize(19),alignment=TextAnchor.MiddleCenter,richText=false};
            button.normal.background=null;button.hover.background=null;button.active.background=null;button.focused.background=null;
            button.normal.textColor=Palette.White;button.hover.textColor=Palette.Gold;button.active.textColor=Palette.Ink;button.focused.textColor=Palette.Gold;
        }
        void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,white);GUI.color=old;}
        void Text(float x,float y,float w,float h,string text,int size=20,Color? color=null,TextAnchor align=TextAnchor.UpperLeft)
        {label.fontSize=FontSize(size);label.normal.textColor=color??Palette.White;label.alignment=align;GUI.Label(new Rect(x,y,w,h),text,label);}
        void Rule(float x,float y,float w,Color? c=null)=>Fill(new Rect(x,y,w,1),c??Palette.Alpha(Palette.Gold,.25f));
        bool Button(float x,float y,float w,float h,string text,bool primary=false,bool enabled=true)
        {
            Rect r=new Rect(x,y,w,h);bool hover=r.Contains(Event.current.mousePosition);
#if !HEADLESS && !DESKTOP_PREVIEW
            if(pixelUi)
            {
                if(Event.current.type==EventType.Repaint)
                    UiKit.Imgui(!enabled?"disabled":primary?(hover?"primaryHover":"primary"):(hover?"buttonHover":"button")).Draw(r,GUIContent.none,false,false,false,false);
            }
            else
#endif
            {
            Fill(r,primary?Palette.Alpha(Palette.Gold,enabled?(hover?1:.87f):.15f):hover&&enabled?new Color(.14f,.21f,.25f,.96f):new Color(.065f,.10f,.14f,.95f));
            Fill(new Rect(x,y,primary?3:1,h),Palette.Alpha(Palette.Gold,enabled?.75f:.15f));
            }
            var old=GUI.enabled;GUI.enabled=old&&enabled;
            button.normal.textColor=primary?Palette.Ink:enabled?Palette.White:muted;button.hover.textColor=primary?Palette.Ink:Palette.Gold;
            bool clicked=GUI.Button(r,text,button);GUI.enabled=old;if(clicked)sound.Play("confirm");return clicked;
        }
        void OnGUI()
        {
            EnsureStyles();float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1600*scale)/2,(Screen.height-900*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
#if !HEADLESS && !DESKTOP_PREVIEW
            if(nativeUI)
            {
                if(showHistory)HistoryUI();
                if(showSettings)SettingsUI();
                if(showHelp)HelpUI();
                GUI.matrix=Matrix4x4.identity;
                return;
            }
#endif
            bool previousEnabled=GUI.enabled;
            GUI.enabled=previousEnabled&&!showCards&&!showHelp&&!showSettings&&!showHistory&&!paused;
            if(mode==ScreenMode.Title)TitleUI();else if(mode==ScreenMode.Lobby)LobbyUI();else if(mode==ScreenMode.Battle)BattleUI();else if(mode==ScreenMode.Result)ResultUI();else EndingUI();
            GUI.enabled=previousEnabled;
            if(showCards&&mode==ScreenMode.Lobby)CardsUI();
            if(noticeTime>0&&mode==ScreenMode.Lobby){Fill(new Rect(270,153,1060,42),panel);Text(285,162,1030,32,notice,16,Palette.Gold,TextAnchor.UpperCenter);}
            if(noticeTime>0&&mode==ScreenMode.Battle)
            {Fill(new Rect(340,187,920,58),new Color(.025f,.045f,.065f,.88f));Text(365,202,870,40,notice,18,Palette.Gold,TextAnchor.UpperCenter);}
            if(flash>0)Fill(new Rect(0,0,1600,900),Palette.Alpha(Palette.Gold,flash*.12f));
            if(phaseFlash>0&&mode==ScreenMode.Battle)
            {Fill(new Rect(0,325,1600,110),new Color(.025f,.035f,.05f,phaseFlash*.85f));Text(0,352,1600,80,thirdPhase?"마지막 잔상":phaseTwo?"두 번째 맹세":"",40,Palette.Gold,TextAnchor.UpperCenter);}
            if(paused&&mode==ScreenMode.Battle)PauseUI();
            if(showHistory)HistoryUI();if(showSettings)SettingsUI();if(showHelp)HelpUI();
            if(!string.IsNullOrEmpty(SaveStore.LastError))Text(20,875,1560,24,SaveStore.LastError,13,Palette.Red);
            GUI.matrix=Matrix4x4.identity;
        }
        void Footer(string right="F1  조작 안내    ·    F12  스크린샷")
        {Rule(76,844,1448,Palette.Alpha(Palette.Stone,.6f));Text(78,861,600,22,"REVERSAL TOWER  /  로비 강화판 0.4",12,muted);Text(850,858,675,28,right,14,muted,TextAnchor.UpperRight);}
        void TitleUI()
        {
            Fill(new Rect(0,0,760,900),new Color(.02f,.035f,.055f,.8f));
            Text(100,113,600,35,"T O W E R   O F   R E V E R S A L",18,Palette.Gold);
            Rule(100,169,72,Palette.Gold);
            Text(93,198,700,125,"역전의 탑",83);
            Text(102,341,590,100,"무너진 맹세를 넘어,\n신의 기술로 신에게 맞서라.",27,Palette.White);
            Text(103,467,545,65,"패턴을 읽고. 검을 받아내고.\n쓰러뜨린 자의 힘을 계승하세요.",18,muted);
            if(Button(103,577,360,62,save.history.Count>0?"탑으로 돌아가기   →":"탑에 들어가기   →",true)){mode=ScreenMode.Lobby;}
            if(Button(103,655,170,45,"조작 안내"))showHelp=true;
            if(Button(287,655,176,45,"설정"))showSettings=true;
            Text(103,730,530,32,"검과 패링으로 오르는 다섯 개의 문",16,muted);
            Text(1040,168,450,40,"01 — THE BROKEN OATH",15,Palette.Gold,TextAnchor.UpperRight);
            Footer("ENTER  입장    ·    A / D  이동    ·    J  공격    ·    K  패링");
        }
        void LobbyUI()
        {
            Text(78,52,600,44,"맹세의 회랑",35);Text(80,105,800,35,"탑은 당신을 기억합니다. 다음 문을 선택하세요.",17,muted);
            Text(1140,57,380,42,save.gold+"  GOLD",27,Palette.Gold,TextAnchor.UpperRight);
            if(Button(1170,111,108,36,"기록"))showHistory=true;if(Button(1290,111,108,36,"설정"))showSettings=true;if(Button(1410,111,108,36,"도움말"))showHelp=true;
            Text(80,176,340,24,"CHAMBERS  /  도전할 보스",14,Palette.Gold);
            for(int i=0;i<5;i++)
            {
                float y=219+i*73;bool locked=i>save.unlockedBoss,selected=i==bossIndex;
                Fill(new Rect(78,y,390,61),selected?new Color(.15f,.20f,.23f,.96f):panel);
                if(selected)Fill(new Rect(78,y,3,61),Palette.Gold);
                Text(95,y+15,45,34,(i+1).ToString("00"),21,locked?muted:Palette.Gold);
                Text(150,y+8,200,34,Catalog.Bosses[i].name,23,locked?muted:Palette.White);
                Text(150,y+39,260,22,locked?"이전 보스를 처치하면 개방":save.cleared[i]?"계승 완료 · 다시 도전 가능":i==4?"히든 보스 · 심연의 문":"미완의 맹세",12,muted);
                if(!locked&&GUI.Button(new Rect(78,y,390,61),GUIContent.none,GUIStyle.none))bossIndex=i;
            }
            var b=Catalog.Bosses[bossIndex];
            Fill(new Rect(503,219,420,351),panel);Text(528,240,375,28,bossIndex==4?"HIDDEN CHAMBER":"CHAMBER 0"+(bossIndex+1),14,Palette.Gold);
            Text(524,283,385,55,b.name,42);Text(528,350,370,32,b.title,18,Palette.White);
            Rule(528,401,365);Text(528,423,365,98,b.lesson,18,muted);
            Text(528,533,365,30,"계승 기술  /  "+b.ultimate,16,Palette.Gold);
            if(Button(503,589,420,58,save.cardChosen?"강화 완료 · 보스전 입장   →":"강화 없이 바로 입장   →",true))StartBattle(bossIndex);
            Fill(new Rect(80,677,843,134),panel);Text(100,690,790,28,"영구 강화  /  세 카드 중 하나 선택 · 사망해도 유지",16,Palette.Gold);
            Text(100,727,435,30,"공격력 "+LobbyCards.Attack(save).ToString("0.0")+"  ·  체력 "+LobbyCards.Health(save).ToString("0")+"  ·  회복 "+LobbyCards.Heal(save).ToString("0"),18);
            Text(100,769,450,25,"누적 선택 "+save.cardPurchases+"회  ·  기존 영구 강화 유지",13,muted);
            if(Button(560,732,340,52,save.cardChosen?"이번 로비 강화 완료":"강화 선택  [C]  ·  "+LobbyCards.Cost(save)+" G",false,!save.cardChosen))OpenCards();
            Fill(new Rect(981,677,540,134),panel);Text(1003,692,490,26,"장착 궁극기",15,Palette.Gold);
            Text(1003,730,335,33,save.equippedUltimate<0?"반역의 불꽃":Catalog.Bosses[save.equippedUltimate].ultimate,23);
            if(Button(1362,730,138,45,"변경  ↻"))CycleUltimate();
            Text(1003,776,490,23,"첫 처치 시 기술 획득 · 전투 중 R로 사용",13,muted);
            Text(1020,222,475,30,bossIndex==4?"BEYOND THE FALLEN GOD":"INHERIT WHAT ONCE BROKE YOU",13,Palette.Alpha(Palette.Gold,.65f),TextAnchor.UpperCenter);
            Footer("C  강화 선택    ·    ENTER  입장    ·    ESC  타이틀");
        }
        void CardsUI()
        {
            Modal(230,205,1140,570,"맹세의 인장 · 하나를 선택하세요");
            Text(270,273,1060,40,"보유 "+save.gold+" G  /  선택 비용 "+LobbyCards.Cost(save)+" G  ·  강화는 영구 누적됩니다.",18,Palette.Gold);
            if(save.cardOffer==null||save.cardOffer.Length!=3){Text(280,355,1000,80,"강화 후보를 불러오지 못했습니다. 닫고 다시 열어주세요.",20);}
            else for(int i=0;i<3;i++)
            {
                var card=LobbyCards.Find(save.cardOffer[i]);if(card==null)continue;float x=280+i*350;
                Fill(new Rect(x,328,330,296),new Color(.075f,.10f,.14f));Rule(x,328,330,Palette.Gold);
                Text(x+20,345,290,50,card.glyph,32,Palette.Gold,TextAnchor.UpperCenter);
                Text(x+20,408,290,40,card.name,23,Palette.White,TextAnchor.UpperCenter);
                Text(x+25,462,280,75,card.description,17,muted,TextAnchor.UpperCenter);
                Text(x+20,550,290,30,"보유 "+LobbyCards.Count(save,card.id)+" → "+(LobbyCards.Count(save,card.id)+1),16,Palette.Teal,TextAnchor.UpperCenter);
                if(Button(x,639,330,51,"이 카드 선택",true,save.gold>=LobbyCards.Cost(save)&&!save.cardChosen))
                {if(ChooseCard(card.id))sound.Play("heal");return;}
            }
            if(Button(650,711,300,42,"선택하지 않고 돌아가기"))showCards=false;
        }
        void Upgrade(bool attack)
        {
            int cost=save.UpgradeCost(attack);if(save.gold<cost||(attack?save.attackLevel:save.healthLevel)>=10)return;
            save.gold-=cost;if(attack)save.attackLevel++;else save.healthLevel++;SaveStore.Save(save);sound.Play("heal");
        }
        public void CycleUltimate()
        {
            int next=save.equippedUltimate;for(int n=0;n<6;n++){next++;if(next>4)next=-1;if(next<0||save.cleared[next]){save.equippedUltimate=next;break;}}SaveStore.Save(save);
        }
        void Bar(float x,float y,float width,float height,float fraction,Color color)
        {Fill(new Rect(x,y,width,height),new Color(.07f,.10f,.14f,.95f));Fill(new Rect(x,y,width*Mathf.Clamp01(fraction),height),color);Fill(new Rect(x,y,width,1),Palette.Alpha(Palette.White,.2f));}
        void BattleUI()
        {
            Fill(new Rect(0,0,1600,164),new Color(.02f,.035f,.05f,.88f));
            Text(66,28,460,31,"제그리스  /  반역자",19,Palette.White);Text(66,70,470,35,Mathf.CeilToInt(playerHp)+" / "+Mathf.CeilToInt(maxHp),17,Palette.Teal,TextAnchor.UpperRight);
            Bar(66,107,470,14,playerHp/maxHp,Palette.Teal);
            Text(66,132,200,26,"회복약  "+potions+" / 3",15,muted);
            Text(271,132,265,26,seal?"회피 봉인  ·  패링 "+sealParries+" / 3":"대시  "+new string('◆',dashes)+new string('◇',2-dashes),15,seal?Palette.Purple:Palette.Gold,TextAnchor.UpperRight);
            Text(610,35,380,35,FormatTime(seconds),25,muted,TextAnchor.UpperCenter);
            Text(610,82,380,30,phaseTwo?(thirdPhase?"PHASE III":"PHASE II"):"PHASE I",13,Palette.Gold,TextAnchor.UpperCenter);
            Text(1041,28,493,35,spec.name,26,Palette.Gold,TextAnchor.UpperRight);
            Text(1041,71,493,30,spec.title,16,muted,TextAnchor.UpperRight);Bar(1041,107,493,14,bossHp/spec.hp,Palette.Gold);
            Text(1041,132,493,27,bossStagger>0?"그로기 · 지금 반격하세요":current==null?"다음 공격을 준비합니다":current.name+(current.parryable?"  ◆ 패링 가능":"  · 회피 / 위치 이동"),15,bossStagger>0?Palette.Teal:muted,TextAnchor.UpperRight);
            if(current!=null&&current.parryable&&patternAge<windup&&windup-patternAge<.3f)
            {Text(600,278,400,52,"◆   패링   K",32,Palette.Gold,TextAnchor.UpperCenter);}
            Fill(new Rect(0,760,1600,140),new Color(.02f,.035f,.05f,.92f));
            Slot(64,"J","검격",0,1);Slot(180,"K","패링",Mathf.Max(0,parryLock),.55f);Slot(296,"Q","돌진 베기",skillQ,6);Slot(412,"W","회전 베기",skillW,10);Slot(528,"E","처형 베기",skillE,15);Slot(644,"F","회복약",potions==0?1:0,1);
            Text(829,777,570,30,save.equippedUltimate<0?"R   반역의 불꽃":"R   "+Catalog.Bosses[save.equippedUltimate].ultimate,19,ult>=100?Palette.Gold:muted);
            Bar(830,824,405,8,ult/100,Palette.Gold);Text(1248,811,90,40,Mathf.FloorToInt(ult)+"%",18,Palette.Gold);
            Text(829,854,700,23,"A D  이동    SPACE  점프    SHIFT  대시    ESC  일시정지",14,muted);
            if(Button(1391,787,143,51,"도움말  F1"))showHelp=true;
        }
        void Slot(float x,string key,string name,float remaining,float max)
        {
            Fill(new Rect(x,779,100,70),new Color(.075f,.11f,.15f));Text(x+11,784,78,30,key,23,remaining>0?muted:Palette.White);
            Text(x+11,820,80,24,remaining>0?remaining.ToString("0.0")+"s":"READY",12,remaining>0?muted:Palette.Teal);
            Bar(x,848,100,2,remaining>0?1-remaining/max:1,remaining>0?Palette.Stone:Palette.Teal);Text(x,861,100,24,name,13,muted,TextAnchor.UpperCenter);
        }
        static string FormatTime(float t)=>((int)t/60).ToString("00")+":"+((int)t%60).ToString("00");
        void ResultUI()
        {
            Fill(new Rect(0,0,1600,900),new Color(.015f,.025f,.04f,.78f));
            Text(0,106,1600,35,result.clear?"O A T H   I N H E R I T E D":"T H E   T O W E R   R E M E M B E R S",16,result.clear?Palette.Gold:muted,TextAnchor.UpperCenter);
            Text(0,166,1600,82,result.clear?"맹세를 계승했습니다":"다시 일어설 시간",52,result.clear?Palette.Gold:Palette.White,TextAnchor.UpperCenter);
            Text(0,262,1600,40,spec.name+(result.clear?"의 문을 넘었습니다.":"에게 쓰러졌습니다. 탑은 다음 도전을 기다립니다."),21,muted,TextAnchor.UpperCenter);
            Rule(395,340,810);
            string[] titles={"전투 시간","패링 성공","받은 공격","획득 보상"};string[] values={FormatTime(result.seconds),result.parries+"회",result.hits+"회",result.gold+" G"};
            for(int i=0;i<4;i++){float x=390+i*210;Text(x,373,200,33,titles[i],16,muted,TextAnchor.UpperCenter);Text(x,418,200,55,values[i],34,i==3?Palette.Gold:Palette.White,TextAnchor.UpperCenter);}
            if(wasFirstClear)Text(390,508,820,52,"새 궁극기 획득  ·  "+spec.ultimate,24,Palette.Gold,TextAnchor.UpperCenter);
            else Text(390,508,820,52,"전투 기록과 진행 상황을 저장했습니다.",19,muted,TextAnchor.UpperCenter);
            if(result.clear&&bossIndex>=3)
            {
                if(Button(475,603,650,62,bossIndex==4?"심연의 끝을 바라보기   →":"창조자의 마지막을 바라보기   →",true))mode=ScreenMode.Ending;
            }
            else if(Button(475,603,650,62,"회랑으로 돌아가기   →",true))ReturnLobby();
            if(!result.clear&&Button(475,685,315,49,"다시 도전"))StartBattle(bossIndex);
            if(!result.clear&&Button(810,685,315,49,"회랑으로"))ReturnLobby();
            Footer("ENTER  회랑으로 돌아가기");
        }
        void EndingUI()
        {
            Fill(new Rect(0,0,1600,900),new Color(.015f,.025f,.04f,.93f));
            Text(0,128,1600,40,bossIndex==4?"THE LAST ECHO":"THE FALLEN CREATOR",17,Palette.Gold,TextAnchor.UpperCenter);
            Text(0,216,1600,75,bossIndex==4?"남겨진 맹세":"신이 무너진 자리",51,Palette.White,TextAnchor.UpperCenter);
            Text(400,343,800,190,bossIndex==4?"당신보다 먼저 이 길을 걸었던 자는\n마침내 검을 내려놓았다.\n\n탑의 끝에는 왕좌 대신, 다음 날의 빛이 있었다.":"당신을 가로막던 힘이, 당신의 힘이 되었다.\n탑 위의 하늘이 처음으로 열렸다.\n\n그러나 심연에는 아직 이름 없는 맹세가 남아 있다.",24,muted,TextAnchor.UpperCenter);
            Text(400,579,800,44,bossIndex==4?"다섯 개의 문을 모두 넘었습니다.":"히든 보스 카로스의 문이 열렸습니다.",20,Palette.Gold,TextAnchor.UpperCenter);
            if(Button(565,680,470,62,"회랑으로 돌아가기   →",true))ReturnLobby();
            Footer("원작 기획  팀 레드불    /    독립 구현 · 절차적 그래픽 · 합성 음악");
        }
        void Modal(float x,float y,float w,float h,string title)
        {
            Fill(new Rect(0,0,1600,900),new Color(0,0,0,.72f));
#if !HEADLESS && !DESKTOP_PREVIEW
            if(pixelUi){if(Event.current.type==EventType.Repaint)UiKit.Imgui("panel").Draw(new Rect(x,y,w,h),GUIContent.none,false,false,false,false);Text(x+32,y+25,w-64,50,title,30);return;}
#endif
            Fill(new Rect(x,y,w,h),panel);Fill(new Rect(x,y,w,2),Palette.Gold);Text(x+32,y+25,w-64,50,title,30);
        }
        void PauseUI()
        {
            Modal(535,215,530,466,"잠시 검을 내려놓고");Text(568,282,460,40,"전투는 멈춰 있습니다.",18,muted);
            if(Button(568,344,464,56,"전투 계속하기",true))paused=false;
            if(Button(568,419,222,48,"조작 안내"))showHelp=true;if(Button(810,419,222,48,"설정"))showSettings=true;
            if(Button(568,490,464,50,"전투 중단 후 회랑으로"))Abandon();
            Text(568,562,464,64,"중단한 전투도 실패 기록과 보상이 남습니다.\nESC로 전투에 복귀할 수 있습니다.",15,muted);
        }
        void HelpUI()
        {
            Modal(260,110,1080,690,"검보다 먼저, 패턴을 읽으세요");
            string[] keys={"A / D · ← / →","SPACE","J / Z · 마우스 왼쪽","K / X · 마우스 오른쪽","SHIFT","Q / W / E","R","F"};
            string[] actions={"이동","점프 · 지면 파동을 뛰어넘습니다","검격","푸른 신호에 맞춰 패링 · 궁극기 +15","무적 대시 · 2스택, 지상에서 스택당 2.5초 충전","돌진 베기 / 회전 베기 / 처형 베기","궁극기 · 게이지 100 소모","회복약 · "+LobbyCards.Heal(save).ToString("0")+" 회복, 한 전투에 3개"};
            for(int i=0;i<keys.Length;i++){float y=202+i*48;Text(298,y,320,32,keys[i],18,Palette.Gold);Text(650,y,640,34,actions[i],18,Palette.White);}
            Rule(298,608,1000);Text(298,632,1000,72,"파랗게 빛나면 받아치고, 붉게 빛나면 피하세요.\n별빛이 번쩍이는 순간이 받아칠 때입니다.",17,muted);
            if(Button(1070,726,230,44,"알겠습니다",true)){showHelp=false;save.tutorialSeen=true;SaveStore.Save(save);}
        }
        void SettingsUI()
        {
            Modal(440,190,720,520,"설정");
            Text(478,280,600,32,"전체 음량",20);save.masterVolume=GUI.HorizontalSlider(new Rect(480,337,600,28),save.masterVolume,0,1);AudioListener.volume=save.masterVolume;
            Text(478,385,600,32,"패턴별 적응 난이도  /  DDA",20);
            if(Button(480,438,600,48,save.dda?"사용 중  ·  클릭하여 고정 난이도로 변경":"고정 난이도  ·  클릭하여 DDA 사용")){save.dda=!save.dda;SaveStore.Save(save);}
            Text(480,505,600,110,"바꾼 설정은 다음 전투부터 적용됩니다. 전투 기록은 이 PC에 저장되고, 인터넷이 연결되면 익명 ID로 팀 서버에도 보내집니다.",16,muted);
            if(Button(480,621,600,48,"저장하고 닫기",true)){SaveStore.Save(save);showSettings=false;}
        }
        void HistoryUI()
        {
            Modal(260,110,1080,690,"탑이 기억하는 전투");
            Text(300,192,1000,35,"최근 30번의 도전",17,muted);
            historyScroll=GUI.BeginScrollView(new Rect(294,245,1012,450),historyScroll,new Rect(0,0,980,Mathf.Max(430,save.history.Count*66)));
            if(save.history.Count==0)Text(15,80,940,80,"첫 번째 문을 열면 여기에 남는다.",22,muted,TextAnchor.UpperCenter);
            for(int i=0;i<save.history.Count;i++)
            {
                var h=save.history[i];float y=i*66;Fill(new Rect(0,y,975,57),new Color(.07f,.10f,.14f));var b=Array.Find(Catalog.Bosses,p=>p.id==h.boss);
                Text(16,y+9,205,35,b==null?h.boss:b.name,20,h.clear?Palette.Gold:Palette.White);Text(240,y+12,160,30,h.clear?"★ 넘음":"쓰러짐",16,h.clear?Palette.Teal:muted);
                Text(420,y+12,470,30,FormatTime(h.seconds)+"   /   패링 "+h.parries+"   /   "+h.gold+" G",16,Palette.White);Text(15,y+37,840,20,h.date,11,muted);   // DDA 적용 여부는 플레이어에게 보이지 않는다 (UI/UX 설계서)
            }
            GUI.EndScrollView();if(Button(1069,727,230,44,"닫기",true))showHistory=false;
        }
    }
}
