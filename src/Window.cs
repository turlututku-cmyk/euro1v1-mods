using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        const float W = 940f, H = 700f, SideW = 300f, HeadH = 58f, FootH = 30f;
        static int _sel;
        static float _py, _rx, _rw;

        static readonly string[] Names =
        {
            "Crosshair Changer", "FPS Counter", "Clock", "Screenshot Key", "FPS Limiter",
            "FOV Changer", "Volume Control", "Run in Background", "Installed"
        };
        static readonly string[] Descs =
        {
            "Custom crosshair overlay", "Live FPS and frame time", "Real-time clock on screen", "F12 saves a screenshot",
            "Cap FPS or force VSync", "Change camera field of view", "Master volume slider", "Keep game running unfocused", "Plugins and files"
        };

        static ConfigEntry<bool> Enabled(int i)
        {
            switch (i)
            {
                case 0: return Cfg.CrossOn;
                case 1: return Cfg.FpsOn;
                case 2: return Cfg.ClockOn;
                case 3: return Cfg.ShotOn;
                case 4: return Cfg.LimitOn;
                case 5: return Cfg.FovOn;
                case 6: return Cfg.VolOn;
                case 7: return Cfg.BgOn;
                default: return null;
            }
        }

        class ModInfo
        {
            public string Name, Version, Author, Desc, Glyph;
            public Color Tint;
            public bool Featured;
            public bool IsNew, Joke;
            public DateTime Added;
            public ConfigEntry<bool> On;
            public int Pane = -1; // index into the pane switch, -1 = external plugin
            public string Guid;
            public string[] Fixes = new string[0];
        }

        static readonly System.Collections.Generic.List<ModInfo> _mods = new System.Collections.Generic.List<ModInfo>();
        static int _tab;          // 0 installed, 1 featured, 2 new, 3 download (whole list)
        static int _view = -1;    // -1 = list, otherwise index into _mods
        static string _search = "";
        static bool _sortAz, _onlyOn;
        static int _page;
        static bool _built;

        // Called right after startup so mods that are not installed are switched off before the menu is ever opened.
        public static void DebugSetTab(int t) { _tab = t; _view = -1; _page = 0; _iconSheet = false; }
        public static void DebugOpenMod(string name)
        {
            if (!_built) Build();
            int i = _mods.FindIndex(m => m.Name == name);
            if (i >= 0) { _view = i; _sel = _mods[i].Pane; }
        }

        public static void EnsureBuilt() { if (!_built) Build(); }
        static readonly HashSet<string> _inst = new HashSet<string>();
        static readonly Dictionary<string, float> _installing = new Dictionary<string, float>();
        static string _unConfirm; static float _unUntil;

        static bool IsInstalled(ModInfo m) => m.Pane < 0 || _inst.Contains(m.Name);

        static void SaveInstalled() { Cfg.Installed.Value = string.Join("|", _inst); Plugin.Instance.Config.Save(); }

        static void PumpInstalls()
        {
            if (_installing.Count == 0) return;
            foreach (var name in _installing.Keys.ToList())
            {
                if (Time.unscaledTime - _installing[name] < 0.9f) continue;
                _installing.Remove(name);
                var m = _mods.FirstOrDefault(x => x.Name == name);
                if (m == null) continue;
                _inst.Add(name);
                if (m.On != null) m.On.Value = true;
                SaveInstalled();
                Features.Toast(name + " installed");
            }
        }
        static readonly System.Collections.Generic.List<string> _recent = new System.Collections.Generic.List<string>();

        static DateTime AddedOf(BepInEx.PluginInfo p)
        {
            try { return File.GetCreationTime(p.Location); } catch { return DateTime.MinValue; }
        }

        static void Build()
        {
            _mods.Clear();
            void Add(string n, string d, string g, Color c, bool feat, ConfigEntry<bool> on, int pane, string added = "2026-10-15", string ver = "v1.0.0", bool isNew = false, bool joke = false) =>
                _mods.Add(new ModInfo { Name = n, Desc = d, Glyph = g, Tint = c, Featured = feat, On = on, Pane = pane, Version = ver, Author = "Built-in", IsNew = isNew, Joke = joke, Added = DateTime.Parse(added, System.Globalization.CultureInfo.InvariantCulture) });

            Add("Crosshair Changer", "Custom crosshair overlay with six styles, colors and outline.", "+", new Color(0.2f, 1f, 0.3f), false, Cfg.CrossOn, 0);
            Add("FPS Counter", "Live FPS and frame time in any screen corner.", "F", new Color(1f, 0.75f, 0.15f), false, Cfg.FpsOn, 1);
            Add("Clock", "Real-time clock on screen, 12 or 24 hour.", "C", new Color(0.35f, 0.7f, 1f), false, Cfg.ClockOn, 2);
            Add("Screenshot Key", "Press F12 to save a PNG screenshot.", "S", new Color(1f, 0.4f, 0.7f), false, Cfg.ShotOn, 3);
            Add("FPS Limiter", "Cap the frame rate or force VSync.", "L", new Color(0.7f, 0.5f, 1f), false, Cfg.LimitOn, 4);
            Add("FOV Changer", "Change the main camera field of view.", "V", new Color(1f, 0.55f, 0.25f), false, Cfg.FovOn, 5);
            Add("Volume Control", "Master volume slider on top of game audio.", "A", new Color(0.3f, 0.9f, 0.8f), false, Cfg.VolOn, 6);
            Add("Run in Background", "Keep the game running when unfocused.", "B", new Color(0.6f, 0.7f, 0.8f), false, Cfg.BgOn, 7);
            Add("Better Visuals", "Vignette, neon border, particles, cursor trail, UI theme, rainbow guns, rainbow text and a hide-useless-GUI cleaner. Fully customizable.", "B", new Color(1f, 0.4f, 0.8f), false, Cfg.BvOn, 8, "2026-10-16", "v1.0.2");
            Add("Crosshair Profiles", "Save and switch between named crosshair presets. F9 cycles.", "P", new Color(0.2f, 1f, 0.3f), false, Cfg.ProfOn, 9, "2026-10-16");
            Add("Pause Helper", "Number-key shortcuts for the pause menu buttons.", "H", new Color(1f, 0.75f, 0.15f), false, Cfg.PauseOn, 10, "2026-10-16");
            Add("Menu Music Player", "Play your own mp3, wav or ogg tracks from the Music folder.", "M", new Color(0.35f, 0.7f, 1f), false, Cfg.MusicOn, 11, "2026-10-16");
            Add("RTX Mode", "Max render quality with bloom, tonemapping and color grading. Not real ray tracing.", "R", new Color(0.3f, 0.95f, 0.95f), false, Cfg.RtxOn, 12, "2026-10-16");
            Add("Freecam (Team)", "Fair freecam: orbit your teammates only, never leave your team. F6 toggles.", "F", new Color(0.7f, 0.5f, 1f), false, Cfg.FreeOn, 13, "2026-10-16");
            Add("Input Display", "On-screen keys and mouse buttons you press. Great for clips.", "I", new Color(0.35f, 0.7f, 1f), false, Cfg.InputOn, 14, "2026-10-16");
            Add("Hit Sounds", "Custom hit and kill sounds, or your own files.", "S", new Color(1f, 0.75f, 0.15f), false, Cfg.HitOn, 15, "2026-10-16");
            Add("Damage Numbers", "Floating damage numbers when you hit enemies.", "D", new Color(1f, 0.4f, 0.4f), true, Cfg.DmgOn, 16, "2026-10-16");
            Add("Clip Maker", "Records the last minutes of a match and replays it in 3D with a timeline and camera path.", "K", new Color(1f, 0.55f, 0.25f), false, Cfg.ClipOn, 17, "2026-10-16");
            Add("Match Stats", "Your damage, kills, deaths, K/D, accuracy and headshots on screen.", "M", new Color(1f, 0.75f, 0.15f), false, Cfg.StatsOn, 18, "2026-10-17");
            Add("Letterbox", "Cinematic black bars for clips and screenshots.", "L", new Color(0.7f, 0.7f, 0.75f), false, Cfg.BarsOn, 19, "2026-10-17");
            Add("Screen Tint", "A soft color wash: warm, cool, sepia, night and more.", "T", new Color(1f, 0.6f, 0.3f), false, Cfg.TintOn, 20, "2026-10-17");
            Add("FPS Graph", "Live frame-time graph to spot stutter.", "G", new Color(0.3f, 0.95f, 0.5f), false, Cfg.GraphOn, 21, "2026-10-17");
            Add("Notes", "Pin your own reminders on screen.", "N", new Color(0.35f, 0.7f, 1f), false, Cfg.NotesOn, 22, "2026-10-17");
            Add("Settings Backup", "Back up and restore all your Mods menu settings.", "B", new Color(0.7f, 0.5f, 1f), false, Cfg.BackupOn, 23, "2026-10-17");
            Add("Kill Messages", "Kill feed for you: You killed X, You assisted Y for X, You were pumped, sniped or shot by Z.", "K", new Color(1f, 0.4f, 0.4f), false, Cfg.KmOn, 24, "2026-10-17");
            Add("Announcer", "Double, triple and quad kills, sprees and headshots with big text and a stinger sound.", "A", new Color(1f, 0.75f, 0.15f), false, Cfg.AnnOn, 25, "2026-10-17", joke: true);
            Add("Kill Confetti", "A burst of confetti every time you get a kill.", "C", new Color(1f, 0.4f, 0.8f), false, Cfg.ConfOn, 26, "2026-10-17", joke: true);
            Add("Session Stats", "Most-kills leaderboard, K/D, assists, damage and streaks for the session.", "S", new Color(0.35f, 0.7f, 1f), false, Cfg.SsOn, 27, "2026-10-17");
            Add("Aim Trainer", "Six aim drills and a warm-up routine, with scores, tips and your progress over time.", "T", new Color(0.3f, 0.95f, 0.95f), false, Cfg.AimOn, 28, "2026-10-17", "v1.1.0");
            Add("Low Health Warning", "Pulsing red screen edges, a heartbeat and a warning text when your health is low.", "H", new Color(1f, 0.3f, 0.3f), false, Cfg.LhOn, 29, "2026-10-17");
            Add("Death Taunts", "A big joke line on your screen whenever you die: cheeky, wholesome or roast.", "D", new Color(1f, 0.55f, 0.2f), false, Cfg.DtOn, 30, "2026-10-17", joke: true);
            Add("Sad Trombone", "Miss five shots in a row and a sad trombone plays.", "T", new Color(0.6f, 0.7f, 1f), false, Cfg.StOn, 31, "2026-10-17", joke: true);
            Add("Scoreboard Screenshot", "When a match ends, shows a result card and saves a PNG of the final scoreboard.", "S", new Color(0.3f, 0.9f, 0.8f), false, Cfg.SbOn, 32, "2026-10-17");
            Add("Chat Macros", "Alt + number sends a quick chat message you set up, like gg or need help.", "C", new Color(0.35f, 0.7f, 1f), false, Cfg.CmOn, 33, "2026-10-17");
            Add("Performance Coach", "Turns your own stats into tips after every round and match. Live accuracy strip.", "P", new Color(0.3f, 0.95f, 0.5f), false, Cfg.CoachOn, 34, "2026-10-17");
            Add("Chat Filter", "Hides spam, swearing and players you block in the game chat.", "F", new Color(1f, 0.5f, 0.35f), false, Cfg.CfOn, 35, "2026-10-17");
            Add("Build Trainer", "Timed building drills (walls, floors, stairs) in a bot match with best scores.", "B", new Color(1f, 0.7f, 0.2f), false, Cfg.BtOn, 36, "2026-10-17");
            Add("Practice Launcher", "One click starts a bot match: Easy, Normal, Hard or Expert. No menu hunting.", "P", new Color(0.4f, 0.75f, 1f), false, Cfg.PlOn, 37, "2026-10-17");
            Add("Match Found Alert", "Chime and taskbar flash when your queue pops while you are alt-tabbed. Queue timer.", "M", new Color(0.35f, 0.9f, 0.7f), false, Cfg.MaOn, 38, "2026-10-17");
            Add("Team Panel", "Your teammates' health, weapon, distance and a direction arrow. Your team only.", "T", new Color(0.35f, 0.9f, 0.55f), false, Cfg.TpOn, 39, "2026-10-17", isNew: true);
            Add("Combat Log", "Live feed of damage you take and deal, healing, and a summary when a fight ends.", "C", new Color(1f, 0.45f, 0.4f), false, Cfg.ClOn, 40, "2026-10-17", isNew: true);
            _inst.Clear();
            foreach (var n in Cfg.Installed.Value.Split(new[] { (char)124 }, StringSplitOptions.RemoveEmptyEntries)) _inst.Add(n);
            foreach (var old in new[] { "Screen Vignette", "Neon Border", "Cursor Trail", "Particles", "UI Theme" })
                if (_inst.Remove(old)) { _inst.Add("Better Visuals"); Cfg.BvOn.Value = true; SaveInstalled(); }
            foreach (var m in _mods) if (m.Pane >= 0 && !_inst.Contains(m.Name) && m.On != null) m.On.Value = false;
            try
            {
                foreach (var p in IL2CPPChainloader.Instance.Plugins.Values)
                {
                    if (p.Metadata.GUID == "local.euro3v3.modsmenu") continue;
                    _mods.Add(new ModInfo
                    {
                        Name = p.Metadata.Name, Desc = "BepInEx plugin (" + p.Metadata.GUID + ")", Glyph = "P",
                        Tint = new Color(0.55f, 0.6f, 0.65f), Featured = false, On = null, Pane = -1, Version = "", Author = "External", Guid = p.Metadata.GUID, Added = AddedOf(p)
                    });
                }
            }
            catch { }
            ApplyFixLogs();
            _built = true;
        }

        static System.Collections.Generic.List<ModInfo> Filtered()
        {
            IEnumerable<ModInfo> q = _mods;
            if (_tab == 0) q = q.Where(IsInstalled);
            if (_tab == 1) q = q.Where(m => m.Featured).OrderBy(m => IsInstalled(m) ? 1 : 0).ThenByDescending(m => m.Added);
            if (_tab == 2) q = q.Where(m => m.IsNew).OrderByDescending(m => m.Added).ThenByDescending(m => _mods.IndexOf(m));
            if (_tab == 3) q = q.Where(m => m.Pane >= 0).OrderBy(m => IsInstalled(m) ? 1 : 0).ThenByDescending(m => m.Featured).ThenByDescending(m => m.IsNew).ThenBy(m => m.Name);
            if (_onlyOn) q = q.Where(m => m.On != null && m.On.Value);
            if (!string.IsNullOrWhiteSpace(_search))
            {
                var s = _search.Trim().ToLowerInvariant();
                q = q.Where(m => m.Name.ToLowerInvariant().Contains(s) || m.Desc.ToLowerInvariant().Contains(s));
            }
            if (_sortAz) q = q.OrderBy(m => m.Name);
            return q.ToList();
        }

        static readonly Color Border = new Color(0.20f, 0.23f, 0.26f, 1f);
        static Color GreenDark => G.Dark;

        static void Outline(Rect r, Color c, float t = 1f)
        {
            G.Fill(new Rect(r.x, r.y, r.width, t), c);
            G.Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            G.Fill(new Rect(r.x, r.y, t, r.height), c);
            G.Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        static void Chip(Rect r, string text, Color fg, Color bg)
        {
            G.Fill(r, bg);
            G.Label(r, text, 11, fg, TextAnchor.MiddleCenter, true);
        }

        public static void Draw()
        {
            if (!Features.PanelOpen) return;
            if (!_built) Build();
            G.Changed = false; G.Released = false;
            PumpInstalls();

            G.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.62f));

            float s = Mathf.Min(1.5f, Screen.height * 0.92f / H, Screen.width * 0.96f / W);
            float ox = (Screen.width - W * s) / 2f, oy = (Screen.height - H * s) / 2f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(ox, oy, 0), Quaternion.identity, new Vector3(s, s, 1));

            // window
            G.Fill(new Rect(-2, -2, W + 4, H + 4), new Color(0, 0, 0, 0.5f));
            G.Fill(new Rect(0, 0, W, H), G.Bg);
            Outline(new Rect(0, 0, W, H), Border);
            G.Fill(new Rect(0, 0, W, 3), G.Accent);

            // top bar
            G.Fill(new Rect(0, 3, W, 66), G.Panel);
            G.Fill(new Rect(0, 68, W, 1), Border);
            var back = new Rect(16, 14, 44, 44);
            G.Fill(back, G.Hover(back) ? G.RowHover : G.Row);
            Outline(back, G.Accent);
            G.Label(back, "<", 20, G.Accent, TextAnchor.MiddleCenter, true);
            if (G.Click(back))
            {
                if (_view >= 0) _view = -1; else Features.PanelOpen = false;
            }

            int nInst = _mods.Count(IsInstalled), nNew = _mods.Count(m => m.IsNew);
            int nAll = _mods.Count(m => m.Pane >= 0);
            string[] tabs = { "INSTALLED  (" + nInst + ")", "FEATURED", "NEW  (" + nNew + ")", "DOWNLOAD  (" + nAll + ")" };
            float tx = 76f, tw = 170f;
            for (int i = 0; i < tabs.Length; i++)
            {
                var r = new Rect(tx + i * (tw + 6), 14, tw, 44);
                bool sel = _view < 0 && i == _tab;
                var gold = new Color(1f, 0.82f, 0.2f);
                bool goldTab = i == 1;
                G.Fill(r, sel ? (goldTab ? new Color(0.36f, 0.29f, 0.04f, 1f) : GreenDark) : (G.Hover(r) ? G.RowHover : G.Row));
                G.Fill(new Rect(r.x, r.yMax - 3, r.width, 3), sel ? (goldTab ? gold : G.Accent) : (goldTab ? new Color(gold.r, gold.g, gold.b, 0.35f) : new Color(0, 0, 0, 0.25f)));
                G.Label(r, tabs[i], 14, sel ? G.Text : (goldTab ? gold : G.Dim), TextAnchor.MiddleCenter, true);
                if (G.Click(r)) { _tab = i; _view = -1; _page = 0; _iconSheet = false; }
            }

            if (_view < 0) DrawList(); else DrawDetail();
            if (_view >= 0) DrawFixPopup();

            // footer
            G.Fill(new Rect(0, H - FootH, W, FootH), G.Panel);
            G.Fill(new Rect(0, H - FootH, W, 1), Border);
            string hint = Features.BindLabel(Cfg.KbMenu.Value) + " open/close";
            if (Cfg.ShotOn.Value) hint += "     " + Features.BindLabel(Cfg.KbShot.Value) + " screenshot";
            G.Label(new Rect(16, H - FootH, 300, FootH), hint, 12, G.Dim);
            G.Label(new Rect(330, H - FootH, 330, FootH), "settings: BepInEx/config/local.euro3v3.modsmenu.cfg", 12, G.Dim);
            G.Label(new Rect(W - 300, H - FootH, 100, FootH), "Menu key", 12, G.Dim, TextAnchor.MiddleRight);
            KeyButton(new Rect(W - 192, H - FootH + 4, 176, FootH - 8), Cfg.KbMenu, "F8");

            GUI.matrix = Matrix4x4.identity;
            if (G.Released)
            {
                Plugin.Instance.Config.Save();
            }
        }

        static void DrawList()
        {
            if (_iconSheet) { DrawIconSheet(); return; }
            var list = Filtered();
            const float rowH = 54f, gap = 4f, top = 132f;
            int pageSize = Mathf.Max(1, (int)((H - FootH - 56f - top) / (rowH + gap)));
            int pages = Mathf.Max(1, (list.Count + pageSize - 1) / pageSize);
            _page = Mathf.Clamp(_page, 0, pages - 1);

            // search row
            var box = new Rect(20, 82, W - 40 - 3 * 52 - 12, 36);
            G.Fill(box, G.Panel);
            Outline(box, Border);
            _search = G.TextField("modsearch", box, _search, _tab == 3 ? "Search all mods to download..." : "Search mods...");
            if (_search.Length > 40) _search = _search.Substring(0, 40);
            var b1 = new Rect(box.xMax + 12, 82, 48, 36);
            var b2 = new Rect(b1.xMax + 4, 82, 48, 36);
            var b3 = new Rect(b2.xMax + 4, 82, 48, 36);
            G.Fill(b1, _sortAz ? GreenDark : (G.Hover(b1) ? G.RowHover : G.Row));
            G.Label(b1, "A-Z", 12, G.Text, TextAnchor.MiddleCenter, true);
            if (G.Click(b1)) _sortAz = !_sortAz;
            G.Fill(b2, _onlyOn ? GreenDark : (G.Hover(b2) ? G.RowHover : G.Row));
            G.Label(b2, "ON", 12, G.Text, TextAnchor.MiddleCenter, true);
            if (G.Click(b2)) { _onlyOn = !_onlyOn; _page = 0; }
            G.Fill(b3, G.Hover(b3) ? new Color(0.55f, 0.15f, 0.15f, 1f) : new Color(0.36f, 0.12f, 0.12f, 1f));
            G.Label(b3, "CLEAR", 11, G.Text, TextAnchor.MiddleCenter, true);
            if (G.Click(b3)) { _search = ""; _onlyOn = false; _sortAz = false; _page = 0; }

            // empty states
            if (list.Count == 0)
            {
                string msg = _tab == 0 ? "No mods installed. Get some from the FEATURED tab." : _tab == 1 ? "Nothing here yet."
                    : _tab == 2 ? "No new mods right now."
                    : "No mods match your search.";
                G.Label(new Rect(20, top + 60, W - 40, 40), msg, 16, G.Dim, TextAnchor.MiddleCenter);
            }

            for (int i = 0; i < pageSize; i++)
            {
                int idx = _page * pageSize + i;
                if (idx >= list.Count) break;
                var m = list[idx];
                var r = new Rect(20, top + i * (rowH + gap), W - 40, rowH);
                bool hov = G.Hover(r);
                bool on = m.On != null && m.On.Value;
                var gold = new Color(1f, 0.82f, 0.2f);
                G.Fill(r, m.Featured ? (hov ? new Color(0.22f, 0.19f, 0.09f, 1f) : new Color(0.17f, 0.15f, 0.08f, 1f)) : (hov ? new Color(0.15f, 0.165f, 0.18f, 1f) : G.Row));
                G.Fill(new Rect(r.x, r.y, 3, r.height), m.Featured ? gold : (on ? m.Tint : G.Off));

                // icon tile
                var ic = new Rect(r.x + 14, r.y + 7, 40, 40);
                G.Fill(ic, new Color(m.Tint.r * 0.22f, m.Tint.g * 0.22f, m.Tint.b * 0.22f, 1f));
                ModIcon(ic, m);

                // name, version chip, description
                float nameW = MeasureWidth(m.Name, 17);
                G.Label(new Rect(r.x + 68, r.y + 5, 360, 26), m.Name, 17, m.Featured ? gold : G.Text, TextAnchor.MiddleLeft, true);
                float chipX = r.x + 68 + nameW;
                if (!string.IsNullOrEmpty(m.Version)) { G.Label(new Rect(chipX, r.y + 9, 70, 18), m.Version, 11, G.Dim); chipX += 52f; }
                if (m.Featured) { Chip(new Rect(chipX, r.y + 9, 76, 18), "FEATURED", new Color(0.12f, 0.09f, 0f), gold); chipX += 82f; }
                if (m.IsNew) { Chip(new Rect(chipX, r.y + 9, 40, 18), "NEW", new Color(0.05f, 0.1f, 0.05f), G.Accent); chipX += 46f; }
                if (m.Joke) { G.Laugh(new Rect(chipX, r.y + 8, 20, 20)); chipX += 26f; }
                G.Label(new Rect(r.x + 68, r.y + 29, 520, 20), m.Desc, 12, G.Dim);

                // right side: GET (not installed) or switch + VIEW (installed)
                bool inst = IsInstalled(m);
                var view = new Rect(r.xMax - 92, r.y + 10, 80, 34);
                if (!inst)
                {
                    bool busy = _installing.TryGetValue(m.Name, out var t0);
                    float prog = busy ? Mathf.Clamp01((Time.unscaledTime - t0) / 0.9f) : 0f;
                    var get = new Rect(r.xMax - 132, r.y + 10, 120, 34);
                    G.Fill(get, busy ? G.Row : (G.Hover(get) ? Color.Lerp(G.Dark, G.Accent, 0.55f) : G.Dark));
                    if (busy) G.Fill(new Rect(get.x, get.y, get.width * prog, get.height), G.Dark);
                    Outline(get, G.Accent);
                    G.Label(get, busy ? "INSTALLING..." : "GET", 12, G.Text, TextAnchor.MiddleCenter, true);
                    if (G.Click(get) && !busy) _installing[m.Name] = Time.unscaledTime;
                    Chip(new Rect(get.x - 122, r.y + 17, 110, 20), "NOT INSTALLED", G.Dim, new Color(0.16f, 0.18f, 0.2f, 1f));
                    continue;
                }

                G.Fill(view, G.Hover(view) ? G.RowHover : new Color(0.10f, 0.115f, 0.13f, 1f));
                Outline(view, G.Hover(view) ? G.Accent : Border);
                G.Label(view, "VIEW", 12, G.Text, TextAnchor.MiddleCenter, true);
                if (G.Click(view)) { _view = _mods.IndexOf(m); _sel = m.Pane; }

                if (m.On != null && (_tab == 1 || _tab == 3))
                {
                    Chip(new Rect(view.x - 90, r.y + 17, 76, 20), "INSTALLED", G.Accent, new Color(0.1f, 0.3f, 0.12f, 1f));
                }
                else if (m.On != null)
                {
                    m.On.Value = G.Toggle(new Rect(view.x - 66, r.y, 52, r.height), m.On.Value);
                    Chip(new Rect(view.x - 120, r.y + 17, 46, 20), on ? "ON" : "OFF", on ? G.Accent : G.Dim,
                         on ? new Color(0.1f, 0.3f, 0.12f, 1f) : new Color(0.16f, 0.18f, 0.2f, 1f));
                }
                else
                {
                    Chip(new Rect(view.x - 90, r.y + 17, 76, 20), "EXTERNAL", G.Dim, new Color(0.16f, 0.18f, 0.2f, 1f));
                }
            }

            // bottom bar: pagination + reload
            float by = H - FootH - 46f;
            var prev = new Rect(20, by, 40, 34);
            var next = new Rect(64, by, 40, 34);
            if (G.Button(prev, "<") && _page > 0) _page--;
            if (G.Button(next, ">") && _page < pages - 1) _page++;
            G.Label(new Rect(114, by, 560, 34), "Page " + (_page + 1) + " / " + pages + "     " + list.Count + " mod" + (list.Count == 1 ? "" : "s") + (_tab == 1 ? "     Featured: popular picks, worth downloading" : _tab == 2 ? "     New: just added" : _tab == 3 ? "     Every mod: search, then press GET" : ""), 13, _tab == 1 ? new Color(1f, 0.82f, 0.2f) : G.Dim);
            var reload = new Rect(W - 150, by, 130, 34);
            if (G.Button(reload, "RELOAD LIST")) Build();
        }

        static float MeasureWidth(string s, int size, bool bold = true) => G.TextWidth(s, size, bold) + 10f;

        static void DrawDetail()
        {
            var m = _mods[_view];
            G.Fill(new Rect(20, 86, W - 40, H - FootH - 96), G.Panel);
            _rx = 52; _rw = W - 104; _py = 104;

            var ic = new Rect(_rx, _py, 56, 56);
            G.Fill(ic, new Color(m.Tint.r * 0.25f, m.Tint.g * 0.25f, m.Tint.b * 0.25f, 1f));
            G.Fill(new Rect(ic.x, ic.y, ic.width, 2), m.Tint);
            ModIcon(ic, m);
            G.Label(new Rect(_rx + 70, _py, 500, 32), m.Name + "  " + m.Version, 24, G.Text, TextAnchor.MiddleLeft, true);
            if (m.Joke) G.Laugh(new Rect(_rx + 80 + MeasureWidth(m.Name + "  " + m.Version, 24), _py + 5, 24, 24));
            if (m.Fixes != null && m.Fixes.Length > 0)
                Chip(new Rect(_rx + 70 + MeasureWidth(m.Name + "  " + m.Version, 24) + (m.Joke ? 36f : 6f), _py + 7, 74, 20), m.Fixes.Length + (m.Fixes.Length == 1 ? " FIX" : " FIXES"), new Color(0.04f, 0.09f, 0.14f), new Color(0.45f, 0.75f, 1f));
            G.Label(new Rect(_rx + 70, _py + 31, 400, 40), m.Author + "  -  " + m.Desc, 12, G.Dim, TextAnchor.UpperLeft, false, true);
            if (m.On != null)
            {
                G.Label(new Rect(W - 240, _py, 130, 56), "Enabled", 15, G.Text, TextAnchor.MiddleRight);
                m.On.Value = G.Toggle(new Rect(W - 100, _py, 60, 56), m.On.Value);
            }
            if (m.Pane >= 0)
            {
                bool sure = _unConfirm == m.Name && Time.unscaledTime < _unUntil;
                var ub = new Rect(W - 400, _py + 10, 140, 36);
                G.Fill(ub, sure ? new Color(0.6f, 0.15f, 0.15f, 1f) : (G.Hover(ub) ? G.RowHover : G.Row));
                Outline(ub, sure ? new Color(1f, 0.4f, 0.4f) : Border);
                G.Label(ub, sure ? "SURE?" : "UNINSTALL", 12, G.Text, TextAnchor.MiddleCenter, true);
                if (G.Click(ub))
                {
                    if (sure)
                    {
                        _inst.Remove(m.Name);
                        if (m.On != null) m.On.Value = false;
                        SaveInstalled();
                        Features.Toast(m.Name + " removed (get it again from Featured)");
                        _view = -1; _unConfirm = null;
                        return;
                    }
                    _unConfirm = m.Name; _unUntil = Time.unscaledTime + 3f;
                }
            }
            _py += 76;

            if (m.Pane < 0)
            {
                G.Label(new Rect(_rx, _py, _rw, 80),
                    "This is an external BepInEx plugin" + (m.Guid != null ? " (" + m.Guid + ")" : "") + ". Its settings, if any, are in BepInEx\\config.", 15, G.Dim, TextAnchor.UpperLeft, false, true);
                return;
            }
            switch (m.Pane)
            {
                case 0: Crosshair(); break;
                case 1: Fps(); break;
                case 2: ClockPane(); break;
                case 3: Shot(); break;
                case 4: Limiter(); break;
                case 5: Fov(); break;
                case 6: Volume(); break;
                case 7: Background(); break;
                case 8: BetterVisuals(); break;
                case 9: ProfilesPane(); break;
                case 10: PausePane(); break;
                case 11: MusicPane(); break;
                case 12: RtxPane(); break;
                case 13: FreecamPane(); break;
                case 14: InputPane(); break;
                case 15: HitSoundsPane(); break;
                case 16: DamagePane(); break;
                case 17: ClipPane(); break;
                case 18: StatsPane(); break;
                case 19: BarsPane(); break;
                case 20: TintPane(); break;
                case 21: GraphPane(); break;
                case 22: NotesPane(); break;
                case 23: BackupPane(); break;
                case 24: KmPane(); break;
                case 25: AnnPane(); break;
                case 26: ConfPane(); break;
                case 27: SessionPane(); break;
                case 28: AimPane(); break;
                case 29: LhPane(); break;
                case 30: DtPane(); break;
                case 31: StPane(); break;
                case 32: SbPane(); break;
                case 33: CmPane(); break;
                case 34: CoachPane(); break;
                case 35: CfPane(); break;
                case 36: BtPane(); break;
                case 37: PlPane(); break;
                case 38: MaPane(); break;
                case 39: TpPane(); break;
                case 40: ClPane(); break;
            }
        }

        // ---------- layout helpers ----------

        static void Title(string t, string d)
        {
            G.Label(new Rect(_rx, _py, _rw, 34), t, 24, G.Text, TextAnchor.MiddleLeft, true);
            _py += 34;
            G.Label(new Rect(_rx, _py, _rw, 22), d, 13, G.Dim);
            _py += 34;
        }

        static float _rowH = 36f;

        static Rect Ctl(string label, float h = -1f)
        {
            if (h < 0) h = _rowH;
            G.Label(new Rect(_rx, _py, _rw * 0.30f, h), label, 15, G.Text);
            var r = new Rect(_rx + _rw * 0.32f, _py + 3, _rw * 0.68f, h - 6);
            _py += h + 4;
            return r;
        }

        static void Note(string text, float h = 44f)
        {
            G.Label(new Rect(_rx, _py, _rw, h), text, 13, G.Dim, TextAnchor.UpperLeft, false, true);
            _py += h;
        }

        static void EnableRow(ConfigEntry<bool> e, string label = "Enabled")
        {
            var r = Ctl(label);
            e.Value = G.Toggle(new Rect(r.x, r.y, 60, r.height), e.Value);
        }

        static void Tog(string label, ConfigEntry<bool> e)
        {
            var r = Ctl(label);
            e.Value = G.Toggle(new Rect(r.x, r.y, 60, r.height), e.Value);
        }

        static void TogPair(string la, ConfigEntry<bool> a, string lb, ConfigEntry<bool> b)
        {
            var h = 36f;
            G.Label(new Rect(_rx, _py, _rw * 0.30f, h), la, 15, G.Text);
            a.Value = G.Toggle(new Rect(_rx + _rw * 0.32f, _py, 60, h), a.Value);
            G.Label(new Rect(_rx + _rw * 0.52f, _py, _rw * 0.30f, h), lb, 15, G.Text);
            b.Value = G.Toggle(new Rect(_rx + _rw * 0.84f, _py, 60, h), b.Value);
            _py += h + 4;
        }

        static void Sld(string label, ConfigEntry<float> e, float min, float max, string fmt = "0.#")
        {
            var r = Ctl(label);
            e.Value = G.Slider(label, r, e.Value, min, max, fmt);
        }

        static void Corner(string label, ConfigEntry<int> e)
        {
            var r = Ctl(label);
            e.Value = G.Segmented(r, new[] { "Top-left", "Top-right", "Bottom-left", "Bottom-right" }, e.Value);
        }

        // ---------- Better Visuals pane ----------

        static int _bvTab;

        static void Header(string t)
        {
            G.Fill(new Rect(_rx, _py + 11, _rw, 1), Border);
            G.Label(new Rect(_rx, _py, 560, 22), t, 12, G.Accent, TextAnchor.MiddleLeft, true);
            _py += 26;
        }

        static void PaletteRow(string label, ConfigEntry<int> e, bool blackFirst = false)
        {
            var c = Ctl(label);
            int n = Swatches.Length + (blackFirst ? 1 : 0);
            float sw = c.width / n;
            for (int i = 0; i < n; i++)
            {
                var b = new Rect(c.x + i * sw, c.y, sw - 6, c.height);
                var col = (blackFirst && i == 0) ? Color.black : Swatches[i - (blackFirst ? 1 : 0)];
                bool cur = e.Value == i;
                G.Fill(new Rect(b.x - 2, b.y - 2, b.width + 4, b.height + 4), cur ? Color.white : new Color(0.25f, 0.28f, 0.3f, 1f));
                G.Fill(b, col);
                if (G.Click(b)) { e.Value = i; G.Changed = true; G.Released = true; }
            }
        }

        static void BetterVisuals()
        {
            _rowH = 30f;
            var tabs = Ctl("", 34);
            _bvTab = G.Segmented(new Rect(_rx, tabs.y, _rw, tabs.height), new[] { "SCREEN", "EFFECTS", "COLORS", "HUD" }, _bvTab);
            _py += 2;

            if (_bvTab == 3)
            {
                Header("HIDE USELESS GUI");
                Tog("Hide selected", Cfg.HideOn);
                Header("MAIN MENU");
                TogPair("Ads and promos", Cfg.HideAds, "News slideshow", Cfg.HideNews);
                TogPair("Feedback button", Cfg.HideFeedback, "Version text", Cfg.HideVersion);
                Header("PANELS");
                TogPair("Daily tasks", Cfg.HideTasks, "Quest panel", Cfg.HideQuest);
                TogPair("Level and XP bar", Cfg.HideLevel, "Party display", Cfg.HideParty);
                Header("IN MATCH");
                TogPair("Compass and dials", Cfg.HideDials, "ATM info panel", Cfg.HideAtm);
                Tog("In-game level display", Cfg.HideIngameLevel);
                Header("LABELS");
                Tog("Integrity label shows Health + number", Cfg.HpLabelOn);
            }
            else if (_bvTab == 0)
            {
                Header("VIGNETTE");
                Tog("Vignette", Cfg.VigOn);
                Sld("Strength", Cfg.VigStrength, 0.1f, 1f, "0.00");
                PaletteRow("Color", Cfg.VigColor, true);
                Header("NEON BORDER");
                Tog("Neon border", Cfg.NeonOn);
                Tog("Rainbow", Cfg.NeonRainbow);
                Sld("Thickness", Cfg.NeonThick, 2, 16, "0");
                PaletteRow("Color", Cfg.NeonColor);
            }
            else if (_bvTab == 1)
            {
                Header("PARTICLES");
                Tog("Particles", Cfg.PartOn);
                var r = Ctl("Style");
                Cfg.PartStyle.Value = G.Segmented(r, new[] { "Snow", "Stars", "Embers" }, Cfg.PartStyle.Value);
                Sld("Density", Cfg.PartDensity, 10, 300, "0");
                Sld("Speed", Cfg.PartSpeed, 0.2f, 3f, "0.0");
                Tog("Menus only", Cfg.PartMenusOnly);
                Header("CURSOR TRAIL");
                Tog("Cursor trail", Cfg.TrailOn);
                Tog("Rainbow", Cfg.TrailRainbow);
                Sld("Length (s)", Cfg.TrailLife, 0.2f, 2f, "0.0");
                Tog("Menus only", Cfg.TrailMenusOnly);
            }
            else
            {
                Header("UI THEME");
                Tog("Recolor menu", Cfg.ThemeOn);
                var c = Ctl("Accent");
                int n = G.Themes.Length + 1;
                float sw = c.width / n;
                for (int i = 0; i < n; i++)
                {
                    var b = new Rect(c.x + i * sw, c.y, sw - 6, c.height);
                    var col = i < G.Themes.Length ? G.Themes[i] : Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.3f, 1f), 0.75f, 1f);
                    bool cur = Cfg.ThemeIdx.Value == i;
                    G.Fill(new Rect(b.x - 2, b.y - 2, b.width + 4, b.height + 4), cur ? Color.white : new Color(0.25f, 0.28f, 0.3f, 1f));
                    G.Fill(b, col);
                    if (G.Click(b)) { Cfg.ThemeIdx.Value = i; Cfg.ThemeOn.Value = true; G.Changed = true; G.Released = true; }
                }
                Header("RAINBOW GUNS");
                Tog("Rainbow guns", Cfg.GunOn);
                Sld("Speed", Cfg.GunSpeed, 0.05f, 1.5f, "0.00");
                Sld("Saturation", Cfg.GunSat, 0.1f, 1f, "0.00");
                var gs = Ctl("Applies to");
                Cfg.GunScope.Value = G.Segmented(gs, new[] { "My weapon", "All weapons" }, Cfg.GunScope.Value);
                Header("RAINBOW TEXT");
                Tog("Rainbow text", Cfg.TextOn);
                Sld("Speed", Cfg.TextSpeed, 0.05f, 1.5f, "0.00");
                var tm = Ctl("Style");
                Cfg.TextMode.Value = G.Segmented(tm, new[] { "Wave", "All together" }, Cfg.TextMode.Value);
                Tog("Menus only", Cfg.TextMenusOnly);
            }
            _rowH = 36f;
        }

        // ---------- panes ----------

        static Color[] Swatches => G.Palette;

        static void Crosshair()
        {
            // live preview (right of the controls)
            _rw -= 160;
            var pv = new Rect(_rx + _rw + 24, _py, 136, 136);
            G.Fill(pv, new Color(0.17f, 0.19f, 0.2f, 1f));
            G.Fill(new Rect(pv.x, pv.y, pv.width, 1), G.Off);
            Features.DrawCrosshair(new Vector2(pv.center.x, pv.center.y));

            var r = Ctl("Style", 34);
            Cfg.CrossStyle.Value = G.Segmented(r, new[] { "Cross", "Dot", "Circle", "Cross+Dot", "X", "T" }, Cfg.CrossStyle.Value);
            Sld("Size", Cfg.CrossSize, 2, 40, "0");
            Sld("Thickness", Cfg.CrossThick, 1, 8, "0");
            Sld("Gap", Cfg.CrossGap, 0, 30, "0");
            Sld("Opacity", Cfg.CrossAlpha, 0.1f, 1f, "0.00");

            var c = Ctl("Color", 34);
            float sw = c.width / Swatches.Length;
            for (int i = 0; i < Swatches.Length; i++)
            {
                var b = new Rect(c.x + i * sw, c.y, sw - 6, c.height);
                var col = Swatches[i];
                bool cur = Mathf.Abs(col.r - Cfg.CrossR.Value) + Mathf.Abs(col.g - Cfg.CrossG.Value) + Mathf.Abs(col.b - Cfg.CrossB.Value) < 0.02f;
                G.Fill(new Rect(b.x - 2, b.y - 2, b.width + 4, b.height + 4), cur ? Color.white : new Color(0, 0, 0, 0));
                G.Fill(b, col);
                if (G.Click(b)) { Cfg.CrossR.Value = col.r; Cfg.CrossG.Value = col.g; Cfg.CrossB.Value = col.b; G.Changed = true; G.Released = true; }
            }
            TogPair("Outline", Cfg.CrossOutline, "Center dot", Cfg.CrossDot);
            TogPair("Show in menus", Cfg.CrossAlways, "Hide default (beta)", Cfg.CrossHideDefault);
            Tog("Rainbow color", Cfg.CrossRainbow);
        }

        static void Fps()
        {
            EnableRow(Cfg.FpsOn);
            Corner("Position", Cfg.FpsCorner);
            Note("Shows frames per second and frame time. Updates twice per second.");
        }

        static void ClockPane()
        {
            EnableRow(Cfg.ClockOn);
            Tog("24-hour time", Cfg.Clock24);
            Corner("Position", Cfg.ClockCorner);
            Note("Handy for long sessions. Uses your computer's local time.");
        }

        static void Shot()
        {
            EnableRow(Cfg.ShotOn);
            KeyRow("Screenshot key", Cfg.KbShot, "F12");
            Note("Press the screenshot key in game to save a PNG of the screen to Mods\\Screenshots. The game's own UI is included.");
            var r = Ctl("", 40);
            if (G.Button(new Rect(r.x, r.y, 220, 34), "TAKE SCREENSHOT NOW")) { Features.PanelOpen = false; Features.Screenshot(); }
        }

        static void Limiter()
        {
            EnableRow(Cfg.LimitOn);
            Tog("Force VSync", Cfg.Vsync);
            Sld("FPS limit", Cfg.LimitFps, 30, 360, "0");
            Note("When disabled, the game's original VSync and frame cap are restored. The limit is ignored while VSync is on.");
        }

        static void Fov()
        {
            EnableRow(Cfg.FovOn);
            Sld("Field of view", Cfg.Fov, 60, 120, "0");
            Note("Applied to the main camera every frame. Some game modes may override it.");
        }

        static void Volume()
        {
            EnableRow(Cfg.VolOn);
            Sld("Master volume", Cfg.Vol, 0, 1, "0.00");
            Note("Scales all game audio on top of the in-game volume settings.");
        }

        static void Background()
        {
            EnableRow(Cfg.BgOn, "Run in background");
            Note("Keeps the game running (and connected) when you alt-tab, instead of pausing.");
        }

    }
}
