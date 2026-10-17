using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Chat Filter: hides spam, swearing and players you block from the in-game chat. Only changes what YOU see.
    // Hooks ServerMessageHandler.DisplayChatMessage (both overloads) with a Harmony prefix.
    public static partial class Features
    {
        public static int CfHidden;
        static bool _cfReenter;
        static readonly Dictionary<string, float> _cfRecent = new Dictionary<string, float>();
        static int _cfLogged;

        // short built-in list of common profanity stems (matched case-insensitively inside words)
        static readonly string[] CfBuiltIn = { "fuck", "shit", "bitch", "asshole", "cunt", "dickhead", "bastard", "nigg", "fagg", "retard", "slut", "whore" };

        static List<string> CfList(string csv)
            => (csv ?? "").Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).ToList();

        static string CfMask(string line, IEnumerable<string> words)
        {
            foreach (var w in words)
            {
                int i = 0;
                while ((i = line.IndexOf(w, i, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    line = line.Substring(0, i) + new string('*', w.Length) + line.Substring(i + w.Length);
                    i += w.Length;
                }
            }
            return line;
        }

        // Returns what to show: the (possibly masked) line, or null to hide the message completely.
        public static string CfProcess(string line)
        {
            if (!Cfg.CfOn.Value || string.IsNullOrEmpty(line)) return line;
            string low = line.ToLowerInvariant();

            // blocked players: the part before the first ':' is the sender
            var blocked = CfList(Cfg.CfBlock.Value);
            if (blocked.Count > 0)
            {
                int c = low.IndexOf(':');
                string sender = c > 0 ? low.Substring(0, c) : low;
                foreach (var b in blocked) if (sender.Contains(b)) return null;
            }

            // spam: the same message again within 6 seconds
            if (Cfg.CfSpam.Value)
            {
                float now = Time.unscaledTime;
                if (_cfRecent.TryGetValue(low, out var last) && now - last < 6f) { _cfRecent[low] = now; return null; }
                _cfRecent[low] = now;
                if (_cfRecent.Count > 60) foreach (var k in _cfRecent.Where(kv => now - kv.Value > 30f).Select(kv => kv.Key).ToList()) _cfRecent.Remove(k);
            }

            // words: your own list plus (optionally) the built-in one
            var words = CfList(Cfg.CfWords.Value);
            if (Cfg.CfSwear.Value) words.AddRange(CfBuiltIn);
            if (words.Count > 0)
            {
                bool hit = words.Any(w => low.Contains(w));
                if (hit)
                {
                    if (Cfg.CfHide.Value) return null;
                    return CfMask(line, words);
                }
            }
            return line;
        }

        public static string CfTest(string sample) { var r = CfProcess(sample); return r == null ? "(hidden)" : r; }

        // Returns true when the original method should still run.
        internal static bool CfPrefix(ServerMessageHandler inst, string line, ChatColorId? color)
        {
            if (_cfReenter || !Cfg.CfOn.Value) return true;
            try
            {
                var shown = CfProcess(line);
                if (shown == line) return true;
                CfHidden++;
                if (_cfLogged < 6) { _cfLogged++; Plugin.Instance.Log.LogInfo("Chat filter: " + (shown == null ? "hid" : "masked") + " a message (" + (line.Length > 60 ? line.Substring(0, 60) : line) + ")"); }
                if (shown == null) return false;
                _cfReenter = true;
                try { if (color.HasValue) inst.DisplayChatMessage(shown, color.Value); else inst.DisplayChatMessage(shown); }
                finally { _cfReenter = false; }
                return false;
            }
            catch { _cfReenter = false; return true; }
        }
    }

    public static class ChatPatch
    {
        public static bool Prefix(ServerMessageHandler __instance, string line) => Features.CfPrefix(__instance, line, null);
    }

    public static class ChatPatch2
    {
        public static bool Prefix(ServerMessageHandler __instance, string line, ChatColorId colorId) => Features.CfPrefix(__instance, line, colorId);
    }
}
