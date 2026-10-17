using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    // Kill Messages, Announcer, Kill Confetti and the Session Stats overlay. All of them react to events from KillTracker.
    public static partial class Features
    {
        // ---------------------------------------------------------------- kill messages

        class FeedLine { public string Text; public Color C; public float T; }
        static readonly List<FeedLine> _feed = new List<FeedLine>();

        static string DeathVerb(KillEvent ev)
        {
            switch (ev.Weapon)
            {
                case WeaponType.Pistol: return "You were shot by ";
                case WeaponType.Shotgun: return "You were pumped by ";
                case WeaponType.Sniper: return "You were sniped by ";
                default: return "You were killed by ";
            }
        }

        static void KmOnKill(KillEvent ev)
        {
            if (!Cfg.KmOn.Value) return;
            float now = Time.unscaledTime;
            Action<string, Color> add = (t, c) => { _feed.Add(new FeedLine { Text = t, C = c, T = now }); if (_feed.Count > 8) _feed.RemoveAt(0); };
            if (ev.KillerIsLocal) add("You killed " + ev.Victim + (ev.Headshot && Cfg.KmHead.Value ? "   (headshot)" : ""), new Color(0.35f, 1f, 0.45f));
            if (ev.LocalAssisted) add("You assisted " + ev.Killer + " for " + ev.Victim, new Color(0.4f, 0.85f, 1f));
            if (ev.VictimIsLocal) add(ev.Killer == null ? "You died" : DeathVerb(ev) + ev.Killer, new Color(1f, 0.4f, 0.4f));
            if (!ev.KillerIsLocal && !ev.VictimIsLocal && !ev.LocalAssisted && Cfg.KmOthers.Value)
                add((ev.Killer ?? "Someone") + " killed " + ev.Victim, new Color(0.8f, 0.82f, 0.85f));
        }

        static void DrawKillMessages()
        {
            if (!Cfg.KmOn.Value || _feed.Count == 0) return;
            float now = Time.unscaledTime, life = Mathf.Max(1.5f, Cfg.KmTime.Value);
            _feed.RemoveAll(f => now - f.T > life);
            int c = Mathf.Clamp(Cfg.KmCorner.Value, 0, 3);
            bool right = (c & 1) == 1, bottom = c >= 2;
            const float lh = 28f, m = 20f;
            for (int i = 0; i < _feed.Count; i++)
            {
                var f = _feed[_feed.Count - 1 - i];     // newest first, nearest the corner
                float age = now - f.T, a = Mathf.Clamp01((life - age) / 0.6f);
                float w = Mathf.Min(520f, f.Text.Length * 8.6f + 30f);
                float x = right ? Screen.width - w - m : m;
                float y = bottom ? Screen.height - m - lh * (i + 1) - 3f * i : m + 200f + (lh + 3f) * i;
                var r = new Rect(x, y, w, lh);
                G.Fill(r, new Color(0, 0, 0, 0.6f * a));
                G.Fill(new Rect(r.x, r.y, 3, r.height), new Color(f.C.r, f.C.g, f.C.b, a));
                G.Label(new Rect(r.x + 10, r.y, r.width - 12, r.height), f.Text, 15, new Color(f.C.r, f.C.g, f.C.b, a), TextAnchor.MiddleLeft, true);
            }
        }

        // ---------------------------------------------------------------- announcer

        static string _annText = ""; static Color _annColor; static float _annT = -99f;
        static float _lastLocalKillT = -99f; static int _multiKills;
        static AudioSource _annSrc;
        static readonly Dictionary<int, AudioClip> _annClips = new Dictionary<int, AudioClip>();

        static AudioClip MakeStinger(int level)
        {
            const int rate = 44100;
            int notes = Mathf.Clamp(level + 2, 2, 7);
            float noteLen = 0.085f;
            int per = (int)(rate * noteLen), n = per * notes + (int)(rate * 0.18f);
            var d = new float[n];
            for (int k = 0; k < notes; k++)
            {
                float f = 523.25f * Mathf.Pow(2f, (k * (level >= 4 ? 4 : 3)) / 12f);
                for (int i = 0; i < per + (int)(rate * 0.18f) && k * per + i < n; i++)
                {
                    float t = i / (float)rate, env = Mathf.Exp(-t * 11f) * Mathf.Clamp01(i / 90f);
                    d[k * per + i] += (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.2f) * env * 0.5f;
                }
            }
            var clip = AudioClip.Create("modsmenu_announce_" + level, n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static void AnnOnKill(KillEvent ev)
        {
            if (!Cfg.AnnOn.Value || !ev.KillerIsLocal) return;
            float now = Time.unscaledTime;
            _multiKills = (now - _lastLocalKillT <= 4f) ? _multiKills + 1 : 1;
            _lastLocalKillT = now;

            string text = null; int level = 0;
            if (_multiKills >= 2) { text = _multiKills == 2 ? "DOUBLE KILL" : _multiKills == 3 ? "TRIPLE KILL" : _multiKills == 4 ? "QUAD KILL" : "MEGA KILL"; level = Mathf.Min(_multiKills + 1, 6); }
            else if (MyStreak == 3) { text = "KILLING SPREE"; level = 3; }
            else if (MyStreak == 5) { text = "RAMPAGE"; level = 4; }
            else if (MyStreak == 7) { text = "UNSTOPPABLE"; level = 5; }
            else if (MyStreak >= 10 && MyStreak % 5 == 0) { text = "GODLIKE"; level = 6; }
            else if (ev.Headshot) { text = "HEADSHOT"; level = 1; }
            if (text == null) return;

            var palette = new[] { Color.white, new Color(1f, 0.9f, 0.3f), new Color(1f, 0.7f, 0.2f), new Color(1f, 0.45f, 0.2f), new Color(1f, 0.25f, 0.25f), new Color(1f, 0.3f, 0.8f), new Color(0.5f, 0.6f, 1f) };
            _annText = text; _annColor = palette[Mathf.Clamp(level, 0, palette.Length - 1)]; _annT = now;

            if (Cfg.AnnSound.Value)
            {
                try
                {
                    if (_annSrc == null)
                    {
                        var go = new GameObject("ModsMenu_Announcer");
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        _annSrc = go.AddComponent<AudioSource>(); _annSrc.playOnAwake = false; _annSrc.spatialBlend = 0f;
                    }
                    if (!_annClips.TryGetValue(level, out var clip) || clip == null) { clip = MakeStinger(level); _annClips[level] = clip; }
                    _annSrc.PlayOneShot(clip, Cfg.AnnVol.Value);
                }
                catch { }
            }
        }

        static void DrawAnnouncer()
        {
            if (!Cfg.AnnOn.Value) return;
            float age = Time.unscaledTime - _annT;
            if (age < 0f || age > 2.4f) return;
            float pop = 1f + 0.7f * Mathf.Clamp01(1f - age * 5f);
            float a = Mathf.Clamp01((2.4f - age) / 0.7f);
            int size = Mathf.RoundToInt(Cfg.AnnSize.Value * pop);
            var r = new Rect(0, Screen.height * 0.17f - size * 0.5f, Screen.width, size * 1.5f);
            G.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), _annText, size, new Color(0, 0, 0, a), TextAnchor.MiddleCenter, true);
            G.Label(r, _annText, size, new Color(_annColor.r, _annColor.g, _annColor.b, a), TextAnchor.MiddleCenter, true);
        }

        // ---------------------------------------------------------------- confetti

        struct Conf { public float X, Y, VX, VY, W, H, Phase, Life, Max; public Color C; }
        static readonly List<Conf> _conf = new List<Conf>();

        static void ConfOnKill(KillEvent ev)
        {
            if (!Cfg.ConfOn.Value) return;
            if (!(ev.KillerIsLocal || (ev.LocalAssisted && Cfg.ConfAssist.Value))) return;
            int n = Mathf.RoundToInt(Cfg.ConfAmount.Value) * (ev.Headshot && Cfg.ConfHead.Value ? 2 : 1);
            n = Mathf.Min(n, 500);
            float cx = Screen.width / 2f, cy = Screen.height * 0.42f;
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.Deg2Rad * (-90f + (UnityEngine.Random.value - 0.5f) * 140f);
                float sp = 350f + UnityEngine.Random.value * 750f;
                float life = 1.8f + UnityEngine.Random.value * 1.4f;
                var col = Color.HSVToRGB(UnityEngine.Random.value, 0.75f, 1f);
                _conf.Add(new Conf
                {
                    X = cx + (UnityEngine.Random.value - 0.5f) * 80f, Y = cy, VX = Mathf.Cos(ang) * sp, VY = Mathf.Sin(ang) * sp,
                    W = 6f + UnityEngine.Random.value * 8f, H = 4f + UnityEngine.Random.value * 8f, Phase = UnityEngine.Random.value * 6.28f,
                    Life = life, Max = life, C = col
                });
            }
            if (_conf.Count > 900) _conf.RemoveRange(0, _conf.Count - 900);
        }

        static void UpdateConfetti()
        {
            if (_conf.Count == 0) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = _conf.Count - 1; i >= 0; i--)
            {
                var c = _conf[i];
                c.VY += 760f * dt; c.VX *= 1f - Mathf.Min(1f, 0.9f * dt); c.VY *= 1f - Mathf.Min(1f, 0.35f * dt);
                c.X += c.VX * dt; c.Y += c.VY * dt; c.Phase += dt * 9f; c.Life -= dt;
                if (c.Life <= 0f || c.Y > Screen.height + 20f) { _conf.RemoveAt(i); continue; }
                _conf[i] = c;
            }
        }

        static void DrawConfetti()
        {
            foreach (var c in _conf)
            {
                float a = Mathf.Clamp01(c.Life / (c.Max * 0.4f));
                float w = Mathf.Max(1.5f, c.W * Mathf.Abs(Mathf.Cos(c.Phase)));
                G.Fill(new Rect(c.X, c.Y, w, c.H), new Color(c.C.r, c.C.g, c.C.b, a));
            }
        }

        // ---------------------------------------------------------------- session stats overlay + csv

        static void UpdateSessionKeys()
        {
            var kb = Keyboard.current;
            if (kb != null && Cfg.SsOn.Value && kb.f3Key.wasPressedThisFrame) Cfg.SsOverlay.Value = !Cfg.SsOverlay.Value;
        }

        public static SessRow LocalRow() => LbSource().FirstOrDefault(r => r.IsLocal);

        public static List<SessRow> Leaderboard(int max = 10)
            => LbSource().OrderByDescending(r => r.Kills).ThenBy(r => r.Deaths).ThenByDescending(r => r.Damage).Take(max).ToList();

        static void DrawSessionOverlay()
        {
            bool popup = Time.unscaledTime < PopupUntil;
            if (!Cfg.SsOn.Value || !(Cfg.SsOverlay.Value || popup)) return;
            var top = Leaderboard(5);
            var me = LocalRow();
            bool waiting = top.Count == 0 && Cfg.SsRoundEnd.Value;
            if (waiting && _prevState == "") return;   // nothing to show outside a match
            var r = Corner(Cfg.SsCorner.Value, 300f, 56f + Mathf.Max(top.Count, waiting ? 1 : 0) * 22f + (me != null ? 26f : 0f), 80f);
            string title = !Cfg.SsRoundEnd.Value ? "SESSION   (F3 hides)" : SnapRound > 0 ? "AFTER ROUND " + SnapRound + (Cfg.SsOverlay.Value ? "   (F3 hides)" : "") : "SESSION   (updates each round)";
            Card(r, title);
            if (waiting) G.Label(new Rect(r.x + 12, r.y + 44, r.width - 20, 22), "Waiting for the first round to end", 12, G.Dim, TextAnchor.MiddleLeft);
            G.Label(new Rect(r.x + 12, r.y + 24, 130, 20), "Player", 11, G.Dim);
            string[] cols = { "K", "D", "A", "Dmg" };
            float[] xs = { 150f, 182f, 214f, 240f };
            for (int i = 0; i < cols.Length; i++) G.Label(new Rect(r.x + xs[i], r.y + 24, 44, 20), cols[i], 11, G.Dim, TextAnchor.MiddleRight);
            for (int i = 0; i < top.Count; i++)
            {
                var s = top[i]; float y = r.y + 44 + i * 22f;
                var c = s.IsLocal ? G.Accent : G.Text;
                string nm = s.Name.Length > 14 ? s.Name.Substring(0, 13) + "..." : s.Name;
                G.Label(new Rect(r.x + 12, y, 140, 22), (i + 1) + ". " + nm, 13, c, TextAnchor.MiddleLeft, s.IsLocal);
                G.Label(new Rect(r.x + xs[0], y, 44, 22), s.Kills.ToString(), 13, c, TextAnchor.MiddleRight);
                G.Label(new Rect(r.x + xs[1], y, 44, 22), s.Deaths.ToString(), 13, c, TextAnchor.MiddleRight);
                G.Label(new Rect(r.x + xs[2], y, 44, 22), s.Assists.ToString(), 13, c, TextAnchor.MiddleRight);
                G.Label(new Rect(r.x + xs[3], y, 52, 22), s.Damage.ToString(), 13, c, TextAnchor.MiddleRight);
            }
            if (me != null)
            {
                float y = r.yMax - 26f;
                G.Fill(new Rect(r.x + 10, y - 2, r.width - 20, 1), new Color(1, 1, 1, 0.15f));
                G.Label(new Rect(r.x + 12, y, r.width - 20, 24), "You:  streak " + MyStreak + "   best " + MyBestStreak + "   K/D " + (me.Deaths > 0 ? (me.Kills / (float)me.Deaths).ToString("0.00") : me.Kills.ToString("0.00")), 12, G.Text, TextAnchor.MiddleLeft, true);
            }
        }

        public static void SaveSessionCsv()
        {
            try
            {
                var dir = Path.Combine(Paths.GameRootPath, "Mods", "Stats");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                var sb = new StringBuilder("Name,Kills,Deaths,Assists,Damage,Headshots,KD\n");
                foreach (var r in Sess.Values.OrderByDescending(x => x.Kills))
                    sb.AppendLine(string.Join(",", new[] { "\"" + r.Name.Replace("\"", "") + "\"", r.Kills.ToString(), r.Deaths.ToString(), r.Assists.ToString(), r.Damage.ToString(), r.Headshots.ToString(), (r.Deaths > 0 ? (r.Kills / (float)r.Deaths) : r.Kills).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) }));
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                Toast("Session saved: Mods\\Stats\\" + Path.GetFileName(path));
            }
            catch (Exception e) { Toast("Could not save: " + e.Message); }
        }
    }
}

namespace Euro3v3.ModsMenu
{
    public static partial class Features
    {
        public static void AnnTest()
        {
            if (!Cfg.AnnOn.Value) { Toast("Turn the Announcer on first"); return; }
            _multiKills = 2; _lastLocalKillT = Time.unscaledTime;
            AnnOnKill(new KillEvent { Killer = "You", Victim = "Test", KillerIsLocal = true });
        }

        public static void ConfTest()
        {
            if (!Cfg.ConfOn.Value) { Toast("Turn Kill Confetti on first"); return; }
            ConfOnKill(new KillEvent { Killer = "You", Victim = "Test", KillerIsLocal = true });
        }
    }
}
