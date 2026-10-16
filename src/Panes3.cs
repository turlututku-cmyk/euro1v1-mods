using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        static void OpenFolder(string dir)
        {
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + dir + "\"") { UseShellExecute = true });
            }
            catch (System.Exception e) { Features.Toast("Could not open folder: " + e.Message); }
        }

        // ---------- Input Display ----------

        static void InputPane()
        {
            EnableRow(Cfg.InputOn);
            var c = Ctl("Position");
            Cfg.InputCorner.Value = G.Segmented(c, new[] { "Top-left", "Top-right", "Bottom-left", "Bottom-right" }, Cfg.InputCorner.Value);
            Sld("Size", Cfg.InputScale, 0.6f, 2f, "0.00");
            Sld("Opacity", Cfg.InputOpacity, 0.2f, 1f, "0.00");
            Tog("Show mouse buttons", Cfg.InputMouse);
            Note("Shows the keys you press (W A S D, Shift, Ctrl, Space, mouse buttons and E). Handy for clips and tutorials.");
        }

        // ---------- Hit Sounds ----------

        static void HitSoundsPane()
        {
            EnableRow(Cfg.HitOn);
            Sld("Volume", Cfg.HitVol, 0f, 1f, "0.00");
            var a = Ctl("Hit sound");
            Cfg.HitStyle.Value = G.Segmented(a, Features.HitStyles, Cfg.HitStyle.Value);
            var b = Ctl("Kill sound");
            Cfg.KillStyle.Value = G.Segmented(b, Features.HitStyles, Cfg.KillStyle.Value);
            Tog("Pitch variation", Cfg.HitVary);
            Tog("Mute the game's hit sound", Cfg.HitMuteGame);
            var t = Ctl("Test", 34);
            if (SmallBtn(new Rect(t.x, t.y, 110, 30), "TEST HIT", G.Accent)) Features.PlayHitSound(false);
            if (SmallBtn(new Rect(t.x + 118, t.y, 110, 30), "TEST KILL", G.Accent)) Features.PlayHitSound(true);
            if (SmallBtn(new Rect(t.x + 236, t.y, 150, 30), "OPEN SOUNDS FOLDER")) OpenFolder(System.IO.Path.Combine(Paths.GameRootPath, "Mods", "Sounds"));
            Note("Custom file: put hit.wav and kill.wav (mp3 or ogg also work) in Mods\\Sounds, then pick \"Custom file\".", 40);
        }

        // ---------- Damage Numbers ----------

        static void DamagePane()
        {
            EnableRow(Cfg.DmgOn);
            Sld("Text size", Cfg.DmgSize, 14f, 60f, "0");
            Sld("Duration (s)", Cfg.DmgTime, 0.4f, 2.5f, "0.0");
            var pl = Ctl("Placement");
            Cfg.DmgPlace.Value = G.Segmented(pl, new[] { "On the enemy", "At the crosshair" }, Cfg.DmgPlace.Value);
            Tog("Show KILL tag", Cfg.DmgKillTag);
            Tog("Show healing (+N in green)", Cfg.DmgHeal);
            Note("One number per shot: shotgun pellets are added together. Healing your teammates shows a green +N. Big hits turn orange and kills turn red. Only enemies, only when your own hitmarker fires.", 60);
        }
    }
}
