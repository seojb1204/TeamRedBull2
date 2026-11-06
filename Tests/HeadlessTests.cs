using System;
using System.IO;
using System.Reflection;
using ReversalTower;
using UnityEngine;

public static class HeadlessTests
{
    static int passed;static TowerGame game;
    static void Check(bool value,string title){if(!value)throw new Exception(title);passed++;Console.WriteLine("PASS "+title);}
    static void Set(string name,object value)=>typeof(TowerGame).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(game,value);
    static object Call(string name,params object[] args)=>typeof(TowerGame).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,args);
    static void Tick(float dt=.016f){Call("TickBattle",dt);Input.Clear();}
    static void Start(int index=0)
    {
        game=new TowerGame();game.save=new SaveData{unlockedBoss=4};Set("sound",new Soundscape());game.StartBattle(index);
    }
    public static int Main()
    {
        try
        {
            Check(Catalog.Bosses.Length==5,"Five distinct boss definitions");
            foreach(var b in Catalog.Bosses){var ids=new System.Collections.Generic.HashSet<string>();int parries=0;foreach(var p in b.patterns){Check(ids.Add(p.id),b.id+" unique "+p.id);if(p.parryable)parries++;}Check(parries>=2,b.id+" multiple parry opportunities");}
            var model=new BanditModel();var x=model.Context(.2f,.4f,0,.4f,2);
            Check(model.Choose(x,0,0,.2f,0)==0,"Neutral first encounter");Check(model.Choose(x,0,5,.2f,3)==-1,"Three-loss protection");Check(model.Choose(x,-2,5,.2f,3)==-2,"Easy lower bound");
            Check(model.Choose(x,0,1,.2f,0)==-1,"Cold-start support for struggling players");Check(model.Choose(x,0,1,.95f,0)==1,"Cold-start progression for skilled players");
            for(int i=0;i<1000;i++){int l=model.Choose(x,0,10,.7f,0);if(Math.Abs(l)>1)throw new Exception("Invalid difficulty jump");model.Update(l,x,.8);if(double.IsNaN(model.Value(l+2,x)))throw new Exception("Invalid model");}
            Check(true,"1000 finite LinUCB updates with one-step bounds");
            var restored=JsonUtility.FromJson<BanditModel>(JsonUtility.ToJson(model));Check(Math.Abs(restored.Value(2,x)-model.Value(2,x))<1e-8,"Learned matrices survive serialization");
            // 안전장치 규칙은 초반 3판 뒤에도 적용된다 (예전에는 5판부터 LinUCB만 남아 레벨이 굳었다)
            Check(model.Choose(x,0,10,.95f,0)==1,"Skilled players keep progressing after the opening battles");
            Check(model.Choose(x,0,10,.2f,0)==-1,"Struggling players stay supported after the opening battles");
            Check(model.Choose(x,2,10,.95f,0)==2,"Hard upper bound");
            {
                // 처음 6판은 전부 맞고 지다가, 그 뒤로 전부 막아 내며 이기는 플레이어: 실력이 늘면 다시 어려워져야 한다
                var learnSave=new SaveData();var arakus=Catalog.Bosses[0];var session=new DdaBattleSession();int low=0;
                for(int battle=0;battle<18;battle++)
                {
                    session.Prepare(learnSave,arakus,false);bool good=battle>=6;
                    foreach(var st in session.Stats){if(st.id=="step"||st.id=="rage")continue;st.opportunities=5;if(good)st.parries=5;else st.hits=5;}
                    if(battle==5)low=session.Stats.Find(st=>st.id=="cleave").level;
                    session.Complete(learnSave,arakus,good);
                }
                session.Prepare(learnSave,arakus,false);int high=session.Stats.Find(st=>st.id=="cleave").level;
                Check(low<=-1&&high>=1,"DDA raises difficulty again when a struggling player improves ("+low+" -> "+high+")");
            }
            {
                // DDA 패널의 '다음 판' 미리보기: 몇 번 불러도 같은 답이고 학습하지 않는다
                var previewSave=new SaveData();var arakus=Catalog.Bosses[0];double[] context;string reason;
                int first=DdaBattleSession.Decide(previewSave,arakus,arakus.patterns[0],true,out context,out reason);
                int again=DdaBattleSession.Decide(previewSave,arakus,arakus.patterns[0],true,out context,out reason);
                Check(first==again&&previewSave.Learning("arakus").battles==0&&reason=="첫 만남 · 기본 난이도","DDA next-battle preview does not learn");
                var learned=previewSave.Learning("arakus");learned.battles=5;var memory=learned.Pattern("cleave");memory.level=2;memory.observations=3;memory.success=1;
                int capped=DdaBattleSession.Decide(previewSave,arakus,arakus.patterns[0],true,out context,out reason);
                Check(capped==2&&reason.Contains("이미 가장 어려움"),"DDA reason says when the hardest level is already reached");
            }
            Start();float hp=game.playerHp;game.parryAge=.1f;game.PlayerHit(20,true,"cleave",5);Check(game.playerHp==hp&&game.parries==1&&game.ult==15,"Parry prevents damage and grants 15 ultimate");
            game.parryAge=.25f;game.playerInv=0;game.PlayerHit(20,true,"cleave",5);Check(game.playerHp==hp-20,"Late parry receives damage");
            game.PlayerHit(20,true,"cleave",5);Check(game.playerHp==hp-20&&game.Stat("cleave").ignored==1,"Post-hit invulnerability prevents repeated damage");
            game.playerInv=0;game.parryAge=.1f;game.PlayerHit(20,false,"kick",5);Check(game.playerHp==hp-40,"Unparryable attack bypasses parry");
            game.playerInv=.18f;game.dashTime=.2f;int dodge=game.Stat("cleave").dodges;game.PlayerHit(20,true,"cleave",5);Check(game.Stat("cleave").dodges==dodge+1,"Dash evasion counts as a dodge, not ignored hit");
            Start();game.playerInv=0;game.PlayerHit(1000,false,"kick",5);Check(game.playerHp==70,"Single-hit damage capped at 30 percent max HP");
            Start();game.player=new Vector2(-2,0);game.boss=new Vector2(-.5f,0);game.face=1;float bh=game.bossHp;
            Input.down.Add(KeyCode.J);Tick();for(int i=0;i<15;i++)Tick();Check(game.bossHp<bh,"Normal attack applies damage after windup");
            Start();game.player=new Vector2(-5,0);Input.down.Add(KeyCode.LeftShift);Tick();Check(game.dashes==1&&game.dashTime>0,"Dash consumes one stack");
            game.dashTime=0;game.player=new Vector2(-5,0);for(int i=0;i<160;i++)Tick();Check(game.dashes==2,"Grounded dash stack recharges");
            Start();game.player=new Vector2(-5,0);game.playerHp=40;Input.down.Add(KeyCode.F);Tick();for(int i=0;i<45;i++)Tick();Check(game.playerHp==80,"Potion applies 40 HP after delay");
            game.StartBattle(0);game.playerHp=40;Input.down.Add(KeyCode.F);Tick();for(int i=0;i<45;i++)Tick();Check(game.playerHp==80,"Potion inventory resets on retry");
            Start(4);Check(game.seal,"Kharos starts with dash sealed");for(int i=0;i<3;i++){game.parryAge=.1f;game.PlayerHit(8,true,"twelve",5);}Check(!game.seal,"Three successful parries release dash seal");
            Start(1);game.shots.Add(new TowerGame.Shot{pos=game.player+Vector2.up,vel=new Vector2(0,0),parry=true,damage=10,pattern="orb"});game.parryAge=.1f;Call("TickShots",.016f);Check(game.shots[0].friendly&&game.shots[0].damage==25,"Phanes orb reflects and increases damage");
            Start();game.bossHp=game.spec.hp*.59f;Call("TickBoss",.016f);Check(game.phaseTwo&&game.rest>1,"Phase transition grants breathing room");
            Start();game.boss=new Vector2(-5,0);game.player=new Vector2(2,0);Call("BeginPattern",game.spec.patterns[3]);game.patternAge=game.windup;Call("ExecutePattern");Call("TickBoss",.1f);Check(game.bossStagger>3&&!game.pillarLeft,"Arakus charge collides with a pillar and staggers");
            Start(2);game.safeSide=-1;game.player=new Vector2(-7,0);Call("BeginPattern",game.spec.patterns[5]);game.patternAge=game.windup;Call("ExecutePattern");hp=game.playerHp;Call("TickHazards",.04f);Check(game.playerHp==hp,"Judgement safe side avoids damage");
            Start(1);game.barrier=true;game.barrierHp=5;game.player=new Vector2(-1,0);game.face=1;Call("BeginAction","J",.35f);Call("TickAction",.2f);Check(!game.barrier&&game.bossStagger==3,"Breaking Phanes barrier grants stagger");
            Start();game.save.dda=false;game.DamageBoss(game.bossHp);Check(game.result.dda&&game.save.Learning("arakus").battles==1,"DDA setting is snapshotted for a running battle");
            Check(game.save.cleared[0]&&game.save.equippedUltimate==0&&game.save.gold>=130,"First victory unlocks inherited ultimate and gold");
            Check(game.mode==ScreenMode.Result,"Victory enters result screen");
            game.ReturnLobby();Check(game.mode==ScreenMode.Lobby&&game.hazards.Count==0,"Returning to lobby clears battle hazards");
            foreach(int i in new[]{1,2,3,4}){game.StartBattle(i);game.DamageBoss(game.bossHp);Check(game.save.cleared[i],"Boss "+i+" can complete and persist progression");}
            Start();Call("EndBattle",false);Check(game.result.gold==0,"Immediate abandon does not generate free gold");
            Start();game.Stat("cleave").opportunities=3;game.Stat("cleave").hits=3;game.Abandon();
            {var learned=game.save.Learning("arakus");Check(game.result.abandoned&&game.save.history.Count==1&&learned.battles==0&&learned.lossStreak==0&&learned.Pattern("cleave").observations==0,"Abandoned battle keeps history but does not train DDA");}
            Start();game.Stat("cleave").opportunities=3;game.Stat("cleave").hits=3;Call("EndBattle",false);
            {var learned=game.save.Learning("arakus");Check(!game.result.abandoned&&learned.battles==1&&learned.lossStreak==1&&learned.Pattern("cleave").observations==1,"Fought-out defeat still trains DDA");}
            Start();game.Stat("cleave").level=-1;Call("BeginPattern",game.spec.patterns[0]);
            {var cds=(System.Collections.Generic.Dictionary<string,float>)typeof(BossPatternSystem).GetField("cooldowns",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game.BossPatterns);
            Check(Math.Abs(cds["cleave"]-game.seconds-game.spec.patterns[0].cooldown*1.12f)<1e-4f,"Easier level -1 lengthens Arakus cooldown by 12%");}
            for(int boss=0;boss<5;boss++)
            {
                Start(boss);float stop=180;int frames=0;
                // Soak all phase logic and moving hazards; invulnerability here is a test fixture only.
                while(game.seconds<stop&&game.mode==ScreenMode.Battle)
                {
                    game.playerHp=game.maxHp;game.playerInv=.5f;
                    float target=(float)Math.Sin(game.seconds*.6f)*9;Input.held.Add(target>game.player.x?KeyCode.D:KeyCode.A);
                    if(frames%65==0)Input.down.Add(KeyCode.Space);
                    if(frames%28==0)Input.down.Add(KeyCode.J);
                    if(frames%100==0)Input.down.Add(KeyCode.K);
                    if(frames%230==0)Input.down.Add(KeyCode.Q);
                    if(frames%430==0)Input.down.Add(KeyCode.W);
                    if(frames%700==0)Input.down.Add(KeyCode.E);
                    if(game.seconds>55&&game.bossHp>game.spec.hp*.59f)game.bossHp=game.spec.hp*.59f;
                    if(boss==4&&game.seconds>110&&game.bossHp>game.spec.hp*.29f)game.bossHp=game.spec.hp*.29f;
                    Tick(1f/60);frames++;
                    if(float.IsNaN(game.player.x)||float.IsNaN(game.boss.x)||game.shots.Count>200||game.hazards.Count>200)throw new Exception("Unbounded combat state");
                }
                Check(game.seconds>=179||game.result.clear,"180-second boss simulation "+Catalog.Bosses[boss].id);
            }
            var saved=new SaveData{gold=123};Check(SaveStore.Save(saved),"Save writes successfully");saved.gold=234;Check(SaveStore.Save(saved),"Atomic save replacement succeeds");Check(SaveStore.Load().gold==234,"Save reload restores progress");
            File.WriteAllText(SaveStore.SavePath,"broken-json");Check(SaveStore.Load().gold==123,"Corrupt main save recovers backup");
            TestLobbyCards();
            Console.WriteLine("HEADLESS_TESTS_PASSED "+passed+" assertions. Rendering/audio require a real Unity player.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void TestLobbyCards()
    {
        string root=Path.Combine(Path.GetDirectoryName(SaveStore.SavePath),"Cards_"+Guid.NewGuid().ToString("N"));
        SaveStore.UseIsolatedRoot(root);
        var legacy=new SaveData{version=1,gold=1000,attackLevel=2,healthLevel=3,unlockedBoss=3,equippedUltimate=1};
        legacy.cleared[1]=true;legacy.Learning("arakus").Pattern("cleave").level=-1;
        Check(SaveStore.Save(legacy),"Legacy fixture saved");
        var loaded=SaveStore.Load();
        Check(loaded.version==2&&loaded.gold==1000&&loaded.attackLevel==2&&loaded.healthLevel==3&&loaded.equippedUltimate==1&&loaded.unlockedBoss==3,"V1 migration preserves gold, old upgrades and progression");
        Check(loaded.Learning("arakus").Pattern("cleave").level==-1,"V1 migration preserves DDA memory");
        Check(LobbyCards.Attack(loaded)==12.4f&&LobbyCards.Health(loaded)==130,"Old permanent stats still contribute");
        LobbyCards.EnsureOffer(loaded);string offers=string.Join(",",loaded.cardOffer);
        Check(loaded.cardOffer.Length==3&&new System.Collections.Generic.HashSet<string>(loaded.cardOffer).Count==3,"Exactly three distinct known cards offered");
        LobbyCards.EnsureOffer(loaded);Check(offers==string.Join(",",loaded.cardOffer),"Reopening does not reroll offers");
        SaveStore.Save(loaded);loaded=SaveStore.Load();Check(offers==string.Join(",",loaded.cardOffer),"Offers survive application restart");
        Check(File.Exists(SaveStore.SavePath+".v1.backup")&&JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveStore.SavePath+".v1.backup")).version==1,"Original v1 save is archived before migration writes");
        loaded.cardOffer=new[]{"blade","vitality","remedy"};string error;
        Check(LobbyCards.Purchase(loaded,"blade",out error),"Offered card purchase succeeds");
        Check(loaded.gold==920&&loaded.cardPurchases==1&&LobbyCards.Count(loaded,"blade")==1,"Purchase deducts once and adds one permanent stack");
        int beforeGold=loaded.gold;Check(!LobbyCards.Purchase(loaded,"vitality",out error)&&loaded.gold==beforeGold,"Double selection blocked without consuming gold");
        loaded=SaveStore.Load();Check(loaded.cardChosen&&LobbyCards.Count(loaded,"blade")==1&&loaded.gold==920,"Chosen state and permanent effect survive restart");
        Start();game.save=loaded;game.mode=ScreenMode.Lobby;game.showHelp=false;
        game.OpenCards();Check(!game.showCards,"Completed lobby cannot open another selection");
        game.StartBattle(0);Check(!game.showCards&&game.maxHp==130,"Battle entry hides cards and uses accumulated health");
        Call("EndBattle",false);Check(!game.save.cardChosen&&game.save.cardOffer.Length==0,"Battle settlement unlocks next lobby offer");
        Check(LobbyCards.Count(game.save,"blade")==1,"Death preserves permanent cards");
        int goldAfterEnd=game.save.gold;Call("EndBattle",false);Check(game.save.gold==goldAfterEnd&&game.result!=null,"Repeated settlement preserves result and grants no extra gold");
        game.ReturnLobby();game.OpenCards();Check(game.showCards&&game.save.cardOffer.Length==3,"Next lobby offers three cards");
        game.save.cardOffer=new[]{"blade","vitality","remedy"};
        Input.down.Add(KeyCode.Return);Call("Update");Input.Clear();Check(game.mode==ScreenMode.Lobby&&game.showCards,"Enter behind card modal does not start battle");
        Check(game.ChooseCard("blade"),"Lobby controller accepts a visible offered card");
        Check(LobbyCards.Count(game.save,"blade")==2&&Math.Abs(LobbyCards.Attack(game.save)-14.4f)<.001,"Repeated card stacks additively");
        float attack=LobbyCards.Attack(game.save);game.StartBattle(0);game.StartBattle(0);
        Check(Math.Abs(LobbyCards.Attack(game.save)-attack)<.001&&!game.showCards,"Repeated battle start does not reapply card effects");
        Check(!game.ChooseCard("vitality"),"Cards cannot be selected during battle");
        Call("EndBattle",false);game.ReturnLobby();game.showCards=false;
        Input.down.Add(KeyCode.Return);Call("Update");Input.Clear();Check(game.mode==ScreenMode.Battle&&!game.showCards,"Skip strengthening enters battle directly");
        var poor=new SaveData{gold=79,cardOffer=new[]{"blade","vitality","remedy"}};
        Check(!LobbyCards.Purchase(poor,"blade",out error)&&poor.gold==79&&poor.cardPurchases==0,"Insufficient funds grants no effect");
        Check(!LobbyCards.Purchase(poor,"unknown",out error),"Unknown or non-offered card rejected");
        var failure=new SaveData{gold=1000,cardOffer=new[]{"blade","vitality","remedy"}};
        string blocked=Path.Combine(root,"blocked-root");File.WriteAllText(blocked,"fixture");SaveStore.UseIsolatedRoot(blocked);
        Check(!LobbyCards.Purchase(failure,"blade",out error)&&failure.gold==1000&&!failure.cardChosen&&LobbyCards.Count(failure,"blade")==0,"Failed disk write rolls back gold and card effect");
        SaveStore.UseIsolatedRoot(root);
        var statsData=new SaveData();statsData.cards.Add(new CardStack{id="vitality",count=2});statsData.cards.Add(new CardStack{id="remedy",count=2});statsData.cards.Add(new CardStack{id="rush",count=2});
        Check(LobbyCards.Health(statsData)==120&&LobbyCards.Heal(statsData)==50,"Health and potion effects sum across stacks");
        Check(Math.Abs(LobbyCards.SkillMultiplier(statsData,"Q")-1.1f)<.001&&LobbyCards.SkillMultiplier(statsData,"W")==1,"Skill cards affect only specified action");
        Check(LobbyCards.Power(statsData)>0,"DDA context includes card power");
        Start();game.save=statsData;game.StartBattle(0);game.playerHp=30;Call("BeginAction","F",.9f);Call("TickAction",.65f);
        Check(game.maxHp==120&&game.playerHp==80,"Combat consumes effective health and healing stats");
        game.playerHp=10;object[] demoArgs={0f,false,false,false,false,false,false,false,false,false,.016f};Call("DemoInput",demoArgs);
        Check(game.playerHp==10,"Automated input has no hidden healing");
        var malformed=new SaveData();malformed.cards.Add(new CardStack{id="blade",count=2});malformed.cards.Add(new CardStack{id="blade",count=3});malformed.cards.Add(new CardStack{id="bad",count=9});
        malformed.cards.Add(new CardStack{id="vitality",count=-2});malformed.cardOffer=new[]{"blade","blade","remedy"};LobbyCards.Normalize(malformed);
        Check(malformed.cards.Count==1&&LobbyCards.Count(malformed,"blade")==5&&malformed.cardOffer.Length==0,"Malformed cards normalize without duplicate stat application");
    }
}
