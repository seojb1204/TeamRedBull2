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
            if(AutoRunArgs.Active){ddaPanelLoaded=true;showDdaPanel=false;return;}
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
            float h = header + 30 + rows.Count * Row + 36;
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
            if (ddaHistoryExpanded) DrawDdaDifficultyGraph(cx, h + 4, inner);
            }
        }

        static string Pct(float value) => Mathf.RoundToInt(value * 100) + "%";
        static string Level(int level) => level > 0 ? "+" + level : level.ToString();
        static Color LevelColor(int level) => level < 0 ? Palette.Teal : level > 0 ? Palette.Red : Palette.White;
        static Color SuccessColor(float success) => success < .45f ? Palette.Red : success > .9f ? Palette.Gold : Palette.White;
    }
}
#endif
