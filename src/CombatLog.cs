using System;
using System.Collections.Generic;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Combat Log: a live feed of the damage YOU take, the damage you deal, healing, and a short summary when a fight ends.
    // Built only from your own health and your own scoreboard stats.
    public static partial class Features
    {
        class ClLine { public string Text; public Color C; public float T; }
        static readonly List<ClLine> _clLines = new List<ClLine>();
        static float _clNext, _clLastHp = -1f, _clLastFight, _clFightDealt, _clFightTaken;
        static int _clLastDmg = -1, _clLastHeads = -1;
        static bool _clWasAlive;

        static void ClAdd(string text, Color c)
        {
            _clLines.Add(new ClLine { Text = text, C = c, T = Time.unscaledTime });
            if (_clLines.Count > 8) _clLines.RemoveAt(0);
        }

        static void UpdateCombatLog()
        {
            if (!Cfg.ClOn.Value) { _clLastHp = -1f; _clLastDmg = -1; return; }
            float now = Time.unscaledTime;
            if (now < _clNext) return;
            _clNext = now + 0.05f;
            try
            {
                var me = LocalPlayer();
                if (me == null || me.health == null) { _clLastHp = -1f; _clLastDmg = -1; _clWasAlive = false; return; }
                float hp = me.health.health; int dmg = me.stat_damage, heads = me.stat_headshots; bool alive = me.alive;

                if (_clLastHp < 0f || _clLastDmg < 0 || dmg < _clLastDmg) { _clLastHp = hp; _clLastDmg = dmg; _clLastHeads = heads; _clWasAlive = alive; return; }

                // respawn or round reset: do not report the jump back up as healing
                bool reset = (alive && !_clWasAlive) || _clLastHp <= 0f;
                if (!reset && hp < _clLastHp - 0.5f)
                {
                    float d = _clLastHp - hp;
                    _clFightTaken += d; _clLastFight = now;
                    ClAdd("-" + Mathf.RoundToInt(d) + "  taken", new Color(1f, 0.4f, 0.4f));
                }
                else if (!reset && hp > _clLastHp + 0.5f && alive)
                {
                    float d = hp - _clLastHp;
                    ClAdd("+" + Mathf.RoundToInt(d) + "  healed", new Color(0.4f, 1f, 0.55f));
                }
                if (Cfg.ClDealt.Value && dmg > _clLastDmg)
                {
                    int d = dmg - _clLastDmg;
                    bool head = heads > _clLastHeads;
                    _clFightDealt += d; _clLastFight = now;
                    ClAdd("+" + d + "  dealt" + (head ? "  (headshot)" : ""), head ? new Color(1f, 0.85f, 0.3f) : new Color(0.45f, 0.85f, 1f));
                }
                if (alive && heads > _clLastHeads) { }

                if (_clWasAlive && !alive)
                {
                    if (Cfg.ClSummary.Value) ClAdd("Fight over: dealt " + Mathf.RoundToInt(_clFightDealt) + ", took " + Mathf.RoundToInt(_clFightTaken), new Color(0.85f, 0.88f, 0.9f));
                    _clFightDealt = 0f; _clFightTaken = 0f; _clLastFight = 0f;
                }
                else if (_clLastFight > 0f && now - _clLastFight > 8f)
                {
                    if (Cfg.ClSummary.Value && _clFightDealt + _clFightTaken >= 40f)
                        ClAdd("Fight summary: dealt " + Mathf.RoundToInt(_clFightDealt) + ", took " + Mathf.RoundToInt(_clFightTaken), new Color(0.7f, 0.74f, 0.78f));
                    _clFightDealt = 0f; _clFightTaken = 0f; _clLastFight = 0f;
                }
                _clLastHp = hp; _clLastDmg = dmg; _clLastHeads = heads; _clWasAlive = alive;
            }
            catch { }
        }

        static void DrawCombatLog()
        {
            if (!Cfg.ClOn.Value || _clLines.Count == 0) return;
            float now = Time.unscaledTime, life = Mathf.Max(2f, Cfg.ClTime.Value);
            _clLines.RemoveAll(l => now - l.T > life);
            int c = Mathf.Clamp(Cfg.ClCorner.Value, 0, 3);
            bool right = (c & 1) == 1, bottom = c >= 2;
            const float lh = 24f, m = 20f, w = 250f;
            for (int i = 0; i < _clLines.Count; i++)
            {
                var l = _clLines[_clLines.Count - 1 - i];
                float a = Mathf.Clamp01((life - (now - l.T)) / 0.5f);
                float x = right ? Screen.width - w - m : m;
                float y = bottom ? Screen.height - m - 120f - (lh + 2f) * (i + 1) : m + 330f + (lh + 2f) * i;
                var r = new Rect(x, y, w, lh);
                G.Fill(r, new Color(0, 0, 0, 0.5f * a));
                G.Fill(new Rect(r.x, r.y, 3, r.height), new Color(l.C.r, l.C.g, l.C.b, a));
                G.Label(new Rect(r.x + 10, r.y, r.width - 12, r.height), l.Text, 13, new Color(l.C.r, l.C.g, l.C.b, a), TextAnchor.MiddleLeft, true);
            }
        }
    }
}
