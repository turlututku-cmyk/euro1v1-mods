using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Team Panel: a compact HUD list of YOUR TEAMMATES only: name, health bar, alive or dead, weapon, kills/deaths, and (optionally) distance
    // with an arrow that points toward them. It never shows enemies and uses only data the game already replicates for your own team.
    public static partial class Features
    {
        class TpRow { public string Name; public float Hp, Max, Dist, Angle; public bool Alive, IsBot; public int K, D; public string Weapon; }

        static readonly List<PlayerObjectController> _tpPlayers = new List<PlayerObjectController>();
        static readonly List<TpRow> _tpRows = new List<TpRow>();
        static float _tpNextScan, _tpNextRows;

        static bool InPlayState()
        {
            string s = StateName();
            return s != null && s != "EndingState" && s.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) < 0;
        }

        static void UpdateTeamPanel()
        {
            if (!Cfg.TpOn.Value) { if (_tpRows.Count > 0) _tpRows.Clear(); return; }
            float now = Time.unscaledTime;
            if (now < _tpNextRows) return;
            _tpNextRows = now + 0.15f;
            try
            {
                var me = LocalPlayer();
                if (me == null || !InPlayState()) { _tpRows.Clear(); return; }
                if (now >= _tpNextScan)
                {
                    _tpNextScan = now + 1f;
                    _tpPlayers.Clear();
                    foreach (var p in UnityEngine.Object.FindObjectsOfType<PlayerObjectController>()) _tpPlayers.Add(p);
                }
                var cam = Cam();
                var fwd = cam != null ? cam.transform.forward : Vector3.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; fwd.Normalize();
                var myPos = cam != null ? cam.transform.position : me.transform.position;

                var rows = new List<TpRow>();
                foreach (var p in _tpPlayers)
                {
                    if (p == null || p == me || p.team != me.team) continue;
                    var r = new TpRow { Name = p.PlayerName ?? "?", IsBot = p.IsBot, Alive = p.alive, K = p.stat_kills, D = p.stat_deaths };
                    try { r.Hp = p.health != null ? p.health.health : 0f; r.Max = p.health != null && p.health.maxHealth > 0f ? p.health.maxHealth : 100f; } catch { r.Max = 100f; }
                    try { r.Weapon = p.playerMovementController.currentWeaponType.ToString(); } catch { r.Weapon = ""; }
                    if (r.Alive)
                    {
                        var to = p.transform.position - myPos;
                        r.Dist = to.magnitude;
                        to.y = 0f;
                        r.Angle = to.sqrMagnitude > 0.01f ? Vector3.SignedAngle(fwd, to.normalized, Vector3.up) : 0f;
                    }
                    if (!r.Alive && !Cfg.TpDead.Value) continue;
                    rows.Add(r);
                }
                _tpRows.Clear();
                _tpRows.AddRange(rows.OrderByDescending(x => x.Alive).ThenBy(x => x.Name));
            }
            catch { }
        }

        static void DrawArrow(Vector2 c, float angle, float size, Color col)
        {
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, c);
            float top = c.y - size / 2f;
            G.Fill(new Rect(c.x - 5f, top + 6f, 10f, 3f), col);
            G.Fill(new Rect(c.x - 3f, top + 3f, 6f, 3f), col);
            G.Fill(new Rect(c.x - 1f, top, 2f, 3f), col);
            G.Fill(new Rect(c.x - 1.5f, top + 9f, 3f, size - 9f), col);
            GUI.matrix = old;
        }

        static void DrawTeamPanel()
        {
            if (!Cfg.TpOn.Value || _tpRows.Count == 0) return;
            bool showDist = Cfg.TpDist.Value;
            const float rowH = 40f, w = 270f;
            float h = 26f + _tpRows.Count * (rowH + 3f);
            var r = Corner(Cfg.TpCorner.Value, w, h, 270f);
            G.Fill(r, new Color(0, 0, 0, 0.55f));
            G.Fill(new Rect(r.x, r.y, 3, r.height), new Color(0.35f, 0.9f, 0.55f));
            G.Label(new Rect(r.x + 12, r.y + 2, r.width - 16, 22), "TEAM", 12, new Color(0.35f, 0.9f, 0.55f), TextAnchor.MiddleLeft, true);

            float y = r.y + 26f;
            foreach (var t in _tpRows)
            {
                var row = new Rect(r.x + 8, y, r.width - 16, rowH);
                float frac = t.Max > 0f ? Mathf.Clamp01(t.Hp / t.Max) : 0f;
                Color hc = !t.Alive ? new Color(0.4f, 0.42f, 0.45f) : frac > 0.6f ? new Color(0.35f, 0.9f, 0.45f) : frac > 0.3f ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 0.35f, 0.3f);
                G.Fill(row, new Color(0.08f, 0.09f, 0.11f, 0.85f));
                string nm = t.Name.Length > 14 ? t.Name.Substring(0, 13) + "..." : t.Name;
                G.Label(new Rect(row.x + 6, row.y + 1, 150, 18), nm + (t.IsBot ? " (bot)" : ""), 13, t.Alive ? G.Text : G.Dim, TextAnchor.MiddleLeft, true);
                G.Label(new Rect(row.x + 6, row.y + 1, row.width - 12 - (showDist && t.Alive ? 46f : 0f), 18), t.Alive ? (t.Weapon ?? "") : "DEAD", 10, G.Dim, TextAnchor.MiddleRight);
                // health bar
                var bar = new Rect(row.x + 6, row.y + 22, row.width - 12 - (showDist && t.Alive ? 46f : 0f), 12f);
                G.Fill(bar, new Color(0.18f, 0.2f, 0.22f, 1f));
                if (t.Alive) G.Fill(new Rect(bar.x, bar.y, bar.width * frac, bar.height), hc);
                G.Label(bar, t.Alive ? Mathf.CeilToInt(t.Hp) + "" : "K " + t.K + "  D " + t.D, 10, t.Alive ? Color.white : G.Dim, TextAnchor.MiddleCenter, true);
                if (showDist && t.Alive)
                {
                    var c = new Vector2(row.xMax - 24f, row.y + 14f);
                    DrawArrow(c, t.Angle, 22f, new Color(0.6f, 0.95f, 0.75f));
                    G.Label(new Rect(row.xMax - 46f, row.y + 24f, 44f, 14f), Mathf.RoundToInt(t.Dist) + " m", 10, G.Dim, TextAnchor.MiddleCenter);
                }
                y += rowH + 3f;
            }
        }
    }
}
