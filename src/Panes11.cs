using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        // ---------- Team Panel ----------
        static void TpPane()
        {
            EnableRow(Cfg.TpOn);
            var c = Ctl("Position");
            Cfg.TpCorner.Value = G.Segmented(c, Corners, Cfg.TpCorner.Value);
            Tog("Distance and arrow", Cfg.TpDist);
            Tog("Show dead teammates", Cfg.TpDead);
            Header("WHAT IT SHOWS");
            G.Label(new Rect(_rx, _py, _rw, 22), "For each teammate: name, health bar, weapon, and a distance with an arrow pointing toward them.", 13, G.Text); _py += 24;
            G.Label(new Rect(_rx, _py, _rw, 22), "Dead teammates are greyed out with their kills and deaths.", 13, G.Text); _py += 28;
            Note("Your own team only. It never shows enemies and uses nothing but the health and position data the game already shares with your team. Shows during matches, not in the lobby hub.", 60);
        }

        // ---------- Combat Log ----------
        static void ClPane()
        {
            EnableRow(Cfg.ClOn);
            var c = Ctl("Position");
            Cfg.ClCorner.Value = G.Segmented(c, Corners, Cfg.ClCorner.Value);
            Sld("Show each line for (s)", Cfg.ClTime, 2f, 10f, "0.0");
            Tog("Show damage dealt", Cfg.ClDealt);
            Tog("Show healing", Cfg.ClHeal);
            Tog("Fight summaries", Cfg.ClSummary);
            Header("EXAMPLE");
            string[] ex = { "-34  taken", "+48  dealt  (headshot)", "+22  healed a teammate", "+9  health regained", "Fight summary: dealt 112, took 87" };
            foreach (var l in ex) { G.Label(new Rect(_rx, _py, _rw, 22), "-  " + l, 13, G.Text); _py += 22; }
            _py += 6;
            Note("Built from your own health and your own scoreboard stats. Damage taken is the drop in your health, so it never says who hit you or from where.", 44);
        }
    }
}
