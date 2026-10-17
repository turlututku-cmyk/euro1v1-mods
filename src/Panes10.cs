using System.Linq;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        // ---------- Aim Trainer ----------
        static void AimPane()
        {
            _rowH = 30f;
            EnableRow(Cfg.AimOn);
            var m = Ctl("Drill");
            Cfg.AimMode.Value = G.Segmented(m, Features.AimModes, Cfg.AimMode.Value);
            Sld("Duration (s)", Cfg.AimTime, 15f, 120f, "0");
            Sld("Target size (m)", Cfg.AimSize, 0.25f, 1.5f, "0.00");
            var d = Ctl("Distance");
            Cfg.AimDist.Value = G.Segmented(d, Features.AimDists, Cfg.AimDist.Value);
            TogPair("Hit sounds", Cfg.AimSound, "Results card", Cfg.AimCard);
            KeyRow("Start / stop key", Cfg.KbAim, "F2");
            var b = Ctl("", 34);
            if (SmallBtn(new Rect(b.x, b.y, 170, 30), Features.AimActive ? "STOP  (" + Features.BindLabel(Cfg.KbAim.Value) + ")" : "START  (" + Features.BindLabel(Cfg.KbAim.Value) + ")", G.Accent)) Features.StartAim();
            if (SmallBtn(new Rect(b.x + 180, b.y, 230, 30), "WARM-UP ROUTINE  (3 min)", new Color(1f, 0.82f, 0.2f))) Features.StartAimRoutine();

            Header("BEST  (points per minute)");
            string l1 = "", l2 = "";
            for (int i = 0; i < Features.AimModes.Length; i++)
            {
                string s = Features.AimModes[i] + " " + Cfg.AimBests[i].Value + "      ";
                if (i < 3) l1 += s; else l2 += s;
            }
            G.Label(new Rect(_rx, _py, _rw, 22), l1, 13, G.Text); _py += 22;
            G.Label(new Rect(_rx, _py, _rw, 22), l2, 13, G.Text); _py += 24;

            Header("RECENT RUNS");
            var recent = Features.AimRecentLines(4);
            if (recent.Count == 0) { G.Label(new Rect(_rx, _py, _rw, 20), "Nothing yet. Start a bot match, press F2.", 12, G.Dim); _py += 22; }
            foreach (var line in recent) { G.Label(new Rect(_rx, _py, _rw, 20), line, 12, G.Dim); _py += 20; }
            _rowH = 36f;
        }
    }
}
