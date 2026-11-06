using UnityEngine;

namespace ReversalTower
{
    // View geometry only. No game, model, save or input polling dependencies.
    internal sealed class DdaPanelWindow
    {
        public const float Handle = 18;
        public Rect Bounds { get; private set; }
        public bool Ready { get; private set; }
        public bool Dragging => corner != 0;
        bool custom;
        int corner;
        float screenWidth, screenHeight, margin;
        Rect start;
        Vector2 pointer;

        public void Reset() { Ready = custom = false; End(); }
        public void End() { corner = 0; }

        public void Resolve(float width, float height, float defaultWidth, float defaultHeight, float defaultY)
        {
            if (width != screenWidth || height != screenHeight) End();
            screenWidth = Mathf.Max(.001f, width); screenHeight = Mathf.Max(.001f, height);
            margin = Mathf.Min(8, Mathf.Min(screenWidth, screenHeight) / 4);
            Rect r = Ready && custom ? Bounds : new Rect(screenWidth - defaultWidth - margin,
                defaultY, defaultWidth, defaultHeight);
            r.width = Mathf.Clamp(r.width, Mathf.Min(320, screenWidth - margin * 2), screenWidth - margin * 2);
            r.height = Mathf.Clamp(r.height, Mathf.Min(160, screenHeight - margin * 2), screenHeight - margin * 2);
            r.x = Mathf.Clamp(r.x, margin, screenWidth - margin - r.width);
            r.y = Mathf.Clamp(r.y, margin, screenHeight - margin - r.height);
            Bounds = r; Ready = true;
        }

        public bool Begin(Vector2 at)
        {
            if (!Ready || !Bounds.Contains(at)) return false;
            bool left = at.x < Bounds.x + Handle, right = at.x >= Bounds.xMax - Handle;
            bool top = at.y < Bounds.y + Handle, bottom = at.y >= Bounds.yMax - Handle;
            if ((!left && !right) || (!top && !bottom)) return false;
            corner = (left ? 1 : 2) | (top ? 4 : 8);
            start = Bounds; pointer = at; custom = true;
            return true;
        }

        public void Drag(Vector2 at)
        {
            if (!Dragging) return;
            float minW = Mathf.Min(320, screenWidth - margin * 2), minH = Mathf.Min(160, screenHeight - margin * 2);
            float x0 = start.x, x1 = start.xMax, y0 = start.y, y1 = start.yMax;
            if ((corner & 1) != 0) x0 = Mathf.Clamp(start.x + at.x - pointer.x, margin, x1 - minW);
            else x1 = Mathf.Clamp(start.xMax + at.x - pointer.x, x0 + minW, screenWidth - margin);
            if ((corner & 4) != 0) y0 = Mathf.Clamp(start.y + at.y - pointer.y, margin, y1 - minH);
            else y1 = Mathf.Clamp(start.yMax + at.y - pointer.y, y0 + minH, screenHeight - margin);
            Bounds = new Rect(x0, y0, x1 - x0, y1 - y0);
        }
    }
}
