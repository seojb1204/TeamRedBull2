#if !HEADLESS && !DESKTOP_PREVIEW
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ReversalTower
{
    /// <summary>
    /// 화면 공통 모양: 도트 폰트(Galmuri11, OFL) + 코드로 그린 도트 테두리.
    ///
    /// - 색이나 테두리 모양을 바꾸려면 이 파일의 Look 정의와 Frame 그림 문자열만 고치면
    ///   uGUI 화면(NativeGameUI)과 도움말·설정·기록 창(GameUI.cs, IMGUI)이 함께 바뀐다.
    /// - 손으로 그린 그림으로 바꾸려면 Resources/UI/Frames/&lt;이름&gt;.png (12×12, 가장자리 3칸이 테두리)를 넣는다.
    ///   그 파일이 있으면 코드 그림 대신 그 그림을 쓴다. 이름: Panel, Button, Primary, Selected, Slot, Card, CardHover, BarFrame
    /// - Galmuri11은 11px 격자라 글자 크기를 11의 배수(22·33·44·66·88)로 써야 또렷하다.
    /// </summary>
    public static class UiKit
    {
        public const int Body = 22, Sub = 33, Head = 44, Huge = 66, Giant = 88;

        /// <summary>테두리 그림 1칸이 UI 좌표(1600×900 기준)로 몇 칸인지</summary>
        public const float Texel = 2;
        const int Border = 3;

        public static readonly Color Ink = new Color(.035f, .05f, .075f);
        public static readonly Color Muted = new Color(.62f, .68f, .72f);
        public static readonly Color Soft = new Color(.80f, .83f, .82f);

        public static Font Regular, Bold;
        public static Sprite Panel, Button, Primary, Selected, Slot, Card, CardHover, BarFrame;
        public static Sprite Solid, FadeDown, FadeUp, FadeRight, Pip, Flask;
        public static bool Ready { get; private set; }

        // 테두리 그림. O 바깥선, H 위·왼쪽 밝은 선, F 안쪽, S 아래·오른쪽 그림자, . 투명 (모서리를 깎아 손으로 찍은 느낌)
        static readonly string[] Frame =
        {
            "..OOOOOOOO..",
            ".OHHHHHHHHO.",
            "OHFFFFFFFFSO",
            "OHFFFFFFFFSO",
            "OHFFFFFFFFSO",
            "OHFFFFFFFFSO",
            "OHFFFFFFFFSO",
            "OHFFFFFFFFSO",
            "OHFFFFFFFFSO",
            "OHFFFFFFFFSO",
            ".OSSSSSSSSO.",
            "..OOOOOOOO..",
        };

        struct Look
        {
            public Color o, h, f, s;
            public Look(string o, string h, string f, string s, float fillAlpha = 1)
            {
                this.o = Hex(o); this.h = Hex(h); this.f = Hex(f); this.s = Hex(s);
                this.f.a = fillAlpha;
            }
        }

        static readonly Look PanelLook = new Look("06090e", "2b3846", "101822", "0a0f16", .94f);
        static readonly Look ButtonLook = new Look("06090e", "41566b", "1c2835", "111821");
        static readonly Look PrimaryLook = new Look("1a0f05", "ffdc8f", "dc9c47", "9a652a");
        static readonly Look SelectedLook = new Look("e8b25e", "41566b", "1f2c3a", "111821");
        static readonly Look SlotLook = new Look("06090e", "334455", "0e151d", "080c11", .92f);
        static readonly Look CardLook = new Look("06090e", "5a4a2a", "141c26", "0b1017");
        static readonly Look CardHoverLook = new Look("f0c06a", "ffe2a0", "1b2633", "0b1017");
        static readonly Look BarLook = new Look("06090e", "06090e", "05080c", "05080c");

        public static void Init()
        {
            if (Ready && Panel && Regular) return;   // 도메인 리로드를 끈 Play 모드에서 그림이 지워졌으면 다시 만든다
            Regular = Resources.Load<Font>("UI/Fonts/Galmuri11");
            Bold = Resources.Load<Font>("UI/Fonts/Galmuri11-Bold");
            if (!Regular)
            {
                Debug.LogWarning("[UiKit] Resources/UI/Fonts/Galmuri11.ttf 를 찾지 못해 시스템 글꼴을 씁니다.");
                Regular = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, Body);
            }
            if (!Bold) Bold = Regular;

            Panel = FrameSprite("Panel", PanelLook);
            Button = FrameSprite("Button", ButtonLook);
            Primary = FrameSprite("Primary", PrimaryLook);
            Selected = FrameSprite("Selected", SelectedLook);
            Slot = FrameSprite("Slot", SlotLook);
            Card = FrameSprite("Card", CardLook);
            CardHover = FrameSprite("CardHover", CardHoverLook);
            BarFrame = FrameSprite("BarFrame", BarLook);

            Solid = Gradient(1, 1, (x, y) => 1f, false);
            FadeDown = Gradient(1, 64, (x, y) => Mathf.SmoothStep(0, 1, y), true);    // 위가 진하고 아래로 사라짐
            FadeUp = Gradient(1, 64, (x, y) => Mathf.SmoothStep(0, 1, 1 - y), true);  // 아래가 진하고 위로 사라짐
            FadeRight = Gradient(64, 1, (x, y) => Mathf.SmoothStep(0, 1, 1 - x), true); // 왼쪽이 진하고 오른쪽으로 사라짐

            Pip = Icon(new[]
            {
                "...O...",
                "..OWO..",
                ".OWWWO.",
                "OWWWWWO",
                ".OWWWO.",
                "..OWO..",
                "...O...",
            });
            Flask = Icon(new[]
            {
                "..OOO..",
                "..OWO..",
                "..OWO..",
                ".OWWWO.",
                "OWWWWWO",
                "OWWWWWO",
                "OWWWWWO",
                ".OOOOO.",
            });
            Ready = true;
        }

        // ─────────────── 그림 만들기 ───────────────

        static Sprite FrameSprite(string name, Look look)
        {
            var drawn = Resources.Load<Texture2D>("UI/Frames/" + name);
            if (drawn) { drawn.filterMode = FilterMode.Point; return Sliced(drawn, name); }
            return Sliced(PaintFrame(look, 1), name);
        }

        static Sprite Sliced(Texture2D tex, string name)
        {
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f),
                100f / Texel, 0, SpriteMeshType.FullRect, new Vector4(Border, Border, Border, Border));
            sprite.name = "UiKit " + name;
            return sprite;
        }

        static Texture2D PaintFrame(Look look, int scale)
        {
            int n = Frame.Length;
            var tex = new Texture2D(n * scale, n * scale, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
            var clear = new Color(0, 0, 0, 0);
            for (int row = 0; row < n; row++)
                for (int col = 0; col < n; col++)
                {
                    char c = Frame[row][col];
                    Color color = c == 'O' ? look.o : c == 'H' ? look.h : c == 'F' ? look.f : c == 'S' ? look.s : clear;
                    for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                            tex.SetPixel(col * scale + dx, (n - 1 - row) * scale + dy, color);
                }
            tex.Apply(false, false);
            return tex;
        }

        static Sprite Icon(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
            var outline = new Color(.02f, .03f, .045f, 1);
            for (int row = 0; row < h; row++)
                for (int col = 0; col < w; col++)
                {
                    char c = rows[row][col];
                    tex.SetPixel(col, h - 1 - row, c == 'W' ? Color.white : c == 'O' ? outline : new Color(0, 0, 0, 0));
                }
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f / Texel);
        }

        static Sprite Gradient(int w, int h, System.Func<float, float, float> alpha, bool smooth)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = smooth ? FilterMode.Bilinear : FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha(w == 1 ? 0 : x / (float)(w - 1), h == 1 ? 0 : y / (float)(h - 1))));
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100);
        }

        // ─────────────── IMGUI (도움말·설정·기록 창) ───────────────

        static GUIStyle panelStyle, buttonStyle, buttonHoverStyle, primaryStyle, primaryHoverStyle, disabledStyle;

        /// <summary>IMGUI용 테두리. GUI.matrix가 1600×900 기준이라 그림을 Texel배로 키워서 쓴다</summary>
        public static GUIStyle Imgui(string kind)
        {
            if (panelStyle == null || !panelStyle.normal.background)
            {
                int s = Mathf.RoundToInt(Texel);
                panelStyle = Style(PaintFrame(PanelLook, s), s);
                buttonStyle = Style(PaintFrame(ButtonLook, s), s);
                buttonHoverStyle = Style(PaintFrame(SelectedLook, s), s);
                primaryStyle = Style(PaintFrame(PrimaryLook, s), s);
                primaryHoverStyle = Style(PaintFrame(new Look("1a0f05", "fff0c4", "eeb35f", "a8732f"), s), s);
                disabledStyle = Style(PaintFrame(new Look("06090e", "232d38", "141b23", "0e141a"), s), s);
            }
            switch (kind)
            {
                case "button": return buttonStyle;
                case "buttonHover": return buttonHoverStyle;
                case "primary": return primaryStyle;
                case "primaryHover": return primaryHoverStyle;
                case "disabled": return disabledStyle;
                default: return panelStyle;
            }
        }

        static GUIStyle Style(Texture2D tex, int scale)
        {
            var style = new GUIStyle();
            style.normal.background = tex;
            int b = Border * scale;
            style.border = new RectOffset(b, b, b, b);
            return style;
        }

        /// <summary>도트 폰트가 또렷한 크기(11의 배수)로 맞춘다. 원래 크기에 가장 가까운 단계</summary>
        public static int Snap(int size)
        {
            if (size <= 13) return 11;
            if (size <= 27) return Body;
            if (size <= 38) return Sub;
            if (size <= 55) return Head;
            if (size <= 77) return Huge;
            return Giant;
        }

        // ─────────────── uGUI 배치 도우미 (좌표는 1600×900 기준, 왼쪽 위가 0,0) ───────────────

        public static RectTransform Place(Component c, float x, float y, float w, float h, bool centerPivot = false)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = centerPivot ? new Vector2(.5f, .5f) : new Vector2(0, 1);
            rt.anchoredPosition = centerPivot ? new Vector2(x + w / 2, -(y + h / 2)) : new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            rt.localScale = Vector3.one;
            return rt;
        }

        /// <summary>
        /// 화면(페이지)을 캔버스 가운데 1600×900 칸으로 둔다. CanvasScaler를 Expand로 쓰면
        /// 화면비가 16:9가 아니어도 1600×900이 항상 다 보이고, 남는 쪽은 양옆(또는 위아래)으로 늘어난다.
        /// </summary>
        public static void Center(Component c)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1600, 900);
            rt.localScale = Vector3.one;
        }

        /// <summary>캔버스 바로 밑에 있는 요소를 가운데 1600×900 칸 기준 좌표로 둔다</summary>
        public static void PlaceCentered(Component c, float x, float y, float w, float h)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(x + w / 2 - 800, -(y + h / 2 - 450));
            rt.sizeDelta = new Vector2(w, h);
            rt.localScale = Vector3.one;
        }

        /// <summary>그림자·띠를 좌우로 화면 끝까지 늘린다 (넓은 화면에서도 끊기지 않게)</summary>
        public static void BleedX(Component c, float y, float h, float extra = 2000)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 1);
            rt.anchoredPosition = new Vector2(0, -y);
            rt.sizeDelta = new Vector2(extra * 2, h);
        }

        /// <summary>페이지 뒤에 화면 전체를 덮는 어두운 막을 깐다. block이면 뒤쪽 클릭을 막는다</summary>
        public static Image Dim(Transform page, Color color, bool block)
        {
            var image = Picture(page, "Dim", 0, 0, 10, 10, Solid, color);
            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = new Vector2(-3000, -3000); rt.offsetMax = new Vector2(3000, 3000);
            rt.SetAsFirstSibling();
            image.raycastTarget = block;
            return image;
        }

        public static void Stretch(Component c, float inset = 0)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            rt.localScale = Vector3.one;
        }

        public static Text Label(Transform parent, string name, float x, float y, float w, float h, int size,
            Color color, TextAnchor align = TextAnchor.UpperLeft, bool bold = false, bool shadow = false)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            Place(text, x, y, w, h);
            Dress(text, size, color, align, bold, shadow);
            return text;
        }

        public static void Dress(Text text, int size, Color color, TextAnchor align = TextAnchor.UpperLeft, bool bold = false, bool shadow = false)
        {
            text.font = bold ? Bold : Regular;
            text.fontStyle = FontStyle.Normal;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.lineSpacing = 1;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;   // 줄 높이(글자 크기의 1.33배)보다 칸이 작아도 글자가 사라지지 않게
            var drop = text.GetComponent<Shadow>();
            if (shadow && !drop) drop = text.gameObject.AddComponent<Shadow>();
            if (drop) { drop.enabled = shadow; drop.effectColor = new Color(0, 0, 0, .85f); drop.effectDistance = new Vector2(Texel, -Texel); }
        }

        public static Image Picture(Transform parent, string name, float x, float y, float w, float h, Sprite sprite, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            Place(image, x, y, w, h);
            Paint(image, sprite, color);
            return image;
        }

        public static void Paint(Image image, Sprite sprite, Color color)
        {
            image.sprite = sprite;
            image.color = color;
            image.type = sprite && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1;
            image.raycastTarget = false;
        }

        /// <summary>버튼 모양. 그림은 밝은 상태로 그려 두고, 평소엔 살짝 어둡게, 마우스를 올리면 원래 밝기로</summary>
        public static void Skin(UnityEngine.UI.Button button, Sprite look)
        {
            var image = button.GetComponent<Image>();
            if (image) { Paint(image, look, Color.white); image.raycastTarget = true; }
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(.86f, .86f, .86f, 1);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.68f, .68f, .68f, 1);
            colors.selectedColor = colors.normalColor;   // 클릭 뒤 밝은 상태로 남지 않게
            colors.disabledColor = new Color(.5f, .5f, .5f, .7f);
            colors.colorMultiplier = 1; colors.fadeDuration = .06f;
            button.colors = colors;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;   // 방향키는 게임 쪽에서 씀
        }

        /// <summary>버튼 글자를 가운데로. 오른쪽에 작은 키 표시(예: Enter)를 붙일 수 있다</summary>
        public static Text Caption(UnityEngine.UI.Button button, string text, int size, Color color, string key = null)
        {
            var label = button.GetComponentInChildren<Text>(true);
            if (!label) label = Label(button.transform, "Label", 0, 0, 10, 10, size, color);
            Stretch(label, 6);
            Dress(label, size, color, TextAnchor.MiddleCenter);
            label.text = text;
            var chip = button.transform.Find("Key");
            Text keyText = chip ? chip.GetComponent<Text>() : null;
            if (!string.IsNullOrEmpty(key))
            {
                if (!keyText) keyText = Label(button.transform, "Key", 0, 0, 10, 10, Body, color);
                var rt = (RectTransform)keyText.transform;
                rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, .5f);
                rt.sizeDelta = new Vector2(96, 0); rt.anchoredPosition = new Vector2(-16, 0);
                Dress(keyText, Body, new Color(color.r, color.g, color.b, .55f), TextAnchor.MiddleRight);
                keyText.text = key;
            }
            else if (keyText) keyText.text = "";
            return label;
        }

        // ─────────────── 글 ───────────────

        public static string Gold(int value) => value.ToString("N0", CultureInfo.InvariantCulture) + " G";

        public static string Clock(float seconds)
        {
            int t = Mathf.Max(0, (int)seconds);
            return (t / 60) + ":" + (t % 60).ToString("00");
        }

        /// <summary>받침에 맞는 조사. Josa("아라쿠스","을","를") → "아라쿠스를"</summary>
        public static string Josa(string word, string withFinal, string withoutFinal)
        {
            if (string.IsNullOrEmpty(word)) return word;
            char last = word[word.Length - 1];
            bool final = last >= 0xAC00 && last <= 0xD7A3 && (last - 0xAC00) % 28 != 0;
            return word + (final ? withFinal : withoutFinal);
        }

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.magenta;
        }

        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.4f, c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
        }
    }
}
#endif
