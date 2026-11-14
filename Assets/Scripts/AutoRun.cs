using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ReversalTower
{
    // 무인 검증 실행용 하네스. 일반 플레이에는 영향을 주지 않는다.
    // 명령행 인자가 하나도 없으면 모든 기능이 꺼진 상태로 남는다.
    //
    //   -towerDemo                자동 조작으로 1번 보스전을 시작한다 (기존 기능)
    //   -towerCapture <파일>      5초 뒤 스크린샷을 저장한다 (기존 기능)
    //   -towerAutoSeconds <초>    제한 시간. 초과하면 정상 종료한다
    //   -towerDataRoot <폴더>     저장/전투CSV/DDA 를 이 폴더로 격리한다
    //   -towerReport <파일>       실행 결과 보고서를 쓴다
    //   -towerFrameRate <정수>    vSync 를 끄고 목표 프레임을 고정한다 (프레임 비교용)
    //   -towerNoDda               이 실행에서 DDA 학습을 끈다
    public static class AutoRunArgs
    {
        public static bool Active;
        public static float SecondsLimit;      // 0 이면 제한 없음
        public static string DataRoot;
        public static string ReportPath;
        public static string CapturePath;
        public static int ForcedFrameRate;     // 0 이면 지정 안 함
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

        /// TowerGame.Awake 의 가장 앞에서 호출된다. 저장을 읽기 전이어야 격리가 의미를 가진다.
        public static void Parse()
        {
            SecondsLimit=0;ForcedFrameRate=0;SaveStore.UseIsolatedRoot(null);
            string[] args;
            try { args = Environment.GetCommandLineArgs(); }
            catch { return; }

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

            Active = AgentProfile!=null || SecondsLimit > 0 || ReportPath != null || DataRoot != null || CapturePath != null ||
                ForcedFrameRate>0 || DisableDda || LobbySmoke || Array.IndexOf(args,"-towerDemo")>=0;
            if(Active&&(SecondsLimit<=0||float.IsNaN(SecondsLimit)||float.IsInfinity(SecondsLimit)))SecondsLimit=30;

            // 자동 실행인데 격리 폴더를 안 줬으면 일반 저장을 건드리지 않도록 강제로 분리한다.
            if (Active && string.IsNullOrEmpty(DataRoot))
            {
                DataRoot = Path.Combine(Application.persistentDataPath, "_autorun",Guid.NewGuid().ToString("N"));
            }

            if (!string.IsNullOrEmpty(DataRoot))
            {
                string isolated=Path.GetFullPath(DataRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
                string normal=Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
                if(string.Equals(isolated,normal,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("자동 검증에 일반 저장 폴더를 사용할 수 없습니다.");
                Directory.CreateDirectory(isolated);SaveStore.UseIsolatedRoot(isolated);
            }
        }
    }

    public sealed partial class TowerGame
    {
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
        string autoExitReason = "미실행";

        // 보스 타격이 예정 시각보다 얼마나 늦게 적용되는지. 가변 스텝의 실제 오차를 재는 값.
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

            // 전투가 끝나면(승리/사망) 결과 화면으로 넘어간다. 그 시점에 보고서를 쓰고 종료한다.
            if (mode == ScreenMode.Result && ended)
            {
                autoExitReason = result != null && result.clear ? "클리어" : "사망";
                AutoWriteReport(); AutoQuit(); return;
            }

            if (AutoRunArgs.SecondsLimit > 0 && autoElapsed >= AutoRunArgs.SecondsLimit)
            {
                autoExitReason = "제한시간 초과";
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
            line("agent.profile",AutoRunArgs.AgentProfile??"legacy-demo");
            line("agent.seed",AutoRunArgs.AgentSeed.ToString(ci));
            line("agent.observation","instrumented state incl. windup; NOT pixels/human reaction");
            line("agent.network","automation args disable CloudSync before config load");
            line("agent.fixture","boss unlock only in isolated save; no HP/damage corrections");
            line("startedUtc", autoStartedUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            line("unityVersion", Application.unityVersion);
            line("platform", Application.platform.ToString());

            // --- 데이터 격리 ---
            line("saveRoot", SaveStore.Root);
            line("saveRootIsolated", SaveStore.IsIsolated ? "true" : "false");
            line("WARNING.sharedWithNormalPlay", SaveStore.IsIsolated ? "no" : "YES - 일반 플레이 저장과 같은 폴더를 썼다");

            // --- 테스트 보정 여부 ---
            // 런타임 자동 조작에는 체력 보정도 무적도 없다. 사람과 같은 규칙으로 싸운다.
            line("testCorrections.autoHeal", "none");
            line("testCorrections.invulnerability", "none");
            line("testCorrections.damageScaling", "none");
            line("testCorrections.lobbyFixture", AutoRunArgs.LobbySmoke ? "isolated Gold=1000, fixed offers, forced battle settlement" : "none");
            line("lobbySmoke.checks", lobbySmokeChecks.ToString(ci));
            line("testCorrections.summary", AutoRunArgs.LobbySmoke ? "로비 통합검사: 테스트 Gold·고정 후보·강제 정산 사용, 정상 플레이 성과 아님" : demo ? "입력만 자동, 전투 규칙 보정 없음" : "자동 조작 미사용");
            line("inputSource", AutoRunArgs.LobbySmoke ? "로비 통합검사 하네스" : demo ? "DemoInput (스크립트 자동 조작)" : "사람 입력");

            // --- 실행 구분 ---
            line("battle.started", autoBossHpStart >= 0 ? "true" : "false");
            line("battle.boss", spec != null ? spec.id : "(없음)");
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

            // --- 프레임/타이밍 ---
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

            // --- 캡처 ---
            bool wanted = !string.IsNullOrEmpty(AutoRunArgs.CapturePath);
            line("capture.requested", wanted ? "true" : "false");
            line("capture.path", AutoRunArgs.CapturePath ?? "(없음)");
            line("capture.triggered", captureDone ? "true" : "false");
            // 스크린샷은 비동기라 이 시점에 파일이 아직 없을 수 있다. 최종 확인은 외부에서 한다.
            string exists = "unknown";
            if (wanted) { try { exists = File.Exists(AutoRunArgs.CapturePath) ? "true" : "false"; } catch { exists = "error"; } }
            line("capture.fileExistsAtExit", exists);

            line("result", "AUTORUN_REPORT_COMPLETE");

            try
            {
                string dir = Path.GetDirectoryName(AutoRunArgs.ReportPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(AutoRunArgs.ReportPath, sb.ToString(), new UTF8Encoding(false));
                Debug.Log("AUTORUN 보고서: " + AutoRunArgs.ReportPath);
            }
            catch (Exception e) { Debug.LogError("AUTORUN 보고서 실패: " + e.Message); }
        }
    }
}
