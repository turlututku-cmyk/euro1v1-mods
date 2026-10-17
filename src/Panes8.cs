using System.Linq;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        static string _coachResetConfirm; static float _coachResetUntil;

        // ---------- Performance Coach ----------
        static void CoachPane()
        {
            _rowH = 30f;
            EnableRow(Cfg.CoachOn);
            TogPair("Round tips", Cfg.CoachRound, "Match summary", Cfg.CoachMatch);
            var lr = Ctl("Live stats strip");
            Cfg.CoachLive.Value = G.Toggle(new Rect(lr.x, lr.y, 60, lr.height), Cfg.CoachLive.Value);
            Cfg.CoachCorner.Value = G.Segmented(new Rect(lr.x + 80, lr.y, lr.width - 80, lr.height), Corners, Cfg.CoachCorner.Value);

            var m = Features.CoachMatchNow;
            Header(Features.CoachInMatch ? "THIS MATCH  (live)" : "LAST MATCH PLAYED / NOT IN A MATCH");
            G.Label(new Rect(_rx, _py, _rw, 22), Features.CoachKpi(m, true), 13, G.Text); _py += 22;
            G.Label(new Rect(_rx, _py, _rw, 22), "Rounds " + m.Rounds + "     Best streak " + m.BestStreak + "     Deaths within 10 s of spawning " + m.EarlyDeaths, 12, G.Dim); _py += 24;

            Header("WEAPONS  (this match)");
            string[] hdr = { "Weapon", "Shots", "Hits", "Acc", "Kills", "Died to" };
            float[] hx = { 0f, 0.30f, 0.43f, 0.56f, 0.69f, 0.84f };
            for (int i = 0; i < hdr.Length; i++) G.Label(new Rect(_rx + _rw * hx[i], _py, _rw * 0.14f, 18), hdr[i], 11, G.Dim, i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            _py += 20;
            for (int i = 0; i < 3; i++)
            {
                var row = new Rect(_rx, _py, _rw, 20);
                G.Fill(row, G.Row);
                G.Label(new Rect(row.x + 8, row.y, 120, row.height), Features.CoachWeapons[i], 12, G.Text, TextAnchor.MiddleLeft);
                string[] v = { m.WShots[i].ToString(), m.WHits[i].ToString(), m.WShots[i] > 0 ? Mathf.RoundToInt(100f * m.WHits[i] / m.WShots[i]) + "%" : "-", m.WKills[i].ToString(), m.WDeaths[i].ToString() };
                for (int j = 0; j < v.Length; j++) G.Label(new Rect(row.x + row.width * hx[j + 1], row.y, row.width * 0.14f, row.height), v[j], 12, G.Text, TextAnchor.MiddleRight);
                _py += 22;
            }

            Header("COACH SAYS  (" + (string.IsNullOrEmpty(Features.CoachLastTitle) ? "nothing yet" : Features.CoachLastTitle) + ")");
            if (Features.CoachLastTips.Count == 0)
                G.Label(new Rect(_rx, _py, _rw, 30), "Tips appear after each round and when a match ends.", 12, G.Dim);
            for (int i = 0; i < Features.CoachLastTips.Count && i < 3; i++)
            {
                G.Label(new Rect(_rx, _py, _rw, 30), "-  " + Features.CoachLastTips[i], 12, G.Text, TextAnchor.UpperLeft, false, true);
                _py += 30;
            }
            if (Features.CoachLastTips.Count == 0) _py += 30;
            else _py += 4;

            var life = Features.CoachLifetime(); int n = Features.CoachHistoryCount;
            G.Label(new Rect(_rx, _py, _rw, 22),
                n == 0 ? "Saved history: none yet (a match is saved when it ends)."
                       : "Saved matches " + n + "     Overall: K/D " + life.KD.ToString("0.00") + "    Acc " + (life.Shots > 0 ? Mathf.RoundToInt(life.Acc * 100f) + "%" : "-") + "    HS " + (life.Hits > 0 ? Mathf.RoundToInt(life.HsRate * 100f) + "%" : "-"), 12, G.Dim);
            _py += 24;

            var b = Ctl("", 32);
            if (SmallBtn(new Rect(b.x, b.y, 150, 28), "SHOW TIPS NOW", G.Accent)) Features.CoachShowNow();
            if (SmallBtn(new Rect(b.x + 158, b.y, 130, 28), "OPEN FOLDER")) OpenFolder(System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "Mods", "Stats"));
            bool sure = _coachResetConfirm == "x" && Time.unscaledTime < _coachResetUntil;
            if (SmallBtn(new Rect(b.x + 296, b.y, 150, 28), sure ? "SURE?" : "RESET HISTORY", new Color(1f, 0.3f, 0.3f)))
            {
                if (sure) { Features.CoachResetHistory(); _coachResetConfirm = null; }
                else { _coachResetConfirm = "x"; _coachResetUntil = Time.unscaledTime + 3f; }
            }
            _rowH = 36f;
        }
    }
}
