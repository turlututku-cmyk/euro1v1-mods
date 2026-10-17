using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public class KillEvent
    {
        public float T;
        public string Killer, Victim;
        public int KillerTeam = -1, VictimTeam = -1;
        public WeaponType Weapon;
        public bool Headshot, KillerIsLocal, VictimIsLocal, LocalAssisted;
        public List<string> Assisters = new List<string>();
    }

    public class SessRow { public string Name = ""; public int Kills, Deaths, Assists, Damage, Headshots, Team; public bool IsLocal; }

    // Works out who killed whom, with what, and who assisted, from the scoreboard stats the game already replicates
    // (kills / deaths / damage / headshots / alive per player). Local and read-only.
    public static partial class Features
    {
        class PState
        {
            public string Name = "?"; public int Team = -1; public bool IsLocal, IsBot, Alive = true;
            public int Kills, Deaths, Damage, Heads;
            public WeaponType Weapon;
            public float LastKillT = -99f, LastDmgT = -99f, LastHeadT = -99f;
            public int KillGain;
        }

        class PendingDeath { public PState V; public float T; }

        static readonly Dictionary<int, PState> _pst = new Dictionary<int, PState>();
        static readonly List<PlayerObjectController> _trkPlayers = new List<PlayerObjectController>();
        static readonly List<PendingDeath> _pendingDeaths = new List<PendingDeath>();
        public static readonly Dictionary<string, SessRow> Sess = new Dictionary<string, SessRow>();
        static float _nextTrk, _nextTrkScan;
        public static int MyStreak, MyBestStreak;
        public static float SessionStart = -1f;

        public static bool TrackerWanted => Cfg.KmOn.Value || Cfg.AnnOn.Value || Cfg.ConfOn.Value || Cfg.SsOn.Value || Cfg.DtOn.Value || Cfg.CoachOn.Value;

        static SessRow Row(string name)
        {
            if (!Sess.TryGetValue(name, out var r)) { r = new SessRow { Name = name }; Sess[name] = r; }
            return r;
        }

        public static void ResetSession()
        {
            Sess.Clear(); ClearSnapshot(); MyStreak = 0; MyBestStreak = 0; SessionStart = Time.unscaledTime;
        }

        static void UpdateKillTracker()
        {
            if (!TrackerWanted) { if (_pst.Count > 0) { _pst.Clear(); _pendingDeaths.Clear(); } return; }
            float now = Time.unscaledTime;
            if (SessionStart < 0f) SessionStart = now;
            if (now < _nextTrk) return;
            _nextTrk = now + 0.1f;

            if (now >= _nextTrkScan)
            {
                _nextTrkScan = now + 1f;
                try { _trkPlayers.Clear(); foreach (var p in UnityEngine.Object.FindObjectsOfType<PlayerObjectController>()) _trkPlayers.Add(p); } catch { }
            }
            if (_trkPlayers.Count == 0) { _pst.Clear(); _pendingDeaths.Clear(); return; }

            foreach (var p in _trkPlayers)
            {
                try
                {
                    int key = p.GetInstanceID();
                    int k = p.stat_kills, d = p.stat_deaths, dmg = p.stat_damage, hs = p.stat_headshots;
                    bool alive = p.alive;
                    if (!_pst.TryGetValue(key, out var st))
                    {
                        st = new PState { Kills = k, Deaths = d, Damage = dmg, Heads = hs, Alive = alive };
                        _pst[key] = st;
                    }
                    st.Name = p.PlayerName ?? "?"; st.Team = p.team; st.IsLocal = p.isLocalPlayer; st.IsBot = p.IsBot;
                    try { st.Weapon = p.playerMovementController.currentWeaponType; } catch { }

                    if (k > st.Kills) { st.KillGain += k - st.Kills; st.LastKillT = now; }
                    if (hs > st.Heads) st.LastHeadT = now;
                    if (dmg > st.Damage) { st.LastDmgT = now; Row(st.Name).Damage += dmg - st.Damage; }
                    if (k < st.Kills || d < st.Deaths) { st.KillGain = 0; } // new round / stats reset
                    st.Kills = k; st.Deaths = d; st.Damage = dmg; st.Heads = hs;
                    Row(st.Name).IsLocal = st.IsLocal; Row(st.Name).Team = st.Team;

                    if (st.Alive && !alive) _pendingDeaths.Add(new PendingDeath { V = st, T = now });
                    st.Alive = alive;
                }
                catch { }
            }

            for (int i = _pendingDeaths.Count - 1; i >= 0; i--)
            {
                var pd = _pendingDeaths[i];
                if (now - pd.T < 0.35f) continue;      // give the killer's stat time to replicate
                _pendingDeaths.RemoveAt(i);
                try { ResolveDeath(pd, now); } catch (Exception e) { Plugin.Instance.Log.LogWarning("Kill tracker: " + e.Message); }
            }
        }

        static void ResolveDeath(PendingDeath pd, float now)
        {
            var v = pd.V;
            PState killer = null;
            foreach (var s in _pst.Values)
            {
                if (s == v || s.KillGain <= 0) continue;
                if (s.LastKillT < pd.T - 0.9f) continue;
                if (s.Team == v.Team && s.Team >= 0) continue;
                if (killer == null || s.LastKillT > killer.LastKillT) killer = s;
            }

            var ev = new KillEvent
            {
                T = now, Victim = v.Name, VictimTeam = v.Team, VictimIsLocal = v.IsLocal,
                Killer = killer != null ? killer.Name : null, KillerTeam = killer != null ? killer.Team : -1,
                KillerIsLocal = killer != null && killer.IsLocal, Weapon = killer != null ? killer.Weapon : WeaponType.other
            };

            if (killer != null)
            {
                killer.KillGain--;
                ev.Headshot = killer.LastHeadT >= pd.T - 0.9f;
                Row(killer.Name).Kills++;
                if (ev.Headshot) Row(killer.Name).Headshots++;
                // assists: killer's teammates that dealt damage in the last 8 s before this death
                foreach (var s in _pst.Values)
                {
                    if (s == killer || s == v) continue;
                    if (s.Team != killer.Team || s.Team < 0) continue;
                    if (pd.T - s.LastDmgT > 8f || s.LastDmgT < 0f) continue;
                    ev.Assisters.Add(s.Name);
                    Row(s.Name).Assists++;
                    if (s.IsLocal) ev.LocalAssisted = true;
                }
            }
            Row(v.Name).Deaths++;

            if (ev.KillerIsLocal) { MyStreak++; if (MyStreak > MyBestStreak) MyBestStreak = MyStreak; }
            if (ev.VictimIsLocal) MyStreak = 0;

            KmOnKill(ev);
            AnnOnKill(ev);
            ConfOnKill(ev);
            DtOnKill(ev);
            CoachOnKill(ev);
        }
    }
}
