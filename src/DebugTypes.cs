using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Diagnostics: write type names (one per line, "*text" = name contains text) into BepInEx/debug-types.trigger
    // and the plugin writes BepInEx/debug-types.txt listing their members. Used to learn the game's API.
    public static partial class Features
    {
        static float _nextTypesCheck;

        static void UpdateDebugTypes()
        {
            if (Time.unscaledTime < _nextTypesCheck) return;
            _nextTypesCheck = Time.unscaledTime + 1f;
            var trig = Path.Combine(Paths.BepInExRootPath, "debug-types.trigger");
            if (!File.Exists(trig)) return;
            string[] wanted;
            try { wanted = File.ReadAllLines(trig).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray(); File.Delete(trig); }
            catch { return; }

            var sb = new StringBuilder();
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Assembly-CSharp");
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var w in wanted)
                {
                    if (w.StartsWith("~"))
                    {
                        var key2 = w.Substring(1); int n2 = 0;
                        sb.AppendLine("## members containing '" + key2 + "':");
                        var fl = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                        foreach (var t2 in types)
                        {
                            foreach (var mi in Safe(() => t2.GetMembers(fl)))
                            {
                                if (mi.Name.IndexOf(key2, StringComparison.OrdinalIgnoreCase) < 0 || mi.Name.StartsWith("UserCode_") || mi.Name.StartsWith("InvokeUserCode") || mi.Name.Contains("Mirror_")) continue;
                                sb.AppendLine("   " + t2.Name + "." + mi.Name + "  [" + mi.MemberType + "]");
                                if (++n2 > 400) break;
                            }
                            if (n2 > 400) break;
                        }
                        continue;
                    }
                    if (w.StartsWith("*"))
                    {
                        var key = w.Substring(1);
                        sb.AppendLine("## types containing '" + key + "':");
                        foreach (var t in types.Where(t => t.Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(t => t.Name))
                            sb.AppendLine("   " + t.FullName + " : " + (t.BaseType != null ? t.BaseType.Name : ""));
                        continue;
                    }
                    foreach (var t in types.Where(t => t.Name == w))
                    {
                        sb.AppendLine("== " + t.FullName + " : " + (t.BaseType != null ? t.BaseType.Name : ""));
                        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                        foreach (var f in Safe(() => t.GetFields(flags))) sb.AppendLine("  field " + f.FieldType.Name + " " + f.Name);
                        foreach (var p in Safe(() => t.GetProperties(flags))) sb.AppendLine("  prop  " + p.PropertyType.Name + " " + p.Name + (p.CanWrite ? " {get;set;}" : " {get;}"));
                        foreach (var m in Safe(() => t.GetMethods(flags)).Where(m => !m.IsSpecialName))
                        {
                            string sig;
                            try { sig = m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")"; }
                            catch { sig = "? " + m.Name; }
                            sb.AppendLine("  method " + sig);
                        }
                    }
                }
            }
            catch (Exception e) { sb.AppendLine("error: " + e); }
            File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "debug-types.txt"), sb.ToString());
        }

        static T[] Safe<T>(Func<T[]> f) { try { return f(); } catch { return new T[0]; } }
    }
}
