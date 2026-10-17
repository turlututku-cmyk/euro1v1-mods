using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    // Chat Macros: a modifier + number key sends one of your own preset chat messages.
    public static partial class Features
    {
        static float _cmLast = -99f;
        public static readonly string[] CmMods = { "Alt", "Ctrl", "Shift" };

        static bool CmModDown(Keyboard kb)
        {
            switch (Cfg.CmMod.Value)
            {
                case 1: return kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                case 2: return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                default: return kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            }
        }

        public static bool SendChat(string msg)
        {
            // the same call the game's own chat box makes; the server attributes the message to the sender
            try
            {
                var ui = UnityEngine.Object.FindObjectOfType<SyncedUI>();
                if (ui == null) { Toast("Chat Macros: join a match or lobby first"); return false; }
                int max = ui.chatMaxChars > 0 ? ui.chatMaxChars : 100;
                if (msg.Length > max) msg = msg.Substring(0, max);
                ui.SendChatMessage(msg);
                return true;
            }
            catch (System.Exception e) { Toast("Chat Macros: send failed (" + e.Message + ")"); return false; }
        }

        public static void CmSend(int i)
        {
            if (i < 0 || i >= 6) return;
            string msg = (Cfg.CmMsgs[i].Value ?? "").Trim();
            if (msg.Length == 0) return;
            float now = Time.unscaledTime;
            if (now - _cmLast < Mathf.Max(0.5f, Cfg.CmCooldown.Value)) return;
            _cmLast = now;
            if (SendChat(msg)) Toast("Sent: " + msg);
        }

        static void UpdateChatMacros()
        {
            if (!Cfg.CmOn.Value || PanelOpen) return;
            var kb = Keyboard.current;
            if (kb == null || !CmModDown(kb)) return;
            if (kb.digit1Key.wasPressedThisFrame) CmSend(0);
            else if (kb.digit2Key.wasPressedThisFrame) CmSend(1);
            else if (kb.digit3Key.wasPressedThisFrame) CmSend(2);
            else if (kb.digit4Key.wasPressedThisFrame) CmSend(3);
            else if (kb.digit5Key.wasPressedThisFrame) CmSend(4);
            else if (kb.digit6Key.wasPressedThisFrame) CmSend(5);
        }
    }
}
