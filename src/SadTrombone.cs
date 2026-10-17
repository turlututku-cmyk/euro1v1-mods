using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Sad Trombone: miss N shots in a row and a sad trombone plays. Reads only your own shot counters.
    public static partial class Features
    {
        static float _stNext, _stPendingT;
        static int _stFired = -1, _stHit = -1, _stPending, _stStreak;
        static AudioSource _stSrc;
        static AudioClip _stClip;
        static float _stTextUntil;

        static AudioClip MakeTrombone()
        {
            const int rate = 44100;
            float[] freqs = { 233.08f, 220f, 207.65f, 196f };
            float[] lens = { 0.42f, 0.42f, 0.42f, 1.1f };
            int total = 0; foreach (var l in lens) total += (int)(l * rate);
            var d = new float[total + rate / 10];
            int pos = 0; double phase = 0;
            for (int k = 0; k < 4; k++)
            {
                int len = (int)(lens[k] * rate);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)rate;
                    float vib = k == 3 ? Mathf.Sin(2f * Mathf.PI * 5.5f * t) * Mathf.Clamp01(t / 0.4f) * 0.035f : 0f;
                    float f = freqs[k] * (1f + vib) * (k == 3 ? 1f - 0.05f * Mathf.Clamp01((t - 0.3f) / 0.8f) : 1f);
                    phase += f / rate;
                    float ph = (float)(phase * 2.0 * System.Math.PI), s = 0f;
                    for (int h = 1; h <= 6; h++) s += Mathf.Sin(h * ph) / h;
                    float env = Mathf.Min(1f, t / 0.035f) * (k < 3 ? Mathf.Clamp01((lens[k] - t) / 0.07f) * (0.85f + 0.15f * Mathf.Clamp01(1f - t / lens[k])) : Mathf.Clamp01((lens[k] - t) / 0.6f));
                    d[pos + i] = s * 0.34f * env;
                }
                pos += len;
            }
            var clip = AudioClip.Create("modsmenu_trombone", d.Length, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        public static void PlayTrombone()
        {
            try
            {
                if (_stSrc == null)
                {
                    var go = new GameObject("ModsMenu_Trombone");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _stSrc = go.AddComponent<AudioSource>(); _stSrc.playOnAwake = false; _stSrc.spatialBlend = 0f;
                }
                if (_stClip == null) _stClip = MakeTrombone();
                _stSrc.PlayOneShot(_stClip, Cfg.StVol.Value);
            }
            catch { }
            _stTextUntil = Time.unscaledTime + 2.2f;
        }

        static void UpdateSadTrombone()
        {
            if (!Cfg.StOn.Value) { _stFired = -1; return; }
            float now = Time.unscaledTime;
            if (now < _stNext) return;
            _stNext = now + 0.05f;
            try
            {
                var me = LocalPlayer();
                if (me == null) { _stFired = -1; _stStreak = 0; _stPending = 0; return; }
                int fired = me.stat_shotsFired, hit = me.stat_shotsHit;
                if (_stFired < 0 || fired < _stFired || hit < _stHit) { _stFired = fired; _stHit = hit; _stPending = 0; _stStreak = 0; return; }
                if (!me.alive) { _stStreak = 0; _stPending = 0; _stFired = fired; _stHit = hit; return; }
                if (hit > _stHit) { _stStreak = 0; _stPending = 0; }
                if (fired > _stFired) { _stPending += fired - _stFired; _stPendingT = now; }
                _stFired = fired; _stHit = hit;
                if (_stPending > 0 && now - _stPendingT > 0.4f)
                {
                    _stStreak += _stPending; _stPending = 0;
                    if (_stStreak >= Mathf.RoundToInt(Cfg.StMisses.Value)) { _stStreak = 0; PlayTrombone(); }
                }
            }
            catch { }
        }

        static void DrawSadTrombone()
        {
            if (!Cfg.StOn.Value || !Cfg.StText.Value) return;
            float left = _stTextUntil - Time.unscaledTime;
            if (left <= 0f) return;
            float a = Mathf.Clamp01(left / 0.6f);
            var r = new Rect(0, Screen.height * 0.66f, Screen.width, 50f);
            G.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), "wah wah wahhh...", 34, new Color(0, 0, 0, 0.8f * a), TextAnchor.MiddleCenter, true);
            G.Label(r, "wah wah wahhh...", 34, new Color(0.75f, 0.8f, 1f, a), TextAnchor.MiddleCenter, true);
        }
    }
}
