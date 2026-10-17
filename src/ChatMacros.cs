using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Chat Macros: one rebindable key per preset chat message (default Alt + 1 to 6).
    public static partial class Features
    {
        static float _cmLast = -99f;

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
            for (int i = 0; i < 6; i++)
                if (KeyHit(Cfg.CmKeys[i].Value)) { CmSend(i); break; }
        }
    }
}
