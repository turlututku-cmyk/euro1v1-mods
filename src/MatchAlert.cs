using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Match Found Alert: a chime and a flashing taskbar icon when the matchmaking queue pops while you are alt-tabbed,
    // plus a small "in queue" timer. Reads only the game's own matchmaking state.
    public static partial class Features
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }
        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        static string _maPrev = "";
        static float _maNext, _maQueueStart, _maLastAlert = -99f;
        static AudioSource _maSrc;
        static AudioClip _maClip;
        static IntPtr _maHwnd;

        static string MmState()
        {
            try
            {
                var mm = Euro3v3MatchmakingManager.Instance;
                return mm != null ? mm.State.ToString() : "";
            }
            catch { return ""; }
        }

        static AudioClip MakeChime()
        {
            const int rate = 44100;
            float[] notes = { 659.25f, 783.99f, 1046.5f, 1318.5f };
            int per = (int)(rate * 0.13f), n = per * notes.Length + (int)(rate * 0.5f);
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
                for (int i = 0; i < per + (int)(rate * 0.5f) && k * per + i < n; i++)
                {
                    float t = i / (float)rate, env = Mathf.Exp(-t * 6f) * Mathf.Clamp01(i / 120f);
                    d[k * per + i] += (Mathf.Sin(2f * Mathf.PI * notes[k] * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * notes[k] * 2f * t) * 0.15f) * env * 0.5f;
                }
            var clip = AudioClip.Create("modsmenu_matchfound", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        public static void MatchAlertNow()
        {
            Toast("MATCH FOUND!");
            if (Cfg.MaSound.Value)
            {
                try
                {
                    if (_maSrc == null)
                    {
                        var go = new GameObject("ModsMenu_MatchAlert");
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        _maSrc = go.AddComponent<AudioSource>(); _maSrc.playOnAwake = false; _maSrc.spatialBlend = 0f;
                    }
                    if (_maClip == null) _maClip = MakeChime();
                    _maSrc.PlayOneShot(_maClip, Cfg.MaVol.Value);
                }
                catch { }
            }
            if (Cfg.MaFlash.Value)
            {
                try
                {
                    if (_maHwnd == IntPtr.Zero) _maHwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    if (_maHwnd != IntPtr.Zero)
                    {
                        var fi = new FLASHWINFO { cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)), hwnd = _maHwnd, dwFlags = 3 | 12, uCount = 0, dwTimeout = 0 };   // all, until the window is in front
                        FlashWindowEx(ref fi);
                    }
                }
                catch { }
            }
        }

        static void UpdateMatchAlert()
        {
            if (!Cfg.MaOn.Value) { _maPrev = ""; return; }
            float now = Time.unscaledTime;
            if (now < _maNext) return;
            _maNext = now + 0.3f;
            string s = MmState();
            if (s == _maPrev) return;
            try { Plugin.Instance.Log.LogInfo("Matchmaking: " + (_maPrev == "" ? "(none)" : _maPrev) + " -> " + (s == "" ? "(none)" : s)); } catch { }
            if (s == "Queued") _maQueueStart = now;
            bool popped = (_maPrev == "Queued" && s != "Queued" && s != "Idle" && s != "Cancelled" && s != "Failed" && s != "Banned" && s != "")
                          || (s == "MatchFound" && _maPrev != "MatchFound");
            _maPrev = s;
            if (popped && now - _maLastAlert > 15f) { _maLastAlert = now; MatchAlertNow(); }
        }

        static void DrawQueueTimer()
        {
            if (!Cfg.MaOn.Value || !Cfg.MaTimer.Value || _maPrev != "Queued") return;
            int sec = Mathf.FloorToInt(Time.unscaledTime - _maQueueStart);
            string txt = "IN QUEUE   " + (sec / 60) + ":" + (sec % 60).ToString("00");
            var r = new Rect(Screen.width / 2f - 110f, 70f, 220f, 30f);
            G.Fill(r, new Color(0, 0, 0, 0.6f));
            G.Fill(new Rect(r.x, r.yMax - 3, r.width, 3), new Color(0.35f, 0.75f, 1f));
            G.Label(r, txt, 15, G.Text, TextAnchor.MiddleCenter, true);
        }
    }
}
