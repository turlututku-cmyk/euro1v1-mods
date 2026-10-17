using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    // Build Trainer: a timed building drill. Place as many walls / floors / stairs (or any) as you can before the time runs out.
    // Counts only parts that appear while the drill runs. Practice only: never starts with other human players present.
    public static partial class Features
    {
        public static bool BtActive;
        public static string BtResult = "";
        public static readonly string[] BtModes = { "Any", "Walls", "Floors", "Stairs" };
        static readonly HashSet<int> _btSeen = new HashSet<int>();
        static float _btStart, _btNext;
        static int _btCount, _btNewAny, _btNewMine, _btMyTeam;
        static bool _btCountAll;

        static bool BtMatchesMode(BuildPart bp)
        {
            int mode = Cfg.BtMode.Value;
            if (mode == 0) return true;
            string t = bp.type.ToString();
            return (mode == 1 && t == "Wall") || (mode == 2 && t == "Floor") || (mode == 3 && t == "Stairs");
        }

        static void BtSnapshot(bool markSeen)
        {
            var live = BuildPart.activeParts;
            if (live == null) return;
            for (int i = 0; i < live.Count; i++)
            {
                var bp = live[i];
                if (bp == null) continue;
                _btSeen.Add(bp.GetInstanceID());
            }
        }

        public static void StartBuildTrainer()
        {
            if (BtActive) { StopBuildTrainer(false); return; }
            var me = LocalPlayer();
            if (me == null) { Toast("Build Trainer needs a map: start a practice match first (Play > VS Bots)"); return; }
            if (OtherHumansPresent()) { Toast("Build Trainer is for practice only, not with other players"); return; }
            _btSeen.Clear();
            try { BtSnapshot(true); } catch { }
            _btMyTeam = me.team; _btCount = 0; _btNewAny = 0; _btNewMine = 0; _btCountAll = false;
            _btStart = Time.unscaledTime; BtActive = true; BtResult = "";
            Toast("Build Trainer: build " + BtModes[Mathf.Clamp(Cfg.BtMode.Value, 0, 3)].ToLowerInvariant() + " as fast as you can!");
        }

        public static void StopBuildTrainer(bool finished)
        {
            if (!BtActive) return;
            BtActive = false;
            if (!finished) { BtResult = ""; Toast("Build Trainer stopped"); return; }
            float secs = Mathf.Max(1f, Cfg.BtTime.Value);
            var best = Cfg.BtBest[Mathf.Clamp(Cfg.BtMode.Value, 0, 3)];
            bool record = _btCount > best.Value;
            if (record) { best.Value = _btCount; Plugin.Instance.Config.Save(); }
            BtResult = BtModes[Mathf.Clamp(Cfg.BtMode.Value, 0, 3)] + ": " + _btCount + " parts in " + Mathf.RoundToInt(secs) + " s (" + (_btCount / secs).ToString("0.0") + " per second)" + (record ? "  - new best!" : "");
            Toast("Build Trainer: " + BtResult);
        }

        static void UpdateBuildTrainer()
        {
            if (!Cfg.BtOn.Value) { if (BtActive) StopBuildTrainer(false); return; }
            var kb = Keyboard.current;
            if (kb != null && kb.f4Key.wasPressedThisFrame && !PanelOpen) StartBuildTrainer();
            if (!BtActive) return;

            float now = Time.unscaledTime;
            if (now - _btStart >= Cfg.BtTime.Value) { StopBuildTrainer(true); return; }
            if (OtherHumansPresent()) { StopBuildTrainer(false); return; }
            if (now < _btNext) return;
            _btNext = now + 0.1f;
            try
            {
                var live = BuildPart.activeParts;
                if (live == null) return;
                for (int i = 0; i < live.Count; i++)
                {
                    var bp = live[i];
                    if (bp == null) continue;
                    if (!_btSeen.Add(bp.GetInstanceID())) continue;
                    _btNewAny++;
                    bool mine = bp.ownerTeam == _btMyTeam;
                    if (mine) _btNewMine++;
                    if (_btNewAny == 1) Plugin.Instance.Log.LogInfo("Build Trainer: first new part type=" + bp.type + " ownerTeam=" + bp.ownerTeam + " myTeam=" + _btMyTeam);
                    if (!_btCountAll && _btNewAny >= 3 && _btNewMine == 0) _btCountAll = true;      // team ids do not line up: count every new part instead
                    if ((mine || _btCountAll) && BtMatchesMode(bp)) _btCount++;
                }
            }
            catch { }
        }

        static void DrawBuildTrainer()
        {
            if (!BtActive) return;
            float left = Mathf.Max(0f, Cfg.BtTime.Value - (Time.unscaledTime - _btStart));
            var r = new Rect(Screen.width / 2f - 230f, 40f, 460f, 34f);
            G.Fill(r, new Color(0, 0, 0, 0.7f));
            G.Fill(new Rect(r.x, r.y, 3, r.height), new Color(1f, 0.7f, 0.2f));
            int best = Cfg.BtBest[Mathf.Clamp(Cfg.BtMode.Value, 0, 3)].Value;
            G.Label(r, "BUILD TRAINER   " + BtModes[Mathf.Clamp(Cfg.BtMode.Value, 0, 3)] + "   " + _btCount + " built   best " + best + "   " + left.ToString("0") + " s", 14, G.Text, TextAnchor.MiddleCenter, true);
        }
    }
}
