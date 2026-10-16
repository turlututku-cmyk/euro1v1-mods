using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    // Named crosshair presets stored in the config file.
    public static class Profiles
    {
        public class P
        {
            public string Name;
            public int Style;
            public float Size, Thick, Gap, Alpha, R, G, B;
            public bool Outline, Dot, Rainbow;
        }

        public static readonly List<P> List = new List<P>();
        static bool _loaded;
        public static string Active = "";
        const int Max = 12;

        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        static float Pf(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            List.Clear();
            foreach (var rec in Cfg.ProfList.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var f = rec.Split('|');
                    if (f.Length < 12) continue;
                    List.Add(new P
                    {
                        Name = f[0], Style = int.Parse(f[1]), Size = Pf(f[2]), Thick = Pf(f[3]), Gap = Pf(f[4]), Alpha = Pf(f[5]),
                        R = Pf(f[6]), G = Pf(f[7]), B = Pf(f[8]), Outline = f[9] == "1", Dot = f[10] == "1", Rainbow = f[11] == "1"
                    });
                }
                catch { }
            }
            Active = Cfg.ProfActive.Value;
        }

        static void Persist()
        {
            Cfg.ProfList.Value = string.Join(";", List.Select(p => string.Join("|", new[]
            {
                p.Name, p.Style.ToString(), F(p.Size), F(p.Thick), F(p.Gap), F(p.Alpha), F(p.R), F(p.G), F(p.B),
                p.Outline ? "1" : "0", p.Dot ? "1" : "0", p.Rainbow ? "1" : "0"
            })));
            Cfg.ProfActive.Value = Active;
            Plugin.Instance.Config.Save();
        }

        static string Clean(string n)
        {
            n = (n ?? "").Replace("|", "").Replace(";", "").Trim();
            return n.Length > 18 ? n.Substring(0, 18) : n;
        }

        public static bool SaveCurrent(string name)
        {
            EnsureLoaded();
            name = Clean(name);
            if (name.Length == 0) { Features.Toast("Type a profile name first"); return false; }
            var p = new P
            {
                Name = name, Style = Cfg.CrossStyle.Value, Size = Cfg.CrossSize.Value, Thick = Cfg.CrossThick.Value, Gap = Cfg.CrossGap.Value,
                Alpha = Cfg.CrossAlpha.Value, R = Cfg.CrossR.Value, G = Cfg.CrossG.Value, B = Cfg.CrossB.Value,
                Outline = Cfg.CrossOutline.Value, Dot = Cfg.CrossDot.Value, Rainbow = Cfg.CrossRainbow.Value
            };
            int i = List.FindIndex(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) List[i] = p;
            else
            {
                if (List.Count >= Max) { Features.Toast("Profile limit reached (" + Max + ")"); return false; }
                List.Add(p);
            }
            Active = name;
            Persist();
            Features.Toast("Saved crosshair profile: " + name);
            return true;
        }

        public static void Load(P p)
        {
            Cfg.CrossStyle.Value = p.Style; Cfg.CrossSize.Value = p.Size; Cfg.CrossThick.Value = p.Thick; Cfg.CrossGap.Value = p.Gap;
            Cfg.CrossAlpha.Value = p.Alpha; Cfg.CrossR.Value = p.R; Cfg.CrossG.Value = p.G; Cfg.CrossB.Value = p.B;
            Cfg.CrossOutline.Value = p.Outline; Cfg.CrossDot.Value = p.Dot; Cfg.CrossRainbow.Value = p.Rainbow;
            Cfg.CrossOn.Value = true;
            Active = p.Name;
            Persist();
        }

        public static void Delete(P p)
        {
            List.Remove(p);
            if (Active == p.Name) Active = "";
            Persist();
        }

        public static void Next()
        {
            EnsureLoaded();
            if (List.Count == 0) { Features.Toast("No crosshair profiles saved yet"); return; }
            int i = List.FindIndex(x => x.Name == Active);
            var p = List[(i + 1) % List.Count];
            Load(p);
            Features.Toast("Crosshair: " + p.Name);
        }
    }

    public static partial class Features
    {
        static void UpdateProfileKeys()
        {
            if (!Cfg.ProfOn.Value) return;
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) Profiles.Next();
        }
    }
}
