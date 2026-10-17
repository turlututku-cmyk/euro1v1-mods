using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    public static partial class Features
    {
        public static bool PanelOpen;
        static bool _builtOnce;

        static float _fps, _acc; static int _frames;
        static string _toast; static float _toastUntil;
        static int _origVsync = -1, _origTarget = -2;
        static bool _limitApplied;
        static float _nextScan;
        static bool _loggedCandidates;
        static readonly List<Transform> _hidden = new List<Transform>();
        static readonly Dictionary<int, Vector3> _origScale = new Dictionary<int, Vector3>();

        public static void Toast(string s) { _toast = s; _toastUntil = Time.unscaledTime + 3f; }

        public static void Update()
        {
            if (!_builtOnce) { _builtOnce = true; try { Window.EnsureBuilt(); } catch (Exception e) { Plugin.Instance.Log.LogWarning("Mod list: " + e.Message); } }
            _acc += Time.unscaledDeltaTime; _frames++;
            if (_acc >= 0.5f) { _fps = _frames / _acc; _acc = 0; _frames = 0; }

            if (Cfg.BgOn.Value) Application.runInBackground = true;

            var kb = Keyboard.current;
            if (kb != null)
            {
                UpdateKeybindCapture();
                if (KeyHit(Cfg.KbMenu.Value)) PanelOpen = !PanelOpen;
                if (PanelOpen && kb.escapeKey.wasPressedThisFrame && !BindEscapeJustUsed) PanelOpen = false;
                if (Cfg.ShotOn.Value && KeyHit(Cfg.KbShot.Value)) Screenshot();
            }

            if (Cfg.LimitOn.Value)
            {
                if (!_limitApplied) { _origVsync = QualitySettings.vSyncCount; _origTarget = Application.targetFrameRate; _limitApplied = true; }
                QualitySettings.vSyncCount = Cfg.Vsync.Value ? 1 : 0;
                Application.targetFrameRate = Cfg.Vsync.Value ? -1 : (int)Cfg.LimitFps.Value;
            }
            else if (_limitApplied)
            {
                QualitySettings.vSyncCount = _origVsync;
                Application.targetFrameRate = _origTarget;
                _limitApplied = false;
            }

            if (Cfg.VolOn.Value) AudioListener.volume = Cfg.Vol.Value;
            UpdateVisuals();

            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1f;
                try { ScanDefaultCrosshair(); } catch { }
            }
        }

        public static void LateUpdate()
        {
            FreecamLateUpdate();
            ViewerLateUpdate();
            if (Cfg.FovOn.Value)
            {
                var cam = Cam();
                if (cam != null) cam.fieldOfView = Cfg.Fov.Value;
            }
            if (Cfg.CrossHideDefault.Value && Cursor.lockState == CursorLockMode.Locked)
            {
                for (int i = 0; i < _hidden.Count; i++)
                {
                    try { if (_hidden[i] != null) _hidden[i].localScale = Vector3.zero; } catch { }
                }
            }
        }

        static void ScanDefaultCrosshair()
        {
            if (!Cfg.CrossHideDefault.Value)
            {
                if (_hidden.Count > 0)
                {
                    foreach (var t in _hidden)
                    {
                        try { if (t != null && _origScale.TryGetValue(t.GetInstanceID(), out var sc)) t.localScale = sc; } catch { }
                    }
                    _hidden.Clear(); _origScale.Clear();
                }
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked) return;

            _hidden.Clear();
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
            {
                var n = t.name.ToLowerInvariant();
                if (!n.Contains("crosshair") && !n.Contains("reticle")) continue;
                if (n.Contains("modsmenu") || n.Contains("scope")) continue;
                var p = t.parent; bool nested = false;
                while (p != null)
                {
                    var pn = p.name.ToLowerInvariant();
                    if (pn.Contains("crosshair") || pn.Contains("reticle")) { nested = true; break; }
                    p = p.parent;
                }
                if (nested) continue;
                if (!_origScale.ContainsKey(t.GetInstanceID())) _origScale[t.GetInstanceID()] = t.localScale;
                _hidden.Add(t);
                if (!_loggedCandidates) Plugin.Instance.Log.LogInfo("Default crosshair object: " + t.name + " (active=" + t.gameObject.activeInHierarchy + ")");
            }
            _loggedCandidates = true;
        }

        public static void Screenshot()
        {
            try
            {
                var dir = Path.Combine(Paths.GameRootPath, "Mods", "Screenshots");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "euro3v3_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                ScreenCapture.CaptureScreenshot(path);
                Toast("Screenshot saved to Mods\\Screenshots");
            }
            catch (Exception e) { Toast("Screenshot failed: " + e.Message); }
        }

        // ---------- drawing (OnGUI) ----------

        public static void DrawHud()
        {
            if (Event.current.type != EventType.Repaint) return;

            DrawVisuals();

            if (Cfg.CrossOn.Value && !PanelOpen && (Cfg.CrossAlways.Value || Cursor.lockState == CursorLockMode.Locked))
                DrawCrosshair(new Vector2(Screen.width / 2f, Screen.height / 2f));

            var lines = new List<string>[4] { new List<string>(), new List<string>(), new List<string>(), new List<string>() };
            if (Cfg.FpsOn.Value) lines[Mathf.Clamp(Cfg.FpsCorner.Value, 0, 3)].Add(((int)_fps) + " FPS  " + (1000f / Mathf.Max(_fps, 0.01f)).ToString("0.0") + " ms");
            if (Cfg.ClockOn.Value) lines[Mathf.Clamp(Cfg.ClockCorner.Value, 0, 3)].Add(DateTime.Now.ToString(Cfg.Clock24.Value ? "HH:mm:ss" : "hh:mm:ss tt"));

            const float w = 170f, h = 26f, m = 10f;
            for (int c = 0; c < 4; c++)
            {
                bool right = (c & 1) == 1, bottom = c >= 2;
                for (int i = 0; i < lines[c].Count; i++)
                {
                    float x = right ? Screen.width - w - m : m;
                    float y = bottom ? Screen.height - m - (h + 2) * (i + 1) : m + (h + 2) * i;
                    var r = new Rect(x, y, w, h);
                    G.Fill(r, new Color(0, 0, 0, 0.55f));
                    G.Fill(new Rect(r.x, r.y, 3, r.height), G.Accent);
                    G.Label(new Rect(r.x + 8, r.y, r.width - 10, r.height), lines[c][i], 15, G.Text, TextAnchor.MiddleLeft, true);
                }
            }

        }

        public static void DrawToast()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_toast != null && Time.unscaledTime < _toastUntil)
            {
                var r = new Rect(Screen.width / 2f - 330, Screen.height - 110, 660, 40);
                G.Fill(r, new Color(0, 0, 0, 0.85f));
                G.Fill(new Rect(r.x, r.y, 3, r.height), G.Accent);
                G.Label(r, _toast, 15, G.Text, TextAnchor.MiddleCenter, true);
            }
        }

        public static void DrawCrosshair(Vector2 c)
        {
            float s = Cfg.CrossSize.Value, t = Mathf.Max(1f, Mathf.Round(Cfg.CrossThick.Value)), g = Cfg.CrossGap.Value;
            var col = new Color(Cfg.CrossR.Value, Cfg.CrossG.Value, Cfg.CrossB.Value, Cfg.CrossAlpha.Value);
            if (Cfg.CrossRainbow.Value) { col = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.3f, 1f), 1f, 1f); col.a = Cfg.CrossAlpha.Value; }
            var rects = new List<Rect>();
            float cx = Mathf.Round(c.x), cy = Mathf.Round(c.y), ht = t / 2f;
            int style = Cfg.CrossStyle.Value;

            if (style == 0 || style == 5 || style == 3)
            {
                if (style != 5) rects.Add(new Rect(cx - ht, cy - g - s, t, s));
                rects.Add(new Rect(cx - ht, cy + g, t, s));
                rects.Add(new Rect(cx - g - s, cy - ht, s, t));
                rects.Add(new Rect(cx + g, cy - ht, s, t));
                if (style == 3) rects.Add(new Rect(cx - ht, cy - ht, t, t));
            }
            else if (style == 1)
            {
                rects.Add(new Rect(cx - (t + 1), cy - (t + 1), (t + 1) * 2, (t + 1) * 2));
            }
            else if (style == 2)
            {
                float r = g + s;
                int n = Mathf.Max(24, (int)(2f * Mathf.PI * r / t));
                for (int i = 0; i < n; i++)
                {
                    float a = i * 2f * Mathf.PI / n;
                    rects.Add(new Rect(Mathf.Round(cx + Mathf.Cos(a) * r - ht), Mathf.Round(cy + Mathf.Sin(a) * r - ht), t, t));
                }
            }
            else if (style == 4)
            {
                for (float k = g; k <= g + s; k += t)
                {
                    float d = Mathf.Round(k / 1.4142f);
                    rects.Add(new Rect(cx + d - ht, cy + d - ht, t, t));
                    rects.Add(new Rect(cx - d - ht, cy + d - ht, t, t));
                    rects.Add(new Rect(cx + d - ht, cy - d - ht, t, t));
                    rects.Add(new Rect(cx - d - ht, cy - d - ht, t, t));
                }
            }
            if (Cfg.CrossDot.Value && style != 1 && style != 3)
                rects.Add(new Rect(cx - ht, cy - ht, t, t));

            if (Cfg.CrossOutline.Value)
            {
                var oc = new Color(0, 0, 0, col.a);
                foreach (var r in rects) G.Fill(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), oc);
            }
            foreach (var r in rects) G.Fill(r, col);
        }
    }
}
