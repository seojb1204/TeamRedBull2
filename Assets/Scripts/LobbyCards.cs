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
        public readonly int limit,unlock,group;
        public UpgradeCard(string id,string name,string description,string glyph,string amount="",string stat="",string detail="",int limit=3,int unlock=0,int group=0)
        {this.id=id;this.name=name;this.description=description;this.glyph=glyph;this.amount=amount;this.stat=stat;this.detail=detail;this.limit=limit;this.unlock=unlock;this.group=group;}
    }
    // Provisional tuning lives here. No timing windows or invulnerability are changed.
    public static class LobbyCards
    {
        public static readonly UpgradeCard[] All={
            new UpgradeCard("blade","검의 단련","기준 공격력 +1 · 기본공격과 모든 스킬에 적용","검","+1","공격력","기본공격과 모든 스킬",5),
            new UpgradeCard("vitality","생명의 인장","최대 체력 +10 · 피해감소율은 그대로","심","+10","최대 체력","피해감소율은 변하지 않는다",5,0,1),
            new UpgradeCard("remedy","회복의 인장","회복약 한 병의 회복량 +5","약","+5","포션 회복량","전투 시작 포션 수는 그대로",3,0,1),
            new UpgradeCard("rush","돌진의 인장","Q 돌진 베기 피해 +8%p","Q","+8%p","Q 피해","기준 피해에 합산",3),
            new UpgradeCard("whirl","회전의 인장","W 회전 베기 피해 +8%p","W","+8%p","W 피해","기준 피해에 합산",3),
            new UpgradeCard("execute","처형의 인장","E 처형 베기 피해 +8%p","E","+8%p","E 피해","기준 피해에 합산",3),
            new UpgradeCard("stride","가벼운 발걸음","일반 이동속도 +4%p · 대시와 무적은 그대로","발","+4%p","이동속도","대시 속도·무적 유지",3,0,2),
            new UpgradeCard("armor","견고한 갑옷","받는 피해 3%p 감소 · 최대 체력은 그대로","갑","−3%p","받는 피해","피격 상한 적용 후 3% 감소",3,2,1),
            new UpgradeCard("flask","예비 물약","전투 시작 회복약 +1개 · 최대 4개","약","+1","포션 개수","전투 시작 때만 적용",1,3,1),
            new UpgradeCard("inherit","계승의 힘","궁극기 공격 피해 +8%p · 회복·경직·무적은 그대로","R","+8%p","궁극기 피해","회복·경직·무적 유지",3,3),
            new UpgradeCard("qtempo","재돌진","Q 기본 쿨타임 −6%p","Q","−6%p","Q 쿨타임","6초 → 5.64초 (1장)",3,1,2),
            new UpgradeCard("wtempo","회전 숙련","W 기본 쿨타임 −6%p","W","−6%p","W 쿨타임","10초 → 9.4초 (1장)",3,1,2),
            new UpgradeCard("etempo","처형 숙련","E 기본 쿨타임 −6%p","E","−6%p","E 쿨타임","15초 → 14.1초 (1장)",3,1,2),
            new UpgradeCard("fervor","전투의 고양","공격 적중 궁 게이지 +10%p · 패링·시간 충전은 그대로","R","+10%p","적중 궁 게이지","패링 +15는 변하지 않는다",2,2,2),
            new UpgradeCard("riposte","반격의 칼날","패링 후 3초 안 다음 기본공격 명중 피해 +25%","검","+25%","패링 후 반격","빗나가면 유지 · 중복 누적 없음",1,1),
            new UpgradeCard("flow","흐름 회수","패링 성공 시 QWE 남은 쿨타임을 기본값의 5% 회복","류","5%","패링 쿨 회복","발동 간격 3초 · 궁극기 제외",1,2,2)
        };
        public static UpgradeCard Find(string id)=>Array.Find(All,c=>c.id==id);
        public static int Count(SaveData data,string id)
        {var s=data.cards==null?null:data.cards.Find(c=>c!=null&&c.id==id);return s==null?0:s.count;}
        public static float Attack(SaveData data)=>10*(1+data.attackLevel*.12f)+Count(data,"blade");
        public static float Health(SaveData data)=>100+data.healthLevel*10+Count(data,"vitality")*10;
        public static float Heal(SaveData data)=>40+Count(data,"remedy")*5;
        public static float SkillMultiplier(SaveData data,string action)
        {string id=action=="Q"?"rush":action=="W"?"whirl":action=="E"?"execute":null;return action=="R"?1+Count(data,"inherit")*.08f:id==null?1:1+Count(data,id)*.08f;}
        public static float Power(SaveData data)
        {
            return (Attack(data)/10-1)+(Health(data)/100-1)+Count(data,"remedy")*.025f+
                (Count(data,"rush")+Count(data,"whirl")+Count(data,"execute"))*.04f+
                Count(data,"inherit")*.04f+Count(data,"stride")*.02f+Count(data,"armor")*.03f+Count(data,"flask")*.15f+
                (Count(data,"qtempo")+Count(data,"wtempo")+Count(data,"etempo"))*.03f+
                Count(data,"fervor")*.025f+Count(data,"riposte")*.05f+Count(data,"flow")*.05f;
        }
        public static int Cost(SaveData data)=>80+Math.Min(5,Math.Max(0,data.cardPurchases))*20;
        // Legacy over-cap stacks are preserved. Limits restrict NEW purchases, never existing effects.
        public static bool Available(SaveData data,UpgradeCard card)
            =>card!=null&&Count(data,card.id)<card.limit&&(card.unlock==0||
              (data.cleared!=null&&data.cleared.Length>=card.unlock&&data.cleared[card.unlock-1]));
        public static float MoveSpeed(SaveData data)=>5.5f*(1+Math.Min(3,Count(data,"stride"))*.04f);
        public static float DamageReduction(SaveData data)=>Math.Min(3,Count(data,"armor"))*.03f;
        public static int Potions(SaveData data)=>3+Math.Min(1,Count(data,"flask"));
        public static float Cooldown(SaveData data,string action)
        {float basis=action=="Q"?6:action=="W"?10:15;string id=action=="Q"?"qtempo":action=="W"?"wtempo":"etempo";return basis*(1-Math.Min(3,Count(data,id))*.06f);}
        public static float HitGaugeMultiplier(SaveData data)=>1+Math.Min(2,Count(data,"fervor"))*.1f;
        public static string Preview(SaveData data,string id)
        {
            int n=Count(data,id);var c=Find(id);if(c==null)return "";
            if(id=="blade")return Attack(data).ToString("0.0")+" → "+(Attack(data)+1).ToString("0.0");
            if(id=="vitality")return Health(data).ToString("0")+" → "+(Health(data)+10).ToString("0");
            if(id=="remedy")return Heal(data).ToString("0")+" → "+(Heal(data)+5).ToString("0");
            if(id=="flask")return Potions(data)+" → "+(Potions(data)+1)+"개";
            if(id=="qtempo"||id=="wtempo"||id=="etempo")
            {string key=id=="qtempo"?"Q":id=="wtempo"?"W":"E";float basis=key=="Q"?6:key=="W"?10:15;return Cooldown(data,key).ToString("0.##")+" → "+(basis*(1-(n+1)*.06f)).ToString("0.##")+"초";}
            if(id=="stride")return (5.5f*(1+n*.04f)).ToString("0.##")+" → "+(5.5f*(1+(n+1)*.04f)).ToString("0.##")+" 이동속도";
            if(id=="armor")return "피해감소 "+(n*3)+"% → "+((n+1)*3)+"%";
            if(id=="rush"||id=="whirl"||id=="execute"||id=="inherit")return "+"+(n*8)+"% → +"+((n+1)*8)+"% 피해";
            if(id=="fervor")return "+"+(n*10)+"% → +"+((n+1)*10)+"% 적중 게이지";
            return "보유 "+n+" → "+(n+1)+" / 최대 "+c.limit;
        }
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
            bool valid=data.cardOffer!=null&&data.cardOffer.Length>0&&data.cardOffer.Length<=3;
            var ids=new HashSet<string>();
            if(valid)foreach(string id in data.cardOffer)if(Find(id)==null||!ids.Add(id))valid=false;
            if(!valid)data.cardOffer=new string[0];
        }
        public static void EnsureOffer(SaveData data)
        {
            Normalize(data);
            bool valid=data.cardOffer.Length>0;
            foreach(string id in data.cardOffer)if(!Available(data,Find(id)))valid=false;
            if(valid)return;
            var pool=new List<UpgradeCard>();foreach(var c in All)if(Available(data,c))pool.Add(c);
            var picked=new List<string>();var rng=new Random();
            // One attack, survival and utility choice when possible, then fill remaining slots.
            for(int group=0;group<3;group++)
            {var candidates=pool.FindAll(c=>c.group==group);if(candidates.Count==0)continue;var c=candidates[rng.Next(candidates.Count)];picked.Add(c.id);pool.Remove(c);}
            while(picked.Count<3&&pool.Count>0){int at=rng.Next(pool.Count);picked.Add(pool[at].id);pool.RemoveAt(at);}
            // Do not make the first position synonymous with attack.
            for(int i=picked.Count-1;i>0;i--){int j=rng.Next(i+1);string t=picked[i];picked[i]=picked[j];picked[j]=t;}
            data.cardOffer=picked.ToArray();
        }
        // 강화 카드를 산 뒤 알림 (카드 id, 비용, 몇 번째 강화인지). CloudSync가 DB로 보냄.
        public static event Action<string,int,int> Purchased;
        public static bool Purchase(SaveData data,string id,out string error)
        {
            error=null;Normalize(data);
            if(data.cardChosen){error="이번 인장은 이미 골랐습니다. 전투가 끝나면 다시 고를 수 있습니다.";return false;}
            if(Array.IndexOf(data.cardOffer,id)<0||Find(id)==null){error="지금 놓인 인장이 아닙니다.";return false;}
            if(!Available(data,Find(id))){error="이 인장은 아직 해금되지 않았거나 최대 강화입니다.";return false;}
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
            if(save.cardOffer.Length==0){Say("지금 강화할 수 있는 인장이 없습니다. 다음 보스를 공략하세요.");return;}
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
