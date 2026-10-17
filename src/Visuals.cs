using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    // Purely cosmetic screen effects drawn with IMGUI. Nothing here touches gameplay, inventory or the network.
    public static partial class Features
    {
        const int MaxParticles = 300;
        static readonly float[] _px = new float[MaxParticles], _py = new float[MaxParticles],
            _pv = new float[MaxParticles], _ps = new float[MaxParticles], _pp = new float[MaxParticles];
        static bool _partInit;

        struct TrailPt { public Vector2 P; public float T; }
        static readonly List<TrailPt> _trail = new List<TrailPt>();
        static Vector2 _lastMouse;

        static bool InMenu => Cursor.lockState != CursorLockMode.Locked;

        public static bool Bv(BepInEx.Configuration.ConfigEntry<bool> e) => Cfg.BvOn.Value && e.Value;

        static Color Rainbow(float speed, float offset = 0f, float a = 1f)
        {
            var c = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * speed + offset, 1f), 0.85f, 1f);
            c.a = a;
            return c;
        }

        static void InitParticles()
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                _px[i] = Random.value * Screen.width;
                _py[i] = Random.value * Screen.height;
                _pv[i] = 0.5f + Random.value;
                _ps[i] = 1.5f + Random.value * 3f;
                _pp[i] = Random.value * 6.28f;
            }
            _partInit = true;
        }

        public static void UpdateVisuals()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateRainbow();
            UpdateProfileKeys();
            UpdatePause();
            UpdateMusic();
            UpdateRtx();
            UpdateHideGui();
            UpdateGraph();
            UpdateKillTracker();
            UpdateConfetti();
            UpdateSessionKeys();
            UpdateRounds();
            UpdateLowHealth();
            UpdateSadTrombone();
            UpdateScoreboardShot();
            UpdateChatMacros();
            UpdateCoach();
            UpdateMatchAlert();
            UpdatePractice();
            UpdateBuildTrainer();
            UpdateTeamPanel();
            UpdateCombatLog();
            UpdateAim();
            UpdateHealthLabel();
            UpdateFreecam();
            UpdateHitSounds();
            UpdateDamageNumbers();
            UpdateClipRecorder();
            UpdateViewer();
            UpdateOpenInMap();
            UpdatePostViewerFix();
            UpdateDebug();
            UpdateDebugTypes();

            if (Bv(Cfg.PartOn) && (!Cfg.PartMenusOnly.Value || InMenu))
            {
                if (!_partInit) InitParticles();
                int n = Mathf.Clamp((int)Cfg.PartDensity.Value, 0, MaxParticles);
                float sp = Cfg.PartSpeed.Value;
                int style = Cfg.PartStyle.Value;
                for (int i = 0; i < n; i++)
                {
                    if (style == 0) // snow: falls and sways
                    {
                        _py[i] += 60f * _pv[i] * sp * dt;
                        _px[i] += Mathf.Sin(Time.unscaledTime + _pp[i]) * 20f * sp * dt;
                        if (_py[i] > Screen.height + 10) { _py[i] = -10; _px[i] = Random.value * Screen.width; }
                    }
                    else if (style == 2) // embers: rise and sway
                    {
                        _py[i] -= 70f * _pv[i] * sp * dt;
                        _px[i] += Mathf.Sin(Time.unscaledTime * 1.5f + _pp[i]) * 30f * sp * dt;
                        if (_py[i] < -10) { _py[i] = Screen.height + 10; _px[i] = Random.value * Screen.width; }
                    }
                    else // stars: slow drift
                    {
                        _px[i] -= 8f * _pv[i] * sp * dt;
                        if (_px[i] < -5) { _px[i] = Screen.width + 5; _py[i] = Random.value * Screen.height; }
                    }
                }
            }

            if (Bv(Cfg.TrailOn) && !ViewerActive && (!Cfg.TrailMenusOnly.Value || InMenu))
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    var raw = mouse.position.ReadValue();
                    var p = new Vector2(raw.x, Screen.height - raw.y);
                    if ((p - _lastMouse).sqrMagnitude > 16f)
                    {
                        _trail.Add(new TrailPt { P = p, T = Time.unscaledTime });
                        _lastMouse = p;
                    }
                }
            }
            float life = Cfg.TrailLife.Value;
            _trail.RemoveAll(t => Time.unscaledTime - t.T > life);
        }

        static void DrawVisuals()
        {
            float W = Screen.width, H = Screen.height;
            DrawTintAndBars();
            DrawPauseHints();
            DrawFreecamHud();
            DrawInputDisplay();
            DrawDamageNumbers();
            DrawMatchStats();
            DrawFpsGraph();
            DrawNotes();
            DrawConfetti();
            DrawKillMessages();
            DrawAnnouncer();
            DrawSessionOverlay();
            DrawAim();
            DrawLowHealth();
            DrawDeathTaunt();
            DrawSadTrombone();
            DrawScoreboardCard();
            DrawCoach();
            DrawQueueTimer();
            DrawBuildTrainer();
            DrawTeamPanel();
            DrawCombatLog();
            DrawRecIndicator();

            // ---- vignette ----
            if (Bv(Cfg.VigOn))
            {
                var vc = Cfg.VigColor.Value <= 0 ? Color.black : G.Palette[Mathf.Clamp(Cfg.VigColor.Value - 1, 0, G.Palette.Length - 1)];
                const int steps = 28;
                float depth = Mathf.Min(W, H) * 0.30f, step = depth / steps;
                for (int i = 0; i < steps; i++)
                {
                    float f = 1f - i / (float)steps;
                    var c = new Color(vc.r, vc.g, vc.b, Cfg.VigStrength.Value * f * f);
                    G.Fill(new Rect(i * step, 0, step, H), c);
                    G.Fill(new Rect(W - (i + 1) * step, 0, step, H), c);
                    G.Fill(new Rect(0, i * step, W, step), c);
                    G.Fill(new Rect(0, H - (i + 1) * step, W, step), c);
                }
            }

            // ---- neon border ----
            if (Bv(Cfg.NeonOn))
            {
                float t = Mathf.Max(2f, Cfg.NeonThick.Value);
                var baseC = Cfg.NeonRainbow.Value ? Rainbow(0.2f) : G.Palette[Mathf.Clamp(Cfg.NeonColor.Value, 0, G.Palette.Length - 1)];
                float[] alphas = { 1f, 0.32f, 0.16f, 0.07f };
                for (int k = 0; k < alphas.Length; k++)
                {
                    var c = new Color(baseC.r, baseC.g, baseC.b, alphas[k]);
                    float o = k * t;
                    G.Fill(new Rect(o, o, W - 2 * o, t), c);
                    G.Fill(new Rect(o, H - o - t, W - 2 * o, t), c);
                    G.Fill(new Rect(o, o + t, t, H - 2 * o - 2 * t), c);
                    G.Fill(new Rect(W - o - t, o + t, t, H - 2 * o - 2 * t), c);
                }
            }

            // ---- particles ----
            if (Bv(Cfg.PartOn) && _partInit && (!Cfg.PartMenusOnly.Value || InMenu))
            {
                int n = Mathf.Clamp((int)Cfg.PartDensity.Value, 0, MaxParticles);
                int style = Cfg.PartStyle.Value;
                for (int i = 0; i < n; i++)
                {
                    Color c;
                    float s = _ps[i];
                    if (style == 0) c = new Color(1f, 1f, 1f, 0.85f);
                    else if (style == 2)
                    {
                        float life = Mathf.Clamp01(_py[i] / H);
                        c = new Color(1f, 0.35f + 0.4f * (1f - life), 0.1f, 0.35f + 0.65f * life);
                    }
                    else
                    {
                        float tw = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f + _pp[i]));
                        c = new Color(0.9f, 0.95f, 1f, tw);
                        s *= 0.7f;
                    }
                    G.Fill(new Rect(_px[i], _py[i], s, s), c);
                }
            }

            // ---- cursor trail ----
            if (Bv(Cfg.TrailOn) && !ViewerActive)
            {
                float life = Mathf.Max(0.1f, Cfg.TrailLife.Value);
                for (int i = 0; i < _trail.Count; i++)
                {
                    float age = (Time.unscaledTime - _trail[i].T) / life;
                    float a = 1f - age;
                    float size = 4f + 12f * a;
                    var c = Cfg.TrailRainbow.Value ? Rainbow(0.5f, i * 0.02f, a * 0.9f) : new Color(G.Accent.r, G.Accent.g, G.Accent.b, a * 0.9f);
                    G.Fill(new Rect(_trail[i].P.x - size / 2f, _trail[i].P.y - size / 2f, size, size), c);
                }
            }
        }
    }
}
