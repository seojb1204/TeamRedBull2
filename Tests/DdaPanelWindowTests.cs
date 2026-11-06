// Standalone geometry harness; intentionally does not reference game code or DB settings.
using System;
using ReversalTower;

namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float xMax => x + width;
        public float yMax => y + height;
        public bool Contains(Vector2 p) => p.x >= x && p.x < xMax && p.y >= y && p.y < yMax;
    }
    public static class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
    }
}

static class DdaPanelWindowTests
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static void Inside(UnityEngine.Rect r, float w, float h)
        => Check(r.x >= 0 && r.y >= 0 && r.xMax <= w + .01f && r.yMax <= h + .01f && r.width > 0 && r.height > 0, "screen bounds");

    static void Main()
    {
        var window = new DdaPanelWindow();
        window.Resolve(1600, 900, 600, 500, 170);
        var original = window.Bounds;
        Check(window.ConsumesPointer(new UnityEngine.Vector2(original.x + 100, original.y + 100)), "panel shields mouse before OnGUI drag starts");
        Check(!window.ConsumesPointer(new UnityEngine.Vector2(0, 0)), "outside panel allows mouse combat");
        Check(!window.Begin(new UnityEngine.Vector2(original.x + 100, original.y + 100)), "body is not a handle");
        Check(window.Begin(new UnityEngine.Vector2(original.x + 2, original.yMax - 2)), "bottom-left handle");
        window.Drag(new UnityEngine.Vector2(original.x - 198, original.yMax + 98));
        Check(window.Bounds.width == 800 && window.Bounds.height == 600 && window.Bounds.xMax == original.xMax, "left expands while right stays anchored");
        Check(window.ConsumesPointer(new UnityEngine.Vector2(0, 0)), "captured drag shields mouse even outside panel");
        window.End();
        Check(!window.ConsumesPointer(new UnityEngine.Vector2(0, 0)), "mouse combat restored outside after release");
        window.Resolve(1600, 900, 1000, 300, 100);
        Check(window.Bounds.width == 800 && window.Bounds.height == 600, "custom size survives content/F9/mode changes");
        window.Reset(); window.Resolve(1600, 900, 600, 500, 170);
        Check(window.Bounds.width == original.width && window.Bounds.height == original.height && window.Bounds.x == original.x, "F8 reopen resets default");

        foreach (var size in new[] { new UnityEngine.Vector2(1920,1080), new UnityEngine.Vector2(1280,720),
            new UnityEngine.Vector2(800,600), new UnityEngine.Vector2(600,1000), new UnityEngine.Vector2(2560,720),
            new UnityEngine.Vector2(160,90), new UnityEngine.Vector2(1,1) })
        {
            // Resolve receives GUI units after the same bounded scale as the panel.
            float scale = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.Min(size.x/1600, size.y/900), .85f, 1.15f);
            float w = size.x/scale, h = size.y/scale;
            for (int corner = 0; corner < 4; corner++)
            {
                window.Reset(); window.Resolve(w, h, 1000, 800, 170);
                Inside(window.Bounds, w, h);
                var r = window.Bounds;
                float inset = Math.Min(1, Math.Min(r.width, r.height)/4);
                bool left = (corner & 1) == 0, top = (corner & 2) == 0;
                var p = new UnityEngine.Vector2(left ? r.x+inset : r.xMax-inset, top ? r.y+inset : r.yMax-inset);
                Check(window.Begin(p), "corner captures drag");
                window.Drag(new UnityEngine.Vector2(left ? -10000 : 10000, top ? -10000 : 10000));
                Inside(window.Bounds, w, h);
                window.Drag(new UnityEngine.Vector2(left ? 10000 : -10000, top ? 10000 : -10000));
                Inside(window.Bounds, w, h);
                Check(window.Bounds.width >= Math.Min(320, w/2)-.01f && window.Bounds.height >= Math.Min(160,h/2)-.01f, "minimum size");
                window.Resolve(w/2, h/2, 1000, 800, 170);
                Check(!window.Dragging, "resolution change cancels stale drag");
                Inside(window.Bounds, w/2, h/2);
            }
        }
        Console.WriteLine("DDA_PANEL_WINDOW_TESTS_PASSED " + checks + " assertions; Unity GUI/input not exercised.");
    }
}
