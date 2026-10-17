using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Knows when a round ends (an attack / tiebreaker state of the game's own state machine is left, or the match enters EndingState)
    // and freezes a copy of the session leaderboard at that moment. Read-only.
    public static partial class Features
    {
        public static int RoundNo, SnapRound;
        static string _prevState = "";
        static float _nextStateCheck, _snapAt, _lastRoundEndT = -99f;
        public static float PopupUntil;
        static List<SessRow> _snap = new List<SessRow>();
        static readonly HashSet<string> _attackStates = new HashSet<string> { "BlueAttackState", "RedAttackState", "DrawArenaState" };

        static string StateName()
        {
            try
            {
                var gm = GameModeManager.instance;
                if (gm == null || gm._state == null) return null;
                IntPtr ptr = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtr(gm._state);
                IntPtr klass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(ptr);
                return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(klass));
            }
            catch { return null; }
        }

        static SessRow CloneRow(SessRow r) => new SessRow { Name = r.Name, Kills = r.Kills, Deaths = r.Deaths, Assists = r.Assists, Damage = r.Damage, Headshots = r.Headshots, Team = r.Team, IsLocal = r.IsLocal };

        public static IEnumerable<SessRow> LbSource() => Cfg.SsRoundEnd.Value ? (IEnumerable<SessRow>)_snap : Sess.Values;

        public static void TakeSnapshot(bool popup)
        {
            _snap = Sess.Values.Select(CloneRow).ToList();
            SnapRound = RoundNo;
            if (popup && Cfg.SsPopup.Value && Cfg.SsOn.Value) PopupUntil = Time.unscaledTime + 9f;
        }

        static void ClearSnapshot() { _snap = new List<SessRow>(); SnapRound = 0; RoundNo = 0; PopupUntil = 0f; _snapAt = 0f; }

        static void UpdateRounds()
        {
            if (!Cfg.SsOn.Value && !Cfg.SbOn.Value && !Cfg.CoachOn.Value) { _prevState = ""; return; }
            float now = Time.unscaledTime;
            if (_snapAt > 0f && now >= _snapAt) { _snapAt = 0f; TakeSnapshot(true); CoachRoundEnd(); }
            if (now < _nextStateCheck) return;
            _nextStateCheck = now + 0.2f;

            string s = StateName();
            if (s == null) { if (_prevState != "") CoachOnState(_prevState, null); _prevState = ""; return; }
            if (s == _prevState) return;
            try { Plugin.Instance.Log.LogInfo("Game state: " + (_prevState == "" ? "(none)" : _prevState) + " -> " + s); } catch { }

            CoachOnState(_prevState, s);
            bool matchEnd = s == "EndingState" && _prevState != "" && _prevState != "EndingState";
            bool ended = _attackStates.Contains(_prevState) || matchEnd;
            _prevState = s;
            if (matchEnd) SbOnMatchEnd();
            if (!ended || now - _lastRoundEndT < 4f) return;
            _lastRoundEndT = now;
            RoundNo++;
            _snapAt = now + 0.8f; // let the last kill of the round register first
        }
    }
}
