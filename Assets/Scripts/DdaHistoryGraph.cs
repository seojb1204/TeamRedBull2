#if !HEADLESS && !DESKTOP_PREVIEW
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ReversalTower
{
    public sealed partial class TowerGame
    {
        const float DdaGraphHeight = 310;
        bool ddaHistoryExpanded = true;
        Vector2 ddaHistoryScroll;
        DdaDifficultyHistoryStore ddaHistoryStore;
        int ddaHistoryRevision = -1;
        string ddaHistoryBoss;
        List<DdaHistoryRecord> ddaHistoryRows = new List<DdaHistoryRecord>();
        List<PatternStats> ddaHistoryLiveStats;
        DdaHistoryRecord ddaHistoryLive;

        // Own the GUI transform/scroll so the original HUD and overlays retain their matrix.
        sealed class DdaPanelScope : IDisposable
        {
            readonly Matrix4x4 previous;
            public DdaPanelScope(Matrix4x4 previous) { this.previous = previous; }
            public void Dispose() { GUI.EndScrollView(); GUI.matrix = previous; }
        }

        DdaPanelScope BeginDdaHistoryPanel(float contentWidth, float tableHeight)
        {
            var previous = GUI.matrix;
            float gameScale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            float scale = Mathf.Clamp(gameScale, .85f, 1.15f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            float sw = Screen.width / scale, sh = Screen.height / scale;
            float width = Mathf.Min(contentWidth + 20, Mathf.Max(1, sw - 16));
            // Leave the HP strip and bottom skill strip clear; scroll rather than shrink tiny text.
            float letterbox = (Screen.height - 900 * gameScale) * .5f;
            float y = Mathf.Max(8, (letterbox + 166 * gameScale + 4) / scale);
            float bottom = Mathf.Min(sh - 8, (letterbox + 752 * gameScale) / scale);
            float contentHeight = tableHeight + (ddaHistoryExpanded ? DdaGraphHeight : 0);
            float height = Mathf.Min(contentHeight + 54, Mathf.Max(60, bottom - y));
            if (y + height > sh - 8) y = Mathf.Max(8, sh - height - 8);
            float x = Mathf.Max(8, sw - width - 8);
            Fill(new Rect(x, y, width, height), new Color(.02f, .035f, .055f, .94f));
            Fill(new Rect(x, y, width, 2), Palette.Gold);
            Text(x + 12, y + 7, Mathf.Max(1, width - 150), 24, "F8 · 전투는 계속됩니다", 13, muted);
            // Mouse buttons are combat inputs. A read-only header + unused F9 avoids attacking
            // or parrying merely to fold this developer overlay; the wheel can still scroll it.
            float foldWidth = Mathf.Min(142, width - 12);
            Text(x + width - foldWidth - 6, y + 7, foldWidth, 24,
                ddaHistoryExpanded ? "F9 그래프 접기 ▲" : "F9 그래프 펼치기 ▼", 13, Palette.Gold, TextAnchor.UpperRight);
            ddaHistoryScroll = GUI.BeginScrollView(new Rect(x + 4, y + 38, Mathf.Max(1, width - 8), Mathf.Max(1, height - 42)),
                ddaHistoryScroll, new Rect(0, 0, contentWidth, contentHeight));
            return new DdaPanelScope(previous);
        }

        void DrawDdaDifficultyGraph(float x, float y, float width)
        {
            Text(x, y, width, 26, DdaDifficultyHistory.AverageLabel, 20, Palette.Gold);
            var store = DdaHistoryRuntime.Store;
            if (store == null)
            {
                Text(x, y + 32, width, 50, "이력 기록기가 준비되지 않았습니다.", 13, Palette.Red);
                return;
            }
            if (store != ddaHistoryStore || store.Revision != ddaHistoryRevision || ddaHistoryBoss != spec.id)
            {
                ddaHistoryStore = store; ddaHistoryRevision = store.Revision; ddaHistoryBoss = spec.id;
                ddaHistoryRows = store.Recent(spec.id);
            }
            if (mode == ScreenMode.Battle && ddaHistoryLiveStats != stats)
            {
                ddaHistoryLiveStats = stats;
                try { ddaHistoryLive = DdaDifficultyHistory.Capture(spec, stats, battleDda, false, false, "current", DateTime.UtcNow); }
                catch (Exception e) { ddaHistoryLive = null; Debug.LogWarning("[DDA History] 현재 배정값 표시 실패: " + e.Message); }
            }
            DdaHistoryRecord live = mode == ScreenMode.Battle ? ddaHistoryLive : null;
            bool hasLive = live != null && live.HasAverage;
            string detail = "기록 시작 이후 완료 전투 · 최근 " + ddaHistoryRows.Count + "판 / 보관 200판 · 공격별 동일 가중치";
            Text(x, y + 30, width, 22, detail, 13, muted);
            float delta;
            string change = DdaDifficultyHistory.TryDelta(ddaHistoryRows, out delta)
                ? "최근 두 완료 전투 변화 " + Signed(delta)
                : "최근 변화: 비교할 연속 DDA 기록 없음";
            Text(x, y + 52, width, 22, change, 13, Palette.White);

            var plot = new Rect(x + 38, y + 84, Mathf.Max(80, width - 60), 150);
            Fill(plot, new Color(.035f, .06f, .085f, .9f));
            for (int level = -2; level <= 2; level++)
            {
                float py = PlotY(plot, level);
                Fill(new Rect(plot.x, py, plot.width, level == 0 ? 2 : 1), Palette.Alpha(level == 0 ? Palette.Gold : Palette.Stone, .65f));
                Text(x, py - 10, 32, 22, level > 0 ? "+" + level : level.ToString(), 13, level == 0 ? Palette.Gold : muted, TextAnchor.UpperRight);
            }
            int first = ddaHistoryRows.Count > 0 ? ddaHistoryRows[0].battle : store.Completed(spec.id) + 1;
            int last = hasLive ? store.Completed(spec.id) + 1 : ddaHistoryRows.Count > 0 ? ddaHistoryRows[ddaHistoryRows.Count - 1].battle : first;
            DdaHistoryRecord previous = null;
            foreach (var r in ddaHistoryRows)
            {
                float px = PlotX(plot, r.battle, first, last);
                if (r.HasAverage)
                {
                    var point = new Vector2(px, PlotY(plot, r.average));
                    if (DdaDifficultyHistory.Connect(previous, r))
                        DdaGraphLine(new Vector2(PlotX(plot, previous.battle, first, last), PlotY(plot, previous.average)), point, LevelColor(r.average > 0 ? 1 : r.average < 0 ? -1 : 0));
                    DdaGraphPoint(point, r.dda ? LevelColor(r.average > 0 ? 1 : r.average < 0 ? -1 : 0) : muted, !r.dda);
                    if (new Rect(px - 8, point.y - 8, 16, 16).Contains(Event.current.mousePosition))
                        Text(x, y + 278, width, 28, "#" + r.battle + " " + (r.dda ? "DDA" : "Fixed") + " " + (r.clear ? "승리" : "패배") + " · " + Signed(r.average) + " · " + r.recordedUtc, 13, Palette.White);
                }
                if (ddaHistoryRows.Count <= 5 || r.battle == first || r.battle == last || r.battle % 5 == 0)
                    Text(px - 24, plot.yMax + 4, 48, 22, r.battle.ToString(), 13, muted, TextAnchor.UpperCenter);
                previous = r;
            }
            if (hasLive)
            {
                float px = PlotX(plot, last, first, last);
                DdaGraphPoint(new Vector2(px, PlotY(plot, live.average)), Palette.Gold, true);
                Text(px - 45, plot.yMax + 4, 90, 22, "진행 " + last, 13, Palette.Gold, TextAnchor.UpperCenter);
            }
            if (ddaHistoryRows.Count == 0)
                Text(plot.x, plot.y + 55, plot.width, 44, hasLive ? "완료 기록 없음 · 금색 점은 현재 배정값" : "기록 없음", 13, muted, TextAnchor.MiddleCenter);
            Text(x, y + 258, width, 22, "● DDA 완료   □ Fixed(선 연결 제외)   □ 금색: 진행 중   ·   포기/종료는 추이 제외", 13, muted);
            string error = DdaHistoryRuntime.Error ?? store.LastError ?? store.Warning;
            if (!string.IsNullOrEmpty(error)) Text(x, y + 278, width, 28, error, 13, Palette.Red);
        }

        static string Signed(float value) => value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
        static float PlotY(Rect plot, float value) => plot.yMax - (value + 2) * plot.height / 4;
        static float PlotX(Rect plot, int battle, int first, int last)
            => last == first ? plot.center.x : plot.x + 10 + (plot.width - 20) * ((float)(battle - first) / (last - first));

        void DdaGraphPoint(Vector2 point, Color color, bool hollow)
        {
            Fill(new Rect(point.x - 3, point.y - 3, 6, 6), color);
            if (hollow) Fill(new Rect(point.x - 2, point.y - 2, 4, 4), new Color(.035f, .06f, .085f, 1));
        }

        void DdaGraphLine(Vector2 from, Vector2 to, Color color)
        {
            var previous = GUI.matrix;
            try
            {
                Vector2 d = to - from;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, from);
                Fill(new Rect(from.x, from.y - 1, d.magnitude, 2), color);
            }
            finally { GUI.matrix = previous; }
        }
    }
}
#endif
