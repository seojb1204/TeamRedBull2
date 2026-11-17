using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ReversalTower
{
    // ���� ���� ����� �ϳ׽�. �Ϲ� �÷��̿��� ������ ���� �ʴ´�.
    // ������ ���ڰ� �ϳ��� ������ ��� ����� ���� ���·� ���´�.
    //
    //   -towerDemo                �ڵ� �������� 1�� �������� �����Ѵ� (���� ���)
    //   -towerCapture <����>      5�� �� ��ũ������ �����Ѵ� (���� ���)
    //   -towerAutoSeconds <��>    ���� �ð�. �ʰ��ϸ� ���� �����Ѵ�
    //   -towerDataRoot <����>     ����/����CSV/DDA �� �� ������ �ݸ��Ѵ�
    //   -towerReport <����>       ���� ��� �������� ����
    //   -towerFrameRate <����>    vSync �� ���� ��ǥ �������� �����Ѵ� (������ �񱳿�)
    //   -towerNoDda               �� ���࿡�� DDA �н��� ����
    public static class AutoRunArgs
    {
        public static bool Active;
        public static bool ExternalInput;
        public static bool ValidateExternalInput(string[] args)
        {
            if(Array.IndexOf(args,"-towerExternalInput")<0)return false;
            foreach(var denied in new[]{"-towerDemo","-towerAgent","-towerLobbySmoke","-towerPixelAgent"})
                if(Array.IndexOf(args,denied)>=0)throw new ArgumentException("External input conflicts with "+denied);
            string root=Value(args,"-towerDataRoot"),seconds=Value(args,"-towerAutoSeconds");
            float limit;
            if(string.IsNullOrEmpty(root)||!Path.IsPathRooted(root))
                throw new ArgumentException("External input requires explicit absolute isolated root");
            if(!float.TryParse(seconds,NumberStyles.Float,CultureInfo.InvariantCulture,out limit)||
               float.IsNaN(limit)||float.IsInfinity(limit)||limit<=0||limit>30)
                throw new ArgumentException("External input requires a finite 0..30 second limit");
            string canonical=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(Directory.Exists(canonical)||File.Exists(canonical))
                throw new ArgumentException("External input requires a new unused run directory");
            foreach(var outputFlag in new[]{"-towerReport","-towerCapture"}){
                string output=Value(args,outputFlag);
                if(output!=null && (!Path.IsPathRooted(output)||!Path.GetFullPath(output).StartsWith(canonical+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("External output must remain inside isolated run directory");
            }
            return true;
        }


        public static float SecondsLimit;      // 0 �̸� ���� ����
        public static string DataRoot;
        public static string ReportPath;
        public static string CapturePath;
        public static int ForcedFrameRate;     // 0 �̸� ���� �� ��
        public static bool DisableDda;
        public static bool LobbySmoke;
        public static string AgentProfile;
        public static int AgentSeed=17, AgentBoss;
        // Usable before TowerGame.Awake: CloudSync may initialize first.
        public static bool HasAutomationArguments(string[] args)
        {
            foreach(var arg in args)if(arg.StartsWith("-tower",StringComparison.Ordinal))return true;
            return false;
        }

        // Raw process flags protect preferences even before Parse/Awake initializes Active.
        public static bool IsAutomationProcess => Active || ProcessHasAutomationArguments;
        static readonly bool ProcessHasAutomationArguments = HasAutomationArguments(Environment.GetCommandLineArgs());

        static string Value(string[] args, string key)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        /// TowerGame.Awake �� ���� �տ��� ȣ��ȴ�. ������ �б� ���̾�� �ݸ��� �ǹ̸� ������.
        public static void Parse()
        {
            SecondsLimit=0;ForcedFrameRate=0;SaveStore.UseIsolatedRoot(null);
            string[] args;
            try { args = Environment.GetCommandLineArgs(); }
            catch { return; }

            ExternalInput=ValidateExternalInput(args);
            AgentProfile=Value(args,"-towerAgent");AgentSeed=17;AgentBoss=0;
            if(AgentProfile!=null&&AgentProfile!="novice"&&AgentProfile!="balanced"&&AgentProfile!="expert")throw new ArgumentException("towerAgent: novice, balanced or expert required");
            string seed=Value(args,"-towerSeed"),boss=Value(args,"-towerBoss");
            if(seed!=null&&!int.TryParse(seed,out AgentSeed))throw new ArgumentException("Invalid towerSeed");
            if(boss!=null&&(!int.TryParse(boss,out AgentBoss)||AgentBoss<0||AgentBoss>=Catalog.Bosses.Length))throw new ArgumentException("towerBoss must be 0..4");
            DataRoot = Value(args, "-towerDataRoot");
            ReportPath = Value(args, "-towerReport");
            CapturePath = Value(args, "-towerCapture");
            DisableDda = Array.IndexOf(args, "-towerNoDda") >= 0;
            LobbySmoke=Array.IndexOf(args,"-towerLobbySmoke")>=0;

            string seconds = Value(args, "-towerAutoSeconds");
            if (seconds != null) float.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out SecondsLimit);

            string fps = Value(args, "-towerFrameRate");
            if (fps != null) int.TryParse(fps, NumberStyles.Integer, CultureInfo.InvariantCulture, out ForcedFrameRate);

            Active = ExternalInput || Array.IndexOf(args,"-towerPixelAgent")>=0 || AgentProfile!=null || SecondsLimit > 0 || ReportPath != null || DataRoot != null || CapturePath != null ||
                ForcedFrameRate>0 || DisableDda || LobbySmoke || Array.IndexOf(args,"-towerDemo")>=0;
            if(Active&&(SecondsLimit<=0||float.IsNaN(SecondsLimit)||float.IsInfinity(SecondsLimit)))SecondsLimit=30;

            // �ڵ� �����ε� �ݸ� ������ �� ������ �Ϲ� ������ �ǵ帮�� �ʵ��� ������ �и��Ѵ�.
            if (Active && string.IsNullOrEmpty(DataRoot))
            {
                DataRoot = Path.Combine(Application.persistentDataPath, "_autorun",Guid.NewGuid().ToString("N"));
            }

            if (!string.IsNullOrEmpty(DataRoot))
            {
                string isolated=Path.GetFullPath(DataRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
                string normal=Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
                if(string.Equals(isolated,normal,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("�ڵ� ������ �Ϲ� ���� ������ ����� �� �����ϴ�.");
                Directory.CreateDirectory(isolated);SaveStore.UseIsolatedRoot(isolated);
            }
        }
    }

    // Opt-in QA evidence. HP loss is clipped at zero; healing is a separate event.
    // This measures game callbacks, not rendered perception or OS keyboard latency.
    public sealed class BattleEvidence
    {
        public const int MaxEvents=20000;
        public readonly System.Collections.Generic.List<string> Events=new System.Collections.Generic.List<string>();
        public float DamageTaken, Healing, DamageDealt;
        public int Parries, DroppedEvents, RequestedInputs, CommittedInputs;
        public void Reset(){Events.Clear();DamageTaken=Healing=DamageDealt=0;Parries=DroppedEvents=RequestedInputs=CommittedInputs=0;}
        public void Note(float seconds,string kind,string detail,float amount=0)
        {
            if(kind=="damage_taken")DamageTaken+=amount;
            if(kind=="heal")Healing+=amount;
            if(kind=="damage_dealt")DamageDealt+=amount;
            if(kind=="parry")Parries++;
            if(kind=="input_requested")RequestedInputs++;
            if(kind=="input_committed")CommittedInputs++;
            if(Events.Count>=MaxEvents){DroppedEvents++;return;}
            Events.Add(seconds.ToString("F6",CultureInfo.InvariantCulture)+","+kind+","+detail+","+amount.ToString("F6",CultureInfo.InvariantCulture));
        }
    }

    public sealed partial class TowerGame
    {
        public readonly BattleEvidence Evidence=new BattleEvidence();
        bool EvidenceEnabled=>AutoRunArgs.Active||AutoRunArgs.AgentProfile!=null;
        void NoteEvidence(string kind,string detail,float amount=0){if(EvidenceEnabled)Evidence.Note(seconds,kind,detail,amount);}
        void HealPlayer(float amount,string cause)
        {
            float before=playerHp;playerHp=Mathf.Min(maxHp,playerHp+amount);
            NoteEvidence("heal",cause,playerHp-before);RecordPixelHpChange(before);
        }
        System.Random agentRandom;
        Pattern agentPattern;float agentLastAge=-1;bool agentRespond,agentDefended;
        void ResetTestAgent()
        {
            agentRandom=new System.Random(AutoRunArgs.AgentSeed);agentPattern=null;agentLastAge=-1;agentDefended=false;demoAction=0;
            if(AutoRunArgs.AgentProfile!=null)BossPatterns.SetTestSeed(AutoRunArgs.AgentSeed);
        }
        // Privileged, deterministic rule policy; emits inputs only, never edits combat outcomes.
        void TestAgentInput(ref float move,ref bool jump,ref bool dash,ref bool attack,ref bool parry,
            ref bool q,ref bool w,ref bool e,ref bool r,ref bool f,float dt)
        {
            float distance=boss.x-player.x;move=Mathf.Abs(distance)>1.8f?Mathf.Sign(distance):0;
            jump=dash=parry=q=w=e=r=f=attack=false;
            if(agentRandom==null)ResetTestAgent();
            if(current!=agentPattern||patternAge<agentLastAge)
            {
                agentPattern=current;agentDefended=false;
                double chance=AutoRunArgs.AgentProfile=="novice"?.35:AutoRunArgs.AgentProfile=="expert"?.95:.70;
                agentRespond=agentRandom.NextDouble()<chance;
            }
            agentLastAge=patternAge;
            float until=current==null?99:windup-patternAge;
            bool guarding=current!=null&&agentRespond&&until<.45f&&until>-.15f;
            if(guarding&&!agentDefended&&until<=.12f&&until>=-.05f)
            {
                if(current.parryable)parry=true;
                else if(current.kind==AttackKind.Wave)jump=true;
                else{dash=true;move=-Mathf.Sign(distance);}
                agentDefended=true;
            }
            demoAction-=dt;
            if(!guarding){attack=demoAction<=0;if(attack)demoAction=AutoRunArgs.AgentProfile=="novice"?.65f:.37f;
                q=skillQ<=0&&Mathf.Abs(distance)>4;w=skillW<=0&&Mathf.Abs(distance)<3;e=skillE<=0&&bossStagger>1;r=ult>=100;f=playerHp<maxHp*.5f;}
        }

        float autoElapsed;
        readonly System.Diagnostics.Stopwatch autoWatch=System.Diagnostics.Stopwatch.StartNew();
        readonly DateTime autoStartedUtc=DateTime.UtcNow;
        int autoFrames;
        float autoDtMin = float.MaxValue, autoDtMax, autoDtSum;
        float autoBossHpStart = -1;
        bool autoReportWritten;
        string autoExitReason = "�̽���";

        // ���� Ÿ���� ���� �ð����� �󸶳� �ʰ� ����Ǵ���. ���� ������ ���� ������ ��� ��.
        int autoHitSamples;
        float autoHitLateSum, autoHitLateMax;
        internal void AutoNoteHitLateness(float late)
        {
            if (!AutoRunArgs.Active || late < 0) return;
            autoHitSamples++; autoHitLateSum += late;
            if (late > autoHitLateMax) autoHitLateMax = late;
        }

        void AutoRunTick(float dt)
        {
            if (!AutoRunArgs.Active) return;

            autoElapsed=(float)autoWatch.Elapsed.TotalSeconds;autoFrames++;
            autoDtSum += dt;
            if (dt < autoDtMin) autoDtMin = dt;
            if (dt > autoDtMax) autoDtMax = dt;
            if (autoBossHpStart < 0 && mode == ScreenMode.Battle && spec != null) autoBossHpStart = spec.hp;

            if (autoReportWritten) return;
            if(AutoRunArgs.LobbySmoke&&!lobbySmokeFinished)
            {
                if(autoElapsed>20){autoExitReason="LOBBY_SMOKE_FAILED: timeout";AutoWriteReport();Application.Quit(1);return;}
                TickLobbySmoke();return;
            }

            // ������ ������(�¸�/���) ��� ȭ������ �Ѿ��. �� ������ �������� ���� �����Ѵ�.
            if (mode == ScreenMode.Result && ended)
            {
                autoExitReason = result != null && result.clear ? "Ŭ����" : "���";
                AutoWriteReport(); AutoQuit(); return;
            }

            if (AutoRunArgs.SecondsLimit > 0 && autoElapsed >= AutoRunArgs.SecondsLimit)
            {
                autoExitReason = "���ѽð� �ʰ�";
                AutoWriteReport(); AutoQuit();
            }
        }

        void AutoQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(0);
#endif
        }

        void AutoWriteReport()
        {
            if (autoReportWritten) return;
            autoReportWritten = true;
            if (string.IsNullOrEmpty(AutoRunArgs.ReportPath)) return;

            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            Action<string, string> line = (k, v) => sb.Append(k).Append('\t').Append(v).Append('\n');

            line("schema", "towerAutoRun/2");
            line("evidence.schema","towerBattleEvidence/1");
            line("evidence.track","logic-qa/instrumented-input");
            line("evidence.inputSemantics","requested then committed callbacks; jump may be buffered; not OS input");
            line("evidence.damageTaken",Evidence.DamageTaken.ToString("F6",ci));
            line("evidence.healing",Evidence.Healing.ToString("F6",ci));
            line("evidence.damageDealt",Evidence.DamageDealt.ToString("F6",ci));
            line("evidence.parries",Evidence.Parries.ToString(ci));
            line("evidence.requestedInputs",Evidence.RequestedInputs.ToString(ci));
            line("evidence.committedInputs",Evidence.CommittedInputs.ToString(ci));
            line("evidence.droppedEvents",Evidence.DroppedEvents.ToString(ci));
            line("evidence.eventsFile",AutoRunArgs.ReportPath+".events.csv");
            line("agent.profile",AutoRunArgs.ExternalInput?"external-normal-input":AutoRunArgs.AgentProfile??"legacy-demo");
            line("agent.seed",AutoRunArgs.AgentSeed.ToString(ci));
            line("agent.observation",AutoRunArgs.ExternalInput?"external normal input; telemetry is report-only":"instrumented state incl. windup; NOT pixels/human reaction");
            line("agent.network","automation args disable CloudSync before config load");
            line("agent.fixture","boss unlock only in isolated save; no HP/damage corrections");
            line("startedUtc", autoStartedUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            line("unityVersion", Application.unityVersion);
            line("platform", Application.platform.ToString());

            // --- ������ �ݸ� ---
            line("saveRoot", SaveStore.Root);
            line("saveRootIsolated", SaveStore.IsIsolated ? "true" : "false");
            line("WARNING.sharedWithNormalPlay", SaveStore.IsIsolated ? "no" : "YES - �Ϲ� �÷��� ����� ���� ������ ���");

            // --- �׽�Ʈ ���� ���� ---
            // ��Ÿ�� �ڵ� ���ۿ��� ü�� ������ ������ ����. ����� ���� ��Ģ���� �ο��.
            line("testCorrections.autoHeal", "none");
            line("testCorrections.invulnerability", "none");
            line("testCorrections.damageScaling", "none");
            line("testCorrections.lobbyFixture", AutoRunArgs.LobbySmoke ? "isolated Gold=1000, fixed offers, forced battle settlement" : "none");
            line("lobbySmoke.checks", lobbySmokeChecks.ToString(ci));
            line("testCorrections.summary", AutoRunArgs.LobbySmoke ? "�κ� ���հ˻�: �׽�Ʈ Gold������ �ĺ������� ���� ���, ���� �÷��� ���� �ƴ�" : demo ? "�Է¸� �ڵ�, ���� ��Ģ ���� ����" : "�ڵ� ���� �̻��");
            line("inputSource", AutoRunArgs.LobbySmoke ? "�κ� ���հ˻� �ϳ׽�" : demo ? "DemoInput (��ũ��Ʈ �ڵ� ����)" : "��� �Է�");

            // --- ���� ���� ---
            line("battle.started", autoBossHpStart >= 0 ? "true" : "false");
            line("battle.boss", spec != null ? spec.id : "(����)");
            line("battle.ended", ended ? "true" : "false");
            line("battle.outcome", autoExitReason);
            line("battle.clearedUnderNormalRules",
                (ended && result != null && result.clear && demo && SaveStore.IsIsolated) ? "true" : "false");
            line("battle.seconds", seconds.ToString("F2", ci));
            line("battle.playerHpEnd", playerHp.ToString("F1", ci));
            line("battle.bossHpStart", autoBossHpStart.ToString("F1", ci));
            line("battle.bossHpEnd", bossHp.ToString("F1", ci));
            line("battle.hitsTaken", hits.ToString(ci));
            line("battle.parries", parries.ToString(ci));
            line("battle.ddaEnabled", battleDda ? "true" : "false");
            foreach(var st in stats)
            {
                string key="pattern."+st.id+".";
                line(key+"level",st.level.ToString(ci));line(key+"opportunities",st.opportunities.ToString(ci));
                line(key+"hits",st.hits.ToString(ci));line(key+"parries",st.parries.ToString(ci));line(key+"dodges",st.dodges.ToString(ci));
                line(key+"success",st.Success.ToString("F4",ci));
                line(key+"reason",dda.Reasons.ContainsKey(st.id)?dda.Reasons[st.id]:"unknown");
            }
            // Bounded diagnostics, only in the existing explicitly requested autorun report.
            foreach(var e in BossPatterns.SelectionLog)
            {
                string key="selection."+e.decision.ToString(ci)+"."+e.id+".";
                line(key+"seconds",e.seconds.ToString("F2",ci));line(key+"selected",e.selected?"true":"false");
                line(key+"opportunities",e.opportunities.ToString(ci));line(key+"success",e.success.ToString("F4",ci));
                line(key+"weight",e.weight.ToString("F4",ci));line(key+"probability",e.probability.ToString("F4",ci));
                line(key+"reason",e.reason);
            }
            line("build.version", "0.5-arakus-editor-foundation");
            line("progression.cardPurchases", save.cardPurchases.ToString(ci));
            line("progression.effectiveAttack", LobbyCards.Attack(save).ToString("F2",ci));
            line("progression.effectiveMaxHp", LobbyCards.Health(save).ToString("F1",ci));

            // --- ������/Ÿ�̹� ---
            line("frames.count", autoFrames.ToString(ci));
            line("frames.wallSeconds", autoElapsed.ToString("F2", ci));
            line("frames.avgFps", autoElapsed > 0 ? (autoFrames / autoElapsed).ToString("F1", ci) : "0");
            line("frames.dtMin", (autoDtMin == float.MaxValue ? 0 : autoDtMin).ToString("F4", ci));
            line("frames.dtAvg", (autoFrames > 0 ? autoDtSum / autoFrames : 0).ToString("F4", ci));
            line("frames.dtMax", autoDtMax.ToString("F4", ci));
            line("frames.vSyncCount", QualitySettings.vSyncCount.ToString(ci));
            line("frames.targetFrameRate", Application.targetFrameRate.ToString(ci));
            line("frames.forcedFrameRateArg", AutoRunArgs.ForcedFrameRate.ToString(ci));
            line("timing.hitLatenessSamples", autoHitSamples.ToString(ci));
            line("timing.hitLatenessAvg", (autoHitSamples > 0 ? autoHitLateSum / autoHitSamples : 0).ToString("F4", ci));
            line("timing.hitLatenessMax", autoHitLateMax.ToString("F4", ci));

            // --- ĸó ---
            bool wanted = !string.IsNullOrEmpty(AutoRunArgs.CapturePath);
            line("capture.requested", wanted ? "true" : "false");
            line("capture.path", AutoRunArgs.CapturePath ?? "(����)");
            line("capture.triggered", captureDone ? "true" : "false");
            // ��ũ������ �񵿱�� �� ������ ������ ���� ���� �� �ִ�. ���� Ȯ���� �ܺο��� �Ѵ�.
            string exists = "unknown";
            if (wanted) { try { exists = File.Exists(AutoRunArgs.CapturePath) ? "true" : "false"; } catch { exists = "error"; } }
            line("capture.fileExistsAtExit", exists);

            line("result", "AUTORUN_REPORT_COMPLETE");

            try
            {
                string dir = Path.GetDirectoryName(AutoRunArgs.ReportPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(AutoRunArgs.ReportPath+".events.csv","seconds,kind,detail,amount\n"+string.Join("\n",Evidence.Events)+"\n",new UTF8Encoding(false));
                File.WriteAllText(AutoRunArgs.ReportPath, sb.ToString(), new UTF8Encoding(false));
                Debug.Log("AUTORUN ������: " + AutoRunArgs.ReportPath);
            }
            catch (Exception e) { Debug.LogError("AUTORUN ������ ����: " + e.Message); }
        }
    }
}
