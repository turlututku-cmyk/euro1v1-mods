using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Low Health Warning: pulsing red screen edges, an optional synthesized heartbeat and a small text when your own health is low.
    // Reads only your own health. Purely visual / audio.
    public static partial class Features
    {
        static float _lhNext, _lhFrac = 1f, _lhSev, _lhTestUntil, _lhBeatAt, _lhPhase;
        static bool _lhActive;
        static AudioSource _lhSrc;
        static AudioClip _lhClip;
        static int _lhHp;

        public static void LhTest() { _lhTestUntil = Time.unscaledTime + 4f; }

        static AudioClip MakeHeartbeat()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.55f);
            var d = new float[n];
            void Thump(float at, float f0, float amp)
            {
                int s0 = (int)(at * rate);
                for (int i = 0; i < (int)(rate * 0.2f) && s0 + i < n; i++)
                {
                    float t = i / (float)rate, f = f0 * (1f - 0.35f * Mathf.Min(1f, t / 0.12f));
                    d[s0 + i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 22f) * Mathf.Clamp01(i / 120f) * amp;
                }
            }
            Thump(0f, 62f, 0.9f);       // lub
            Thump(0.21f, 52f, 0.6f);    // dub
            var clip = AudioClip.Create("modsmenu_heartbeat", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static void UpdateLowHealth()
        {
            if (!Cfg.LhOn.Value) { _lhActive = false; return; }
            float now = Time.unscaledTime;
            if (now >= _lhNext)
            {
                _lhNext = now + 0.1f;
                float frac = 1f; bool alive = false; int hp = 0;
                try
                {
                    var me = LocalPlayer();
                    if (me != null && me.health != null)
                    {
                        float max = me.health.maxHealth > 0f ? me.health.maxHealth : 100f;
                        hp = Mathf.CeilToInt(me.health.health);
                        frac = Mathf.Clamp01(me.health.health / max);
                        alive = me.alive && me.health.health > 0f;
                    }
                }
                catch { }
                float thr = Mathf.Clamp(Cfg.LhThreshold.Value, 5f, 90f) / 100f;
                _lhHp = hp; _lhFrac = frac;
                if (alive && frac <= thr) { _lhActive = true; _lhSev = Mathf.Clamp01(1f - frac / thr); }
                else if (now < _lhTestUntil) { _lhActive = true; _lhSev = 0.7f; }
                else _lhActive = false;
            }

            if (!_lhActive) return;
            float rate = 1.1f + 2.0f * _lhSev;                 // beats per second
            _lhPhase += Time.unscaledDeltaTime * rate;
            if (Cfg.LhSound.Value && now >= _lhBeatAt)
            {
                _lhBeatAt = now + 1f / rate;
                try
                {
                    if (_lhSrc == null)
                    {
                        var go = new GameObject("ModsMenu_LowHealth");
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        _lhSrc = go.AddComponent<AudioSource>(); _lhSrc.playOnAwake = false; _lhSrc.spatialBlend = 0f;
                    }
                    if (_lhClip == null) _lhClip = MakeHeartbeat();
                    _lhSrc.PlayOneShot(_lhClip, Cfg.LhVol.Value * (0.5f + 0.5f * _lhSev));
                }
                catch { }
            }
        }

        static void DrawLowHealth()
        {
            if (!Cfg.LhOn.Value || !_lhActive) return;
            float W = Screen.width, H = Screen.height;
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_lhPhase * 2f * Mathf.PI)), 3f);   // sharp pulse once per beat
            float pulse = Cfg.LhPulse.Value ? 0.45f + 0.55f * beat : 1f;
            float a = Cfg.LhStrength.Value * (0.35f + 0.65f * _lhSev) * pulse;

            const int steps = 26;
            float depth = Mathf.Min(W, H) * (0.22f + 0.16f * _lhSev), step = depth / steps;
            for (int i = 0; i < steps; i++)
            {
                float f = 1f - i / (float)steps;
                var c = new Color(0.85f, 0.05f, 0.05f, a * f * f);
                G.Fill(new Rect(i * step, 0, step, H), c);
                G.Fill(new Rect(W - (i + 1) * step, 0, step, H), c);
                G.Fill(new Rect(0, i * step, W, step), c);
                G.Fill(new Rect(0, H - (i + 1) * step, W, step), c);
            }

            if (Cfg.LhText.Value)
            {
                var col = new Color(1f, 0.3f + 0.3f * beat, 0.3f + 0.3f * beat, 0.55f + 0.45f * pulse);
                G.Label(new Rect(W / 2f - 200f, H * 0.74f, 400f, 36f), "LOW HEALTH" + (_lhHp > 0 ? "   " + _lhHp : ""), 24, col, TextAnchor.MiddleCenter, true);
            }
        }
    }
}
