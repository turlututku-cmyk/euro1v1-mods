using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    // Rebindable keys. A bind is stored in the config as text: "F2", "Shift+F7", "Alt+Digit1", "None".
    // Modifiers are matched exactly (Shift+F7 does not fire while only F7 is held, and F7 does not fire while Shift is held).
    public static partial class Features
    {
        class BindSpec { public bool Ctrl, Shift, Alt, Valid; public Key Key; }
        static readonly Dictionary<string, BindSpec> _bindCache = new Dictionary<string, BindSpec>();
        static ConfigEntry<string> _bindTarget;
        static string _bindId, _bindDefault;
        static float _bindIgnoreUntil;
        static int _bindEndFrame = -1, _bindEscFrame = -1;
        static List<Key> _bindKeys;

        public static bool BindListening => _bindTarget != null;
        public static bool BindEscapeJustUsed => _bindEscFrame == Time.frameCount;
        public static bool BindIsListening(string id) => _bindTarget != null && _bindId == id;

        static BindSpec ParseBind(string s)
        {
            if (s == null) s = "";
            if (_bindCache.TryGetValue(s, out var cached)) return cached;
            var b = new BindSpec();
            var parts = s.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (i < parts.Length - 1)
                {
                    if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) b.Ctrl = true;
                    else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) b.Shift = true;
                    else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) b.Alt = true;
                }
                else if (p.Length > 0 && !p.Equals("None", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<Key>(p, true, out var k) && k != Key.None) { b.Key = k; b.Valid = true; }
            }
            _bindCache[s] = b;
            return b;
        }

        static string KeyName(Key k)
        {
            string n = k.ToString();
            if (n.StartsWith("Digit") && n.Length == 6) return n.Substring(5);
            switch (n)
            {
                case "LeftBracket": return "[";
                case "RightBracket": return "]";
                case "Comma": return ",";
                case "Period": return ".";
                case "Backquote": return "`";
                case "Minus": return "-";
                case "Equals": return "=";
                case "Slash": return "/";
                case "Semicolon": return ";";
                case "Quote": return "'";
            }
            return n;
        }

        public static string BindLabel(string spec)
        {
            var b = ParseBind(spec);
            if (!b.Valid) return "unbound";
            return (b.Ctrl ? "Ctrl + " : "") + (b.Shift ? "Shift + " : "") + (b.Alt ? "Alt + " : "") + KeyName(b.Key);
        }

        // true on the frame the bound key goes down (with exactly the bound modifiers held)
        public static bool KeyHit(string spec)
        {
            if (BindListening || _bindEndFrame == Time.frameCount) return false;
            var kb = Keyboard.current;
            if (kb == null) return false;
            var b = ParseBind(spec);
            if (!b.Valid) return false;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            if (b.Ctrl != ctrl || b.Shift != shift || b.Alt != alt) return false;
            try { var c = kb[b.Key]; return c != null && c.wasPressedThisFrame; }
            catch { return false; }
        }

        public static void BindBegin(string id, ConfigEntry<string> e, string def)
        {
            _bindId = id; _bindTarget = e; _bindDefault = def;
            _bindIgnoreUntil = Time.unscaledTime + 0.2f;      // swallow the click that opened the capture
        }

        public static void BindCancel() { _bindTarget = null; _bindId = null; }

        static bool IsModifierKey(Key k)
            => k == Key.LeftShift || k == Key.RightShift || k == Key.LeftCtrl || k == Key.RightCtrl || k == Key.LeftAlt || k == Key.RightAlt
               || k == Key.LeftMeta || k == Key.RightMeta || k == Key.IMESelected || k == Key.None;

        // runs first thing every frame: while a bind box is waiting, the next key press becomes the new bind
        static void UpdateKeybindCapture()
        {
            if (_bindTarget == null) return;
            var kb = Keyboard.current;
            if (kb == null || Time.unscaledTime < _bindIgnoreUntil) return;
            try
            {
                if (kb.escapeKey.wasPressedThisFrame) { _bindEscFrame = Time.frameCount; BindCancel(); return; }
                if (kb.deleteKey.wasPressedThisFrame) { _bindTarget.Value = "None"; Plugin.Instance.Config.Save(); _bindEndFrame = Time.frameCount; BindCancel(); Toast("Key unbound"); return; }
                if (_bindKeys == null)
                {
                    _bindKeys = new List<Key>();
                    foreach (Key k in Enum.GetValues(typeof(Key))) if (!IsModifierKey(k)) _bindKeys.Add(k);
                }
                foreach (var k in _bindKeys)
                {
                    if (!kb[k].wasPressedThisFrame) continue;
                    bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                    bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                    bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
                    string spec = (ctrl ? "Ctrl+" : "") + (shift ? "Shift+" : "") + (alt ? "Alt+" : "") + k;
                    _bindTarget.Value = spec;
                    Plugin.Instance.Config.Save();
                    _bindEndFrame = Time.frameCount;
                    Toast("Key set to " + BindLabel(spec));
                    BindCancel();
                    return;
                }
            }
            catch (Exception e) { Plugin.Instance.Log.LogWarning("Keybind capture: " + e.Message); BindCancel(); }
        }
    }
}
