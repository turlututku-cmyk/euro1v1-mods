using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Rainbow text and rainbow weapons. Cosmetic only: tints colors on this client, never touches gameplay or the network.
    public static partial class Features
    {
        static float _nextRb, _nextScanRb;
        static readonly List<TMP_Text> _texts = new List<TMP_Text>();
        static readonly Dictionary<int, Color> _textOrig = new Dictionary<int, Color>();
        static bool _textActive;

        class GunR { public Renderer R; public string[] Props; public Color[] Orig; }
        static readonly List<GunR> _guns = new List<GunR>();
        static MaterialPropertyBlock _mpb;
        static bool _gunActive, _gunLogged;

        static readonly string[] GunWords = { "weapon", "gun", "pistol", "shotgun", "rifle", "smg", "sniper", "hammer", "viewmodel", "spear" };

        public static void UpdateRainbow()
        {
            bool textOn = Bv(Cfg.TextOn) && (!Cfg.TextMenusOnly.Value || InMenu);
            bool gunOn = Bv(Cfg.GunOn);

            if (Time.unscaledTime >= _nextScanRb)
            {
                _nextScanRb = Time.unscaledTime + 1f;
                try { if (textOn) ScanTexts(); } catch { }
                try { if (gunOn) ScanGuns(); } catch { }
            }
            if (!textOn && _textActive) RestoreTexts();
            if (!gunOn && _gunActive) RestoreGuns();

            if (Time.unscaledTime < _nextRb) return;
            _nextRb = Time.unscaledTime + 0.05f;
            if (textOn) ApplyTexts();
            if (gunOn) ApplyGuns();
        }

        // ---------- text ----------

        static void ScanTexts()
        {
            _texts.Clear();
            foreach (var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
            {
                _texts.Add(t);
                int id = t.GetInstanceID();
                if (!_textOrig.ContainsKey(id)) _textOrig[id] = t.color;
            }
        }

        static void ApplyTexts()
        {
            float speed = Cfg.TextSpeed.Value;
            bool wave = Cfg.TextMode.Value == 0;
            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                try
                {
                    var t = _texts[i];
                    int id = t.GetInstanceID();
                    float off = wave ? (Mathf.Abs(id) % 97) / 97f : 0f;
                    var c = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * speed + off, 1f), 0.8f, 1f);
                    c.a = t.color.a; // keep the game's own fade (chat lines, popups) instead of forcing the first-seen alpha
                    t.color = c;
                    _textActive = true;
                }
                catch { _texts.RemoveAt(i); }
            }
        }

        static void RestoreTexts()
        {
            foreach (var t in _texts)
            {
                try { if (_textOrig.TryGetValue(t.GetInstanceID(), out var o)) t.color = new Color(o.r, o.g, o.b, t.color.a); } catch { }
            }
            _texts.Clear(); _textOrig.Clear();
            _textActive = false;
        }

        // ---------- weapons ----------

        static bool LooksLikeGun(Transform t)
        {
            for (int i = 0; i < 6 && t != null; i++, t = t.parent)
            {
                var n = t.name.ToLowerInvariant();
                foreach (var w in GunWords) if (n.Contains(w)) return true;
            }
            return false;
        }

        static void ScanGuns()
        {
            var found = new List<Renderer>();
            if (Cfg.GunScope.Value == 0)
            {
                // the weapon is drawn by a separate overlay camera: take renderers on layers only that camera sees
                Camera wc = null;
                foreach (var c in Camera.allCameras) if (c.name.ToLowerInvariant().Contains("weapon")) { wc = c; break; }
                var main = Cam();
                if (wc != null)
                {
                    int wm = wc.cullingMask, mm = main != null ? main.cullingMask : 0;
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                    {
                        int bit = 1 << r.gameObject.layer;
                        if ((wm & bit) != 0 && (mm & bit) == 0) found.Add(r);
                    }
                }
                else if (main != null)
                    foreach (var r in main.GetComponentsInChildren<Renderer>(false)) found.Add(r);
            }
            else
            {
                foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                    if (LooksLikeGun(r.transform)) found.Add(r);
            }

            var keep = new HashSet<int>();
            foreach (var r in found)
            {
                var tn = r.GetIl2CppType().Name;
                if (tn != "MeshRenderer" && tn != "SkinnedMeshRenderer") continue;
                var sm = r.sharedMaterial;
                if (sm == null) continue;
                var props = new List<string>();
                foreach (var p in new[] { "_BaseColor", "_Color" }) if (sm.HasProperty(p)) props.Add(p);
                if (props.Count == 0) continue;
                int id = r.GetInstanceID();
                keep.Add(id);
                if (_guns.Any(g => { try { return g.R.GetInstanceID() == id; } catch { return false; } })) continue;
                _guns.Add(new GunR { R = r, Props = props.ToArray(), Orig = props.Select(p => sm.GetColor(p)).ToArray() });
            }
            _guns.RemoveAll(g => { try { return !keep.Contains(g.R.GetInstanceID()); } catch { return true; } });

            if (!_gunLogged)
            {
                _gunLogged = true;
                Plugin.Instance.Log.LogInfo("Rainbow guns: " + _guns.Count + " renderer(s) tinted (scope " + Cfg.GunScope.Value + "): "
                    + string.Join(", ", _guns.Take(8).Select(g => { try { return g.R.name; } catch { return "?"; } })));
            }
        }

        static void ApplyGuns()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            float h = Mathf.Repeat(Time.unscaledTime * Cfg.GunSpeed.Value, 1f);
            var c = Color.HSVToRGB(h, Cfg.GunSat.Value, 1f);
            foreach (var g in _guns)
            {
                try
                {
                    g.R.GetPropertyBlock(_mpb);
                    foreach (var p in g.Props) _mpb.SetColor(p, c);
                    g.R.SetPropertyBlock(_mpb);
                    _gunActive = true;
                }
                catch { }
            }
        }

        static void RestoreGuns()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            foreach (var g in _guns)
            {
                try
                {
                    g.R.GetPropertyBlock(_mpb);
                    for (int i = 0; i < g.Props.Length; i++) _mpb.SetColor(g.Props[i], g.Orig[i]);
                    g.R.SetPropertyBlock(_mpb);
                }
                catch { }
            }
            _guns.Clear();
            _gunActive = false;
            _gunLogged = false;
        }
    }
}
