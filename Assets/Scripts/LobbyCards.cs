using System;
using System.Collections.Generic;

namespace ReversalTower
{
    [Serializable] public class CardStack { public string id; public int count; }
    public sealed class UpgradeCard
    {
        public readonly string id,name,description,glyph;
        // 카드 화면용: amount 크게 보이는 수치, stat 무엇이 오르는지, detail 덧붙일 한 줄(없으면 빈 문자열)
        public readonly string amount,stat,detail;
        public UpgradeCard(string id,string name,string description,string glyph,string amount="",string stat="",string detail="")
        {this.id=id;this.name=name;this.description=description;this.glyph=glyph;this.amount=amount;this.stat=stat;this.detail=detail;}
    }
    // Provisional tuning lives here. No timing windows or invulnerability are changed.
    public static class LobbyCards
    {
        public static readonly UpgradeCard[] All={
            new UpgradeCard("blade","검의 단련","기본 공격력 +1\n스킬과 궁극기의 기준 공격력도 증가","검","+1","공격력","스킬과 궁극기도 함께 세진다"),
            new UpgradeCard("vitality","생명의 인장","최대 체력 +10","심","+10","최대 체력"),
            new UpgradeCard("remedy","회복의 인장","회복약 회복량 +5","약","+5","회복약 회복량","한 병에 더 많이 회복한다"),
            new UpgradeCard("rush","돌진의 인장","Q 돌진 베기 피해 +5%p","Q","+5%","돌진 베기 피해"),
            new UpgradeCard("whirl","회전의 인장","W 회전 베기 피해 +5%p","W","+5%","회전 베기 피해"),
            new UpgradeCard("execute","처형의 인장","E 처형 베기 피해 +5%p","E","+5%","처형 베기 피해")
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
        // 강화 카드를 산 뒤 알림 (카드 id, 비용, 몇 번째 강화인지). CloudSync가 DB로 보냄.
        public static event Action<string,int,int> Purchased;
        public static bool Purchase(SaveData data,string id,out string error)
        {
            error=null;Normalize(data);
            if(data.cardChosen){error="이번 인장은 이미 골랐습니다. 전투가 끝나면 다시 고를 수 있습니다.";return false;}
            if(Array.IndexOf(data.cardOffer,id)<0||Find(id)==null){error="지금 놓인 인장이 아닙니다.";return false;}
            if(Count(data,id)>=10000){error="이 인장은 더 쌓을 수 없습니다.";return false;}
            int cost=Cost(data);if(data.gold<cost){error="골드가 모자랍니다.";return false;}
            // Commit a detached copy first: failed disk writes never consume live Gold or grant an effect.
            var candidate=UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(data));
            var stack=candidate.cards.Find(c=>c.id==id);
            if(stack==null){stack=new CardStack{id=id};candidate.cards.Add(stack);}
            stack.count++;candidate.cardPurchases++;candidate.gold-=cost;candidate.cardChosen=true;candidate.version=2;
            if(!SaveStore.Save(candidate)){error="저장하지 못해 인장을 취소했습니다. "+SaveStore.LastError;return false;}
            data.version=candidate.version;data.cards=candidate.cards;data.gold=candidate.gold;
            data.cardPurchases=candidate.cardPurchases;data.cardChosen=true;
            try{Purchased?.Invoke(id,cost,data.cardPurchases-1);}catch(Exception){}
            return true;
        }
    }
    public sealed partial class TowerGame
    {
        public bool showCards;
        public void OpenCards()
        {
            if(mode!=ScreenMode.Lobby)return;
            if(save.cardChosen){Say("이번 인장은 이미 골랐다. 전투가 끝나면 다시 고를 수 있다.");return;}
            string[] previous=save.cardOffer;
            LobbyCards.EnsureOffer(save);
            if(!SaveStore.Save(save)){save.cardOffer=previous;Say("저장하지 못해 인장을 열 수 없습니다.");return;}
            showCards=true;
        }
        public bool ChooseCard(string id)
        {
            if(mode!=ScreenMode.Lobby||!showCards)return false;
            string error;if(!LobbyCards.Purchase(save,id,out error)){Say(error,5);return false;}
            var card=LobbyCards.Find(id);showCards=false;Say(card.stat+" "+card.amount+"  ·  "+card.name,3);return true;
        }
    }
}
