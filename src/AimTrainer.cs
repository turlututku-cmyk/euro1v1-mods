using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    // Aim Trainer: practice-only drills in a bot match. Six drills, a warm-up routine, combos, hit feedback, a results card with coaching tips
    // and a saved history. Targets are plain visual spheres checked with a ray from your camera, so nothing here touches the game's damage
    // or networking, and it never runs with other human players present.
    public static partial class Features
    {
        public static bool AimActive;
        public static string AimResult = "";
        public static readonly string[] AimModes = { "Gridshot", "Flick", "Moving", "Tracking", "Head level", "Reaction" };
        public static readonly string[] AimDists = { "Near", "Mid", "Far" };

        class AimTarget
        {
            public GameObject Go; public Material Mat; public float Spawn, Born, Radius;
            public Vector3 Anchor, Right, Pos;
            public float X, Dir = 1f, Speed, Range, Vamp, F1, F2, F3, P1, P2, P3;
            public bool On;
        }
        class AimPop { public GameObject Go; public float T0; public Vector3 Scale; }
        class AimFloat { public Vector3 World; public string Text; public float T0; public Color C; }
        public class AimRunRow { public string Date = ""; public int Mode, Score, Hits, Shots; public float Seconds, Acc, ReactMs; }
        class RoutineStep { public int Mode; public float Secs; }

        static readonly List<AimTarget> _aimTargets = new List<AimTarget>();
        static readonly List<AimPop> _aimPops = new List<AimPop>();
        static readonly List<AimFloat> _aimFloats = new List<AimFloat>();
        static int _aimMode, _aimScore, _aimHits, _aimShots, _aimCombo, _aimBestCombo, _aimBiasN;
        static float _aimStart, _aimLen, _aimNextSpawn, _aimReactSum, _aimReactBest, _aimOnTarget, _aimBiasYaw, _aimBiasPitch;
        static Material _aimMat;
        static AudioSource _aimSrc;
        static readonly Dictionary<int, AudioClip> _aimClips = new Dictionary<int, AudioClip>();

        static string _aimCardTitle = "", _aimCardScore = "";
        static bool _aimCardRecord;
        static readonly List<string> _aimCardLines = new List<string>();
        static float _aimCardUntil, _aimCardTotal;

        static List<RoutineStep> _aimRoutine;
        static int _aimRoutineIdx, _aimRoutineTotal;
        static float _aimNextStepAt;
        static readonly List<string> _aimRoutineLog = new List<string>();

        static readonly List<AimRunRow> _aimHist = new List<AimRunRow>();
        static bool _aimHistLoaded;

        static bool OtherHumansPresent()
        {
            try
            {
                var me = LocalPlayer();
                foreach (var p in UnityEngine.Object.FindObjectsOfType<PlayerObjectController>())
                    if (p != me && !p.IsBot) return true;
            }
            catch { }
            return false;
        }

        // ------------------------------------------------------------ starting and stopping

        public static void StartAim()
        {
            if (AimActive) { StopAim(false); return; }
            _aimRoutine = null;
            BeginAimRun(Mathf.Clamp(Cfg.AimMode.Value, 0, AimModes.Length - 1), Cfg.AimTime.Value);
        }

        public static void StartAimRoutine()
        {
            if (AimActive) { StopAim(false); return; }
            _aimRoutine = new List<RoutineStep>
            {
                new RoutineStep { Mode = 0, Secs = 45 }, new RoutineStep { Mode = 1, Secs = 30 }, new RoutineStep { Mode = 2, Secs = 40 },
                new RoutineStep { Mode = 3, Secs = 30 }, new RoutineStep { Mode = 5, Secs = 25 },
            };
            _aimRoutineIdx = 0; _aimRoutineTotal = 0; _aimRoutineLog.Clear(); _aimNextStepAt = 0f;
            if (!BeginAimRun(_aimRoutine[0].Mode, _aimRoutine[0].Secs)) _aimRoutine = null;
        }

        static void EnsureAimMat()
        {
            if (_aimMat != null) return;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (sh != null) _aimMat = new Material(sh);
        }

        static bool BeginAimRun(int mode, float secs)
        {
            var cam = Cam();
            if (LocalPlayer() == null || cam == null) { Toast("Aim Trainer needs a map: start a practice match first (Practice Launcher or Play > VS Bots)"); return false; }
            if (OtherHumansPresent()) { Toast("Aim Trainer is for practice only, not with other players"); return false; }
            EnsureAimMat();
            ClearAimTargets(); ClearAimPops();
            _aimMode = mode; _aimLen = Mathf.Max(5f, secs); _aimStart = Time.unscaledTime;
            _aimScore = 0; _aimHits = 0; _aimShots = 0; _aimCombo = 0; _aimBestCombo = 0; _aimBiasN = 0;
            _aimReactSum = 0f; _aimReactBest = 99f; _aimOnTarget = 0f; _aimBiasYaw = 0f; _aimBiasPitch = 0f; _aimNextSpawn = 0f;
            AimActive = true; AimResult = ""; _aimCardUntil = 0f;
            if (mode == 5) _aimNextSpawn = _aimStart + 1.2f + UnityEngine.Random.value * 1.5f;
            else for (int i = 0; i < (mode == 0 ? 3 : 1); i++) SpawnAimTarget();
            string[] hint =
            {
                "shoot the targets, they stay until you hit them", "hit each target before it vanishes", "lead the moving target and shoot it",
                "keep the crosshair on the moving target, no need to shoot", "shoot the small head-height targets", "wait for the target, then hit it as fast as you can"
            };
            Toast("Aim Trainer - " + AimModes[mode] + ": " + hint[mode] + "   (" + BindLabel(Cfg.KbAim.Value) + " stops)");
            return true;
        }

        public static void StopAim(bool finished)
        {
            if (!AimActive) return;
            AimActive = false;
            ClearAimTargets();
            if (!finished) { _aimRoutine = null; _aimNextStepAt = 0f; AimResult = ""; Toast("Aim Trainer stopped"); return; }
            FinishAimRun();
        }

        static void ClearAimTargets()
        {
            foreach (var t in _aimTargets)
            {
                try { if (t.Go != null) UnityEngine.Object.Destroy(t.Go); } catch { }
                try { if (t.Mat != null) UnityEngine.Object.Destroy(t.Mat); } catch { }
            }
            _aimTargets.Clear();
        }

        static void ClearAimPops()
        {
            foreach (var p in _aimPops) { try { if (p.Go != null) UnityEngine.Object.Destroy(p.Go); } catch { } }
            _aimPops.Clear(); _aimFloats.Clear();
        }

        // ------------------------------------------------------------ spawning

        static Color AimColor(int mode)
        {
            switch (mode)
            {
                case 0: return new Color(0.2f, 0.95f, 1f);
                case 1: return new Color(1f, 0.6f, 0.15f);
                case 2: return new Color(1f, 0.4f, 0.8f);
                case 3: return new Color(1f, 0.7f, 0.2f);
                case 4: return new Color(1f, 0.92f, 0.25f);
                default: return new Color(1f, 0.3f, 0.3f);
            }
        }

        static void SetAimColor(Material m, Color col)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        }

        static void SpawnAimTarget()
        {
            var cam = Cam();
            if (cam == null) return;
            int mode = _aimMode;
            var origin = cam.transform.position;
            float yaw = cam.transform.eulerAngles.y + (UnityEngine.Random.value - 0.5f) * (mode == 2 || mode == 3 ? 36f : 70f);
            float pitch = mode == 4 ? (UnityEngine.Random.value - 0.5f) * 5f : (UnityEngine.Random.value - 0.5f) * 22f;
            var dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            int d = Mathf.Clamp(Cfg.AimDist.Value, 0, 2);
            float[] lo = { 6f, 11f, 20f }, hi = { 11f, 20f, 32f };
            float dist = lo[d] + UnityEngine.Random.value * (hi[d] - lo[d]);
            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, dist, ~0, QueryTriggerInteraction.Ignore)) dist = Mathf.Max(4f, hit.distance - 1.2f);

            float size = Mathf.Max(0.2f, Cfg.AimSize.Value) * (mode == 4 ? 0.7f : 1f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "AimTarget";
            try { var col = go.GetComponent<Collider>(); if (col != null) UnityEngine.Object.Destroy(col); } catch { }   // visual only: hits are checked with a ray
            go.transform.localScale = Vector3.one * size;

            var t = new AimTarget { Go = go, Spawn = Time.unscaledTime, Born = Time.unscaledTime, Radius = size / 2f, Anchor = origin + dir * dist };
            t.Right = Vector3.Cross(Vector3.up, dir).normalized;
            float spread = Mathf.Max(0.6f, dist / 14f);
            if (mode == 2)
            {
                t.Range = (2.5f + UnityEngine.Random.value * 3f) * spread; t.Speed = (3f + UnityEngine.Random.value * 3f) * spread;
                t.X = (UnityEngine.Random.value * 2f - 1f) * t.Range; t.Dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            }
            else if (mode == 3)
            {
                t.Range = (3f + UnityEngine.Random.value * 2.5f) * spread; t.Vamp = 0.4f + UnityEngine.Random.value * 0.5f;
                t.F1 = 0.6f + UnityEngine.Random.value * 0.5f; t.F2 = 1.3f + UnityEngine.Random.value * 0.8f; t.F3 = 0.8f + UnityEngine.Random.value * 0.7f;
                t.P1 = UnityEngine.Random.value * 6.28f; t.P2 = UnityEngine.Random.value * 6.28f; t.P3 = UnityEngine.Random.value * 6.28f;
            }
            t.Pos = t.Anchor + t.Right * t.X;
            go.transform.position = t.Pos;

            var r = go.GetComponent<Renderer>();
            if (r != null && _aimMat != null)
            {
                t.Mat = new Material(_aimMat);
                SetAimColor(t.Mat, AimColor(mode));
                r.sharedMaterial = t.Mat;
            }
            _aimTargets.Add(t);
        }

        static void MoveAimTargets(float dt)
        {
            float now = Time.unscaledTime;
            foreach (var t in _aimTargets)
            {
                if (t.Go == null) continue;
                if (_aimMode == 2)
                {
                    t.X += t.Dir * t.Speed * dt;
                    if (Mathf.Abs(t.X) > t.Range) { t.X = Mathf.Clamp(t.X, -t.Range, t.Range); t.Dir = -t.Dir; }
                    t.Pos = t.Anchor + t.Right * t.X;
                }
                else if (_aimMode == 3)
                {
                    float tt = now - t.Born;
                    float x = t.Range * (0.6f * Mathf.Sin(t.F1 * tt + t.P1) + 0.4f * Mathf.Sin(t.F2 * tt + t.P2));
                    float y = t.Vamp * Mathf.Sin(t.F3 * tt + t.P3);
                    t.Pos = t.Anchor + t.Right * x + Vector3.up * y;
                }
                else continue;
                t.Go.transform.position = t.Pos;
            }
        }

        // ------------------------------------------------------------ shooting

        static bool AimRayHit(Vector3 o, Vector3 d, AimTarget t, out float along)
        {
            var oc = t.Pos - o;
            along = Vector3.Dot(oc, d);
            if (along < 0.5f) return false;
            return ((o + d * along) - t.Pos).sqrMagnitude <= t.Radius * t.Radius;
        }

        static AudioClip AimTone(int key, float freq, float len, float vol)
        {
            if (_aimClips.TryGetValue(key, out var c) && c != null) return c;
            const int rate = 44100;
            int n = (int)(rate * len);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * (key >= 100 ? 28f : 38f)) * Mathf.Clamp01(i / 60f) * vol;
            }
            var clip = AudioClip.Create("modsmenu_aim_" + key, n, 1, rate, false);
            clip.SetData(d, 0);
            _aimClips[key] = clip;
            return clip;
        }

        static void AimPlay(bool hit)
        {
            if (!Cfg.AimSound.Value) return;
            try
            {
                if (_aimSrc == null)
                {
                    var go = new GameObject("ModsMenu_AimSfx");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _aimSrc = go.AddComponent<AudioSource>(); _aimSrc.playOnAwake = false; _aimSrc.spatialBlend = 0f;
                }
                AudioClip clip = hit ? AimTone(Mathf.Min(_aimCombo, 10), 900f + Mathf.Min(_aimCombo, 10) * 55f, 0.09f, 0.55f) : AimTone(100, 170f, 0.12f, 0.5f);
                _aimSrc.PlayOneShot(clip, 0.6f);
            }
            catch { }
        }

        static void OnAimHit(AimTarget t, float now)
        {
            float react = now - t.Spawn;
            float mult = 1f + Mathf.Min(2, _aimCombo / 5);
            int pts;
            if (_aimMode == 5) pts = Mathf.Max(50, Mathf.RoundToInt(1000f - react * 1000f));
            else
            {
                float baseMult = _aimMode == 2 ? 1.3f : _aimMode == 4 ? 1.2f : 1f;
                pts = Mathf.RoundToInt((100 + Mathf.Max(0f, 100f - react * 60f)) * baseMult);
            }
            pts = Mathf.RoundToInt(pts * mult);
            _aimScore += pts; _aimHits++; _aimReactSum += react; _aimReactBest = Mathf.Min(_aimReactBest, react);
            _aimCombo++; _aimBestCombo = Mathf.Max(_aimBestCombo, _aimCombo);
            AimPlay(true);

            _aimFloats.Add(new AimFloat { World = t.Pos, Text = "+" + pts + (mult > 1f ? "  x" + mult.ToString("0") : ""), T0 = now, C = mult > 1f ? new Color(1f, 0.85f, 0.25f) : Color.white });
            if (t.Go != null) _aimPops.Add(new AimPop { Go = t.Go, T0 = now, Scale = t.Go.transform.localScale });
            if (t.Mat != null) { try { UnityEngine.Object.Destroy(t.Mat); } catch { } }
            _aimTargets.Remove(t);

            if (_aimMode == 5) _aimNextSpawn = now + 1f + UnityEngine.Random.value * 2f;
            else SpawnAimTarget();
        }

        static void OnAimMiss(Vector3 o, Vector3 d)
        {
            _aimCombo = 0;
            AimPlay(false);
            var cam = Cam();
            if (cam == null || _aimTargets.Count == 0) return;
            AimTarget near = null; float bestAng = 12f;
            foreach (var t in _aimTargets)
            {
                float a = Vector3.Angle(d, (t.Pos - o).normalized);
                if (a < bestAng) { bestAng = a; near = t; }
            }
            if (near == null) return;
            var local = cam.transform.InverseTransformDirection((near.Pos - o).normalized);
            _aimBiasYaw += Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;       // + means the target was to the right of your crosshair
            _aimBiasPitch += Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;     // + means the target was above it
            _aimBiasN++;
        }

        // ------------------------------------------------------------ per-frame update

        static void UpdateAim()
        {
            if (!Cfg.AimOn.Value) { if (AimActive) StopAim(false); _aimRoutine = null; return; }
            var kb = Keyboard.current;
            if (KeyHit(Cfg.KbAim.Value)) StartAim();

            float now = Time.unscaledTime;
            if (!AimActive)
            {
                if (_aimRoutine != null && _aimNextStepAt > 0f && now >= _aimNextStepAt)
                {
                    _aimNextStepAt = 0f;
                    var st = _aimRoutine[_aimRoutineIdx];
                    if (!BeginAimRun(st.Mode, st.Secs)) _aimRoutine = null;
                }
                AimPops(now);
                return;
            }

            if (now - _aimStart >= _aimLen) { StopAim(true); return; }
            if (OtherHumansPresent()) { StopAim(false); return; }
            float dt = Time.unscaledDeltaTime;
            MoveAimTargets(dt);
            AimPops(now);

            var cam = Cam();
            if (cam == null) return;
            var o = cam.transform.position; var d = cam.transform.forward;

            if (_aimMode == 1)
            {
                for (int i = _aimTargets.Count - 1; i >= 0; i--)
                    if (now - _aimTargets[i].Spawn > 1.7f)
                    {
                        var t = _aimTargets[i];
                        try { if (t.Go != null) UnityEngine.Object.Destroy(t.Go); if (t.Mat != null) UnityEngine.Object.Destroy(t.Mat); } catch { }
                        _aimTargets.RemoveAt(i);
                        _aimCombo = 0;
                        SpawnAimTarget();
                    }
            }
            else if (_aimMode == 5)
            {
                if (_aimNextSpawn > 0f && now >= _aimNextSpawn) { _aimNextSpawn = 0f; SpawnAimTarget(); }
                for (int i = _aimTargets.Count - 1; i >= 0; i--)
                    if (now - _aimTargets[i].Spawn > 3f)
                    {
                        var t = _aimTargets[i];
                        try { if (t.Go != null) UnityEngine.Object.Destroy(t.Go); if (t.Mat != null) UnityEngine.Object.Destroy(t.Mat); } catch { }
                        _aimTargets.RemoveAt(i);
                        _aimCombo = 0;
                        _aimNextSpawn = now + 1f + UnityEngine.Random.value * 2f;
                    }
            }

            if (_aimMode == 3)
            {
                // tracking: points for the time your crosshair stays on the target; no shooting needed
                bool on = false;
                foreach (var t in _aimTargets)
                {
                    bool h = AimRayHit(o, d, t, out _);
                    if (h) on = true;
                    if (h != t.On) { t.On = h; SetAimColor(t.Mat, h ? new Color(0.3f, 1f, 0.4f) : AimColor(3)); }
                }
                if (on) _aimOnTarget += dt;
                _aimScore = Mathf.RoundToInt(_aimOnTarget * 100f);
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
            {
                _aimShots++;
                AimTarget hitT = null; float bestAlong = 1e9f;
                foreach (var t in _aimTargets)
                    if (AimRayHit(o, d, t, out float along) && along < bestAlong) { bestAlong = along; hitT = t; }
                if (hitT != null) OnAimHit(hitT, now); else OnAimMiss(o, d);
            }
        }

        static void AimPops(float now)
        {
            for (int i = _aimPops.Count - 1; i >= 0; i--)
            {
                var p = _aimPops[i];
                float k = (now - p.T0) / 0.14f;
                if (k >= 1f || p.Go == null) { try { if (p.Go != null) UnityEngine.Object.Destroy(p.Go); } catch { } _aimPops.RemoveAt(i); continue; }
                try { p.Go.transform.localScale = p.Scale * (1f - k); } catch { }
            }
            _aimFloats.RemoveAll(f => now - f.T0 > 0.9f);
        }

        // ------------------------------------------------------------ results, tips, history

        static string AimHistPath() => Path.Combine(Paths.GameRootPath, "Mods", "Stats", "aim_history.csv");

        static void AimLoadHist()
        {
            if (_aimHistLoaded) return;
            _aimHistLoaded = true;
            try
            {
                var p = AimHistPath();
                if (!File.Exists(p)) return;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                foreach (var line in File.ReadAllLines(p).Skip(1))
                {
                    var f = line.Split(',');
                    if (f.Length < 8) continue;
                    var r = new AimRunRow { Date = f[0] };
                    int.TryParse(f[1], out r.Mode); float.TryParse(f[2], System.Globalization.NumberStyles.Float, ci, out r.Seconds);
                    int.TryParse(f[3], out r.Score); int.TryParse(f[4], out r.Hits); int.TryParse(f[5], out r.Shots);
                    float.TryParse(f[6], System.Globalization.NumberStyles.Float, ci, out r.Acc); float.TryParse(f[7], System.Globalization.NumberStyles.Float, ci, out r.ReactMs);
                    _aimHist.Add(r);
                }
            }
            catch { }
        }

        static void AimSaveRow(AimRunRow r)
        {
            try
            {
                var p = AimHistPath();
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                bool fresh = !File.Exists(p);
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                if (fresh) sb.AppendLine("Date,Mode,Seconds,Score,Hits,Shots,AccPercent,AvgReactionMs");
                sb.AppendLine(string.Join(",", new[] { r.Date, r.Mode.ToString(), r.Seconds.ToString("0", ci), r.Score.ToString(), r.Hits.ToString(), r.Shots.ToString(), r.Acc.ToString("0.0", ci), r.ReactMs.ToString("0", ci) }));
                File.AppendAllText(p, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static List<string> AimRecentLines(int n)
        {
            AimLoadHist();
            var res = new List<string>();
            foreach (var r in Enumerable.Reverse(_aimHist).Take(n))
            {
                string name = AimModes[Mathf.Clamp(r.Mode, 0, AimModes.Length - 1)];
                string extra = r.Mode == 3 ? "on target " + Mathf.RoundToInt(r.Seconds > 0 ? 100f * r.Score / 100f / r.Seconds : 0f) + "%"
                    : "acc " + Mathf.RoundToInt(r.Acc) + "%   reaction " + (r.ReactMs / 1000f).ToString("0.00") + " s";
                string when = r.Date.Length >= 16 ? r.Date.Substring(5) : r.Date;
                res.Add(when + "   " + name + "   score " + r.Score + "   " + extra);
            }
            return res;
        }

        static List<string> AimTips(int mode, float acc, float avgReact, float onTargetPct)
        {
            var tips = new List<string>();
            if (_aimBiasN >= 4)
            {
                float by = _aimBiasYaw / _aimBiasN, bp = _aimBiasPitch / _aimBiasN;
                var parts = new List<string>();
                if (Mathf.Abs(by) >= 1.2f) parts.Add(Mathf.Abs(by).ToString("0.0") + " degrees " + (by > 0 ? "left" : "right") + " of the target");
                if (Mathf.Abs(bp) >= 1.2f) parts.Add(Mathf.Abs(bp).ToString("0.0") + " degrees " + (bp > 0 ? "below" : "above") + " it");
                if (parts.Count > 0)
                    tips.Add("Your misses land " + string.Join(" and ", parts) + ". If you keep overshooting, lower your sensitivity a little; if you stop short, settle the crosshair before you click.");
            }
            if (mode != 3 && _aimShots >= 8 && acc < 45f) tips.Add("Accuracy was " + Mathf.RoundToInt(acc) + "%. Slow down: let the crosshair settle on the target, then click.");
            if ((mode == 0 || mode == 1) && _aimHits >= 5 && avgReact > 1.1f) tips.Add("About " + avgReact.ToString("0.0") + " s per target. Start moving to the next target before you shoot the current one.");
            if (mode == 3) tips.Add(onTargetPct >= 60f ? "On target " + Mathf.RoundToInt(onTargetPct) + "% of the time. Smooth tracking, now try a smaller target." : "On target " + Mathf.RoundToInt(onTargetPct) + "%. Keep the wrist smooth and aim at the middle instead of chasing the edge.");
            if (mode == 5 && _aimHits >= 4) tips.Add("Average " + Mathf.RoundToInt(avgReact * 1000f) + " ms, best " + Mathf.RoundToInt(_aimReactBest * 1000f) + " ms. Keep your crosshair near the middle of the screen so the flick is short.");
            if (mode == 4 && _aimHits >= 4) tips.Add("Keep your crosshair at head height while you move. That habit is what wins real fights.");
            if (_aimBestCombo >= 8) tips.Add("Best combo " + _aimBestCombo + ". Consistent.");
            if (tips.Count == 0) tips.Add("Solid run. Raise the difficulty (farther targets or smaller size) when this feels easy.");
            return tips.Take(3).ToList();
        }

        static void FinishAimRun()
        {
            float secs = _aimLen; int mode = _aimMode;
            float acc = _aimShots > 0 ? 100f * _aimHits / _aimShots : 0f;
            float avg = _aimHits > 0 ? _aimReactSum / _aimHits : 0f;
            float onPct = secs > 0f ? 100f * _aimOnTarget / secs : 0f;
            int perMin = Mathf.RoundToInt(_aimScore * 60f / secs);
            var best = Cfg.AimBests[mode];
            bool record = perMin > best.Value;
            if (record) { best.Value = perMin; Plugin.Instance.Config.Save(); }

            var row = new AimRunRow { Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Mode = mode, Score = _aimScore, Hits = _aimHits, Shots = _aimShots, Seconds = secs, Acc = acc, ReactMs = avg * 1000f };
            AimLoadHist(); _aimHist.Add(row); AimSaveRow(row);

            AimResult = AimModes[mode] + ": score " + _aimScore + (record ? " (new best!)" : "") + (mode == 3 ? "   on target " + Mathf.RoundToInt(onPct) + "%" : "   hits " + _aimHits + "   accuracy " + Mathf.RoundToInt(acc) + "%   avg reaction " + avg.ToString("0.00") + " s");

            if (_aimRoutine != null)
            {
                _aimRoutineLog.Add(AimModes[mode] + "   score " + _aimScore + (record ? "  NEW BEST" : "") + (mode == 3 ? "   on target " + Mathf.RoundToInt(onPct) + "%" : "   acc " + Mathf.RoundToInt(acc) + "%"));
                _aimRoutineTotal += _aimScore;
                _aimRoutineIdx++;
                if (_aimRoutineIdx < _aimRoutine.Count)
                {
                    _aimNextStepAt = Time.unscaledTime + 3f;
                    Toast("Warm-up: " + AimModes[mode] + " done. Next: " + AimModes[_aimRoutine[_aimRoutineIdx].Mode] + " in 3 s");
                    return;
                }
                _aimRoutine = null; _aimNextStepAt = 0f;
                _aimCardTitle = "WARM-UP COMPLETE"; _aimCardScore = _aimRoutineTotal + " points";
                _aimCardRecord = false;
                _aimCardLines.Clear(); _aimCardLines.AddRange(_aimRoutineLog);
                _aimCardLines.Add("Nice. You are warmed up, now go play.");
                ShowAimCard(14f);
                return;
            }

            _aimCardTitle = "AIM TRAINER   " + AimModes[mode].ToUpperInvariant(); _aimCardScore = _aimScore + " points"; _aimCardRecord = record;
            _aimCardLines.Clear();
            _aimCardLines.Add(mode == 3
                ? "On target " + Mathf.RoundToInt(onPct) + "% of " + Mathf.RoundToInt(secs) + " s     score per minute " + perMin
                : "Hits " + _aimHits + " of " + _aimShots + " shots     accuracy " + Mathf.RoundToInt(acc) + "%     avg reaction " + avg.ToString("0.00") + " s     best combo " + _aimBestCombo);
            foreach (var t in AimTips(mode, acc, avg, onPct)) _aimCardLines.Add("-  " + t);
            ShowAimCard(10f);
            Toast("Aim Trainer: " + AimResult);
        }

        static void ShowAimCard(float secs) { if (Cfg.AimCard.Value) { _aimCardTotal = secs; _aimCardUntil = Time.unscaledTime + secs; } }

        // ------------------------------------------------------------ drawing

        static void DrawAim()
        {
            float now = Time.unscaledTime;
            if (AimActive)
            {
                float left = Mathf.Max(0f, _aimLen - (now - _aimStart));
                var r = new Rect(Screen.width / 2f - 280f, 40f, 560f, 34f);
                G.Fill(r, new Color(0, 0, 0, 0.7f));
                G.Fill(new Rect(r.x, r.y, 3, r.height), G.Accent);
                string mid = _aimMode == 3
                    ? "on target " + Mathf.RoundToInt(100f * _aimOnTarget / Mathf.Max(0.01f, now - _aimStart)) + "%"
                    : "hits " + _aimHits + "   acc " + (_aimShots > 0 ? Mathf.RoundToInt(100f * _aimHits / _aimShots) : 0) + "%" + (_aimCombo >= 2 ? "   combo " + _aimCombo : "");
                G.Label(r, AimModes[_aimMode].ToUpperInvariant() + "   score " + _aimScore + "   " + mid + "   " + left.ToString("0") + " s", 14, G.Text, TextAnchor.MiddleCenter, true);

                var cam = Cam();
                if (cam != null)
                    foreach (var f in _aimFloats)
                    {
                        float age = now - f.T0;
                        var sp = cam.WorldToScreenPoint(f.World);
                        if (sp.z <= 0f) continue;
                        float a = Mathf.Clamp01(1f - age / 0.9f);
                        G.Label(new Rect(sp.x - 70f, Screen.height - sp.y - 40f * age - 14f, 140f, 28f), f.Text, 20, new Color(f.C.r, f.C.g, f.C.b, a), TextAnchor.MiddleCenter, true);
                    }
            }
            else if (_aimRoutine != null && _aimNextStepAt > 0f)
            {
                var r = new Rect(Screen.width / 2f - 220f, 40f, 440f, 34f);
                G.Fill(r, new Color(0, 0, 0, 0.7f));
                G.Label(r, "WARM-UP   next: " + AimModes[_aimRoutine[Mathf.Min(_aimRoutineIdx, _aimRoutine.Count - 1)].Mode] + " in " + Mathf.Max(0f, _aimNextStepAt - now).ToString("0") + " s", 14, G.Text, TextAnchor.MiddleCenter, true);
            }

            float cl = _aimCardUntil - now;
            if (cl > 0f && _aimCardLines.Count > 0)
            {
                float a = Mathf.Clamp01(cl / 0.6f) * Mathf.Clamp01((_aimCardTotal - cl) / 0.25f + 0.001f);
                float w = 560f, h = 118f + _aimCardLines.Count * 38f;
                var r = new Rect((Screen.width - w) / 2f, Screen.height * 0.16f, w, h);
                var accent = G.Accent;
                G.Fill(r, new Color(0.03f, 0.05f, 0.07f, 0.92f * a));
                G.Fill(new Rect(r.x, r.y, r.width, 3), new Color(accent.r, accent.g, accent.b, a));
                G.Label(new Rect(r.x, r.y + 8, r.width, 26), _aimCardTitle, 15, new Color(accent.r, accent.g, accent.b, a), TextAnchor.MiddleCenter, true);
                G.Label(new Rect(r.x, r.y + 34, r.width, 46), _aimCardScore, 36, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, true);
                if (_aimCardRecord) G.Label(new Rect(r.x, r.y + 80, r.width, 22), "NEW BEST", 13, new Color(1f, 0.82f, 0.2f, a), TextAnchor.MiddleCenter, true);
                float y = r.y + 108f;
                foreach (var line in _aimCardLines)
                {
                    G.Label(new Rect(r.x + 20, y, r.width - 40, 36), line, 12, new Color(0.88f, 0.9f, 0.92f, a), TextAnchor.UpperLeft, false, true);
                    y += 38f;
                }
            }
        }
    }
}
