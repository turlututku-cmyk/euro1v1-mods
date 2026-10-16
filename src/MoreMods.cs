using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Match Stats, Letterbox, Screen Tint, FPS Graph, Notes and Settings Backup. Local, cosmetic, no gameplay effect.
    public static partial class Features
    {
        public static readonly string[] TintNames = { "Warm", "Cool", "Sepia", "Night", "Red", "Green", "Purple" };
        static readonly Color[] TintColors =
        {
            new Color(1f, 0.78f, 0.45f), new Color(0.45f, 0.7f, 1f), new Color(0.7f, 0.48f, 0.28f), new Color(0.08f, 0.12f, 0.4f),
            new Color(1f, 0.15f, 0.15f), new Color(0.15f, 1f, 0.4f), new Color(0.6f, 0.3f, 1f)
        };

        static Rect Corner(int corner, float w, float h, float yTop = 50f)
        {
            const float m = 20f;
            int c = Mathf.Clamp(corner, 0, 3);
            float x = (c & 1) == 1 ? Screen.width - w - m : m;
            float y = c >= 2 ? Screen.height - h - m : m + yTop;
            return new Rect(x, y, w, h);
        }

        static void Card(Rect r, string title)
        {
            G.Fill(r, new Color(0, 0, 0, 0.62f));
            G.Fill(new Rect(r.x, r.y, 3, r.height), G.Accent);
            G.Label(new Rect(r.x + 12, r.y + 3, r.width - 16, 22), title, 12, G.Accent, TextAnchor.MiddleLeft, true);
        }

        // ---------------------------------------------------------------- screen tint + letterbox (drawn first, under the other overlays)

        static void DrawTintAndBars()
        {
            if (Cfg.TintOn.Value)
            {
                var c = TintColors[Mathf.Clamp(Cfg.TintIdx.Value, 0, TintColors.Length - 1)];
                G.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(c.r, c.g, c.b, Mathf.Clamp(Cfg.TintStrength.Value, 0f, 0.7f)));
            }
            if (Cfg.BarsOn.Value)
            {
                float h = Screen.height * Mathf.Clamp(Cfg.BarsSize.Value, 0.02f, 0.3f);
                G.Fill(new Rect(0, 0, Screen.width, h), Color.black);
                G.Fill(new Rect(0, Screen.height - h, Screen.width, h), Color.black);
            }
        }

        // ---------------------------------------------------------------- match stats (your own numbers)

        static void DrawMatchStats()
        {
            if (!Cfg.StatsOn.Value) return;
            var me = LocalPlayer();
            if (me == null) return;
            int dmg = 0, kills = 0, deaths = 0, hs = 0, shots = 0, hits = 0, heal = 0;
            try { dmg = me.stat_damage; kills = me.stat_kills; deaths = me.stat_deaths; hs = me.stat_headshots; shots = me.stat_shotsFired; hits = me.stat_shotsHit; heal = me.stat_healing; } catch { }
            string kd = deaths > 0 ? (kills / (float)deaths).ToString("0.00") : kills.ToString("0.00");
            string acc = shots > 0 ? Mathf.RoundToInt(100f * hits / shots) + "%" : "-";
            string[] k = { "Damage", "Kills", "Deaths", "K/D", "Accuracy", "Headshots", "Healing" };
            string[] v = { dmg.ToString(), kills.ToString(), deaths.ToString(), kd, acc, hs.ToString(), heal.ToString() };
            var r = Corner(Cfg.StatsCorner.Value, 190f, 32f + k.Length * 20f);
            Card(r, "MATCH STATS");
            for (int i = 0; i < k.Length; i++)
            {
                float y = r.y + 28 + i * 20;
                G.Label(new Rect(r.x + 12, y, 110, 20), k[i], 13, G.Dim);
                G.Label(new Rect(r.x + 100, y, r.width - 112, 20), v[i], 14, G.Text, TextAnchor.MiddleRight, true);
            }
        }

        // ---------------------------------------------------------------- fps graph

        static readonly float[] _ft = new float[150];
        static int _ftPos;

        static void UpdateGraph()
        {
            if (!Cfg.GraphOn.Value) return;
            _ft[_ftPos] = Time.unscaledDeltaTime * 1000f;
            _ftPos = (_ftPos + 1) % _ft.Length;
        }

        static void DrawFpsGraph()
        {
            if (!Cfg.GraphOn.Value) return;
            float s = Cfg.GraphScale.Value;
            var r = Corner(Cfg.GraphCorner.Value, 300f * s, 110f * s, 140f);
            Card(r, "FRAME TIME");
            var plot = new Rect(r.x + 10, r.y + 28 * s, r.width - 20, r.height - 40 * s);
            float max = 40f;
            float sum = 0f, worst = 0f;
            for (int i = 0; i < _ft.Length; i++) { sum += _ft[i]; worst = Mathf.Max(worst, _ft[i]); }
            float avg = sum / _ft.Length;
            // 60 fps and 30 fps guide lines
            G.Fill(new Rect(plot.x, plot.yMax - plot.height * (16.7f / max), plot.width, 1), new Color(1, 1, 1, 0.18f));
            G.Fill(new Rect(plot.x, plot.yMax - plot.height * (33.3f / max), plot.width, 1), new Color(1, 0.4f, 0.4f, 0.25f));
            float bw = plot.width / _ft.Length;
            for (int i = 0; i < _ft.Length; i++)
            {
                float v = _ft[(_ftPos + i) % _ft.Length];
                float h = Mathf.Clamp01(v / max) * plot.height;
                var c = v < 16.7f ? G.Accent : v < 33.3f ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.3f, 0.3f);
                G.Fill(new Rect(plot.x + i * bw, plot.yMax - h, Mathf.Max(1f, bw), h), c);
            }
            G.Label(new Rect(r.x + 100 * s, r.y + 3, r.width - 110 * s, 22), "avg " + avg.ToString("0.0") + " ms   worst " + worst.ToString("0") + " ms", 11, G.Dim, TextAnchor.MiddleRight);
        }

        // ---------------------------------------------------------------- notes

        static void DrawNotes()
        {
            if (!Cfg.NotesOn.Value) return;
            var lines = (Cfg.NotesText.Value ?? "").Split('|').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            if (lines.Length == 0) return;
            int size = Mathf.Clamp((int)Cfg.NotesSize.Value, 10, 32);
            float lh = size + 8f;
            var r = Corner(Cfg.NotesCorner.Value, 320f, 30f + lines.Length * lh, 200f);
            Card(r, "NOTES");
            for (int i = 0; i < lines.Length; i++)
                G.Label(new Rect(r.x + 12, r.y + 26 + i * lh, r.width - 20, lh), lines[i], size, G.Text, TextAnchor.MiddleLeft, true);
        }

        // ---------------------------------------------------------------- settings backup

        public static string BackupDir => Path.Combine(Paths.GameRootPath, "Mods", "Backups");
        static string CfgPath => Path.Combine(Paths.ConfigPath, "local.euro3v3.modsmenu.cfg");

        public static List<string> BackupFiles()
        {
            try { Directory.CreateDirectory(BackupDir); return Directory.GetFiles(BackupDir, "*.cfg").OrderByDescending(f => File.GetLastWriteTime(f)).ToList(); }
            catch { return new List<string>(); }
        }

        public static void BackupNow()
        {
            try
            {
                Plugin.Instance.Config.Save();
                Directory.CreateDirectory(BackupDir);
                var dst = Path.Combine(BackupDir, "settings_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".cfg");
                File.Copy(CfgPath, dst, true);
                Toast("Settings backed up");
            }
            catch (Exception e) { Toast("Backup failed: " + e.Message); }
        }

        public static void RestoreBackup(string file)
        {
            try
            {
                File.Copy(file, CfgPath, true);
                Plugin.Instance.Config.Reload();
                Toast("Settings restored from " + Path.GetFileName(file));
            }
            catch (Exception e) { Toast("Restore failed: " + e.Message); }
        }
    }
}
