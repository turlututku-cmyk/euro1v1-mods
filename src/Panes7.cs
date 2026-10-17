using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        static readonly string[] DtStyles = { "Cheeky", "Wholesome", "Roast" };

        // ---------- Death Taunts ----------
        static void DtPane()
        {
            EnableRow(Cfg.DtOn);
            var c = Ctl("Style");
            Cfg.DtStyle.Value = G.Segmented(c, DtStyles, Cfg.DtStyle.Value);
            Sld("Text size", Cfg.DtSize, 24f, 90f, "0");
            Tog("Sometimes use the killer's name", Cfg.DtKiller);
            var t = Ctl("Test", 34);
            if (SmallBtn(new Rect(t.x, t.y, 130, 30), "TEST TAUNT", G.Accent)) Features.DtShow("SomeoneElse");
            Note("Pops a big joke line on your own screen when you die. Nothing is sent to other players.");
        }

        // ---------- Sad Trombone ----------
        static void StPane()
        {
            EnableRow(Cfg.StOn);
            Sld("Misses in a row", Cfg.StMisses, 3f, 12f, "0");
            Sld("Volume", Cfg.StVol, 0f, 1f, "0.00");
            Tog("Show 'wah wah wahhh' text", Cfg.StText);
            var t = Ctl("Test", 34);
            if (SmallBtn(new Rect(t.x, t.y, 130, 30), "PLAY IT", G.Accent)) Features.PlayTrombone();
            Note("Counts your own shots that hit nothing. Any hit resets the count. Resets when you die.");
        }

        // ---------- Scoreboard Screenshot ----------
        static void SbPane()
        {
            EnableRow(Cfg.SbOn);
            Tog("Show a result card (also in the picture)", Cfg.SbCard);
            Sld("Wait after match end (s)", Cfg.SbDelay, 0.5f, 6f, "0.0");
            var t = Ctl("", 34);
            if (SmallBtn(new Rect(t.x, t.y, 140, 30), "PREVIEW CARD", G.Accent)) Features.SbTest();
            if (SmallBtn(new Rect(t.x + 148, t.y, 130, 30), "OPEN FOLDER")) OpenFolder(System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "Mods", "Screenshots"));
            Note("When a match ends, a card with everyone's kills, deaths and damage is drawn and a PNG is saved to Mods\\Screenshots. Uses only the stats the scoreboard already shows.", 60);
        }

        // ---------- Chat Macros ----------
        static readonly string[] _cmBox = new string[6];
        static void CmPane()
        {
            EnableRow(Cfg.CmOn);
            var m = Ctl("Hold key");
            Cfg.CmMod.Value = G.Segmented(m, Features.CmMods, Cfg.CmMod.Value);
            Sld("Cooldown (s)", Cfg.CmCooldown, 1f, 10f, "0.0");
            Header("MESSAGES");
            string modName = Features.CmMods[Mathf.Clamp(Cfg.CmMod.Value, 0, 2)];
            for (int i = 0; i < 6; i++)
            {
                var r = Ctl(modName + " + " + (i + 1));
                var box = new Rect(r.x, r.y, r.width - 90, r.height);
                G.Fill(box, G.Panel);
                Outline(box, Border);
                if (_cmBox[i] == null) _cmBox[i] = Cfg.CmMsgs[i].Value;
                var nv = G.TextField("cmbox" + i, box, _cmBox[i], "message " + (i + 1), 80);
                if (nv != _cmBox[i]) { _cmBox[i] = nv; Cfg.CmMsgs[i].Value = nv; G.Changed = true; }
                int idx = i;
                if (SmallBtn(new Rect(box.xMax + 8, box.y, 80, box.height), "SEND")) Features.CmSend(idx);
            }
            Note("Hold the key and press 1 to 6 to send that message to the game chat as you. Keep it short. The cooldown stops accidental spam. If the game also switches weapon on the number, pick Ctrl or Shift instead of Alt.", 44);
        }
    }
}
