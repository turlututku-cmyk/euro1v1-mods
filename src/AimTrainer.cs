using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    // Practice-only aim trainer: glowing targets appear in front of you in the real map; shoot them for points.
    // Gridshot keeps 3 targets up until you hit them; Flick shows one target at a time for a short moment.
    // Uses its own ray from your camera, so it never touches the game's damage or networking. Never runs with other humans present.
    public static partial class Features
    {
        public static bool AimActive;
        class AimTarget { public GameObject Go; public float Spawn; public int Id; }
        static readonly List<AimTarget> _aimTargets = new List<AimTarget>();
        static float _aimStart;
        static int _aimScore, _aimHits, _aimShots;
        static float _aimReact;
        static Material _aimMat;
        public static string AimResult = "";
        public static readonly string[] AimModes = { "Gridshot", "Flick" };

        static bool OtherHumansPresent()
        {
            try
            {
                var me = LocalPlayer();
                foreach (var p in UnityEngine.Object.FindObjectsOfType<PlayerObjectController>())
                    if (p != me && !p.IsBot) return true;
            }
            catch { }
            return false;
        }

        public static void StartAim()
        {
            if (AimActive) { StopAim(false); return; }
            var cam = Cam();
            if (LocalPlayer() == null || cam == null) { Toast("Aim Trainer needs a map: start a practice match first (Play > VS Bots)"); return; }
            if (OtherHumansPresent()) { Toast("Aim Trainer is for practice only, not with other players"); return; }
            if (_aimMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
                if (sh != null) _aimMat = new Material(sh);
            }
            ClearAimTargets();
            _aimScore = 0; _aimHits = 0; _aimShots = 0; _aimReact = 0f; _aimStart = Time.unscaledTime;
            AimActive = true; AimResult = "";
            int count = Cfg.AimMode.Value == 0 ? 3 : 1;
            for (int i = 0; i < count; i++) SpawnAimTarget();
            Toast("Aim Trainer: shoot the targets! F2 stops");
        }

        public static void StopAim(bool finished)
        {
            if (!AimActive) return;
            AimActive = false;
            ClearAimTargets();
            if (!finished) { AimResult = ""; Toast("Aim Trainer stopped"); return; }
            float acc = _aimShots > 0 ? 100f * _aimHits / _aimShots : 0f;
            float avg = _aimHits > 0 ? _aimReact / _aimHits : 0f;
            var best = Cfg.AimMode.Value == 0 ? Cfg.AimBest0 : Cfg.AimBest1;
            bool record = _aimScore > best.Value;
            if (record) { best.Value = _aimScore; Plugin.Instance.Config.Save(); }
            AimResult = "Score " + _aimScore + (record ? " (new best!)" : "") + "   hits " + _aimHits + "   accuracy " + acc.ToString("0") + "%   avg reaction " + avg.ToString("0.00") + " s";
            Toast("Aim Trainer: " + AimResult);
        }

        static void ClearAimTargets()
        {
            foreach (var t in _aimTargets) { try { if (t.Go != null) UnityEngine.Object.Destroy(t.Go); } catch { } }
            _aimTargets.Clear();
        }

        static void SpawnAimTarget()
        {
            var cam = Cam();
            if (cam == null) return;
            var origin = cam.transform.position;
            float yaw = cam.transform.eulerAngles.y + (UnityEngine.Random.value - 0.5f) * 70f;
            float pitch = (UnityEngine.Random.value - 0.5f) * 24f;
            var dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            float dist = 11f + UnityEngine.Random.value * 9f;
            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, dist, ~0, QueryTriggerInteraction.Ignore)) dist = Mathf.Max(4f, hit.distance - 1.2f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "AimTarget";
            go.layer = 2; // Ignore Raycast: the game's own shots pass through, only our ray sees it
            go.transform.position = origin + dir * dist;
            go.transform.localScale = Vector3.one * Mathf.Max(0.2f, Cfg.AimSize.Value);
            var r = go.GetComponent<Renderer>();
            if (r != null && _aimMat != null)
            {
                var m = new Material(_aimMat);
                var col = Cfg.AimMode.Value == 0 ? new Color(0.2f, 0.95f, 1f) : new Color(1f, 0.6f, 0.15f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                if (m.HasProperty("_Color")) m.SetColor("_Color", col);
                r.sharedMaterial = m;
            }
            _aimTargets.Add(new AimTarget { Go = go, Spawn = Time.unscaledTime, Id = go.GetInstanceID() });
        }

        static void UpdateAim()
        {
            if (!Cfg.AimOn.Value) { if (AimActive) StopAim(false); return; }
            var kb = Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame) StartAim();
            if (!AimActive) return;

            float now = Time.unscaledTime;
            if (now - _aimStart >= Cfg.AimTime.Value) { StopAim(true); return; }
            if (OtherHumansPresent()) { StopAim(false); return; }

            if (Cfg.AimMode.Value == 1)
            {
                for (int i = _aimTargets.Count - 1; i >= 0; i--)
                    if (now - _aimTargets[i].Spawn > 1.7f)
                    {
                        try { UnityEngine.Object.Destroy(_aimTargets[i].Go); } catch { }
                        _aimTargets.RemoveAt(i);
                        SpawnAimTarget();
                    }
            }

            var mouse = Mouse.current; var cam = Cam();
            if (mouse != null && cam != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
            {
                _aimShots++;
                RaycastHit hit;
                if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 150f, ~0, QueryTriggerInteraction.Collide))
                {
                    int id = hit.collider.gameObject.GetInstanceID();
                    var t = _aimTargets.FirstOrDefault(x => x.Id == id);
                    if (t != null)
                    {
                        float react = now - t.Spawn;
                        _aimScore += 100 + Mathf.RoundToInt(Mathf.Max(0f, 100f - react * 60f));
                        _aimHits++; _aimReact += react;
                        try { UnityEngine.Object.Destroy(t.Go); } catch { }
                        _aimTargets.Remove(t);
                        SpawnAimTarget();
                    }
                }
            }
        }

        static void DrawAim()
        {
            if (!AimActive) return;
            float left = Mathf.Max(0f, Cfg.AimTime.Value - (Time.unscaledTime - _aimStart));
            float acc = _aimShots > 0 ? 100f * _aimHits / _aimShots : 0f;
            var r = new Rect(Screen.width / 2f - 230f, 40f, 460f, 34f);
            G.Fill(r, new Color(0, 0, 0, 0.7f));
            G.Fill(new Rect(r.x, r.y, 3, r.height), G.Accent);
            G.Label(r, "AIM TRAINER   score " + _aimScore + "   hits " + _aimHits + "   acc " + acc.ToString("0") + "%   " + left.ToString("0") + " s", 14, G.Text, TextAnchor.MiddleCenter, true);
        }
    }
}
