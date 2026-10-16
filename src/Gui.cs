using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Tiny immediate-mode widget kit on top of IMGUI (no layout groups, no options arrays).
    public static class G
    {
        public static readonly Color Bg = new Color(0.055f, 0.06f, 0.07f, 0.97f);
        public static readonly Color Panel = new Color(0.09f, 0.10f, 0.115f, 1f);
        public static readonly Color Row = new Color(0.125f, 0.14f, 0.155f, 1f);
        public static readonly Color RowHover = new Color(0.17f, 0.19f, 0.21f, 1f);
        public static readonly Color[] Palette =
        {
            new Color(0.2f, 1f, 0.2f), Color.white, new Color(1f, 0.2f, 0.2f), new Color(0.2f, 0.9f, 1f),
            new Color(1f, 0.9f, 0.2f), new Color(1f, 0.3f, 0.9f), new Color(1f, 0.55f, 0.1f), new Color(0.3f, 0.5f, 1f)
        };
        public static readonly Color[] Themes =
        {
            new Color(0.24f, 1f, 0.3f), new Color(0.3f, 0.6f, 1f), new Color(0.7f, 0.45f, 1f), new Color(1f, 0.4f, 0.8f),
            new Color(1f, 0.6f, 0.2f), new Color(1f, 0.3f, 0.3f), new Color(0.3f, 0.95f, 0.95f)
        };
        public static Color Accent
        {
            get
            {
                if (!(Cfg.BvOn.Value && Cfg.ThemeOn.Value)) return Themes[0];
                int i = Cfg.ThemeIdx.Value;
                if (i >= Themes.Length) return Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.15f, 1f), 0.75f, 1f);
                return Themes[Mathf.Clamp(i, 0, Themes.Length - 1)];
            }
        }
        public static Color Dark { get { var a = Accent; return new Color(a.r * 0.5f, a.g * 0.5f, a.b * 0.5f, 1f); } }
        public static readonly Color Text = new Color(0.93f, 0.95f, 0.96f, 1f);
        public static readonly Color Dim = new Color(0.60f, 0.64f, 0.67f, 1f);
        public static readonly Color Off = new Color(0.30f, 0.33f, 0.36f, 1f);

        static GUIStyle _label, _empty;
        static string _drag;
        public static bool Changed, Released;

        static void Ensure()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip };
            _empty = new GUIStyle();
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Label(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false, bool wrap = false)
        {
            Ensure();
            _label.fontSize = size; _label.alignment = a; _label.wordWrap = wrap;
            _label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            _label.normal.textColor = c;
            GUI.Label(r, s, _label);
        }

        static GUIStyle _field;

        public static string TextField(string id, Rect r, string v, string placeholder, int maxLen = 64)
        {
            Ensure();
            if (_field == null)
            {
                _field = new GUIStyle(GUI.skin.textField);
                _field.normal.background = null; _field.hover.background = null;
                _field.focused.background = null; _field.active.background = null;
                _field.normal.textColor = Text; _field.focused.textColor = Text; _field.hover.textColor = Text;
                _field.fontSize = 16; _field.alignment = TextAnchor.MiddleLeft;
            }
            var inner = new Rect(r.x + 12, r.y, r.width - 24, r.height);
            GUI.SetNextControlName(id);
            var res = GUI.TextField(inner, v, maxLen, _field);
            if (string.IsNullOrEmpty(res) && GUI.GetNameOfFocusedControl() != id)
                Label(inner, placeholder, 15, Dim, TextAnchor.MiddleLeft, true);
            return res;
        }

        public static bool Check(Rect r, bool v)
        {
            Fill(r, Row);
            Fill(new Rect(r.x, r.y, r.width, 2), v ? Accent : Off);
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), v ? Accent : Off);
            Fill(new Rect(r.x, r.y, 2, r.height), v ? Accent : Off);
            Fill(new Rect(r.xMax - 2, r.y, 2, r.height), v ? Accent : Off);
            if (v)
            {
                float[,] px = { {8,16},{11,19},{14,22},{17,19},{20,16},{23,13},{26,10} };
                for (int i = 0; i < px.GetLength(0); i++)
                    Fill(new Rect(r.x + px[i, 0] - 2, r.y + px[i, 1] - 3, 5, 5), Accent);
            }
            if (Click(r)) { Changed = true; Released = true; return !v; }
            return v;
        }

        public static bool Hover(Rect r) => r.Contains(Event.current.mousePosition);

        public static bool Click(Rect r)
        {
            Ensure();
            return GUI.Button(r, "", _empty);
        }

        public static bool Button(Rect r, string text, Color? accent = null, bool selected = false)
        {
            var bg = selected ? RowHover : (Hover(r) ? RowHover : Row);
            Fill(r, bg);
            if (selected || accent.HasValue) Fill(new Rect(r.x, r.y, 3, r.height), accent ?? Accent);
            Label(r, text, 15, Text, TextAnchor.MiddleCenter, true);
            return Click(r);
        }

        public static bool Toggle(Rect r, bool v)
        {
            var w = 44f; var h = 22f;
            var t = new Rect(r.x, r.y + (r.height - h) / 2f, w, h);
            Fill(t, v ? Dark : Off);
            var knob = new Rect(v ? t.x + w - h + 3 : t.x + 3, t.y + 3, h - 6, h - 6);
            Fill(knob, v ? Accent : new Color(0.75f, 0.78f, 0.8f, 1f));
            if (Click(t)) { Changed = true; Released = true; return !v; }
            return v;
        }

        public static int Segmented(Rect r, string[] opts, int sel)
        {
            var w = r.width / opts.Length;
            for (int i = 0; i < opts.Length; i++)
            {
                var b = new Rect(r.x + i * w, r.y, w - 2, r.height);
                Fill(b, i == sel ? Dark : (Hover(b) ? RowHover : Row));
                Label(b, opts[i], 13, i == sel ? Text : Dim, TextAnchor.MiddleCenter, i == sel);
                if (Click(b) && i != sel) { sel = i; Changed = true; Released = true; }
            }
            return sel;
        }

        public static float Slider(string id, Rect r, float v, float min, float max, string fmt = "0.#")
        {
            var valW = 56f;
            var track = new Rect(r.x, r.y + r.height / 2f - 3, r.width - valW - 8, 6);
            var hit = new Rect(track.x - 6, r.y, track.width + 12, r.height);
            var e = Event.current;

            if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _drag = id; e.Use(); }
            if (_drag == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                var t = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                var nv = min + t * (max - min);
                if (Mathf.Abs(nv - v) > 1e-4f) { v = nv; Changed = true; }
                e.Use();
            }
            if (_drag == id && e.type == EventType.MouseUp) { _drag = null; Released = true; e.Use(); }

            var f = Mathf.Clamp01((v - min) / (max - min));
            Fill(track, Off);
            Fill(new Rect(track.x, track.y, track.width * f, track.height), Accent);
            var knob = new Rect(track.x + track.width * f - 7, r.y + r.height / 2f - 9, 14, 18);
            Fill(knob, _drag == id || Hover(hit) ? Color.white : new Color(0.85f, 0.88f, 0.9f, 1f));
            Label(new Rect(r.xMax - valW, r.y, valW, r.height), v.ToString(fmt), 14, Text, TextAnchor.MiddleRight);
            return v;
        }
    }
}
