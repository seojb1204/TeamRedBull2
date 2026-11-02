using System;
using UnityEngine;

namespace ReversalTower
{
    // Opt-in integration check in an isolated player save. Never enabled in normal play.
    public sealed partial class TowerGame
    {
        int lobbySmokeStage,lobbySmokeChecks;
        float lobbySmokeStageAt;
        float lobbySmokeCapturedAt=-1;
        bool lobbySmokeFinished;
        void LobbyCheck(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);lobbySmokeChecks++;Debug.Log("LOBBY_CHECK_PASS "+message);}
        void TickLobbySmoke()
        {
            try
            {
                if(captureDone&&lobbySmokeCapturedAt<0&&System.IO.File.Exists(AutoRunArgs.CapturePath))lobbySmokeCapturedAt=autoElapsed;
                if(lobbySmokeStage==0)
                {
                    LobbyCheck(SaveStore.IsIsolated,"isolated save active");
                    LobbyCheck(mode==ScreenMode.Title,"player starts at title");
                    mode=ScreenMode.Lobby;save.gold=1000;save.tutorialSeen=true;
                    save.cardOffer=new[]{"blade","vitality","remedy"};save.cardChosen=false;
                    OpenCards();LobbyCheck(showCards&&save.cardOffer.Length==3,"lobby opens three-card selection");
                    lobbySmokeStage=1;
                }
                else if(lobbySmokeStage==1&&autoElapsed>6&&
                    (string.IsNullOrEmpty(AutoRunArgs.CapturePath)||(lobbySmokeCapturedAt>=0&&autoElapsed-lobbySmokeCapturedAt>1)))
                {
                    LobbyCheck(ChooseCard("blade"),"choose first card");
                    LobbyCheck(!showCards&&save.gold==920&&LobbyCards.Count(save,"blade")==1,"selection commits once and closes");
                    var loaded=SaveStore.Load();
                    LobbyCheck(loaded.cardChosen&&LobbyCards.Count(loaded,"blade")==1&&loaded.gold==920,"reload preserves permanent selection");
                    save=loaded;OpenCards();LobbyCheck(!showCards,"second selection blocked");
                    StartBattle(0);LobbyCheck(mode==ScreenMode.Battle&&!showCards,"battle has no card overlay");
                    lobbySmokeStageAt=autoElapsed;lobbySmokeStage=2;
                }
                else if(lobbySmokeStage==2&&autoElapsed-lobbySmokeStageAt>1)
                {
                    EndBattle(false);int settled=save.gold;
                    EndBattle(false);LobbyCheck(save.gold==settled,"settlement cannot repeat");
                    ReturnLobby();LobbyCheck(!save.cardChosen&&LobbyCards.Count(save,"blade")==1,"death returns to lobby and keeps effect");
                    save.cardOffer=new[]{"blade","vitality","remedy"};OpenCards();
                    LobbyCheck(ChooseCard("vitality"),"next lobby permits new choice");
                    save=SaveStore.Load();LobbyCheck(LobbyCards.Health(save)==110&&LobbyCards.Count(save,"blade")==1,"multiple permanent choices coexist after reload");
                    StartBattle(0);StartBattle(0);LobbyCheck(maxHp==110,"battle restart never reapplies stacks");
                    EndBattle(false);ReturnLobby();StartBattle(0);
                    LobbyCheck(mode==ScreenMode.Battle&&!showCards,"entry without upgrade remains available");
                    EndBattle(false);ReturnLobby();lobbySmokeFinished=true;
                    autoExitReason="LOBBY_SMOKE_PASSED";AutoWriteReport();AutoQuit();
                }
            }
            catch(Exception e)
            {
                lobbySmokeFinished=true;autoExitReason="LOBBY_SMOKE_FAILED: "+e.Message;
                Debug.LogError(autoExitReason);AutoWriteReport();Application.Quit(1);
            }
        }
    }
}
