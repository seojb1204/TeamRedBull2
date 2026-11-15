#if !HEADLESS && !DESKTOP_PREVIEW
using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower
{
    // DDA 확인 패널. F8로 켜고 끄며, 켠 상태는 다음 실행에도 기억한다(PlayerPrefs).
    // 플레이어에게는 DDA 보정값을 보여 주지 않는 것이 기본이라 꺼진 채로 시작한다. 개발 확인과 발표 시연용이다.
    //  - 전투 중: 패턴별 이번 판 레벨, 이번 판 회피·패링 기록(실시간), 지금까지 기억하는 성공률
    //  - 결과 화면: 위 내용 + 이번 판까지 학습한 뒤 '다음 판'에 고를 레벨과 그 이유
    public sealed partial class TowerGame
    {
        const string DdaPanelPref = "DdaPanelVisible";
        bool ddaPanelLoaded, showDdaPanel;
        HistoryEntry ddaPreviewFor;
        BossLearning ddaPreviewLearning;
        bool ddaPreviewEnabled;
        readonly Dictionary<string,int> ddaNextLevel = new Dictionary<string,int>();
        readonly Dictionary<string,string> ddaNextReason = new Dictionary<string,string>();

        void DdaPanelInput()
        {
            if(AutoRunArgs.IsAutomationProcess){ddaPanelLoaded=true;showDdaPanel=false;return;}
            if (!ddaPanelLoaded) { showDdaPanel = PlayerPrefs.GetInt(DdaPanelPref, 0) == 1; ddaPanelLoaded = true; }
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.F8)
            {
                ResetDdaPanelWindow();
                showDdaPanel = !showDdaPanel;
                PlayerPrefs.SetInt(DdaPanelPref, showDdaPanel ? 1 : 0);
                PlayerPrefs.Save();
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
            ddaPreviewFor = result;
            ddaNextLevel.Clear();
            ddaNextReason.Clear();
            bool enabled = save.dda && !AutoRunArgs.DisableDda;
            ddaPreviewEnabled = enabled;
            foreach (Pattern p in spec.patterns)
            {
                double[] context;
                string reason;
                ddaNextLevel[p.id] = DdaBattleSession.Decide(save, spec, p, enabled, out context, out reason);
                ddaNextReason[p.id] = reason;
            }
            ddaPreviewLearning = save.learning.Find(b => b.id == spec.id);
        }

        void DdaPanelUI()
        {
            DdaPanelInput();
            if (!showDdaPanel || save == null || spec == null || stats == null || stats.Count == 0 ||
                (mode != ScreenMode.Battle && mode != ScreenMode.Result))
            {
                EndDdaPanelResize();
                return;
            }
            bool full = mode == ScreenMode.Result;
            // Settings can reset learning or toggle DDA while the same result stays open.
            if (full && (result != ddaPreviewFor || ddaPreviewEnabled != (save.dda && !AutoRunArgs.DisableDda) ||
                !object.ReferenceEquals(ddaPreviewLearning, save.learning.Find(b => b.id == spec.id)))) RefreshDdaPreview();

            BossLearning learning = save.learning.Find(b => b.id == spec.id);
            // 무작위로 나오지 않는 연출 패턴(아라쿠스 퀵스텝·분노)은 이번 판에 기회가 있었을 때만 보여 준다
            var rows = new List<Pattern>();
            foreach (Pattern p in spec.patterns)
            {
                PatternStats s = Stat(p.id);
                if (p.minPhase >= 0 || (s != null && s.opportunities > 0)) rows.Add(p);
            }

            const float Pad = 16, Row = 28, NameW = 240, LevelW = 56, NowW = 74, MemoryW = 74, NextW = 96, ReasonW = 330;
            bool abandonedLine = full && result != null && result.abandoned;
            float w = Pad * 2 + NameW + LevelW + NowW + MemoryW + (full ? NextW + ReasonW : 0);
            float header = 86 + (abandonedLine ? 20 : 0);
            int lastDecision=0;foreach(var e in BossPatterns.SelectionLog)lastDecision=System.Math.Max(lastDecision,e.decision);
            var frequency=new List<PatternSelectionEvidence>();foreach(var e in BossPatterns.SelectionLog)if(e.decision==lastDecision)frequency.Add(e);
            float frequencyHeight=120+frequency.Count*52;
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
                Text(cx, cy, NameW, Row, p.name, 20, Palette.White);
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
            Text(cx,cy,inner,24,"실시간 빈도 · 직전 선택 #"+lastDecision+" (현재 공격을 바꾸지 않음)",16,Palette.Gold);cy+=26;
            Text(cx,cy,inner,22,"패링/회피/피격/제외 · 기본 가중치 × 배수 → 최종 / 선택 확률 · ★ 실제 선택",12,muted);cy+=24;
            foreach(var e in frequency)
            {
                string name=e.id;foreach(var p in spec.patterns)if(p.id==e.id){name=p.name;break;}
                Text(cx,cy,inner,24,(e.selected?"★ ":"   ")+name+"  "+e.parries+"/"+e.dodges+"/"+e.hits+"/"+e.ignored+
                    "   "+e.baseWeight.ToString("F2")+" × "+e.multiplier.ToString("F3")+" → "+e.weight.ToString("F2")+" / "+Pct(e.probability),15,e.selected?Palette.Gold:Palette.White);
                Text(cx+12,cy+24,inner-12,22,FrequencyReason(e.reason),12,muted);cy+=52;
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
        static string Pct(float value) => Mathf.RoundToInt(value * 100) + "%";
        static string Level(int level) => level > 0 ? "+" + level : level.ToString();
        static Color LevelColor(int level) => level < 0 ? Palette.Teal : level > 0 ? Palette.Red : Palette.White;
        static Color SuccessColor(float success) => success < .45f ? Palette.Red : success > .9f ? Palette.Gold : Palette.White;
    }
}
#endif
