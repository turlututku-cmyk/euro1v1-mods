using System.Linq;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        static readonly string[] Corners = { "Top-left", "Top-right", "Bottom-left", "Bottom-right" };
        static string _noteBox;

        // ---------- Match Stats ----------
        static void StatsPane()
        {
            EnableRow(Cfg.StatsOn);
            var c = Ctl("Position");
            Cfg.StatsCorner.Value = G.Segmented(c, Corners, Cfg.StatsCorner.Value);
            Note("Shows your own damage, kills, deaths, K/D, accuracy, headshots and healing for the current match, using the game's own counters.", 60);
        }

        // ---------- Letterbox ----------
        static void BarsPane()
        {
            EnableRow(Cfg.BarsOn);
            Sld("Bar height", Cfg.BarsSize, 0.02f, 0.3f, "0.00");
            Note("Cinematic black bars at the top and bottom of the screen. Nice for clips and screenshots.");
        }

        // ---------- Screen Tint ----------
        static void TintPane()
        {
            EnableRow(Cfg.TintOn);
            var c = Ctl("Color");
            Cfg.TintIdx.Value = G.Segmented(c, Features.TintNames, Cfg.TintIdx.Value);
            Sld("Strength", Cfg.TintStrength, 0f, 0.7f, "0.00");
            Note("A soft color wash over the whole screen: warm sunset, cool blue, sepia, night and more. Purely visual.");
        }

        // ---------- FPS Graph ----------
        static void GraphPane()
        {
            EnableRow(Cfg.GraphOn);
            var c = Ctl("Position");
            Cfg.GraphCorner.Value = G.Segmented(c, Corners, Cfg.GraphCorner.Value);
            Sld("Size", Cfg.GraphScale, 0.7f, 1.8f, "0.00");
            Note("A live graph of your last 150 frames. Green is under 16.7 ms (60 fps), yellow is slower, red is under 30 fps. Good for spotting stutter.", 60);
        }

        // ---------- Notes ----------
        static void NotesPane()
        {
            EnableRow(Cfg.NotesOn);
            var c = Ctl("Position");
            Cfg.NotesCorner.Value = G.Segmented(c, Corners, Cfg.NotesCorner.Value);
            Sld("Text size", Cfg.NotesSize, 10f, 32f, "0");
            var r = Ctl("Text");
            var box = new Rect(r.x, r.y, r.width, r.height);
            G.Fill(box, G.Panel);
            Outline(box, Border);
            if (_noteBox == null) _noteBox = Cfg.NotesText.Value;
            var nv = G.TextField("notebox", box, _noteBox, "e.g. warm up | aim for the head", 200);
            if (nv != _noteBox) { _noteBox = nv; Cfg.NotesText.Value = nv; G.Changed = true; }
            if (G.Released || (Event.current.type == EventType.MouseUp)) { }
            Note("Use | to start a new line. Your notes stay on screen while you play. Saved in the settings file.");
        }

        // ---------- Settings Backup ----------
        static void BackupPane()
        {
            EnableRow(Cfg.BackupOn, "Installed");
            var b = Ctl("", 34);
            if (SmallBtn(new Rect(b.x, b.y, 150, 30), "BACK UP NOW", G.Accent)) Features.BackupNow();
            if (SmallBtn(new Rect(b.x + 158, b.y, 160, 30), "OPEN BACKUP FOLDER")) OpenFolder(Features.BackupDir);
            Header("BACKUPS  (newest first)");
            var files = Features.BackupFiles();
            if (files.Count == 0) G.Label(new Rect(_rx, _py, _rw, 24), "No backups yet.", 13, G.Dim);
            foreach (var f in files.Take(5))
            {
                var row = new Rect(_rx, _py, _rw, 34);
                G.Fill(row, G.Row);
                G.Label(new Rect(row.x + 12, row.y, row.width - 130, row.height), System.IO.Path.GetFileNameWithoutExtension(f), 13, G.Text);
                if (SmallBtn(new Rect(row.xMax - 108, row.y + 4, 100, 26), "RESTORE", G.Accent)) Features.RestoreBackup(f);
                _py += 38;
            }
            Note("Backs up all your Mods menu settings (including your crosshair profiles). Restore brings them back instantly.", 40);
        }
    }
}
