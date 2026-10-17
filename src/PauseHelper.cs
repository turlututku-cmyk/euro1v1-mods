using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Euro3v3.ModsMenu
{
    // Number-key shortcuts for the pause menu buttons. Only reacts while the PAUSED screen is showing.
    public static partial class Features
    {
        static float _nextPauseScan;
        static bool _pauseVisible;

        static string NormText(string s)
        {
            if (s == null) return "";
            return s.Replace('İ', 'I').Replace('ı', 'i').Trim().ToLowerInvariant();
        }

        static bool FindPause()
        {
            foreach (var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
            {
                if (NormText(t.text) == "paused" && t.gameObject.activeInHierarchy) return true;
            }
            return false;
        }

        static bool ClickPauseButton(string word)
        {
            foreach (var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
            {
                if (!t.gameObject.activeInHierarchy) continue;
                if (!NormText(t.text).Contains(word)) continue;
                var b = t.GetComponentInParent<Button>();
                if (b == null || !b.interactable) continue;
                b.onClick.Invoke();
                return true;
            }
            return false;
        }

        static void UpdatePause()
        {
            if (!Cfg.PauseOn.Value) { _pauseVisible = false; return; }
            if (Time.unscaledTime >= _nextPauseScan)
            {
                _nextPauseScan = Time.unscaledTime + 0.25f;
                _pauseVisible = FindPause();
            }
            if (!_pauseVisible) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (KeyHit(Cfg.PhKeys[0].Value)) ClickPauseButton("resume");
            else if (KeyHit(Cfg.PhKeys[1].Value)) ClickPauseButton("settings");
            else if (KeyHit(Cfg.PhKeys[2].Value)) ClickPauseButton("locker");
            else if (KeyHit(Cfg.PhKeys[3].Value)) ClickPauseButton("edit profile");
            else if (KeyHit(Cfg.PhKeys[4].Value)) PanelOpen = !PanelOpen;
        }

        static void DrawPauseHints()
        {
            if (!Cfg.PauseOn.Value || !Cfg.PauseHints.Value || !_pauseVisible) return;
            string[] keys = new string[5]; for (int q = 0; q < 5; q++) keys[q] = BindLabel(Cfg.PhKeys[q].Value);
            string[] names = { "Resume", "Settings", "Locker / Shop", "Edit Profile", "Mods" };
            const float w = 250f, rowH = 28f;
            float h = 40f + keys.Length * rowH;
            var r = new Rect(18, Screen.height / 2f - h / 2f, w, h);
            G.Fill(r, new Color(0, 0, 0, 0.7f));
            G.Fill(new Rect(r.x, r.y, 3, r.height), G.Accent);
            G.Label(new Rect(r.x + 14, r.y + 4, w - 20, 30), "PAUSE SHORTCUTS", 12, G.Accent, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < keys.Length; i++)
            {
                var row = new Rect(r.x + 14, r.y + 38 + i * rowH, w - 24, rowH);
                var kb = new Rect(row.x, row.y + 3, 62, 22);
                G.Fill(kb, G.Row);
                G.Label(kb, keys[i], 13, G.Text, TextAnchor.MiddleCenter, true);
                G.Label(new Rect(row.x + 72, row.y, row.width - 72, rowH), names[i], 14, G.Text);
            }
        }
    }
}
