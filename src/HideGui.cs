using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Hides clutter in the game's UI by fading it out (CanvasGroup alpha 0, no raycasts). Nothing is destroyed or deactivated,
    // so turning an option off restores the element exactly as it was.
    // The health bar (HUD/Healthbar, the one labelled "Integrity") is deliberately never touched.
    public static partial class Features
    {
        class Hidden
        {
            public CanvasGroup Group;
            public float Alpha; public bool Interactable, Raycasts;
            public bool Added;
        }

        static readonly Dictionary<int, Hidden> _hiddenUi = new Dictionary<int, Hidden>();
        static float _nextHideScan;

        static bool IsHudChild(Transform t) => t.parent != null && t.parent.name == "HUD";

        static bool WantHidden(Transform t, string name)
        {
            switch (name)
            {
                case "SpacepodAD": return Cfg.HideAds.Value;
                case "NewsSlideshow": return Cfg.HideNews.Value;
                case "ReportIssue": return Cfg.HideFeedback.Value;
                case "Tasks": return Cfg.HideTasks.Value;
                case "QuestSystemPreparedUI": return Cfg.HideQuest.Value;
                case "StaticLevelDisplay": return Cfg.HideLevel.Value;
                case "PartyDisplay": return Cfg.HideParty.Value;
                // in-match HUD (direct children of UIManager/Menu/HUD)
                case "Compass":
                case "Altimeter":
                case "Airspeed": return Cfg.HideDials.Value && IsHudChild(t);
                case "ATM INFO": return Cfg.HideAtm.Value && IsHudChild(t);
                case "Ingame Level Display": return Cfg.HideIngameLevel.Value && IsHudChild(t);
            }
            return false;
        }

        static bool IsVersionLabel(TMP_Text t)
        {
            var s = t.text;
            return s != null && s.Length > 3 && s.Length < 16 && s[0] == '(' && s[1] == 'v' && s[s.Length - 1] == ')' && char.IsDigit(s[2]);
        }

        static void Track(Transform t, HashSet<int> want)
        {
            int id = t.gameObject.GetInstanceID();
            want.Add(id);
            if (_hiddenUi.ContainsKey(id)) return;
            var g = t.GetComponent<CanvasGroup>();
            var h = new Hidden();
            if (g == null) { g = t.gameObject.AddComponent<CanvasGroup>(); h.Added = true; }
            h.Group = g; h.Alpha = g.alpha; h.Interactable = g.interactable; h.Raycasts = g.blocksRaycasts;
            _hiddenUi[id] = h;
            Plugin.Instance.Log.LogInfo("Hide GUI: hiding '" + t.name + "'");
        }

        static void UpdateHideGui()
        {
            if (Time.unscaledTime < _nextHideScan) return;
            _nextHideScan = Time.unscaledTime + 0.5f;
            bool master = Cfg.BvOn.Value && Cfg.HideOn.Value;

            if (!master)
            {
                if (_hiddenUi.Count > 0) RestoreHidden(null);
                return;
            }

            var want = new HashSet<int>();
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
            {
                bool hide = WantHidden(t, t.name);
                if (!hide && Cfg.HideVersion.Value)
                {
                    var tmp = t.GetComponent<TMP_Text>();
                    if (tmp != null && IsVersionLabel(tmp)) hide = true;
                }
                if (hide) Track(t, want);
            }

            foreach (var kv in _hiddenUi)
            {
                try
                {
                    var g = kv.Value.Group;
                    if (want.Contains(kv.Key)) { g.alpha = 0f; g.interactable = false; g.blocksRaycasts = false; }
                }
                catch { }
            }
            RestoreHidden(want);
        }

        // Restore everything not in 'keep' (null = restore all).
        static void RestoreHidden(HashSet<int> keep)
        {
            var remove = new List<int>();
            foreach (var kv in _hiddenUi)
            {
                if (keep != null && keep.Contains(kv.Key)) continue;
                try
                {
                    var h = kv.Value;
                    h.Group.alpha = h.Alpha; h.Group.interactable = h.Interactable; h.Group.blocksRaycasts = h.Raycasts;
                    if (h.Added) UnityEngine.Object.Destroy(h.Group);
                }
                catch { }
                remove.Add(kv.Key);
            }
            foreach (var id in remove) _hiddenUi.Remove(id);
        }
    }
}
