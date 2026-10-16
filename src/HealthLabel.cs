using TMPro;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Relabels the health bar's "Integrity" text as "Integrity (Health) 100" so it's obvious it is your health, with the number.
    public static partial class Features
    {
        static TMP_Text _hpLabel;
        static string _hpOrig = "Integrity";
        static float _nextHpLabel;

        static void UpdateHealthLabel()
        {
            if (Time.unscaledTime < _nextHpLabel) return;
            bool on = Cfg.BvOn.Value && Cfg.HpLabelOn.Value;
            _nextHpLabel = Time.unscaledTime + (on ? 0.15f : 0.6f);

            try
            {
                if (_hpLabel != null) { var probe = _hpLabel.text; }
            }
            catch { _hpLabel = null; }

            if (_hpLabel == null)
            {
                if (!on) return;
                try
                {
                    foreach (var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
                    {
                        if (t.gameObject.name != "IntegrityText") continue;
                        _hpLabel = t;
                        var cur = t.text ?? "";
                        if (!cur.StartsWith("Integrity (Health)")) _hpOrig = cur;
                        break;
                    }
                }
                catch { }
                if (_hpLabel == null) { _nextHpLabel = Time.unscaledTime + 1f; return; }
            }

            try
            {
                if (!on)
                {
                    if (_hpLabel.text != _hpOrig) _hpLabel.text = _hpOrig;
                    return;
                }
                var me = LocalPlayer();
                int hp = (me != null && me.health != null) ? Mathf.CeilToInt(me.health.health) : -1;
                string txt = hp >= 0 ? "Integrity (Health)  " + hp : "Integrity (Health)";
                if (_hpLabel.text != txt) _hpLabel.text = txt;
            }
            catch { _hpLabel = null; }
        }
    }
}
