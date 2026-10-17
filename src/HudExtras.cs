using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    // Input display, hit/kill sounds and damage numbers. All client-side, purely feedback; nothing is sent anywhere.
    public static partial class Features
    {
        // ---------------------------------------------------------------- shared helpers

        static PlayerObjectController _localPoc;
        static float _nextLocalScan;

        static PlayerObjectController LocalPlayer()
        {
            if (Time.unscaledTime >= _nextLocalScan || _localPoc == null)
            {
                _nextLocalScan = Time.unscaledTime + 1f;
                _localPoc = null;
                try
                {
                    foreach (var p in UnityEngine.Object.FindObjectsOfType<PlayerObjectController>())
                        if (p.isLocalPlayer) { _localPoc = p; break; }
                }
                catch { }
            }
            return _localPoc;
        }

        // ---------------------------------------------------------------- input display

        static void KeyBox(Rect r, string label, bool down, float a, int size)
        {
            var bg = down ? new Color(G.Accent.r, G.Accent.g, G.Accent.b, a) : new Color(0f, 0f, 0f, 0.55f * a);
            G.Fill(r, bg);
            G.Fill(new Rect(r.x, r.yMax - 2, r.width, 2), down ? new Color(1, 1, 1, a) : new Color(1, 1, 1, 0.18f * a));
            G.Label(r, label, size, down ? Color.black : new Color(1, 1, 1, a), TextAnchor.MiddleCenter, true);
        }

        static void DrawInputDisplay()
        {
            if (!Cfg.InputOn.Value) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            var ms = Mouse.current;

            float s = Cfg.InputScale.Value, k = 42f * s, g = 4f * s, a = Cfg.InputOpacity.Value;
            int fs = Mathf.Max(10, (int)(15 * s));
            bool mouse = Cfg.InputMouse.Value && ms != null;
            float w = 3 * k + 2 * g + (mouse ? 3 * g + 2.2f * k : 0f);
            float h = 4 * k + 3 * g;
            const float m = 20f;
            int c = Mathf.Clamp(Cfg.InputCorner.Value, 0, 3);
            float x0 = (c & 1) == 1 ? Screen.width - w - m : m;
            float y0 = c >= 2 ? Screen.height - h - m : m + 50f;

            Func<int, int, Rect> cell = (col, row) => new Rect(x0 + col * (k + g), y0 + row * (k + g), k, k);
            KeyBox(cell(1, 0), "W", kb.wKey.isPressed, a, fs);
            KeyBox(cell(0, 1), "A", kb.aKey.isPressed, a, fs);
            KeyBox(cell(1, 1), "S", kb.sKey.isPressed, a, fs);
            KeyBox(cell(2, 1), "D", kb.dKey.isPressed, a, fs);
            var shift = new Rect(x0, y0 + 2 * (k + g), 1.5f * k + g / 2f, k);
            var ctrl = new Rect(shift.xMax + g, shift.y, 1.5f * k + g / 2f, k);
            KeyBox(shift, "SHIFT", kb.leftShiftKey.isPressed, a, Mathf.Max(9, fs - 4));
            KeyBox(ctrl, "CTRL", kb.leftCtrlKey.isPressed, a, Mathf.Max(9, fs - 4));
            KeyBox(new Rect(x0, y0 + 3 * (k + g), 3 * k + 2 * g, k), "SPACE", kb.spaceKey.isPressed, a, Mathf.Max(9, fs - 3));
            if (mouse)
            {
                float mx = x0 + 3 * k + 5 * g;
                float mw = 1.1f * k - g / 2f;
                KeyBox(new Rect(mx, y0, mw, 1.6f * k), "LMB", ms.leftButton.isPressed, a, Mathf.Max(9, fs - 4));
                KeyBox(new Rect(mx + mw + g, y0, mw, 1.6f * k), "RMB", ms.rightButton.isPressed, a, Mathf.Max(9, fs - 4));
                KeyBox(new Rect(mx, y0 + 1.6f * k + g, 2 * mw + g, 0.7f * k), "E", kb.eKey.isPressed, a, fs);
            }
        }

        // ---------------------------------------------------------------- hit / kill sounds

        static AudioSource _sfx;
        static readonly Dictionary<int, AudioClip> _synth = new Dictionary<int, AudioClip>();
        static AudioClip _customHit, _customKill;
        static bool _customLoaded;
        static readonly List<Hitmarker> _hms = new List<Hitmarker>();
        static readonly Dictionary<int, bool> _hmWas = new Dictionary<int, bool>();
        static float _nextHmScan;
        public static string[] HitStyles = { "Ding", "Tick", "Pop", "Chime", "Custom file" };

        static AudioClip MakeTone(int style, bool kill)
        {
            const int rate = 44100;
            float pitch = kill ? 0.75f : 1f;
            float len = style == 1 ? 0.045f : style == 2 ? 0.09f : style == 3 ? 0.32f : 0.16f;
            if (kill) len *= 1.6f;
            int n = (int)(rate * len);
            var d = new float[n];
            var rnd = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate, env = Mathf.Exp(-t * (style == 1 ? 90f : style == 3 ? 9f : 22f));
                float v;
                if (style == 1) v = ((float)rnd.NextDouble() * 2f - 1f) * 0.6f + Mathf.Sin(2f * Mathf.PI * 2400f * pitch * t) * 0.4f;
                else if (style == 2) v = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(700f, 220f, t / len) * pitch * t * 1.0f);
                else if (style == 3) v = 0.6f * Mathf.Sin(2f * Mathf.PI * 880f * pitch * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 1320f * pitch * t);
                else v = 0.75f * Mathf.Sin(2f * Mathf.PI * 1250f * pitch * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 2500f * pitch * t);
                float attack = Mathf.Clamp01(i / 60f);
                d[i] = v * env * attack * 0.8f;
            }
            var clip = AudioClip.Create("modsmenu_hit_" + style + (kill ? "k" : "h"), n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static AudioClip LoadCustomSound(string file)
        {
            try
            {
                var dir = Path.Combine(Paths.GameRootPath, "Mods", "Sounds");
                Directory.CreateDirectory(dir);
                foreach (var ext in new[] { ".wav", ".mp3", ".ogg" })
                {
                    var path = Path.Combine(dir, file + ext);
                    if (!File.Exists(path)) continue;
                    var d = DecodeFile(path, 0);
                    if (d.Error != null || d.Data == null || d.Channels <= 0) continue;
                    var clip = AudioClip.Create("modsmenu_custom_" + file, d.Data.Length / d.Channels, d.Channels, d.Rate, false);
                    clip.SetData(d.Data, 0);
                    return clip;
                }
            }
            catch (Exception e) { Plugin.Instance.Log.LogWarning("Custom sound: " + e.Message); }
            return null;
        }

        public static void PlayHitSound(bool kill)
        {
            if (_sfx == null)
            {
                var go = new GameObject("ModsMenu_Sfx");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _sfx = go.AddComponent<AudioSource>();
                _sfx.playOnAwake = false;
                _sfx.spatialBlend = 0f;
            }
            int style = kill ? Cfg.KillStyle.Value : Cfg.HitStyle.Value;
            AudioClip clip;
            if (style >= 4)
            {
                if (!_customLoaded) { _customLoaded = true; _customHit = LoadCustomSound("hit"); _customKill = LoadCustomSound("kill"); }
                clip = kill ? (_customKill ?? _customHit) : _customHit;
                if (clip == null) { style = kill ? 3 : 0; clip = null; }
            }
            else clip = null;
            if (clip == null)
            {
                int key = style * 2 + (kill ? 1 : 0);
                if (!_synth.TryGetValue(key, out clip) || clip == null) { clip = MakeTone(Mathf.Min(style, 3), kill); _synth[key] = clip; }
            }
            _sfx.pitch = Cfg.HitVary.Value ? 1f + (UnityEngine.Random.value - 0.5f) * 0.12f : 1f;
            _sfx.PlayOneShot(clip, Cfg.HitVol.Value);
        }

        // One hitmarker flash = one hit. Called from the Harmony hook on Hitmarker.Hit() and (as a fallback) from polling.
        static readonly Dictionary<int, float> _hmLast = new Dictionary<int, float>();
        static int _hmLogCount;

        public static void HandleHitmarker(Hitmarker hm, string via)
        {
            bool dmg = DmgActive;
            if (!Cfg.HitOn.Value && !dmg && !Cfg.ClipOn.Value) return;
            int id = hm.GetInstanceID();
            float now = Time.unscaledTime;
            if (_hmLast.TryGetValue(id, out float last) && now - last < 0.04f) return; // hook + polling firing for the same hit
            _hmLast[id] = now;

            var n = hm.gameObject.name;
            if (_hmLogCount++ < 25) Plugin.Instance.Log.LogInfo("Hitmarker '" + n + "' fired (" + via + ")");
            if (n.IndexOf("Health", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (dmg && Cfg.DmgHeal.Value && !_healPending) { _healPending = true; _healT0 = now; }
                return; // healing marker
            }
            bool kill = n.IndexOf("Kill", StringComparison.OrdinalIgnoreCase) >= 0;
            if (Cfg.HitOn.Value) PlayHitSound(kill);
            ClipNoteHit(kill);
            if (dmg) OnDamageHit(kill);
        }

        static void UpdateHitSounds()
        {
            bool dmg = DmgActive;
            if (!Cfg.HitOn.Value && !dmg && !Cfg.ClipOn.Value)
            {
                foreach (var h in _hms) { try { if (h.source != null) h.source.mute = false; } catch { } }
                return;
            }
            if (Time.unscaledTime >= _nextHmScan)
            {
                _nextHmScan = Time.unscaledTime + 1f;
                try { _hms.Clear(); foreach (var h in UnityEngine.Object.FindObjectsOfType<Hitmarker>()) _hms.Add(h); } catch { }
            }
            foreach (var hm in _hms)
            {
                try
                {
                    int id = hm.GetInstanceID();
                    bool on = hm.hitImage != null && hm.hitImage.activeInHierarchy;
                    _hmWas.TryGetValue(id, out bool was);
                    _hmWas[id] = on;
                    bool replace = Cfg.HitOn.Value && Cfg.HitMuteGame.Value;
                    if (hm.source != null) hm.source.mute = replace;
                    if (!on || was) continue;
                    HandleHitmarker(hm, "poll");
                }
                catch { }
            }
        }

        // ---------------------------------------------------------------- damage numbers

        class DmgNum { public Vector3 World; public float T; public string Text; public Color C; public bool Kill; }
        class HpDrop { public Health H; public float Amount, T; }
        static readonly List<DmgNum> _dmgNums = new List<DmgNum>();
        static readonly List<HpDrop> _drops = new List<HpDrop>();
        static readonly List<HpDrop> _gains = new List<HpDrop>();
        static bool _healPending; static float _healT0;
        static readonly List<Health> _healths = new List<Health>();
        static readonly Dictionary<int, float> _hpPrev = new Dictionary<int, float>();
        static float _nextHpScan;

        static void UpdateDamageNumbers()
        {
            if (!DmgActive) { if (_dmgNums.Count > 0) _dmgNums.Clear(); return; }
            if (Time.unscaledTime >= _nextHpScan)
            {
                _nextHpScan = Time.unscaledTime + 0.5f;
                try { _healths.Clear(); foreach (var h in UnityEngine.Object.FindObjectsOfType<Health>()) _healths.Add(h); } catch { }
            }
            float now = Time.unscaledTime;
            foreach (var h in _healths)
            {
                try
                {
                    float hp = h.health;
                    int id = h.GetInstanceID();
                    if (_hpPrev.TryGetValue(id, out float prev) && hp < prev - 0.01f)
                        _drops.Add(new HpDrop { H = h, Amount = prev - hp, T = now });
                    else if (_hpPrev.TryGetValue(id, out float prev2) && hp > prev2 + 0.01f)
                        _gains.Add(new HpDrop { H = h, Amount = hp - prev2, T = now });
                    _hpPrev[id] = hp;
                }
                catch { }
            }
            _drops.RemoveAll(d => now - d.T > 1.5f);
            _gains.RemoveAll(d => now - d.T > 1.2f);
        }

        // A "shot" = the first hitmarker plus a short window to catch the other pellets / follow-up damage ticks.
        static bool _dmgShotPending, _dmgShotKill;
        static float _dmgShotT0;
        const float ShotWindow = 0.15f;

        static void OnDamageHit(bool kill)
        {
            if (!_dmgShotPending) { _dmgShotPending = true; _dmgShotT0 = Time.unscaledTime; _dmgShotKill = kill; }
            else _dmgShotKill |= kill;
        }

        static bool IsEnemyHealth(Health h, PlayerObjectController me, int myTeam)
        {
            if (me != null && h.gameObject == me.gameObject) return false;
            try
            {
                var poc = h.GetComponentInParent<PlayerObjectController>();
                if (poc != null) return poc != me && (myTeam < 0 || poc.team != myTeam);
            }
            catch { }
            return myTeam < 0 || h.team != myTeam;
        }

        static int _dmgLogCount;
        static void DmgLog(string s) { if (_dmgLogCount++ < 40) Plugin.Instance.Log.LogInfo("Damage numbers: " + s); }

        static void FinishShot()
        {
            _dmgShotPending = false;
            var me = LocalPlayer();
            int myTeam = me != null ? me.team : -1;
            float since = _dmgShotT0 - 0.35f;

            // total damage per enemy within this shot
            var totals = new Dictionary<int, float>();
            var who = new Dictionary<int, Health>();
            foreach (var d in _drops)
            {
                try
                {
                    if (d.T < since) continue;
                    if (!IsEnemyHealth(d.H, me, myTeam)) continue; // enemies only
                    int id = d.H.GetInstanceID();
                    totals.TryGetValue(id, out float cur);
                    totals[id] = cur + d.Amount;
                    who[id] = d.H;
                }
                catch { }
            }
            if (totals.Count == 0)
            {
                foreach (var hh in _healths)
                {
                    try
                    {
                        var atk = hh.LastDamageAttacker;
                        if (me == null || atk == null || atk.GetInstanceID() != me.GetInstanceID()) continue;
                        if (Time.time - hh.LastDamageAt > 1.2f || hh.LastDamageAmount <= 0f) continue;
                        int fid = hh.GetInstanceID();
                        totals[fid] = hh.LastDamageAmount; who[fid] = hh;
                    }
                    catch { }
                }
                DmgLog("no health drop seen after the hit; fallback " + (totals.Count > 0 ? "found " + totals.Count : "found nothing") + " (healths tracked " + _healths.Count + ", drops " + _drops.Count + ")");
                if (totals.Count == 0) return;
            }
            else DmgLog("shot total from " + totals.Count + " target(s)");

            int bestId = totals.OrderByDescending(kv => kv.Value).First().Key;
            float amount = totals[bestId];
            var h = who[bestId];
            _drops.RemoveAll(d => { try { return d.H.GetInstanceID() == bestId && d.T >= since; } catch { return true; } });

            float max = 100f;
            try { max = Mathf.Max(1f, h.maxHealth); } catch { }
            var col = _dmgShotKill ? new Color(1f, 0.25f, 0.25f) : amount >= max * 0.5f ? new Color(1f, 0.65f, 0.15f) : Color.white;
            Vector3 pos;
            try { pos = h.transform.position + Vector3.up * 2.1f; } catch { return; }
            _dmgNums.Add(new DmgNum { World = pos, T = Time.unscaledTime, Text = Mathf.RoundToInt(amount).ToString(), C = col, Kill = _dmgShotKill });
            ClShotDealt(Mathf.RoundToInt(amount), _dmgShotKill);
        }

        static void FinishHeal()
        {
            _healPending = false;
            var me = LocalPlayer();
            int myTeam = me != null ? me.team : -1;
            float since = _healT0 - 0.6f;
            var totals = new Dictionary<int, float>();
            var who = new Dictionary<int, Health>();
            foreach (var g in _gains)
            {
                try
                {
                    if (g.T < since) continue;
                    if (myTeam >= 0 && g.H.team != myTeam) continue; // teammates (or yourself) only
                    int id = g.H.GetInstanceID();
                    totals.TryGetValue(id, out float cur);
                    totals[id] = cur + g.Amount;
                    who[id] = g.H;
                }
                catch { }
            }
            if (totals.Count == 0) return;
            int bestId = totals.OrderByDescending(kv => kv.Value).First().Key;
            var h = who[bestId];
            float amount = totals[bestId];
            _gains.RemoveAll(g => { try { return g.H.GetInstanceID() == bestId && g.T >= since; } catch { return true; } });
            Vector3 pos;
            try { pos = h.transform.position + Vector3.up * 2.1f; } catch { return; }
            _dmgNums.Add(new DmgNum { World = pos, T = Time.unscaledTime, Text = "+" + Mathf.RoundToInt(amount), C = new Color(0.3f, 1f, 0.45f), Kill = false });
        }

        static void DrawDamageNumbers()
        {
            if (!DmgActive) return;
            if (_dmgShotPending)
            {
                float el = Time.unscaledTime - _dmgShotT0;
                bool any = false;
                var me0 = LocalPlayer();
                int mt0 = me0 != null ? me0.team : -1;
                foreach (var d in _drops) { try { if (d.T >= _dmgShotT0 - 0.35f && IsEnemyHealth(d.H, me0, mt0)) { any = true; break; } } catch { } }
                if ((any && el >= 0.3f) || el >= 0.9f) FinishShot();
            }
            if (_healPending && Time.unscaledTime - _healT0 >= 0.3f) FinishHeal();
            if (!Cfg.DmgOn.Value) { _dmgNums.Clear(); return; }   // only the Combat Log wanted the numbers: nothing to draw
            if (_dmgNums.Count == 0) return;
            var cam = Cam();
            if (cam == null) return;
            float dur = Mathf.Max(0.3f, Cfg.DmgTime.Value), baseSize = Cfg.DmgSize.Value, now = Time.unscaledTime;
            bool atCrosshair = Cfg.DmgPlace.Value == 1;
            _dmgNums.RemoveAll(n => now - n.T > dur);
            for (int i = 0; i < _dmgNums.Count; i++)
            {
                var n = _dmgNums[i];
                float age = now - n.T, f = age / dur;
                float pop = 1f + 0.5f * Mathf.Clamp01(1f - age * 5f);
                int size = Mathf.RoundToInt(baseSize * pop * (n.Kill ? 1.25f : 1f));
                float a = 1f - f * f;
                Rect r;
                if (atCrosshair)
                {
                    float rise = age * 40f;
                    r = new Rect(Screen.width / 2f + 70f, Screen.height / 2f - 50f - rise - i * 4f, 200f, size * 1.4f);
                }
                else
                {
                    var sp = cam.WorldToScreenPoint(n.World + Vector3.up * (age * 0.9f));
                    bool onScreen = sp.z > 0f && sp.x > 0f && sp.x < Screen.width && sp.y > 0f && sp.y < Screen.height;
                    if (onScreen) r = new Rect(sp.x - 80f, Screen.height - sp.y - size, 160f, size * 1.4f);
                    else r = new Rect(Screen.width / 2f + 70f, Screen.height / 2f - 50f - age * 40f - i * 4f, 200f, size * 1.4f); // target not in view: show it at the crosshair
                }
                string text = n.Kill && Cfg.DmgKillTag.Value ? n.Text + "  KILL" : n.Text;
                var anchor = atCrosshair ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                G.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, size, new Color(0, 0, 0, a), anchor, true);
                G.Label(r, text, size, new Color(n.C.r, n.C.g, n.C.b, a), anchor, true);
            }
        }
    }
}

namespace Euro3v3.ModsMenu
{
    // Harmony postfix on the game's Hitmarker.Hit(): fires once per hit, no matter how the marker is animated.
    public static class HitPatch
    {
        public static void Postfix(Hitmarker __instance)
        {
            try { Features.HandleHitmarker(__instance, "hook"); } catch { }
        }
    }
}
