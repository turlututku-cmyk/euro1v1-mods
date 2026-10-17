using BepInEx.Configuration;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        static int _clipOff;
        static string _clipDelete;
        static float _clipDeleteUntil;

        // ---------- Clip Maker ----------

        static void ClipPane()
        {
            Features.EnsureClipsScanned();
            _rowH = 30f;
            EnableRow(Cfg.ClipOn, "Record clips");
            Sld("Keep last (s)", Cfg.ClipSeconds, 30f, 300f, "0");
            Tog("Open clips in the real map", Cfg.ClipAutoMap);

            var st = new Rect(_rx, _py, _rw, 28);
            G.Fill(st, G.Panel);
            string status = Features.Recording
                ? "Recording: " + Mathf.RoundToInt(Features.BufferSeconds) + " s buffered, " + Features.BufferPlayers + " players"
                : (Cfg.ClipOn.Value ? "Waiting for a match..." : "Off");
            G.Label(new Rect(st.x + 12, st.y, st.width - 24, st.height), status, 13, Features.Recording ? G.Accent : G.Dim, TextAnchor.MiddleLeft, true);
            _py += 34;

            string recLabel = Features.ManualRec ? "STOP & SAVE  " + ((int)(Features.ManualElapsed / 60f)).ToString("00") + ":" + ((int)(Features.ManualElapsed % 60f)).ToString("00") : "RECORD (" + Features.BindLabel(Cfg.KbClipRec.Value) + ")";
            if (SmallBtn(new Rect(_rx, _py, 180, 30), recLabel, new Color(1f, 0.25f, 0.25f))) Features.ToggleManualRecord();
            if (SmallBtn(new Rect(_rx + 188, _py, 170, 30), "SAVE LAST (" + Features.BindLabel(Cfg.KbClipSave.Value) + ")", G.Accent)) Features.SaveClip(null);
            if (SmallBtn(new Rect(_rx + 366, _py, 120, 30), "OPEN FOLDER")) OpenFolder(Features.ClipsDir);
            if (SmallBtn(new Rect(_rx + 494, _py, 80, 30), "RESCAN")) Features.ScanClips();
            if (SmallBtn(new Rect(_rx + 582, _py, 110, 30), "VIDEOS")) OpenFolder(Features.VideosDir);
            _py += 36;

            string[] kcap = { "Save last clip", "Record clip", "Leave the viewer" };
            ConfigEntry<string>[] kent = { Cfg.KbClipSave, Cfg.KbClipRec, Cfg.KbViewerExit };
            string[] kdef = { "F7", "Shift+F7", "Backspace" };
            for (int i = 0; i < 3; i++)
            {
                float kx = _rx + i * 232f;
                G.Label(new Rect(kx, _py, 220, 17), kcap[i] + " key", 11, G.Dim);
                KeyButton(new Rect(kx, _py + 15f, 220, 26), kent[i], kdef[i]);
            }
            _py += 46f;
            Header("SAVED CLIPS  (" + Features.Clips.Count + ")   view them inside the same map");
            const int visible = 3;
            _clipOff = Mathf.Clamp(_clipOff, 0, Mathf.Max(0, Features.Clips.Count - visible));
            if (Features.Clips.Count == 0)
                G.Label(new Rect(_rx, _py, _rw, 40), "No clips yet. During a match press F7 to save the last " + Mathf.RoundToInt(Cfg.ClipSeconds.Value) + " seconds.", 13, G.Dim, TextAnchor.UpperLeft, false, true);
            for (int i = 0; i < visible; i++)
            {
                int idx = _clipOff + i;
                if (idx >= Features.Clips.Count) break;
                var ci = Features.Clips[idx];
                var row = new Rect(_rx, _py, _rw - 40, 46);
                G.Fill(row, G.Hover(row) ? G.RowHover : G.Row);
                G.Label(new Rect(row.x + 12, row.y + 3, row.width - 250, 22), ci.Name, 14, G.Text, TextAnchor.MiddleLeft, true);
                G.Label(new Rect(row.x + 12, row.y + 24, row.width - 250, 20), Mathf.RoundToInt(ci.Duration) + " s   map " + ci.Scene + "   " + ci.PlayerCount + " players   " + ci.Saved.ToString("dd.MM HH:mm"), 11, G.Dim);
                if (SmallBtn(new Rect(row.xMax - 168, row.y + 8, 76, 30), "PLAY", G.Accent)) { Features.StartViewer(ci); if (Features.ViewerActive) Features.PanelOpen = false; }
                bool sure = _clipDelete == ci.Path && Time.unscaledTime < _clipDeleteUntil;
                if (SmallBtn(new Rect(row.xMax - 86, row.y + 8, 80, 30), sure ? "SURE?" : "DELETE", new Color(1f, 0.3f, 0.3f)))
                {
                    if (sure) { Features.DeleteClip(ci); _clipDelete = null; break; }
                    _clipDelete = ci.Path; _clipDeleteUntil = Time.unscaledTime + 3f;
                }
                _py += 50;
            }
            if (Features.Clips.Count > visible)
            {
                float top = _py - 50f * visible;
                if (SmallBtn(new Rect(_rx + _rw - 34, top, 34, 30), "^")) _clipOff = Mathf.Max(0, _clipOff - 1);
                if (SmallBtn(new Rect(_rx + _rw - 34, top + 50, 34, 30), "v")) _clipOff = Mathf.Min(Features.Clips.Count - visible, _clipOff + 1);
            }
            _rowH = 36f;
        }
    }
}
