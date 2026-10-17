using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public class CoachStat
    {
        public string Date = "";
        public int Kills, Deaths, Assists, Shots, Hits, Heads, Damage, Rounds, Lives, EarlyDeaths, BestStreak;
        public float LifeTime;
        public int[] WShots = new int[4], WHits = new int[4], WKills = new int[4], WDeaths = new int[4];   // 0 pistol, 1 shotgun, 2 sniper, 3 other

        public float Acc => Shots > 0 ? Hits / (float)Shots : 0f;
        public float HsRate => Hits > 0 ? Heads / (float)Hits : 0f;
        public float KD => Deaths > 0 ? Kills / (float)Deaths : Kills;
        public float AvgLife => Lives > 0 ? LifeTime / Lives : 0f;

        public void Add(CoachStat o)
        {
            Kills += o.Kills; Deaths += o.Deaths; Assists += o.Assists; Shots += o.Shots; Hits += o.Hits; Heads += o.Heads; Damage += o.Damage;
            Rounds += o.Rounds; Lives += o.Lives; EarlyDeaths += o.EarlyDeaths; LifeTime += o.LifeTime; BestStreak = Math.Max(BestStreak, o.BestStreak);
            for (int i = 0; i < 4; i++) { WShots[i] += o.WShots[i]; WHits[i] += o.WHits[i]; WKills[i] += o.WKills[i]; WDeaths[i] += o.WDeaths[i]; }
        }
    }

    // Performance Coach: tracks only YOUR OWN stats (shots, hits, headshots, kills, deaths and who killed you, how long each life lasted)
    // and turns them into specific tips after every round and match, with trends over your recent matches. No hidden information is used.
    public static partial class Features
    {
        static CoachStat _cpMatch = new CoachStat(), _cpRound = new CoachStat();
        static readonly List<CoachStat> _cpHist = new List<CoachStat>();
        static bool _cpHistLoaded, _cpInMatch, _cpWasAlive;
        static float _cpNext, _cpLifeStart = -1f, _cpMatchEndAt;
        static int _cpFired = -1, _cpHit, _cpHeads, _cpDmg;
        static readonly Queue<bool> _cpRoll = new Queue<bool>();
        public static List<string> CoachLastTips = new List<string>();
        public static string CoachLastTitle = "";
        static List<string> _cpCardTips = new List<string>(); static string _cpCardTitle = "", _cpCardKpi = ""; static float _cpCardUntil, _cpCardTotal;

        public static readonly string[] CoachWeapons = { "Pistol", "Shotgun", "Sniper", "Other" };
        static int Wi(WeaponType w) => w == WeaponType.Pistol ? 0 : w == WeaponType.Shotgun ? 1 : w == WeaponType.Sniper ? 2 : 3;
        static string Pct(float x) => Mathf.RoundToInt(x * 100f) + "%";
        static string HistPath() => Path.Combine(Paths.GameRootPath, "Mods", "Stats", "coach_history.csv");

        public static CoachStat CoachMatchNow => _cpMatch;
        public static bool CoachInMatch => _cpInMatch;
        public static int CoachHistoryCount { get { CoachLoadHist(); return _cpHist.Count; } }
        public static CoachStat CoachLifetime() { CoachLoadHist(); var t = new CoachStat(); foreach (var h in _cpHist) t.Add(h); return t; }
        public static CoachStat CoachRecent(int n) { CoachLoadHist(); var t = new CoachStat(); foreach (var h in _cpHist.Skip(Math.Max(0, _cpHist.Count - n))) t.Add(h); return t; }

        // ------------------------------------------------------------ events from the kill tracker and the round tracker

        static void CoachOnKill(KillEvent ev)
        {
            if (!Cfg.CoachOn.Value || !_cpInMatch) return;
            float now = Time.unscaledTime;
            foreach (var p in new[] { _cpMatch, _cpRound })
            {
                if (ev.KillerIsLocal) { p.Kills++; p.WKills[Wi(ev.Weapon)]++; p.BestStreak = Math.Max(p.BestStreak, MyStreak); }
                if (ev.LocalAssisted) p.Assists++;
                if (ev.VictimIsLocal)
                {
                    p.Deaths++; p.Lives++;
                    float life = _cpLifeStart >= 0f ? Mathf.Max(0f, now - 0.35f - _cpLifeStart) : 0f;
                    p.LifeTime += life;
                    if (life < 10f) p.EarlyDeaths++;
                    if (ev.Killer != null) p.WDeaths[Wi(ev.Weapon)]++;
                }
            }
        }

        public static void CoachOnState(string prev, string s)
        {
            if (!Cfg.CoachOn.Value) return;
            float now = Time.unscaledTime;
            if (s == null) { if (_cpInMatch) { _cpMatchEndAt = 0f; CoachFinishMatch(false); } return; }
            bool play = s == "BuildPhaseState" || s == "PlanPhaseState" || s == "BlueAttackState" || s == "RedAttackState" || s == "HalfBreakState" || s == "DrawArenaState" || s == "MissionBuildHoldState";
            if (!_cpInMatch && play)
            {
                _cpInMatch = true; _cpMatch = new CoachStat(); _cpRound = new CoachStat(); _cpMatchEndAt = 0f; _cpRoll.Clear();
            }
            if (s == "EndingState" && _cpInMatch && _cpMatchEndAt <= 0f) _cpMatchEndAt = now + 1.4f;
        }

        static void CoachRoundEnd()
        {
            if (!Cfg.CoachOn.Value || !_cpInMatch) return;
            _cpMatch.Rounds++; _cpRound.Rounds = 1;
            if (Cfg.CoachRound.Value && _cpMatchEndAt <= 0f && (_cpRound.Shots + _cpRound.Deaths + _cpRound.Kills) > 0)
                CoachShowCard("COACH   ROUND " + RoundNo, _cpRound, false, 10f);
            _cpRound = new CoachStat();
        }

        static void CoachFinishMatch(bool showCard)
        {
            if (!_cpInMatch) return;
            _cpInMatch = false;
            var m = _cpMatch;
            _cpMatch = new CoachStat(); _cpRound = new CoachStat();
            if (m.Shots + m.Deaths + m.Kills == 0) return;
            m.Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            CoachLoadHist();
            var tips = CoachTips(m, true);
            CoachLastTips = tips; CoachLastTitle = "Match summary  " + m.Date;
            if (showCard && Cfg.CoachMatch.Value) CoachShowCard("COACH   MATCH SUMMARY", m, true, 16f, tips);
            CoachSave(m);
            _cpHist.Add(m);
        }

        // ------------------------------------------------------------ live collection

        static void UpdateCoach()
        {
            if (!Cfg.CoachOn.Value) { _cpFired = -1; return; }
            float now = Time.unscaledTime;
            if (_cpMatchEndAt > 0f && now >= _cpMatchEndAt) { _cpMatchEndAt = 0f; CoachFinishMatch(true); }
            if (now < _cpNext) return;
            _cpNext = now + 0.1f;
            try
            {
                var me = LocalPlayer();
                if (me == null) { _cpFired = -1; _cpWasAlive = false; return; }
                bool alive = me.alive;
                if (alive && !_cpWasAlive) _cpLifeStart = now;
                _cpWasAlive = alive;
                if (!_cpInMatch) { _cpFired = -1; return; }

                int fired = me.stat_shotsFired, hit = me.stat_shotsHit, heads = me.stat_headshots, dmg = me.stat_damage;
                if (_cpFired < 0 || fired < _cpFired || hit < _cpHit) { _cpFired = fired; _cpHit = hit; _cpHeads = heads; _cpDmg = dmg; return; }
                int dF = fired - _cpFired, dH = hit - _cpHit, dHS = Math.Max(0, heads - _cpHeads), dD = Math.Max(0, dmg - _cpDmg);
                _cpFired = fired; _cpHit = hit; _cpHeads = heads; _cpDmg = dmg;
                if (dF == 0 && dH == 0 && dHS == 0 && dD == 0) return;

                int wi = 3;
                try { wi = Wi(me.playerMovementController.currentWeaponType); } catch { }
                foreach (var p in new[] { _cpMatch, _cpRound })
                {
                    p.Shots += dF; p.Hits += dH; p.Heads += dHS; p.Damage += dD;
                    p.WShots[wi] += dF; p.WHits[wi] += dH;
                }
                for (int i = 0; i < dH; i++) _cpRoll.Enqueue(true);
                for (int i = 0; i < Math.Max(0, dF - dH); i++) _cpRoll.Enqueue(false);
                while (_cpRoll.Count > 30) _cpRoll.Dequeue();
            }
            catch { }
        }

        // ------------------------------------------------------------ the coaching itself

        public static List<string> CoachTips(CoachStat p, bool match)
        {
            var L = new List<KeyValuePair<float, string>>();
            Action<float, string> T = (pr, s) => L.Add(new KeyValuePair<float, string>(pr, s));
            int minShots = match ? 20 : 6, minHits = match ? 10 : 4, minDeaths = match ? 3 : 2;
            string[] wn = { "pistol", "shotgun", "sniper", "weapon" };

            if (p.Shots >= minShots)
            {
                float a = p.Acc;
                if (a < 0.25f) T(90f - a * 100f, "Accuracy is " + Pct(a) + ". Slow down and fire only when the crosshair is on target. The Aim Trainer (Gridshot) is a quick fix.");
                else if (a >= 0.5f) T(10f, "Accuracy " + Pct(a) + ": sharp aim, keep it up.");

                int worst = -1, best = -1; float wa = 2f, ba = -1f;
                for (int i = 0; i < 3; i++)
                {
                    if (p.WShots[i] < Math.Max(5, minShots / 2)) continue;
                    float ai = p.WHits[i] / (float)p.WShots[i];
                    if (ai < wa) { wa = ai; worst = i; }
                    if (ai > ba) { ba = ai; best = i; }
                }
                if (worst >= 0 && best >= 0 && worst != best && ba - wa >= 0.15f)
                    T(60f, "Your " + wn[worst] + " (" + Pct(wa) + ") trails your " + wn[best] + " (" + Pct(ba) + "). Use it at the range where it works, or practice it in a bot match.");
            }
            if (p.Hits >= minHits)
            {
                float h = p.HsRate;
                if (h < 0.08f) T(70f, "Only " + Pct(h) + " of your hits were headshots. Keep the crosshair at head height while you move, not at the chest.");
                else if (h >= 0.25f) T(10f, "Headshots on " + Pct(h) + " of your hits. Nice.");
            }
            if (p.EarlyDeaths >= (match ? 2 : 1))
                T(80f, "You died within 10 s of spawning " + p.EarlyDeaths + (p.EarlyDeaths == 1 ? " time" : " times") + ". Let a teammate lead or peek from cover before you commit.");
            if (p.Deaths >= minDeaths)
            {
                int mx = 0;
                for (int i = 1; i < 3; i++) if (p.WDeaths[i] > p.WDeaths[mx]) mx = i;
                int known = p.WDeaths[0] + p.WDeaths[1] + p.WDeaths[2] + p.WDeaths[3];
                if (p.WDeaths[mx] >= 2 && p.WDeaths[mx] * 2 >= known)
                {
                    string head = (mx == 0 ? "Pistols" : mx == 1 ? "Shotguns" : "Snipers") + " got " + p.WDeaths[mx] + " of your " + known + " deaths. ";
                    T(75f, head + (mx == 2 ? "Break line of sight, use cover and build a wall before you cross open ground."
                        : mx == 1 ? "Avoid close fights around corners; keep your distance or fall back to a teammate."
                        : "In straight duels strafe and fire short bursts instead of holding the trigger."));
                }
            }
            if (match && p.Lives >= 3 && p.AvgLife < 20f)
                T(65f, "Your average life lasts " + Mathf.RoundToInt(p.AvgLife) + " s. Staying alive longer means more damage: retreat at low health (try Low Health Warning).");
            if (match && p.Deaths >= 3 && p.Damage / (float)p.Deaths < 60f)
                T(55f, "You deal only " + Mathf.RoundToInt(p.Damage / (float)p.Deaths) + " damage per death. Skip coin-flip duels and fight next to a teammate.");

            if (match && _cpHist.Count >= 3 && p.Shots >= minShots)
            {
                var r = new CoachStat(); foreach (var h in _cpHist.Skip(Math.Max(0, _cpHist.Count - 5))) r.Add(h);
                if (r.Shots >= 30)
                {
                    float diff = p.Acc - r.Acc;
                    if (diff <= -0.08f) T(78f, "Accuracy is down " + Mathf.RoundToInt(-diff * 100f) + " points versus your last matches. Take a short break or warm up with the Aim Trainer.");
                    else if (diff >= 0.08f) T(20f, "Accuracy is up " + Mathf.RoundToInt(diff * 100f) + " points versus your last matches. Real progress.");
                }
            }
            if (p.Deaths > 0 && p.Kills >= 2 && p.KD >= 1.5f) T(15f, "K/D " + p.KD.ToString("0.0") + ": you are winning your fights.");
            if (p.BestStreak >= 3) T(12f, "Best streak " + p.BestStreak + " kills in one life.");

            if (L.Count == 0) return new List<string> { "Not enough data yet. Play a few more rounds and the tips will get specific." };
            return L.OrderByDescending(x => x.Key).Take(3).Select(x => x.Value).ToList();
        }

        public static string CoachKpi(CoachStat p, bool match)
        {
            string s = "K/D/A " + p.Kills + "/" + p.Deaths + "/" + p.Assists + "    Acc " + (p.Shots > 0 ? Pct(p.Acc) : "-") + "    HS " + (p.Hits > 0 ? Pct(p.HsRate) : "-") + "    Dmg " + p.Damage;
            if (match && p.Lives > 0) s += "    Life " + Mathf.RoundToInt(p.AvgLife) + " s";
            return s;
        }

        static void CoachShowCard(string title, CoachStat p, bool match, float seconds, List<string> tips = null)
        {
            _cpCardTitle = title; _cpCardKpi = CoachKpi(p, match);
            _cpCardTips = tips ?? CoachTips(p, match);
            if (!match) { CoachLastTips = _cpCardTips; CoachLastTitle = title; }
            _cpCardTotal = seconds; _cpCardUntil = Time.unscaledTime + seconds;
        }

        public static void CoachShowNow()
        {
            if (_cpMatch.Shots + _cpMatch.Deaths + _cpMatch.Kills == 0) { Toast("Coach: no data yet, play a round first"); return; }
            CoachShowCard("COACH   THIS MATCH SO FAR", _cpMatch, true, 14f);
            CoachLastTips = _cpCardTips; CoachLastTitle = "This match so far";
        }

        // ------------------------------------------------------------ history file

        static void CoachLoadHist()
        {
            if (_cpHistLoaded) return;
            _cpHistLoaded = true;
            try
            {
                var path = HistPath();
                if (!File.Exists(path)) return;
                foreach (var line in File.ReadAllLines(path).Skip(1))
                {
                    var f = line.Split(',');
                    if (f.Length < 23) continue;
                    var c = new CoachStat { Date = f[0] };
                    int[] v = new int[22]; bool ok = true;
                    for (int i = 0; i < 22; i++) if (!int.TryParse(f[i + 1], out v[i])) ok = false;
                    if (!ok) continue;
                    c.Kills = v[0]; c.Deaths = v[1]; c.Assists = v[2]; c.Shots = v[3]; c.Hits = v[4]; c.Heads = v[5]; c.Damage = v[6]; c.Rounds = v[7]; c.BestStreak = v[8];
                    c.Lives = v[9]; c.LifeTime = v[10]; c.EarlyDeaths = v[11];
                    c.WShots[0] = v[12]; c.WHits[0] = v[13]; c.WShots[1] = v[14]; c.WHits[1] = v[15]; c.WShots[2] = v[16]; c.WHits[2] = v[17];
                    c.WDeaths[0] = v[18]; c.WDeaths[1] = v[19]; c.WDeaths[2] = v[20]; c.WDeaths[3] = v[21];
                    _cpHist.Add(c);
                }
            }
            catch { }
        }

        static void CoachSave(CoachStat m)
        {
            try
            {
                var path = HistPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                bool fresh = !File.Exists(path);
                var sb = new StringBuilder();
                if (fresh) sb.AppendLine("Date,Kills,Deaths,Assists,Shots,Hits,Heads,Damage,Rounds,BestStreak,Lives,LifeSeconds,EarlyDeaths,PistolShots,PistolHits,ShotgunShots,ShotgunHits,SniperShots,SniperHits,DeathsPistol,DeathsShotgun,DeathsSniper,DeathsOther");
                sb.AppendLine(string.Join(",", new object[]
                {
                    m.Date, m.Kills, m.Deaths, m.Assists, m.Shots, m.Hits, m.Heads, m.Damage, m.Rounds, m.BestStreak, m.Lives, Mathf.RoundToInt(m.LifeTime), m.EarlyDeaths,
                    m.WShots[0], m.WHits[0], m.WShots[1], m.WHits[1], m.WShots[2], m.WHits[2], m.WDeaths[0], m.WDeaths[1], m.WDeaths[2], m.WDeaths[3]
                }.Select(x => x.ToString())));
                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Plugin.Instance.Log.LogWarning("Coach: could not save history: " + e.Message); }
        }

        public static void CoachResetHistory()
        {
            try { var p = HistPath(); if (File.Exists(p)) File.Delete(p); } catch { }
            _cpHist.Clear(); _cpHistLoaded = true;
            Toast("Coach history cleared");
        }

        // ------------------------------------------------------------ drawing

        static void DrawCoach()
        {
            if (!Cfg.CoachOn.Value) return;

            if (Cfg.CoachLive.Value && _cpInMatch && (_cpMatch.Shots + _cpMatch.Deaths + _cpMatch.Kills) > 0)
            {
                int n = _cpRoll.Count; int hits = _cpRoll.Count(x => x);
                string roll = n >= 5 ? "ACC " + Pct(hits / (float)n) + " (last " + n + ")" : "ACC " + (_cpMatch.Shots > 0 ? Pct(_cpMatch.Acc) : "-");
                float life = (_cpWasAlive && _cpLifeStart >= 0f) ? Time.unscaledTime - _cpLifeStart : 0f;
                string txt = roll + "    HS " + (_cpMatch.Hits > 0 ? Pct(_cpMatch.HsRate) : "-") + "    K/D " + _cpMatch.KD.ToString("0.0") + "    LIFE " + Mathf.FloorToInt(life) + " s";
                var r = Corner(Cfg.CoachCorner.Value, 360f, 26f, 110f);
                G.Fill(r, new Color(0, 0, 0, 0.55f));
                G.Fill(new Rect(r.x, r.y, 3, r.height), new Color(0.3f, 0.95f, 0.5f));
                G.Label(new Rect(r.x + 10, r.y, r.width - 12, r.height), txt, 12, G.Text, TextAnchor.MiddleLeft, true);
            }

            float left = _cpCardUntil - Time.unscaledTime;
            if (left > 0f && _cpCardTips.Count > 0)
            {
                float a = Mathf.Clamp01(left / 0.6f) * Mathf.Clamp01((_cpCardTotal - left) / 0.25f + 0.001f);
                float w = 420f, h = 58f + _cpCardTips.Count * 38f + 10f;
                var r = new Rect(Screen.width - w - 20f, Screen.height * 0.30f, w, h);
                var green = new Color(0.3f, 0.95f, 0.5f);
                G.Fill(r, new Color(0.03f, 0.05f, 0.04f, 0.88f * a));
                G.Fill(new Rect(r.x, r.y, 3, r.height), new Color(green.r, green.g, green.b, a));
                G.Label(new Rect(r.x + 14, r.y + 4, r.width - 20, 24), _cpCardTitle, 13, new Color(green.r, green.g, green.b, a), TextAnchor.MiddleLeft, true);
                G.Label(new Rect(r.x + 14, r.y + 28, r.width - 20, 22), _cpCardKpi, 12, new Color(1f, 1f, 1f, a), TextAnchor.MiddleLeft, true);
                float y = r.y + 56f;
                foreach (var t in _cpCardTips)
                {
                    G.Label(new Rect(r.x + 14, y, r.width - 24, 36), "-  " + t, 12, new Color(0.85f, 0.88f, 0.9f, a), TextAnchor.UpperLeft, false, true);
                    y += 38f;
                }
            }
        }
    }
}
