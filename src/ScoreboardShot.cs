using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Scoreboard Screenshot: when the match ends, shows a clean result card (everyone's kills / deaths / damage)
    // and saves a PNG of the screen to Mods\Screenshots. Uses only the stats the game already shows on its scoreboard.
    public static partial class Features
    {
        class SbRow { public string Name; public bool Mine, Local; public int K, D, Dmg; }
        static List<SbRow> _sbRows = new List<SbRow>();
        static float _sbCardUntil, _sbShotAt;
        static int _sbTotalMine, _sbTotalTheirs;

        static void SbCollect()
        {
            var rows = new List<SbRow>();
            try
            {
                var me = LocalPlayer();
                int myTeam = me != null ? me.team : -1;
                foreach (var p in UnityEngine.Object.FindObjectsOfType<PlayerObjectController>())
                    rows.Add(new SbRow { Name = p.PlayerName ?? "?", Mine = p.team == myTeam, Local = p.isLocalPlayer, K = p.stat_kills, D = p.stat_deaths, Dmg = p.stat_damage });
            }
            catch { }
            _sbRows = rows.OrderByDescending(r => r.Mine).ThenByDescending(r => r.K).ThenBy(r => r.D).ToList();
            _sbTotalMine = _sbRows.Where(r => r.Mine).Sum(r => r.K);
            _sbTotalTheirs = _sbRows.Where(r => !r.Mine).Sum(r => r.K);
        }

        static void SbOnMatchEnd()
        {
            if (!Cfg.SbOn.Value) return;
            SbCollect();
            float now = Time.unscaledTime, delay = Mathf.Clamp(Cfg.SbDelay.Value, 0.5f, 6f);
            _sbShotAt = now + delay;
            _sbCardUntil = now + delay + 4f;
        }

        public static void SbTest()
        {
            SbCollect();
            _sbCardUntil = Time.unscaledTime + 6f;
            if (_sbRows.Count == 0) Toast("Scoreboard Screenshot: join a match to see players here");
        }

        static void UpdateScoreboardShot()
        {
            if (!Cfg.SbOn.Value) return;
            if (_sbShotAt > 0f && Time.unscaledTime >= _sbShotAt)
            {
                _sbShotAt = 0f;
                try
                {
                    var dir = Path.Combine(Paths.GameRootPath, "Mods", "Screenshots");
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, "scoreboard_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                    ScreenCapture.CaptureScreenshot(path);
                    Toast("Scoreboard saved to Mods\\Screenshots");
                }
                catch (Exception e) { Toast("Scoreboard screenshot failed: " + e.Message); }
            }
        }

        static void DrawScoreboardCard()
        {
            if (!Cfg.SbOn.Value || !Cfg.SbCard.Value || Time.unscaledTime > _sbCardUntil || _sbRows.Count == 0) return;
            float a = Mathf.Clamp01((_sbCardUntil - Time.unscaledTime) / 0.5f);
            int n = Mathf.Min(_sbRows.Count, 14);
            float w = 520f, h = 92f + n * 24f;
            var r = new Rect((Screen.width - w) / 2f, Screen.height * 0.10f, w, h);
            var dim = new Color(0.6f, 0.65f, 0.7f, a);
            G.Fill(r, new Color(0.04f, 0.05f, 0.07f, 0.9f * a));
            G.Fill(new Rect(r.x, r.y, r.width, 3), new Color(G.Accent.r, G.Accent.g, G.Accent.b, a));
            G.Label(new Rect(r.x, r.y + 8, r.width, 32), "MATCH RESULT", 22, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, true);
            G.Label(new Rect(r.x, r.y + 38, r.width, 22), "Your team " + _sbTotalMine + "   -   " + _sbTotalTheirs + " Enemy team  (kills)", 13, new Color(0.7f, 0.75f, 0.8f, a), TextAnchor.MiddleCenter);
            float y = r.y + 64f;
            G.Label(new Rect(r.x + 22, y, 220, 20), "Player", 11, dim);
            G.Label(new Rect(r.x + 300, y, 50, 20), "K", 11, dim, TextAnchor.MiddleRight);
            G.Label(new Rect(r.x + 350, y, 50, 20), "D", 11, dim, TextAnchor.MiddleRight);
            G.Label(new Rect(r.x + 410, y, 90, 20), "Damage", 11, dim, TextAnchor.MiddleRight);
            y += 22f;
            for (int i = 0; i < n; i++)
            {
                var s = _sbRows[i];
                var team = s.Mine ? new Color(0.35f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
                var row = new Rect(r.x + 10, y, r.width - 20, 22);
                G.Fill(row, new Color(s.Local ? 0.14f : 0.09f, s.Local ? 0.2f : 0.1f, s.Local ? 0.16f : 0.12f, 0.9f * a));
                G.Fill(new Rect(row.x, row.y, 3, row.height), new Color(team.r, team.g, team.b, a));
                string nm = s.Name.Length > 22 ? s.Name.Substring(0, 21) + "..." : s.Name;
                G.Label(new Rect(row.x + 12, row.y, 260, row.height), nm, 13, s.Local ? new Color(1, 1, 1, a) : new Color(team.r, team.g, team.b, a), TextAnchor.MiddleLeft, s.Local);
                var white = new Color(1, 1, 1, a);
                G.Label(new Rect(r.x + 300, row.y, 50, row.height), s.K.ToString(), 13, white, TextAnchor.MiddleRight);
                G.Label(new Rect(r.x + 350, row.y, 50, row.height), s.D.ToString(), 13, white, TextAnchor.MiddleRight);
                G.Label(new Rect(r.x + 410, row.y, 90, row.height), s.Dmg.ToString(), 13, white, TextAnchor.MiddleRight);
                y += 24f;
            }
        }
    }
}
