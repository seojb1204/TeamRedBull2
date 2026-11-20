#if !HEADLESS && !DESKTOP_PREVIEW
using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower
{
    // DDA 확인 패널. F8로 켜고 끄며, 재실행과 메뉴 복귀 시 기본적으로 닫는다.
    // 플레이어에게는 DDA 보정값을 보여 주지 않는 것이 기본이라 꺼진 채로 시작한다. 개발 확인과 발표 시연용이다.
    //  - 전투 중: 패턴별 이번 판 레벨, 이번 판 회피·패링 기록(실시간), 지금까지 기억하는 성공률
    //  - 결과 화면: 위 내용 + 이번 판까지 학습한 뒤 '다음 판'에 고를 레벨과 그 이유
    public sealed partial class TowerGame
    {
        bool ddaPanelLoaded, showDdaPanel;
        ScreenMode ddaPanelMode;
        readonly DdaPreviewStamp ddaPreviewStamp=new DdaPreviewStamp();
        readonly Dictionary<string,int> ddaNextLevel = new Dictionary<string,int>();
        readonly Dictionary<string,string> ddaNextReason = new Dictionary<string,string>();

        public bool TutorialSummaryBlocksMenu=>showDdaPanel&&ddaPanelLoaded&&ddaPanelMode==mode&&!TutorialActive&&LastTutorialAssessment!=null&&(mode==ScreenMode.Lobby||mode==ScreenMode.Title);

        void DdaPanelInput()
        {
            if(AutoRunArgs.IsAutomationProcess){ddaPanelLoaded=true;showDdaPanel=false;return;}
            // Visibility belongs to this session, never to a previous process.
            if(!ddaPanelLoaded){showDdaPanel=false;ddaPanelLoaded=true;ddaPanelMode=mode;}
            if(ddaPanelMode!=mode){if(mode==ScreenMode.Lobby||mode==ScreenMode.Title){showDdaPanel=false;EndDdaPanelResize();}ddaPanelMode=mode;}
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.F8)
            {
                ResetDdaPanelWindow();
                showDdaPanel = !showDdaPanel;
                // F8 remains available for explicitly requested tutorial summaries.
                e.Use();
            }
            if (showDdaPanel && e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.F9)
            {
                ddaHistoryExpanded = !ddaHistoryExpanded;
                e.Use();
            }
        }

        // 결과 화면에서 한 번만 계산한다 (DdaBattleSession.Decide는 학습하지 않으므로 저장 데이터가 바뀌지 않음)
        void RefreshDdaPreview()
        {
            ddaNextLevel.Clear();
            ddaNextReason.Clear();
            bool enabled = save.dda && !AutoRunArgs.DisableDda;
            foreach (Pattern p in spec.patterns)
            {
                double[] context;
                string reason;
                ddaNextLevel[p.id] = DdaBattleSession.Decide(save, spec, p, enabled, out context, out reason);
                ddaNextReason[p.id] = reason;
            }
            ddaPreviewStamp.Capture(result,save.learning.Find(b => b.id == spec.id),enabled,CommunityStats.Revision);
        }

        void DdaPanelUI()
        {
            DdaPanelInput();
            if(showDdaPanel&&(TutorialActive||TutorialSummaryBlocksMenu)){DrawTutorialDdaPanel();return;}
            if (!showDdaPanel || save == null || spec == null || stats == null || stats.Count == 0 ||
                (mode != ScreenMode.Battle && mode != ScreenMode.Result))
            {
                EndDdaPanelResize();
                return;
            }
            bool full = mode == ScreenMode.Result;
            // Settings can reset learning or toggle DDA while the same result stays open.
            if (full && !ddaPreviewStamp.Matches(result,save.learning.Find(b => b.id == spec.id),
                save.dda&&!AutoRunArgs.DisableDda,CommunityStats.Revision)) RefreshDdaPreview();

            BossLearning learning = save.learning.Find(b => b.id == spec.id);
            // 무작위로 나오지 않는 연출 패턴(아라쿠스 퀵스텝·분노)은 이번 판에 기회가 있었을 때만 보여 준다
            var rows = new List<Pattern>();
            foreach (Pattern p in spec.patterns)
            {
                PatternStats s = Stat(p.id);
                if (p.minPhase >= 0 || (s != null && s.opportunities > 0)) rows.Add(p);
            }

            const float Pad = 16, Row = 52, NameW = 240, LevelW = 56, NowW = 74, MemoryW = 74, NextW = 96, ReasonW = 330;
            bool abandonedLine = full && result != null && result.abandoned;
            float w = Pad * 2 + NameW + LevelW + NowW + MemoryW + (full ? NextW + ReasonW : 0);
            float header = 268 + (abandonedLine ? 20 : 0);
            int lastDecision=0;foreach(var e in BossPatterns.SelectionLog)lastDecision=System.Math.Max(lastDecision,e.decision);
            var frequency=new List<PatternSelectionEvidence>();foreach(var e in BossPatterns.SelectionLog)if(e.decision==lastDecision)frequency.Add(e);
            float frequencyHeight=136+frequency.Count*90;
            float h = header + 30 + rows.Count * Row + 36+frequencyHeight;
            w=Mathf.Max(w,1000);
            using (BeginDdaHistoryPanel(w, h))
            {
            w = ddaPanelContentWidth;
            float x = 0, y = 0, inner = w - Pad * 2;

            Fill(new Rect(x, y, w, h), new Color(.02f, .035f, .055f, .9f));
            Fill(new Rect(x, y, w, 2), Palette.Gold);
            float cx = x + Pad, cy = y + 12;
            Text(cx, cy, inner, 28, "DDA 패널", 20, Palette.Gold);
            Text(cx, cy + 4, inner, 24, (battleDda ? "DDA 켜짐" : "DDA 꺼짐 (고정 난이도)") + "   ·   F8 닫기", 13, muted, TextAnchor.UpperRight);
            cy += 32;
            string learned = learning == null ? "기록 없음"
                : "끝까지 싸운 판 " + learning.battles + "   ·   연패 " + learning.lossStreak + "   ·   최근 평균 성공률 " + Pct(learning.globalSuccess);
            Text(cx, cy, inner, 20, spec.name + "   ·   " + learned, 13, Palette.White);
            cy += 20;
            Text(cx, cy, inner, 20, "DB: " + CommunityStats.Status + "   ·   목표 성공률 60~80%", 13, muted);
            cy += 22;
            Text(cx,cy,inner,20,"현재 수신: "+CommunityStats.ReceiveSummary,11,muted);cy+=20;
            Text(cx,cy,inner,20,"이번 배정 시점 r"+dda.CommunityRevisionAtPrepare+": "+dda.CommunitySnapshotAtPrepare,11,muted);cy+=20;
            Text(cx,cy,inner,20,"늦은 수신은 이번 배정을 유지하며 다음 전투 예측만 갱신합니다.",11,muted);cy+=22;
            var live=DdaSnapshot();
            Text(cx,cy,inner,24,live.hasAttack?"현재 공격: "+live.patternName+"  [실제 적용 Lv "+Level(live.level)+"]":"현재 공격 없음 · 아래는 이번 전투 배정 / 다음 판은 별도 예측",14,Palette.Gold);cy+=24;
            Text(cx,cy,inner,24,live.hasAttack?"선딜 "+live.windup.ToString("F3")+"s (×"+live.windupScale.ToString("F2")+") · 피해 "+live.damage.ToString("F2")+" (×"+live.damageScale.ToString("F2")+") · 쿨다운 "+live.cooldown.ToString("F2")+"s (×"+live.cooldownScale.ToString("F2")+")":"공격 객체 없음: 이전 공격 수치를 현재 값으로 표시하지 않음",12,Palette.White);cy+=24;
            Text(cx,cy,inner,24,"현재 배정 근거: "+live.source+" · "+live.reason,11,muted);cy+=24;
            Text(cx,cy,inner,24,"최초 튜토리얼: "+(live.assessment==null?"없음":live.assessment.skipped?"스킵 · 근거 제외":live.assessment.group+" / score "+live.assessment.score.ToString("F2")+" / confidence "+live.assessment.confidence.ToString("F2"))+" · "+live.uploadStatus,11,muted);cy+=24;
            Text(cx,cy,inner,24,"그룹 수신: "+CommunityStats.GroupState+" · "+CommunityStats.GroupOrigin+" · 개인 학습 이후 초기값 덮어쓰기 없음",11,muted);cy+=24;
            if (abandonedLine)
            {
                Text(cx, cy, inner, 20, "포기한 판이라 DDA가 학습하지 않았습니다 (기록과 골드만 남음)", 13, Palette.Red);
                cy += 20;
            }

            // 표 머리
            float c1 = cx + NameW, c2 = c1 + LevelW, c3 = c2 + NowW, c4 = c3 + MemoryW, c5 = c4 + NextW;
            Text(cx, cy, NameW, 22, "패턴", 13, muted);
            Text(c1, cy, LevelW, 22, "레벨", 13, muted);
            Text(c2, cy, NowW, 22, "이번 판", 13, muted);
            Text(c3, cy, MemoryW, 22, full ? "기억(갱신)" : "기억", 13, muted);
            if (full)
            {
                Text(c4, cy, NextW, 22, "다음 판", 13, muted);
                Text(c5, cy, ReasonW, 22, "다음 판 판단", 13, muted);
            }
            cy += 24;
            Rule(cx, cy, inner);
            cy += 6;

            foreach (Pattern p in rows)
            {
                PatternStats s = Stat(p.id);
                PatternMemory m = learning == null ? null : learning.patterns.Find(q => q.id == p.id);
                int level = s == null ? 0 : s.level;
                Text(cx, cy, NameW, 28, p.name, 20, Palette.White);
                string source;dda.Sources.TryGetValue(p.id,out source);
                Text(cx,cy+29,NameW,21,(source??"배정 근거 없음").Replace(" · 개인 패턴 기록",""),10,muted);
                Text(c1, cy, LevelW, Row, Level(level), 20, LevelColor(level));
                if (s == null || s.opportunities == 0) Text(c2, cy, NowW, Row, "-", 20, muted);
                else Text(c2, cy, NowW, Row, (s.parries + s.dodges) + "/" + s.opportunities, 20, SuccessColor(s.Success));
                if (m == null || m.observations == 0) Text(c3, cy, MemoryW, Row, "-", 20, muted);
                else Text(c3, cy, MemoryW, Row, Pct(m.success), 20, SuccessColor(m.success));
                if (full)
                {
                    int next;
                    if (!ddaNextLevel.TryGetValue(p.id, out next)) next = level;
                    string change = next > level ? " 올림" : next < level ? " 내림" : " 유지";
                    Text(c4, cy, NextW, Row, Level(next) + change, 20, LevelColor(next));
                    string reason;
                    ddaNextReason.TryGetValue(p.id, out reason);
                    Text(c5, cy + 5, ReasonW, Row, reason ?? "", 13, muted);
                }
                cy += Row;
            }

            cy += 8;
            Rule(cx, cy, inner);
            Text(cx, cy + 8, inner, 22, "레벨 -2 쉬움 ~ +2 어려움   ·   선딜 x1.3 ~ x0.8   ·   피해 x0.8 ~ x1.2   ·   쿨타임 x1.25 ~ x0.8", 13, muted);
            cy+=42;
            Text(cx,cy,inner,32,"실시간 빈도 · 직전 선택 #"+lastDecision+" (현재 공격을 바꾸지 않음)",16,Palette.Gold);cy+=34;
            Text(cx,cy,inner,22,"패링/회피/피격/제외 · 기본 가중치 × 배수 → 최종 / 선택 확률 · ★ 실제 선택",12,muted);cy+=24;
            foreach(var e in frequency)
            {
                string name=e.id;foreach(var p in spec.patterns)if(p.id==e.id){name=p.name;break;}
                // Pixel UI snaps 15px to 22px. Give each line its full glyph height
                // and split the evidence so names/numbers never clip or wrap into the reason.
                Color ink=e.selected?Palette.Gold:Palette.White;
                Text(cx,cy,inner,32,(e.selected?"★ ":"   ")+name+"  "+e.parries+"/"+e.dodges+"/"+e.hits+"/"+e.ignored+
                    "   성공 "+(e.opportunities>0?Pct(e.success):"관측 없음"),15,ink);
                Text(cx+12,cy+30,inner-12,32,e.baseWeight.ToString("F2")+" × "+e.multiplier.ToString("F3")+" → "+e.weight.ToString("F2")+"   선택 확률 "+Pct(e.probability),15,ink);
                Text(cx+12,cy+62,inner-12,22,FrequencyReason(e.reason),12,muted);cy+=90;
            }
            Text(cx,cy,inner,22,"후보가 아닌 거리/쿨다운/페이즈 제외 패턴은 표시하지 않음 · 확률과 실제 빈도는 다를 수 있음",12,muted);cy+=22;
            Text(cx,cy,inner,22,BossPatterns.DiagnosticStatus,11,muted);
            if (ddaHistoryExpanded) DrawDdaDifficultyGraph(cx, h + 4, inner);
            }
        }

        static string FrequencyReason(string reason)
        {
            return (reason??"").Replace("fewer than 4 valid opportunities","유효 관측 4회 미만: 기본 빈도")
                .Replace("low HP: no extra practice pressure","체력 30% 이하: 추가 압박 없음")
                .Replace("DDA off","DDA 꺼짐: 기본 빈도").Replace("invalid evidence ignored","유효하지 않은 통계 제외")
                .Replace("smoothed success below 70%; practice boost capped at 35%","보정 성공률 70% 미만: 연습 가중치 (상한 35%)")
                .Replace("success sufficient; base frequency","대응 충분: 기본 빈도")
                .Replace("eligible authored opening","조건을 만족한 첫 공격")
                .Replace("recent repetition limit: 2 in last 6","최근 6회 중 2회: 반복 제한으로 제외");
        }
        void DrawTutorialDdaPanel()
        {
            var view=DdaSnapshot();var a=TutorialActive?view.assessment:LastTutorialAssessment;if(a==null)return;
            using(BeginDdaHistoryPanel(1060,ddaHistoryExpanded?610:360,true))
            {
                float y=10,w=ddaPanelContentWidth-32;
                System.Action<string> line=text=>{Text(16,y,w,25,text,13,Palette.White);y+=28;};
                line("튜토리얼 숙련도 평가 · 규칙 기반 / LinUCB 전투 학습 아님");
                line("단계: "+(TutorialActive?Tutorial.Step.ToString():"종료 · F8 닫으면 메뉴 입력 복귀")+" · "+TutorialLocalStatus);
                line((a.completed?"확정":a.skipped?"스킵(숙련도 근거 제외)":"잠정")+" 그룹 "+a.group+" · score "+a.score.ToString("F3")+" · confidence "+a.confidence.ToString("F3")+" (검증된 확률 아님)");
                line("유효 수행 "+a.activeDurationSeconds.ToString("F1")+"s / 전체 "+a.elapsedDurationSeconds.ToString("F1")+"s / 설명 "+a.explanationSeconds.ToString("F1")+"s / 일시정지 "+a.pausedSeconds.ToString("F1")+"s");
                line("로컬: "+TutorialLocalStatus+" · DB: "+SaveStore.ProficiencyStatus);
                line("수신: "+CommunityStats.ReceiveSummary);
                if(TutorialActive){line(Tutorial.WeightSummary);line(Tutorial.EvidenceSummary);}
                if(ddaHistoryExpanded)
                {
                    line("실제 패링 "+a.parrySuccesses+"/"+a.parryAttempts+" · 회피 "+a.dodgeSuccesses+"/"+a.dodgeAttempts+" · 피격 "+a.damageHits+" · 재시도 "+a.retryCount);
                    line("수행 조작 "+a.skillActionsCompleted+" · 잘못된 입력 묶음 "+a.inputMistakes+" · 궁극기 명중 "+a.ultimateExecutionSuccess);
                    line("수행시간은 점수의 기존 10% 항목이며 설명/일시정지는 제외합니다.");
                    line("평가 중 DB 전투 통계/보상/개인 LinUCB를 갱신하지 않습니다.");
                    line("완료·유효 최초 평가만 그룹 초기값 근거입니다. 모의 통계는 모의로 표시합니다.");
                }
            }
        }

        static string Pct(float value) => Mathf.RoundToInt(value * 100) + "%";
        static string Level(int level) => level > 0 ? "+" + level : level.ToString();
        static Color LevelColor(int level) => level < 0 ? Palette.Teal : level > 0 ? Palette.Red : Palette.White;
        static Color SuccessColor(float success) => success < .45f ? Palette.Red : success > .9f ? Palette.Gold : Palette.White;
    }
}
#endif
