using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        static string _profName = "";
        static int _trackOff;

        static bool SmallBtn(Rect r, string text, Color? accent = null)
        {
            G.Fill(r, G.Hover(r) ? G.RowHover : G.Row);
            if (accent.HasValue) G.Fill(new Rect(r.x, r.yMax - 2, r.width, 2), accent.Value);
            G.Label(r, text, 12, G.Text, TextAnchor.MiddleCenter, true);
            return G.Click(r);
        }

        // ---------- Crosshair Profiles ----------

        static void ProfilesPane()
        {
            Profiles.EnsureLoaded();
            EnableRow(Cfg.ProfOn);

            var r = Ctl("Name");
            var nameBox = new Rect(r.x, r.y, r.width - 150, r.height);
            G.Fill(nameBox, G.Panel);
            Outline(nameBox, Border);
            _profName = G.TextField("profname", nameBox, _profName, "e.g. Competitive", 18);
            if (SmallBtn(new Rect(nameBox.xMax + 8, r.y, 142, r.height), "SAVE CURRENT", G.Accent))
            {
                if (Profiles.SaveCurrent(_profName)) _profName = "";
            }

            Header("SAVED PROFILES  (F9 = next profile)");
            if (Profiles.List.Count == 0)
            {
                G.Label(new Rect(_rx, _py, _rw, 30), "No profiles yet. Set up your crosshair in Crosshair Changer, then save it here.", 13, G.Dim);
                _py += 34;
            }
            foreach (var p in Profiles.List.ToList())
            {
                var row = new Rect(_rx, _py, _rw, 34);
                bool active = p.Name == Profiles.Active;
                G.Fill(row, active ? new Color(0.12f, 0.2f, 0.13f, 1f) : G.Row);
                if (active) G.Fill(new Rect(row.x, row.y, 3, row.height), G.Accent);

                // mini preview
                var pv = new Rect(row.x + 12, row.y + 5, 24, 24);
                G.Fill(pv, new Color(0.17f, 0.19f, 0.2f, 1f));
                G.Fill(new Rect(pv.center.x - 1, pv.y + 3, 2, pv.height - 6), new Color(p.R, p.G, p.B, 1f));
                G.Fill(new Rect(pv.x + 3, pv.center.y - 1, pv.width - 6, 2), new Color(p.R, p.G, p.B, 1f));

                G.Label(new Rect(row.x + 46, row.y, _rw - 250, row.height), p.Name + (active ? "   (active)" : ""), 14, G.Text, TextAnchor.MiddleLeft, true);
                if (SmallBtn(new Rect(row.xMax - 168, row.y + 4, 76, 26), "LOAD", G.Accent)) { Profiles.Load(p); G.Changed = true; G.Released = true; }
                if (SmallBtn(new Rect(row.xMax - 86, row.y + 4, 76, 26), "DELETE", new Color(1f, 0.3f, 0.3f))) { Profiles.Delete(p); break; }
                _py += 38;
            }
        }

        // ---------- Pause Helper ----------

        static void PausePane()
        {
            EnableRow(Cfg.PauseOn);
            Tog("Show key hints", Cfg.PauseHints);
            Header("SHORTCUTS (while the PAUSED screen is showing)");
            string[] k = { "1", "2", "3", "4", "5" };
            string[] n = { "Resume", "Settings", "Locker / Shop", "Edit Profile", "Open this Mods menu" };
            for (int i = 0; i < k.Length; i++)
            {
                var kb = new Rect(_rx, _py, 30, 26);
                G.Fill(kb, G.Row);
                G.Label(kb, k[i], 14, G.Text, TextAnchor.MiddleCenter, true);
                G.Label(new Rect(_rx + 44, _py, _rw - 44, 26), n[i], 14, G.Text);
                _py += 32;
            }
            Note("Disconnect has no shortcut on purpose, so you can't leave a match by accident.");
        }

        // ---------- Menu Music Player ----------

        static void MusicPane()
        {
            _rowH = 30f;
            EnableRow(Cfg.MusicOn);

            var np = new Rect(_rx, _py, _rw, 34);
            G.Fill(np, G.Panel);
            G.Label(new Rect(np.x + 12, np.y, np.width - 24, np.height),
                (Features.MusicPlaying ? "Playing: " : "Stopped: ") + (string.IsNullOrEmpty(Features.NowPlaying) ? "-" : Features.NowPlaying), 14, G.Text, TextAnchor.MiddleLeft, true);
            _py += 40;

            float bw = 96f;
            if (SmallBtn(new Rect(_rx, _py, bw, 32), "< PREV")) Features.MusicNext(-1);
            if (SmallBtn(new Rect(_rx + bw + 6, _py, bw + 20, 32), Features.MusicPlaying ? "PAUSE" : "PLAY", G.Accent)) Features.MusicToggle();
            if (SmallBtn(new Rect(_rx + 2 * bw + 32, _py, bw, 32), "NEXT >")) Features.MusicNext(1);
            if (SmallBtn(new Rect(_rx + _rw - 250, _py, 120, 32), "OPEN FOLDER")) { try { System.IO.Directory.CreateDirectory(Features.MusicDir); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + Features.MusicDir + "\"") { UseShellExecute = true }); } catch (System.Exception e) { Features.Toast("Could not open folder: " + e.Message); } }
            if (SmallBtn(new Rect(_rx + _rw - 124, _py, 124, 32), "RESCAN")) Features.MusicScan();
            _py += 40;

            Sld("Volume", Cfg.MusicVol, 0f, 1f, "0.00");
            Tog("Shuffle", Cfg.MusicShuffle);
            Tog("Menus only", Cfg.MusicMenusOnly);
            Tog("Mute game music", Cfg.MusicMuteGame);

            Header("TRACKS  (" + Features.Tracks.Count + ")  folder: Mods\\Music");
            if (Features.Tracks.Count == 0)
            {
                G.Label(new Rect(_rx, _py, _rw, 44), "No tracks yet. Put .mp3, .wav or .ogg files in the Music folder, then press RESCAN.", 13, G.Dim, TextAnchor.UpperLeft, false, true);
            }
            const int visible = 5;
            _trackOff = Mathf.Clamp(_trackOff, 0, Mathf.Max(0, Features.Tracks.Count - visible));
            for (int i = 0; i < visible; i++)
            {
                int idx = _trackOff + i;
                if (idx >= Features.Tracks.Count) break;
                var row = new Rect(_rx, _py, _rw - 40, 30);
                bool cur = idx == Features.MusicCur;
                G.Fill(row, cur ? new Color(0.12f, 0.2f, 0.13f, 1f) : (G.Hover(row) ? G.RowHover : G.Row));
                if (cur) G.Fill(new Rect(row.x, row.y, 3, row.height), G.Accent);
                G.Label(new Rect(row.x + 14, row.y, row.width - 20, row.height), System.IO.Path.GetFileNameWithoutExtension(Features.Tracks[idx]), 13, G.Text);
                if (G.Click(row)) Features.MusicPlay(idx);
                _py += 33;
            }
            if (Features.Tracks.Count > visible)
            {
                float top = _py - 33f * visible;
                if (SmallBtn(new Rect(_rx + _rw - 34, top, 34, 30), "^")) _trackOff = Mathf.Max(0, _trackOff - 1);
                if (SmallBtn(new Rect(_rx + _rw - 34, top + 33, 34, 30), "v")) _trackOff = Mathf.Min(Features.Tracks.Count - visible, _trackOff + 1);
            }
            _rowH = 36f;
        }

        // ---------- Freecam (Team) ----------

        static void FreecamPane()
        {
            EnableRow(Cfg.FreeOn);
            Sld("Mouse sensitivity", Cfg.FreeSens, 0.03f, 0.4f, "0.00");
            Header("HOW IT WORKS");
            string[] lines =
            {
                "F6 turns the team cam on and off while in a match.",
                "Q and E switch between teammates. Scroll zooms (short leash).",
                "It only follows your own team and cannot pass through walls.",
                "Works when you are dead, or in matches with no other human players.",
                "Turns itself off if the rules stop being met (for example you respawn in a live match).",
                "Local only: nothing is sent to the server.",
            };
            foreach (var l in lines)
            {
                G.Label(new Rect(_rx, _py, _rw, 24), "-  " + l, 13, G.Text);
                _py += 26;
            }
        }

        // ---------- RTX Mode ----------

        static void RtxPane()
        {
            _rowH = 30f;
            EnableRow(Cfg.RtxOn);
            Header("QUALITY");
            Sld("Render scale", Cfg.RtxScale, 0.75f, 1.5f, "0.00");
            var ms = Ctl("Anti-aliasing");
            Cfg.RtxMsaa.Value = G.Segmented(ms, new[] { "Off", "2x", "4x", "8x" }, Cfg.RtxMsaa.Value);
            Tog("Shadows", Cfg.RtxShadows);
            Sld("Shadow distance", Cfg.RtxShadowDist, 40f, 300f, "0");
            TogPair("HDR", Cfg.RtxHdr, "Sharp textures", Cfg.RtxAniso);
            Sld("Detail distance", Cfg.RtxLod, 1f, 3f, "0.0");
            Header("LOOK");
            Sld("Bloom", Cfg.RtxBloom, 0f, 2f, "0.00");
            Sld("Saturation", Cfg.RtxSat, -50f, 50f, "0");
            Sld("Contrast", Cfg.RtxContrast, -30f, 40f, "0");
            Sld("Exposure", Cfg.RtxExposure, -1f, 1f, "0.00");
            Tog("Cinematic tonemap", Cfg.RtxTonemap);
            G.Label(new Rect(_rx, _py + 2, _rw, 40),
                "Not real ray tracing: raises render quality and adds bloom, tonemapping and color grading. Higher settings cost FPS.",
                12, G.Dim, TextAnchor.UpperLeft, false, true);
            _rowH = 36f;
        }
    }
}
