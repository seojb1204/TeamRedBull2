#if !HEADLESS && !DESKTOP_PREVIEW
using UnityEngine;
using UnityEngine.UI;
namespace ReversalTower
{
    // Serialized Canvas hierarchy. Help/history/settings keep legacy overlays in this migration.
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
        void Awake()
        {
            Font font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","맑은 고딕","Arial"},22);
            foreach(Text text in GetComponentsInChildren<Text>(true))text.font=font;
        }
        public void Refresh()
        {
            if(!game||game.save==null)return;
            ScreenMode mode=game.mode;
            titlePage.SetActive(mode==ScreenMode.Title);lobbyPage.SetActive(mode==ScreenMode.Lobby);
            battlePage.SetActive(mode==ScreenMode.Battle);resultPage.SetActive(mode==ScreenMode.Result);
            endingPage.SetActive(mode==ScreenMode.Ending);cardsPage.SetActive(mode==ScreenMode.Lobby&&game.showCards);
            pausePage.SetActive(mode==ScreenMode.Battle&&game.paused);
            inputGroup.interactable=!(game.showHelp||game.showSettings||game.showHistory);
            notice.text=game.noticeTime>0?game.notice:"";
            SaveData save=game.save;
            if(mode==ScreenMode.Lobby)
            {
                lobbyStats.text=save.gold+" GOLD  /  영구 강화 "+save.cardPurchases+"회\n공격 "+LobbyCards.Attack(save).ToString("0.0")+"  체력 "+LobbyCards.Health(save)+"  회복 "+LobbyCards.Heal(save);
                BossSpec boss=Catalog.Bosses[game.bossIndex];
                bossDescription.text=boss.name+"\n\n"+boss.title+"\n\n"+boss.lesson+"\n\n계승 기술: "+boss.ultimate;
                for(int i=0;i<bossButtons.Length;i++)bossButtons[i].interactable=i<=save.unlockedBoss;
                upgradeButton.interactable=!save.cardChosen;
                upgradeLabel.text=save.cardChosen?"이번 로비 강화 완료":"강화 선택 [C]  "+LobbyCards.Cost(save)+" G";
                if(game.showCards&&save.cardOffer.Length==3)for(int i=0;i<3;i++)
                {
                    UpgradeCard card=LobbyCards.Find(save.cardOffer[i]);
                    cardLabels[i].text=card.name+"\n\n"+card.description+"\n\n보유 "+LobbyCards.Count(save,card.id)+" → "+(LobbyCards.Count(save,card.id)+1)+"\n\n"+LobbyCards.Cost(save)+" G";
                    cardButtons[i].interactable=!save.cardChosen&&save.gold>=LobbyCards.Cost(save);
                }
            }
            if(mode==ScreenMode.Battle&&game.spec!=null)
            {
                playerHealth.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,470*Mathf.Clamp01(game.playerHp/game.maxHp));
                bossHealth.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,470*Mathf.Clamp01(game.bossHp/game.spec.hp));
                battleStats.text="HP "+Mathf.CeilToInt(game.playerHp)+" / "+Mathf.CeilToInt(game.maxHp)+"     "+game.spec.name+"     "+(int)game.seconds+"s\n"+
                    (game.current==null?game.bossState:game.current.name+(game.current.parryable?" ◆ 패링 가능":" · 회피"))+"\n"+
                    "Q "+Cooldown(game.skillQ)+"  W "+Cooldown(game.skillW)+"  E "+Cooldown(game.skillE)+"  R "+(int)game.ult+"%  포션 "+game.PotionCount+"  대시 "+game.dashes;
            }
            if(mode==ScreenMode.Result&&game.result!=null)
            {
                HistoryEntry result=game.result;
                resultStats.text=(result.clear?"맹세를 계승했습니다":"다시 일어설 시간")+"\n\n"+game.spec.name+"\n\n전투 "+result.seconds.ToString("0.0")+"초  패링 "+result.parries+"회  피격 "+result.hits+"회\n\n획득 "+result.gold+" GOLD\n\n강화는 사망해도 유지됩니다.";
                retryButton.gameObject.SetActive(!result.clear);endingButton.gameObject.SetActive(result.clear&&game.bossIndex>=3);
            }
            endingText.text=game.bossIndex==4?"남겨진 맹세\n\n다섯 개의 문을 모두 넘었습니다.":"신이 무너진 자리\n\n심연에 남은 맹세를 찾아가세요.";
        }
        static string Cooldown(float value)=>value>0?value.ToString("0.0"):"준비";
        public void EnterLobby(){game.ReturnLobby();}
        public void SelectBoss(int index){if(index<=game.save.unlockedBoss)game.bossIndex=index;}
        public void EnterBattle(){game.StartBattle(game.bossIndex);}
        public void OpenCards(){game.OpenCards();}
        public void ChooseCard(int index){if(game.save.cardOffer.Length==3)game.ChooseCard(game.save.cardOffer[index]);}
        public void CloseCards(){game.showCards=false;}
        public void Retry(){game.StartBattle(game.bossIndex);}
        public void OpenSettings(){game.showSettings=true;}
        public void OpenHelp(){game.showHelp=true;}
        public void OpenHistory(){game.showHistory=true;}
        public void Resume(){game.paused=false;}
        public void Abandon(){game.Abandon();}
        public void ShowEnding(){game.mode=ScreenMode.Ending;}
    }
}
#endif
