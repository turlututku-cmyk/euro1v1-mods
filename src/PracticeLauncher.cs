using System;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Practice Launcher: one click starts one of the game's own bot missions (Easy / Normal / Hard / Expert).
    // Uses the same button the "Play vs Bots" menu uses, so nothing is faked.
    public static partial class Features
    {
        public static readonly string[] PracticeNames = { "Easy", "Normal", "Hard", "Expert" };
        static readonly string[] PracticeKeys = { "bot_mission_easy", "bot_mission_normal", "bot_mission_hard", "bot_mission_expert" };
        static string _plKey; static float _plT, _plNext; static bool _plFallback;

        static bool InLiveMatchWithHumans()
        {
            var s = StateName();
            bool play = s == "BuildPhaseState" || s == "PlanPhaseState" || s == "BlueAttackState" || s == "RedAttackState" || s == "HalfBreakState" || s == "DrawArenaState";
            return play && OtherHumansPresent();
        }

        public static void PracticeStart(int level)
        {
            level = Mathf.Clamp(level, 0, 3);
            if (!Cfg.PlOn.Value) { Toast("Turn Practice Launcher on first"); return; }
            if (InLiveMatchWithHumans()) { Toast("Leave your current match first"); return; }
            _plKey = PracticeKeys[level]; _plT = Time.unscaledTime; _plNext = 0f; _plFallback = false;
            PanelOpen = false;
            Toast("Starting practice: " + PracticeNames[level]);
        }

        static void UpdatePractice()
        {
            if (_plKey == null) return;
            float now = Time.unscaledTime;
            if (now - _plT > 30f) { _plKey = null; Toast("Could not start practice. Use Play > VS Bots"); return; }
            if (now < _plNext) return;
            _plNext = now + 0.5f;

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
                if (!_plFallback && ClickVsBotsButton()) _plFallback = true;
                return;
            }
            try
            {
                if (!pick.IsRunning) pick.StartChallenge(_plKey);
                Plugin.Instance.Log.LogInfo("Practice launcher: started '" + _plKey + "' via '" + pick.gameObject.name + "'");
                _plKey = null;
            }
            catch (Exception e)
            {
                Plugin.Instance.Log.LogWarning("Practice launcher: " + e.Message);
                if (!_plFallback && ClickVsBotsButton()) _plFallback = true;
            }
        }
    }
}
