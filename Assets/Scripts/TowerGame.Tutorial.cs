using UnityEngine;
using System;
using System.Collections.Generic;
namespace ReversalTower
{
    public sealed partial class TowerGame
    {
        TutorialSession tutorial;
        SaveData tutorialOriginalSave;
        int tutorialOriginalBoss;
        string tutorialSaveError;
        SaveData tutorialCompletionCandidate;
        float tutorialThreatClock,tutorialThreatOrigin;
        long tutorialThreatId;
        bool tutorialThreatStarted,tutorialWasThreatened;
        public bool TutorialThreatVisible=>TutorialActive&&tutorialThreatStarted;
        public float TutorialThreatX=>tutorialThreatOrigin;
        public bool TutorialActive=>tutorial!=null;
        public bool TutorialExplaining=>TutorialActive&&tutorial.FreezeGameplay;
        public TutorialSession Tutorial=>tutorial;
        public TutorialAssessment InitialTutorialAssessment=>save==null?null:save.tutorialAssessment;
        public bool RequiresTutorial=>save!=null&&save.tutorialOnboardingVersion<1;
        public void StartGame()
        { if(RequiresTutorial)StartTutorial(false);else ReturnLobby(); }
        public void StartTutorial(bool replay)
        {
            if(TutorialActive||mode==ScreenMode.Battle)return;
            if(replay&&save.tutorialOnboardingVersion<1)return;
            tutorialOriginalSave=save;tutorialOriginalBoss=bossIndex;tutorialSaveError=null;tutorialCompletionCandidate=null;
            // Practice uses a detached fresh profile. No real progression or equipped inherited ultimate.
            save=new SaveData();tutorial=new TutorialSession(replay);sparkTime=0;echo=null;hitStop=slowMo=0;
            bossIndex=0;spec=new BossSpec("tutorial_dummy","훈련용 허수아비","훈련","공격 예고를 살피세요","",1000,0);
            mode=ScreenMode.Battle;paused=showHelp=showSettings=showHistory=showCards=false;ended=false;
            player=new Vector2(-4,0);boss=new Vector2(3,0);face=1;bossFace=-1;
            maxHp=100;playerHp=100;bossHp=1000;ult=seconds=0;parries=hits=0;
            playerInv=stun=parryLock=dashTime=dashRecharge=actionTime=attackAnim=healing=skillQ=skillW=skillE=pvY=horizontalSpeed=0;
            parryAge=99;dashes=2;potions=3;playerAction="";current=null;barrier=pillarLeft=pillarRight=seal=false;
            hazards.Clear();shots.Clear();minions.Clear();motes.Clear();impacts.Clear();
            tutorialThreatClock=0;tutorialThreatStarted=false;
        }
        void TutorialBeforeUpdate(float dt)
        {
            if(!TutorialActive)return;
            if(tutorial.Finished)
            {
                if(Input.GetKeyDown(KeyCode.Return))FinishTutorial();
                else if(Input.GetKeyDown(KeyCode.Escape))ExitTutorial();
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || EXHIBITION_BUILD
            if(Input.GetKeyDown(KeyCode.F10)){tutorial.Skip();FinishTutorial();return;}
#endif
            if(Input.GetKeyDown(KeyCode.Escape)){paused=!paused;}
            if(paused||showSettings||showHelp)return;
            if(tutorial.Explaining)
            {
                // The same frame's action is retained by TickBattle after this gate opens.
                if(TutorialContinuePressed())
                {
                    var step=tutorial.Step;
                    if(step==TutorialStep.Potion){playerHp=maxHp*.65f;potions=Math.Max(1,potions);}
                    tutorial.Acknowledge();
                }
            }
            if(!tutorial.Explaining)RecordTutorialControlInputs();
            tutorial.Tick(dt,paused);
        }
        void RecordTutorialControlInputs()
        {
            var pressed=new List<TutorialAction>();
            if(Input.GetKeyDown(KeyCode.Q))pressed.Add(TutorialAction.Q);
            if(Input.GetKeyDown(KeyCode.W))pressed.Add(TutorialAction.W);
            if(Input.GetKeyDown(KeyCode.E))pressed.Add(TutorialAction.E);
            if(Input.GetKeyDown(KeyCode.R))pressed.Add(TutorialAction.Ultimate);
            if(Input.GetKeyDown(KeyCode.F))pressed.Add(TutorialAction.Potion);
            if(Input.GetKeyDown(KeyCode.J)||Input.GetKeyDown(KeyCode.Z)||Input.GetMouseButtonDown(0))pressed.Add(TutorialAction.Attack);
            if(Input.GetKeyDown(KeyCode.Space))pressed.Add(TutorialAction.Jump);
            if(Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift))pressed.Add(TutorialAction.Dash);
            if(Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.D)||Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.RightArrow))pressed.Add(TutorialAction.Move);
            tutorial.RecordControlInputs(pressed.ToArray());
        }
        bool TutorialContinuePressed()
        {
            switch(tutorial.Step)
            {
                case TutorialStep.Q:return Input.GetKeyDown(KeyCode.Q);
                case TutorialStep.W:return Input.GetKeyDown(KeyCode.W);
                case TutorialStep.E:return Input.GetKeyDown(KeyCode.E);
                case TutorialStep.Move:return Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.LeftArrow)||Input.GetKey(KeyCode.RightArrow);
                case TutorialStep.Jump:return Input.GetKeyDown(KeyCode.Space);
                case TutorialStep.Attack:return Input.GetKeyDown(KeyCode.J)||Input.GetMouseButtonDown(0);
                case TutorialStep.Dash:return Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift);
                case TutorialStep.Potion:return Input.GetKeyDown(KeyCode.F);
                case TutorialStep.Ultimate:return Input.GetKeyDown(KeyCode.R);
                default:return Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space);
            }
        }
        void TickTutorialOpponent(float dt)
        {
            if(actionTime<=0)tutorial.ActionAnimationFinished();
            if(Math.Abs(horizontalSpeed)>.1f)tutorial.ConfirmAction(TutorialAction.Move,true);
            if(player.y>.1f)tutorial.ConfirmAction(TutorialAction.Jump,true);
            if(dashTime>0)tutorial.ConfirmAction(TutorialAction.Dash,true);
            tutorial.GaugeChanged(ult);
            if(tutorial.FreezeGameplay)return;
            bool blue=tutorial.Step==TutorialStep.Parry||tutorial.Step==TutorialStep.Charge;
            if(!blue&&tutorial.Step!=TutorialStep.Dodge)return;
            tutorialThreatClock+=dt;
            if(!tutorialThreatStarted&&tutorialThreatClock>=1.2f)
            {
                tutorialThreatStarted=true;tutorialThreatId++;tutorialThreatOrigin=player.x;
                tutorialWasThreatened=true;bossState=blue?"파란 공격 · K 패링":"빨간 공격 · SHIFT 회피";
            }
            if(!tutorialThreatStarted||tutorialThreatClock<2.2f)return;
            bool overlap=Math.Abs(player.x-tutorialThreatOrigin)<1.3f&&player.y<1.8f;
            bool parried=blue&&overlap&&parryAge>=.05f&&parryAge<=.2f&&dashTime<=0;
            bool dodged=!overlap||dashTime>0&&playerInv>0;
            bool hit=!parried&&!dodged;
            if(parried){parries++;ult=Mathf.Min(100,ult+15);parryAge=99;ParryFeedback(boss.x);}
            else if(hit){hits++;playerHp=Mathf.Max(1,playerHp-10);hitFlash=.2f;sound.Play("player_hurt");}
            tutorial.ResolveThreat(tutorialThreatId,blue,tutorialWasThreatened,blue?parried:dodged,hit);
            tutorialThreatClock=0;tutorialThreatStarted=false;
        }
        void TutorialActionStarted(string name)
        {
            if(!TutorialActive)return;
            if(name=="R"){tutorial.ConfirmAction(TutorialAction.Ultimate,false);return;}
            TutorialAction a;if(Enum.TryParse<TutorialAction>(name,out a))tutorial.ConfirmAction(a,false);
            else if(name=="J")tutorial.ConfirmAction(TutorialAction.Attack,false);
        }
        void TutorialHealing(float amount){if(TutorialActive)tutorial.ConfirmAction(TutorialAction.Potion,true,amount);}
        public void TutorialEmpoweredHit(bool empowered)
        { if(!TutorialActive)return;tutorial.ConfirmEmpoweredSwordHit(true,empowered);if(tutorial.Finished)FinishTutorial(); }
 #if !HEADLESS
        void DrawTutorialGuide()
        {
            if(!TutorialActive)return;
            string text=tutorial.Finished?"훈련 결과를 아직 저장하지 못했습니다.\n기존 진행도는 유지됩니다. [Enter] 저장 재시도 / [Esc] 나가기\n"+tutorialSaveError:TutorialText();
            if(tutorial.FreezeGameplay)
            {
                Rect hole=new Rect();bool hasHole=false;
#if !HEADLESS && !DESKTOP_PREVIEW
                string key=tutorial.Step==TutorialStep.Parry?"K":tutorial.Step==TutorialStep.Potion?"F":tutorial.Step==TutorialStep.Ultimate?"R":tutorial.Step.ToString();
                if(nativeUI)hasHole=nativeUI.TutorialSlotRect(key,out hole);
#endif
                Color dim=new Color(0,0,0,.68f);
                if(hasHole){Fill(new Rect(0,0,1600,hole.y),dim);Fill(new Rect(0,hole.yMax,1600,900-hole.yMax),dim);Fill(new Rect(0,hole.y,hole.x,hole.height),dim);Fill(new Rect(hole.xMax,hole.y,1600-hole.xMax,hole.height),dim);}
                else Fill(new Rect(0,0,1600,900),dim);
            }
            Fill(new Rect(300,170,1000,180),panel);Text(330,190,940,135,text,24,Palette.White,TextAnchor.MiddleCenter);
            if(paused&&Button(650,390,300,55,"튜토리얼 나가기"))ExitTutorial();
        }
        string TutorialText()
        {
            switch(tutorial.Step)
            {
                case TutorialStep.Introduction:return "역전의 탑에 오신 것을 환영합니다.\n적의 공격을 읽고 검으로 맞서 싸우세요. 전투의 기본 조작을 익혀봅시다.\n[Enter] 계속";
                case TutorialStep.PlayerHp:return "플레이어 HP: 공격을 받으면 감소합니다.\n[Enter] 계속";
                case TutorialStep.EnemyHp:return "적 HP: 상대는 훈련용 허수아비입니다.\n마지막 궁극기 훈련 전까지 쓰러지지 않습니다. [Enter] 계속";
                case TutorialStep.Skills:return "하단 슬롯에서 스킬과 남은 재사용 대기시간을 확인하세요. [Enter] 계속";
                case TutorialStep.UltimateGauge:return "공격과 패링으로 궁극기 게이지를 채웁니다. [Enter] 계속";
                case TutorialStep.Telegraphs:return "파란 예고는 패링, 빨간 예고는 회피하세요. [Enter] 계속";
                case TutorialStep.Q:return "Q — 돌진 베기\n전방으로 짧게 돌진하며 피해를 입힙니다. [Q] 사용";
                case TutorialStep.W:return "W — 회전 베기\n주변을 회전하며 가까운 적에게 피해를 입힙니다. [W] 사용";
                case TutorialStep.E:return "E — 처형 베기\n강력한 공격으로 큰 피해를 입힙니다. [E] 사용";
                case TutorialStep.Move:return "[A / D 또는 방향키] 이동하세요.";
                case TutorialStep.Jump:return "[Space] 점프하세요.";
                case TutorialStep.Attack:return "[J 또는 왼쪽 클릭] 검을 휘두르세요.";
                case TutorialStep.Dash:return "[Shift] 회피 대시를 사용하세요.";
                case TutorialStep.Potion:return "F — 회복약\n잃은 체력을 회복합니다. 횟수가 제한됩니다. [F] 사용";
                case TutorialStep.UltimateExplanation:return "기본 공격·Q/W/E·성공한 패링으로 게이지를 채웁니다.\n100%에서 R 사용. 궁극기 시전 중 받는 피해 50% 감소. [Enter] 계속";
                case TutorialStep.Parry:return "파란 공격은 패링할 수 있습니다.\n공격 적중 직전 K로 받아치세요. 성공하면 게이지도 충전됩니다. [Enter] 시작";
                case TutorialStep.Dodge:return "빨간 공격은 패링할 수 없습니다.\n예고 범위를 확인하고 Shift 대시나 이동으로 피하세요. [Enter] 시작";
                case TutorialStep.Charge:return "공격과 패링으로 궁극기 게이지를 100%까지 충전하세요.\n허수아비에 다가가 J·Q·W·E를 사용할 수 있습니다. [Enter] 시작";
                case TutorialStep.Ultimate:return "궁극기 준비 완료! [R]로 검을 강화한 뒤\n허수아비에 다가가 실제 검 공격을 명중시키세요.";
                default:return "훈련 완료";
            }
        }
 #endif
        public void ExitTutorial()
        {
            if(tutorial==null)return;
            bool replay=tutorial.Replay;RestoreTutorialProfile();mode=replay?ScreenMode.Lobby:ScreenMode.Title;
        }
        void FinishTutorial()
        {
            if(tutorial==null||!tutorial.Finished||tutorialOriginalSave==null)return;
            // Keep the playable original untouched until the completed candidate reaches disk.
            try
            {
                if(tutorialCompletionCandidate==null)
                {
                    tutorialCompletionCandidate=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(tutorialOriginalSave));
                    if(tutorialCompletionCandidate==null)throw new InvalidOperationException("진행도 복제 실패");
                    if(tutorial.Replay)tutorialCompletionCandidate.tutorialReplayAssessment=tutorial.Assessment;
                    else {tutorialCompletionCandidate.tutorialAssessment=tutorial.Assessment;tutorialCompletionCandidate.tutorialOnboardingVersion=1;tutorialCompletionCandidate.tutorialSeen=true;}
                }
                if(!SaveStore.Save(tutorialCompletionCandidate))
                { tutorialSaveError="저장 공간과 파일 접근 권한을 확인한 뒤 다시 시도하세요.";return; }
                var committed=tutorialCompletionCandidate;RestoreTutorialProfile();save=committed;
                // Already committed: avoid ReturnLobby's redundant second save.
                mode=ScreenMode.Lobby;showHelp=showSettings=showHistory=showCards=false;potions=3;
            }
            catch(Exception e){tutorialSaveError="저장 처리 실패. 기존 진행도는 보존되어 있습니다.";Debug.LogWarning("Tutorial completion: "+e.Message);}
        }
        void RestoreTutorialProfile()
        {
            tutorialCompletionCandidate=null;tutorialSaveError=null;
            sparkTime=0;echo=null;actionTime=0;hitStop=slowMo=0;
            save=tutorialOriginalSave;tutorialOriginalSave=null;tutorial=null;bossIndex=tutorialOriginalBoss;spec=Catalog.Bosses[bossIndex];
            current=null;hazards.Clear();shots.Clear();minions.Clear();motes.Clear();impacts.Clear();paused=false;ended=true;
        }
    }
}
