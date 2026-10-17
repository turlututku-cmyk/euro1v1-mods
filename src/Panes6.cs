using System.Linq;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        // ---------- Kill Messages ----------
        static void KmPane()
        {
            EnableRow(Cfg.KmOn);
            var c = Ctl("Position");
            Cfg.KmCorner.Value = G.Segmented(c, Corners, Cfg.KmCorner.Value);
            Sld("Show for (s)", Cfg.KmTime, 2f, 12f, "0.0");
            Tog("Tag headshots", Cfg.KmHead);
            Tog("Also show other players' kills", Cfg.KmOthers);
            Header("EXAMPLES");
            string[] ex =
            {
                "You killed Alex   (headshot)",
                "You assisted Sam for Alex",
                "You were shot by Alex  (pistol)",
                "You were pumped by Alex  (shotgun)",
                "You were sniped by Alex  (sniper)",
            };
            foreach (var l in ex) { G.Label(new Rect(_rx, _py, _rw, 22), "-  " + l, 13, G.Text); _py += 24; }
        }

        // ---------- Announcer ----------
        static void AnnPane()
        {
            EnableRow(Cfg.AnnOn);
            Sld("Text size", Cfg.AnnSize, 24f, 90f, "0");
            Tog("Play a sound", Cfg.AnnSound);
            Sld("Volume", Cfg.AnnVol, 0f, 1f, "0.00");
            var t = Ctl("Test", 34);
            if (SmallBtn(new Rect(t.x, t.y, 130, 30), "TEST ANNOUNCE", G.Accent)) Features.AnnTest();
            Note("Double, triple and quad kills within 4 seconds, killing sprees at 3, 5, 7 and 10 kills in one life, and headshots. Each level has its own color and a rising sound.", 60);
        }

        // ---------- Kill Confetti ----------
        static void ConfPane()
        {
            EnableRow(Cfg.ConfOn);
            Sld("Amount", Cfg.ConfAmount, 40f, 300f, "0");
            Tog("Double on headshots", Cfg.ConfHead);
            Tog("Also on assists", Cfg.ConfAssist);
            var t = Ctl("Test", 34);
            if (SmallBtn(new Rect(t.x, t.y, 130, 30), "TEST CONFETTI", G.Accent)) Features.ConfTest();
            Note("A burst of confetti on your screen whenever you get a kill. Purely visual.");
        }

        // ---------- Session Stats ----------
        static void SessionPane()
        {
            _rowH = 30f;
            EnableRow(Cfg.SsOn);
            Tog("On-screen leaderboard", Cfg.SsOverlay);
            KeyRow("Overlay key", Cfg.KbSession, "F3");
            Tog("Freeze between rounds", Cfg.SsRoundEnd);
            Tog("Pop up the leaderboard after each round", Cfg.SsPopup);
            var c = Ctl("Position");
            Cfg.SsCorner.Value = G.Segmented(c, Corners, Cfg.SsCorner.Value);

            var b = Ctl("", 32);
            if (SmallBtn(new Rect(b.x, b.y, 140, 28), "RESET SESSION", new Color(1f, 0.3f, 0.3f))) Features.ResetSession();
            if (SmallBtn(new Rect(b.x + 148, b.y, 130, 28), "SAVE AS CSV", G.Accent)) Features.SaveSessionCsv();
            if (SmallBtn(new Rect(b.x + 286, b.y, 130, 28), "OPEN FOLDER")) OpenFolder(System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "Mods", "Stats"));
            if (SmallBtn(new Rect(b.x + 424, b.y, 130, 28), "UPDATE NOW", G.Accent)) { Features.TakeSnapshot(true); }

            var me = Features.LocalRow();
            Header("YOU");
            string you = me == null ? "No kills tracked yet. Play a match with this mod on."
                : "Kills " + me.Kills + "   Deaths " + me.Deaths + "   Assists " + me.Assists + "   K/D " + (me.Deaths > 0 ? (me.Kills / (float)me.Deaths).ToString("0.00") : me.Kills.ToString("0.00"))
                  + "   Damage " + me.Damage + "   Headshots " + me.Headshots + "   Best streak " + Features.MyBestStreak;
            G.Label(new Rect(_rx, _py, _rw, 24), you, 13, G.Text); _py += 28;

            Header(Cfg.SsRoundEnd.Value ? (Features.SnapRound > 0 ? "MOST KILLS  (as of the end of round " + Features.SnapRound + ")" : "MOST KILLS  (fills in when the first round ends)") : "MOST KILLS  (everyone seen this session, live)");
            string[] hdr = { "#  Player", "K", "D", "A", "Damage", "K/D" };
            float[] hx = { 0f, 0.46f, 0.55f, 0.64f, 0.74f, 0.88f };
            for (int i = 0; i < hdr.Length; i++) G.Label(new Rect(_rx + _rw * hx[i], _py, _rw * 0.12f, 20), hdr[i], 11, G.Dim, i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            _py += 22;
            var top = Features.Leaderboard(5);
            if (top.Count == 0) G.Label(new Rect(_rx, _py, _rw, 22), "Nothing yet.", 13, G.Dim);
            for (int i = 0; i < top.Count; i++)
            {
                var s = top[i];
                var row = new Rect(_rx, _py, _rw, 24);
                G.Fill(row, s.IsLocal ? new Color(0.12f, 0.2f, 0.13f, 1f) : G.Row);
                string nm = s.Name.Length > 22 ? s.Name.Substring(0, 21) + "..." : s.Name;
                G.Label(new Rect(row.x + 8, row.y, row.width * 0.45f, row.height), (i + 1) + ".  " + nm, 13, s.IsLocal ? G.Accent : G.Text, TextAnchor.MiddleLeft, s.IsLocal);
                string[] vals = { s.Kills.ToString(), s.Deaths.ToString(), s.Assists.ToString(), s.Damage.ToString(), (s.Deaths > 0 ? (s.Kills / (float)s.Deaths).ToString("0.0") : s.Kills.ToString("0.0")) };
                for (int j = 0; j < vals.Length; j++) G.Label(new Rect(row.x + row.width * hx[j + 1], row.y, row.width * 0.12f, row.height), vals[j], 13, G.Text, TextAnchor.MiddleRight);
                _py += 26;
            }
            _rowH = 36f;
        }

    }
}

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        // ---------- Low Health Warning ----------
        static void LhPane()
        {
            EnableRow(Cfg.LhOn);
            Sld("Warn below (% health)", Cfg.LhThreshold, 10f, 60f, "0");
            Sld("Strength", Cfg.LhStrength, 0.2f, 1f, "0.00");
            Tog("Pulse the red screen edges", Cfg.LhPulse);
            Tog("Heartbeat sound", Cfg.LhSound);
            Sld("Heartbeat volume", Cfg.LhVol, 0f, 1f, "0.00");
            Tog("Show LOW HEALTH text", Cfg.LhText);
            var t = Ctl("Test", 34);
            if (SmallBtn(new Rect(t.x, t.y, 150, 30), "TEST WARNING", G.Accent)) Features.LhTest();
            Note("Shows a red pulse on the screen edges and plays a heartbeat when your health drops under the limit. The lower your health, the stronger and faster it gets. Only your own health is used.", 60);
        }
    }
}
