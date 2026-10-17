using System;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Euro3v3.ModsMenu
{
    [BepInPlugin("local.euro3v3.modsmenu", "Euro3v3 Mods Menu", "1.0.4")]
    public class Plugin : BasePlugin
    {
        public static Plugin Instance;

        public override void Load()
        {
            Instance = this;
            Cfg.Init();
            ClassInjector.RegisterTypeInIl2Cpp<MenuInjector>();
            AddComponent<MenuInjector>();
            try
            {
                var harmony = new HarmonyLib.Harmony("local.euro3v3.modsmenu");
                harmony.Patch(HarmonyLib.AccessTools.Method(typeof(Hitmarker), "Hit"), postfix: new HarmonyLib.HarmonyMethod(typeof(HitPatch), nameof(HitPatch.Postfix)));
                Log.LogInfo("Hitmarker hook installed");
            }
            catch (Exception e) { Log.LogWarning("Hitmarker hook failed (falling back to polling): " + e.Message); }
            try
            {
                var chatHarmony = new HarmonyLib.Harmony("local.euro3v3.modsmenu.chat");
                chatHarmony.Patch(HarmonyLib.AccessTools.Method(typeof(ServerMessageHandler), "DisplayChatMessage", new[] { typeof(string) }), prefix: new HarmonyLib.HarmonyMethod(typeof(ChatPatch), nameof(ChatPatch.Prefix)));
                chatHarmony.Patch(HarmonyLib.AccessTools.Method(typeof(ServerMessageHandler), "DisplayChatMessage", new[] { typeof(string), typeof(ChatColorId) }), prefix: new HarmonyLib.HarmonyMethod(typeof(ChatPatch2), nameof(ChatPatch2.Prefix)));
                Log.LogInfo("Chat filter hook installed");
            }
            catch (Exception e) { Log.LogWarning("Chat filter hook failed: " + e.Message); }
            Log.LogInfo("Mods menu plugin loaded");
        }
    }

    public class MenuInjector : MonoBehaviour
    {
        const string ButtonName = "ModsMenuButton";
        float _next;
        bool _dumped, _updErr;

        public MenuInjector(IntPtr ptr) : base(ptr) { }

        static string ModsDir => Path.Combine(Paths.GameRootPath, "Mods");

        void Update()
        {
            try { Features.Update(); } catch (Exception e) { if (!_updErr) { _updErr = true; Plugin.Instance.Log.LogError(e.ToString()); } }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            try { Tick(); }
            catch (Exception e) { Plugin.Instance.Log.LogError(e.ToString()); _next = Time.unscaledTime + 10f; }
        }

        static string Norm(string s)
        {
            if (s == null) return "";
            return s.Replace('\u0130', 'I').Replace('\u0131', 'i').Trim().ToLowerInvariant();
        }

        static readonly string[] Anchors = { "campaign", "edit profile" };

        void Tick()
        {
            var texts = UnityEngine.Object.FindObjectsOfType<TMP_Text>();
            foreach (var anchor in Anchors)
            {
                foreach (var t in texts)
                {
                    if (Norm(t.text) != anchor) continue;
                    try { AddTo(t, anchor); } catch (Exception e) { Plugin.Instance.Log.LogError(e.ToString()); }
                }
            }
        }

        void AddTo(TMP_Text label, string anchor)
        {
            var srcButton = label.GetComponentInParent<Button>();
            if (srcButton == null)
            {
                if (!_dumped) { _dumped = true; DumpParents(label.transform, "no uGUI Button above '" + anchor + "' text"); }
                return;
            }

            var parent = srcButton.transform.parent;
            if (parent.Find(ButtonName) != null) return;

            if (!_dumped) { _dumped = true; DumpParents(srcButton.transform, anchor + " button"); }

            var go = UnityEngine.Object.Instantiate(srcButton.gameObject, parent);
            go.name = ButtonName;

            // strip localization components so they don't reset the label
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var n = c.GetIl2CppType().Name;
                if (n.IndexOf("Locali", StringComparison.OrdinalIgnoreCase) >= 0)
                    UnityEngine.Object.Destroy(c);
            }

            var btn = go.GetComponent<Button>();
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener((UnityEngine.Events.UnityAction)(() => Features.PanelOpen = !Features.PanelOpen));

            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                if (Norm(t.text) != anchor) continue;
                var orig = t.text.Trim();
                bool upper = orig == orig.ToUpperInvariant() || orig == orig.ToUpper(new System.Globalization.CultureInfo("tr-TR"));
                t.text = upper ? "MODS" : "Mods";
            }

            go.transform.SetSiblingIndex(srcButton.transform.GetSiblingIndex() + 1);
            go.SetActive(true);
            Plugin.Instance.Log.LogInfo("Added Mods button after '" + anchor + "' under '" + parent.name + "'");
        }

        static void DumpParents(Transform t, string why)
        {
            var sb = new StringBuilder();
            sb.AppendLine(why);
            var root = t;
            for (int i = 0; i < 4 && root.parent != null; i++) root = root.parent;
            DumpTree(root, 0, sb);
            File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "ui-dump.txt"), sb.ToString());
        }

        static void DumpTree(Transform t, int depth, StringBuilder sb)
        {
            sb.Append(new string(' ', depth * 2)).Append(t.name).Append(" [");
            foreach (var c in t.GetComponents<Component>()) if (c != null) sb.Append(c.GetIl2CppType().Name).Append(' ');
            sb.AppendLine("]");
            if (depth > 8) return;
            for (int i = 0; i < t.childCount; i++) DumpTree(t.GetChild(i), depth + 1, sb);
        }

        void LateUpdate() { try { Features.LateUpdate(); } catch { } }

        void OnGUI()
        {
            try { Features.DrawHud(); Features.DrawClipUi(); Window.Draw(); Features.DrawToast(); }
            catch (Exception e) { if (!_guiErr) { _guiErr = true; Plugin.Instance.Log.LogError(e.ToString()); } }
        }
        bool _guiErr;
    }
}
