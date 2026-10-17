using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    class FixLog { public string Prefix; public string[] Fixes; public FixLog(string p, params string[] f) { Prefix = p; Fixes = f; } }

    public static partial class Window
    {
        // A mod's version is its prefix plus the number of fixes below, so the patch number is literally "how many fixes it got".
        static readonly Dictionary<string, FixLog> FixLogs = new Dictionary<string, FixLog>
        {
            { "Clip Maker", new FixLog("v1.0.",
                "Clips now record build parts (walls, floors, stairs)",
                "Map boxes are only drawn when no real map is loaded",
                "Esc no longer glitches the pause menu, Backspace leaves the viewer",
                "Replay players use the game's own model and size",
                "Clips recorded before parts were saved are detected",
                "Walls now show up in the replay map",
                "Building parts are visible in the replay viewer",
                "Walls load with the map instead of staying missing",
                "The mouse unlocks while the viewer is open",
                "Save, record and leave-viewer keys can be rebound") },
            { "Damage Numbers", new FixLog("v1.0.",
                "Numbers come from the game's own hit event instead of polling",
                "Waits for the health drop and checks who owns the target",
                "Falls back to the last attacker when the hit owner is unclear") },
            { "Better Visuals", new FixLog("v1.0.",
                "Rainbow guns tint the weapon camera's layer",
                "Rainbow text keeps its live alpha, so chat is visible again",
                "The HUD cleaner never hides the Integrity (health) bar",
                "The Integrity label now shows the health number") },
            { "RTX Mode", new FixLog("v1.0.", "Bloom works: the effect volume sits on the camera's layer") },
            { "Freecam (Team)", new FixLog("v1.0.",
                "Only follows your own team",
                "Fairness rules: dead or no other humans, and it cannot pass walls",
                "Cannot leave your team's area",
                "Toggle and target keys can be rebound") },
            { "Menu Music Player", new FixLog("v1.0.",
                "The game's menu music is muted while your track plays",
                "The Open Folder button works",
                "Streams about 10 seconds at a time instead of one 83 MB array") },
            { "Hit Sounds", new FixLog("v1.0.", "Custom sounds decode with a size cap, so they cannot use much memory") },
            { "Session Stats", new FixLog("v1.0.", "The leaderboard updates at the end of each round", "The overlay key can be rebound") },
            { "Aim Trainer", new FixLog("v1.1.", "The start / stop key can be rebound") },
            { "Chat Macros", new FixLog("v1.0.", "Sends through the game's own chat call", "Each macro key can be rebound") },
            { "Screenshot Key", new FixLog("v1.0.", "The screenshot key can be rebound") },
            { "Crosshair Profiles", new FixLog("v1.0.", "The next-profile key can be rebound") },
            { "Build Trainer", new FixLog("v1.0.", "The start / stop key can be rebound") },
            { "Pause Helper", new FixLog("v1.0.", "All five shortcut keys can be rebound") },
        };

        static void ApplyFixLogs()
        {
            foreach (var kv in FixLogs)
            {
                var m = _mods.Find(x => x.Name == kv.Key);
                if (m == null) continue;
                m.Fixes = kv.Value.Fixes;
                m.Version = kv.Value.Prefix + kv.Value.Fixes.Length;
            }
        }

        // ---------- keybind widgets ----------

        static void KeyButton(Rect b, ConfigEntry<string> e, string def)
        {
            string id = e.Definition.Section + "." + e.Definition.Key;
            bool listening = Features.BindIsListening(id);
            var gold = new Color(1f, 0.82f, 0.2f);
            G.Fill(b, listening ? new Color(0.36f, 0.29f, 0.04f, 1f) : (G.Hover(b) ? G.RowHover : G.Row));
            Outline(b, listening ? gold : Border);
            G.Label(b, listening ? "PRESS A KEY..." : Features.BindLabel(e.Value), 13, listening ? gold : G.Text, TextAnchor.MiddleCenter, true);
            if (G.Click(b)) { if (listening) Features.BindCancel(); else Features.BindBegin(id, e, def); }
        }

        static void KeyRow(string label, ConfigEntry<string> e, string def)
        {
            var r = Ctl(label);
            var b = new Rect(r.x, r.y, 190, r.height);
            KeyButton(b, e, def);
            if (SmallBtn(new Rect(b.xMax + 8, r.y, 80, r.height), "RESET")) { e.Value = def; Plugin.Instance.Config.Save(); }
            G.Label(new Rect(b.xMax + 98, r.y, 320, r.height), "click, then press a key  (Esc cancels, Del unbinds)", 11, G.Dim, TextAnchor.MiddleLeft);
        }

        // ---------- version history popup (hover the title of a mod's page) ----------

        static void DrawFixPopup()
        {
            if (_view < 0 || _view >= _mods.Count) return;
            var m = _mods[_view];
            if (m.Fixes == null || m.Fixes.Length == 0) return;
            var area = new Rect(52 + 70, 104, MeasureWidth(m.Name + "  " + m.Version, 24) + 100f, 34f);
            if (!G.Hover(area)) return;
            float w = 620f, h = 40f + m.Fixes.Length * 22f;
            var r = new Rect(area.x, 148f, w, h);
            G.Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(0, 0, 0, 0.6f));
            G.Fill(r, new Color(0.07f, 0.09f, 0.11f, 0.98f));
            Outline(r, G.Accent);
            G.Label(new Rect(r.x + 14, r.y + 6, r.width - 28, 22), "WHAT CHANGED   " + m.Version + "   (" + m.Fixes.Length + (m.Fixes.Length == 1 ? " fix" : " fixes") + ")", 12, G.Accent, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < m.Fixes.Length; i++)
                G.Label(new Rect(r.x + 14, r.y + 34 + i * 22, r.width - 28, 22), (i + 1) + ".  " + m.Fixes[i], 12, G.Text, TextAnchor.MiddleLeft);
        }
    }
}
