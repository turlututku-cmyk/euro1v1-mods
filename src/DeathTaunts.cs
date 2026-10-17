using System;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Death Taunts: a big joke line on YOUR screen when you die. Purely cosmetic, nothing is sent to anyone.
    public static partial class Features
    {
        static string _dtText = ""; static float _dtT = -99f; static int _dtStyleShown;
        static readonly string[][] DtLines =
        {
            new[] { "SKILL ISSUE", "Have you tried not dying?", "That's gonna leave a mark", "Tactical retreat. Too late.", "You blinked. They didn't.", "Lag. Definitely lag.", "Respawning... try harder this time", "Unlucky. (it was not luck)" },
            new[] { "Good try!", "They got lucky", "Shake it off!", "Next one is yours", "Nice effort!", "You are learning!", "Every pro died like that once", "Still a great round" },
            new[] { "Was that a speedrun?", "Bold strategy. It did not work.", "Your aim went on a coffee break", "Even the bots felt bad", "The floor looked comfy, huh", "Gravity called, it wants you back", "That was... a choice", "RIP your K/D" },
        };
        static readonly string[][] DtKillerLines =
        {
            new[] { "{k} sends their regards", "{k} says hi", "{k} got you that time", "Thanks for the free kill, {k}" },
            new[] { "{k} was just lucky", "Well played, {k}!", "{k} had a good moment", "Give {k} a high five" },
            new[] { "{k} did not even sweat", "{k} woke up and chose violence", "{k} has the walkthrough, apparently", "{k} says: too easy" },
        };

        public static void DtShow(string killer)
        {
            int st = Mathf.Clamp(Cfg.DtStyle.Value, 0, 2);
            string[] pool = (killer != null && Cfg.DtKiller.Value && UnityEngine.Random.value < 0.5f) ? DtKillerLines[st] : DtLines[st];
            string line = pool[UnityEngine.Random.Range(0, pool.Length)];
            _dtText = line.Replace("{k}", killer ?? "someone");
            _dtT = Time.unscaledTime; _dtStyleShown = st;
        }

        static void DtOnKill(KillEvent ev)
        {
            if (!Cfg.DtOn.Value || !ev.VictimIsLocal) return;
            DtShow(ev.Killer);
        }

        static void DrawDeathTaunt()
        {
            if (!Cfg.DtOn.Value) return;
            float age = Time.unscaledTime - _dtT;
            if (age < 0f || age > 3f) return;
            float pop = 1f + 0.5f * Mathf.Clamp01(1f - age * 4f);
            float a = Mathf.Clamp01((3f - age) / 0.8f);
            int size = Mathf.RoundToInt(Cfg.DtSize.Value * pop);
            Color c = _dtStyleShown == 0 ? new Color(1f, 0.55f, 0.2f) : _dtStyleShown == 1 ? new Color(0.45f, 1f, 0.7f) : new Color(1f, 0.35f, 0.5f);
            var r = new Rect(0, Screen.height * 0.22f, Screen.width, 90f);
            G.Label(new Rect(r.x + 2, r.y + 3, r.width, r.height), _dtText, size, new Color(0, 0, 0, 0.8f * a), TextAnchor.MiddleCenter, true);
            G.Label(r, _dtText, size, new Color(c.r, c.g, c.b, a), TextAnchor.MiddleCenter, true);
        }
    }
}
