using System;
using System.IO;
using System.Linq;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Euro3v3.ModsMenu
{
    // PLAY in the main menu has no map loaded. This starts the game's own practice lobby (a private mission with one bot),
    // waits for the map to load, switches the bot off and then opens the clip inside the real map.
    public static partial class Features
    {
        const string ViewMissionKey = "bot_mission_clipview";
        static ClipInfo _openPending;
        static int _openStage;           // 0 idle, 1 starting the mission, 2 waiting for the map
        static float _openT, _nextOpenTry, _openReadyAt;
        static bool _openFallback;

        static string ViewMissionSrc => Path.Combine(Paths.GameRootPath, "Mods", "Missions", ViewMissionKey + ".json");
        static string ViewMissionDst => Path.Combine(Paths.GameRootPath, "Euro3v3_Data", "StreamingAssets", "Missions", ViewMissionKey + ".json");

        static bool EnsureViewMission()
        {
            try
            {
                if (!File.Exists(ViewMissionSrc)) { Toast("Clip map mission file is missing (Mods/Missions)"); return false; }
                var src = new FileInfo(ViewMissionSrc); var dst = new FileInfo(ViewMissionDst);
                if (!dst.Exists || dst.Length != src.Length) { Directory.CreateDirectory(dst.DirectoryName); File.Copy(src.FullName, dst.FullName, true); }
                return true;
            }
            catch (Exception e) { Toast("Could not prepare the map: " + e.Message); return false; }
        }

        static bool ClickVsBotsButton()
        {
            foreach (var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
            {
                if (!t.gameObject.activeInHierarchy || NormText(t.text) != "play vs bots") continue;
                var b = t.GetComponentInParent<Button>();
                if (b == null) continue;
                b.onClick.Invoke();
                return true;
            }
            return false;
        }

        public static void OpenClipInMap(ClipInfo ci)
        {
            if (!EnsureViewMission()) return;
            _openPending = ci; _openStage = 1; _openT = Time.unscaledTime; _nextOpenTry = 0f; _openFallback = false;
            Toast("Loading the map for your clip...");
        }

        static void UpdateOpenInMap()
        {
            if (_openStage == 0) return;
            float now = Time.unscaledTime;
            if (now - _openT > 45f) { _openStage = 0; _openPending = null; Toast("Could not load the map. Start a match, then press PLAY"); return; }

            if (_openStage == 1)
            {
                if (now < _nextOpenTry) return;
                _nextOpenTry = now + 0.5f;
                BotMissionPlayModeButton pick = null;
                try
                {
                    foreach (var c in Resources.FindObjectsOfTypeAll<BotMissionPlayModeButton>())
                    {
                        if (c == null || c.gameObject == null || c.gameObject.scene.name == null) continue;
                        if (pick == null || c.gameObject.activeInHierarchy) pick = c;
                        if (c.gameObject.activeInHierarchy) break;
                    }
                }
                catch { }
                if (pick == null)
                {
                    if (!_openFallback && ClickVsBotsButton()) _openFallback = true;
                    return;
                }
                try
                {
                    if (!pick.IsRunning) pick.StartChallenge(ViewMissionKey);
                    Plugin.Instance.Log.LogInfo("Clip viewer: loading the map via '" + pick.gameObject.name + "'");
                    _openStage = 2; _openReadyAt = now + 4f;
                }
                catch (Exception e)
                {
                    Plugin.Instance.Log.LogWarning("Clip viewer: could not start the map: " + e.Message);
                    if (!_openFallback && ClickVsBotsButton()) _openFallback = true;
                }
                return;
            }

            // stage 2: wait until the map and the player exist
            if (now < _openReadyAt) return;
            var me = LocalPlayer();
            if (me == null) return;
            try
            {
                foreach (var p in UnityEngine.Object.FindObjectsOfType<PlayerObjectController>())
                    if (p != me && p.IsBot) p.gameObject.SetActive(false); // the helper bot is only there to load the map
            }
            catch { }
            var ci = _openPending;
            _openStage = 0; _openPending = null;
            if (ci != null) StartViewerNow(ci);
        }
    }
}
