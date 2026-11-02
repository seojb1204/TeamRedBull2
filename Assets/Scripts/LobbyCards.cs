using System;
using System.Collections.Generic;

namespace ReversalTower
{
    [Serializable] public class CardStack { public string id; public int count; }
    public sealed class UpgradeCard
    {
        public readonly string id,name,description,glyph;
        public UpgradeCard(string id,string name,string description,string glyph)
        {this.id=id;this.name=name;this.description=description;this.glyph=glyph;}
    }
    // Provisional tuning lives here. No timing windows or invulnerability are changed.
    public static class LobbyCards
    {
        public static readonly UpgradeCard[] All={
            new UpgradeCard("blade","검의 단련","기본 공격력 +1\n스킬과 궁극기의 기준 공격력도 증가","검"),
            new UpgradeCard("vitality","생명의 인장","최대 체력 +10","심"),
            new UpgradeCard("remedy","회복의 인장","회복약 회복량 +5","약"),
            new UpgradeCard("rush","돌진의 인장","Q 돌진 베기 피해 +5%p","Q"),
            new UpgradeCard("whirl","회전의 인장","W 회전 베기 피해 +5%p","W"),
            new UpgradeCard("execute","처형의 인장","E 처형 베기 피해 +5%p","E")
        };
        public static UpgradeCard Find(string id)=>Array.Find(All,c=>c.id==id);
        public static int Count(SaveData data,string id)
        {var s=data.cards==null?null:data.cards.Find(c=>c!=null&&c.id==id);return s==null?0:s.count;}
        public static float Attack(SaveData data)=>10*(1+data.attackLevel*.12f)+Count(data,"blade");
        public static float Health(SaveData data)=>100+data.healthLevel*10+Count(data,"vitality")*10;
        public static float Heal(SaveData data)=>40+Count(data,"remedy")*5;
        public static float SkillMultiplier(SaveData data,string action)
        {string id=action=="Q"?"rush":action=="W"?"whirl":action=="E"?"execute":null;return id==null?1:1+Count(data,id)*.05f;}
        public static float Power(SaveData data)
        {
            return (Attack(data)/10-1)+(Health(data)/100-1)+Count(data,"remedy")*.025f+
                (Count(data,"rush")+Count(data,"whirl")+Count(data,"execute"))*.025f;
        }
        public static int Cost(SaveData data)=>80+Math.Min(10000,data.cardPurchases)*25;
        public static void Normalize(SaveData data)
        {
            var clean=new List<CardStack>();
            if(data.cards!=null)foreach(var s in data.cards)
            {
                if(s==null||Find(s.id)==null||s.count<=0)continue;
                var target=clean.Find(c=>c.id==s.id);
                if(target==null){target=new CardStack{id=s.id};clean.Add(target);}
                target.count=(int)Math.Min(10000L,(long)target.count+s.count);
            }
            data.cards=clean;data.cardPurchases=Math.Max(0,Math.Min(10000,data.cardPurchases));
            bool valid=data.cardOffer!=null&&data.cardOffer.Length==3;
            var ids=new HashSet<string>();
            if(valid)foreach(string id in data.cardOffer)if(Find(id)==null||!ids.Add(id))valid=false;
            if(!valid)data.cardOffer=new string[0];
        }
        public static void EnsureOffer(SaveData data)
        {
            Normalize(data);if(data.cardOffer.Length==3)return;
            var pool=new List<UpgradeCard>(All);var rng=new Random();data.cardOffer=new string[3];
            for(int i=0;i<3;i++){int at=rng.Next(pool.Count);data.cardOffer[i]=pool[at].id;pool.RemoveAt(at);}
        }
        public static bool Purchase(SaveData data,string id,out string error)
        {
            error=null;Normalize(data);
            if(data.cardChosen){error="이번 로비에서는 이미 강화했습니다. 전투 정산 후 다시 선택할 수 있습니다.";return false;}
            if(Array.IndexOf(data.cardOffer,id)<0||Find(id)==null){error="현재 제시된 카드가 아닙니다.";return false;}
            if(Count(data,id)>=10000){error="이 강화는 저장 가능한 최대 누적 횟수에 도달했습니다.";return false;}
            int cost=Cost(data);if(data.gold<cost){error="Gold가 부족합니다. 강화하지 않고 입장할 수 있습니다.";return false;}
            // Commit a detached copy first: failed disk writes never consume live Gold or grant an effect.
            var candidate=UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(data));
            var stack=candidate.cards.Find(c=>c.id==id);
            if(stack==null){stack=new CardStack{id=id};candidate.cards.Add(stack);}
            stack.count++;candidate.cardPurchases++;candidate.gold-=cost;candidate.cardChosen=true;candidate.version=2;
            if(!SaveStore.Save(candidate)){error="저장하지 못해 강화를 취소했습니다. "+SaveStore.LastError;return false;}
            data.version=candidate.version;data.cards=candidate.cards;data.gold=candidate.gold;
            data.cardPurchases=candidate.cardPurchases;data.cardChosen=true;return true;
        }
    }
    public sealed partial class TowerGame
    {
        public bool showCards;
        public void OpenCards()
        {
            if(mode!=ScreenMode.Lobby)return;
            if(save.cardChosen){Say("이번 로비의 강화는 완료했습니다. 다음 전투를 진행하세요.");return;}
            string[] previous=save.cardOffer;
            LobbyCards.EnsureOffer(save);
            if(!SaveStore.Save(save)){save.cardOffer=previous;Say("강화 후보를 저장하지 못했습니다.");return;}
            showCards=true;
        }
        public bool ChooseCard(string id)
        {
            if(mode!=ScreenMode.Lobby||!showCards)return false;
            string error;if(!LobbyCards.Purchase(save,id,out error)){Say(error,5);return false;}
            showCards=false;Say(LobbyCards.Find(id).name+" 강화 완료 · 영구 저장했습니다.",4);return true;
        }
    }
}
