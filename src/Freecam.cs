using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Euro3v3.ModsMenu
{
    // Team-only orbit camera ("fair freecam").
    //  - Only orbits teammates (same team number as you); it can never leave your team.
    //  - Stays within a short leash of the teammate and cannot pass through walls (sphere-cast pull-in).
    //  - Only available when you are dead (spectating) or when no other human players are present (bots / solo / practice).
    //  - Purely local: no network messages, no markers, no enemy info beyond what the normal view would show.
    public static partial class Features
    {
        public static bool FreeActive;
        public static string FreeTargetName = "";

        const float FreeMaxDist = 6f, FreeMinDist = 1.2f;
        static CinemachineBrain _fcBrain;
        static Camera _fcWeaponCam;
        static PlayerObjectController _fcLocal;
        static readonly List<PlayerObjectController> _fcTargets = new List<PlayerObjectController>();
        static readonly List<Behaviour> _fcDisabled = new List<Behaviour>();
        static int _fcIdx;
        static float _fcYaw, _fcPitch, _fcDist = 3.5f, _nextFcScan;
        static Vector3 _fcPos;
        static bool _fcHavePos;

        // Refreshes the local player + the list of valid targets. Returns false with a reason if freecam isn't allowed.
        static bool FcRefresh(out string why)
        {
            why = "";
            _fcTargets.Clear();
            var all = UnityEngine.Object.FindObjectsOfType<PlayerObjectController>();
            _fcLocal = null;
            foreach (var p in all) if (p.isLocalPlayer) { _fcLocal = p; break; }
            if (_fcLocal == null) { why = "Freecam: you are not in a match"; return false; }

            if (_fcLocal.alive)
            {
                bool otherHumans = all.Any(p => p != _fcLocal && !p.IsBot);
                if (otherHumans) { why = "Freecam is off while you are alive in a match with other players"; return false; }
                _fcTargets.Add(_fcLocal); // offline / practice: orbit yourself
            }
            foreach (var p in all)
            {
                if (p == _fcLocal || p.IsBot && false) continue;
                if (p.team != _fcLocal.team || !p.alive) continue; // teammates only
                _fcTargets.Add(p);
            }
            if (_fcTargets.Count == 0) { why = "Freecam: no living teammates to follow"; return false; }
            return true;
        }

        static void StartFree()
        {
            var cam = Cam();
            if (cam == null) { Toast("Freecam: no camera"); return; }
            _fcBrain = cam.GetComponent<CinemachineBrain>();
            if (_fcBrain != null) _fcBrain.enabled = false;
            var wc = TheOnlyCamera.instance != null ? TheOnlyCamera.instance.weaponCamera : null;
            _fcWeaponCam = wc != null ? wc.GetComponent<Camera>() : null;
            if (_fcWeaponCam != null) _fcWeaponCam.enabled = false;

            _fcDisabled.Clear();
            if (_fcLocal != null)
            {
                if (_fcLocal.playerMovementController != null && _fcLocal.playerMovementController.enabled) { _fcLocal.playerMovementController.enabled = false; _fcDisabled.Add(_fcLocal.playerMovementController); }
                var look = _fcLocal.GetComponent<PlayerCameraController>();
                if (look != null && look.enabled) { look.enabled = false; _fcDisabled.Add(look); }
            }

            var e = cam.transform.eulerAngles;
            _fcYaw = e.y; _fcPitch = Mathf.Clamp(e.x > 180f ? e.x - 360f : e.x, -30f, 80f);
            _fcDist = Mathf.Clamp(_fcDist, FreeMinDist, FreeMaxDist);
            _fcHavePos = false;
            _fcIdx = Mathf.Clamp(_fcTargets.IndexOf(_fcLocal), 0, Mathf.Max(0, _fcTargets.Count - 1));
            FreeActive = true;
            Toast("Freecam on: Q/E switch teammate, scroll zoom, F6 exit");
        }

        static void StopFree()
        {
            try { if (_fcBrain != null) _fcBrain.enabled = true; } catch { }
            try { if (_fcWeaponCam != null) _fcWeaponCam.enabled = true; } catch { }
            foreach (var b in _fcDisabled) { try { b.enabled = true; } catch { } }
            _fcDisabled.Clear();
            _fcBrain = null; _fcWeaponCam = null;
            FreeActive = false;
            FreeTargetName = "";
        }

        static void UpdateFreecam()
        {
            if (!Cfg.FreeOn.Value)
            {
                if (FreeActive) StopFree();
                return;
            }
            var kb = Keyboard.current;
            if (KeyHit(Cfg.KbFreecam.Value))
            {
                if (FreeActive) { StopFree(); Toast("Freecam off"); }
                else if (FcRefresh(out var why)) StartFree();
                else Toast(why);
                return;
            }
            if (!FreeActive) return;

            if (Time.unscaledTime >= _nextFcScan)
            {
                _nextFcScan = Time.unscaledTime + 0.5f;
                var current = (_fcIdx >= 0 && _fcIdx < _fcTargets.Count) ? _fcTargets[_fcIdx] : null;
                if (!FcRefresh(out var why)) { Toast(why); StopFree(); return; }
                int i = current != null ? _fcTargets.IndexOf(current) : -1;
                _fcIdx = i >= 0 ? i : 0;
            }

            if (kb != null)
            {
                if (KeyHit(Cfg.KbFcNext.Value)) _fcIdx = (_fcIdx + 1) % _fcTargets.Count;
                if (KeyHit(Cfg.KbFcPrev.Value)) _fcIdx = (_fcIdx - 1 + _fcTargets.Count) % _fcTargets.Count;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var d = mouse.delta.ReadValue();
                float sens = Cfg.FreeSens.Value;
                _fcYaw += d.x * sens;
                _fcPitch = Mathf.Clamp(_fcPitch - d.y * sens, -30f, 80f);
                float sc = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(sc) > 0.01f) _fcDist = Mathf.Clamp(_fcDist - Mathf.Sign(sc) * 0.4f, FreeMinDist, FreeMaxDist);
            }
        }

        static void FreecamLateUpdate()
        {
            if (!FreeActive || _fcIdx < 0 || _fcIdx >= _fcTargets.Count) return;
            var target = _fcTargets[_fcIdx];
            var cam = Cam();
            if (target == null || cam == null) return;
            try
            {
                FreeTargetName = target.PlayerName + (target == _fcLocal ? " (you)" : "");
                var pivot = target.transform.position + Vector3.up * 1.5f;
                var rot = Quaternion.Euler(_fcPitch, _fcYaw, 0f);
                var back = rot * Vector3.back;

                // stay on the leash and never pass through walls: pull the camera in if something is in the way
                float dist = Mathf.Min(_fcDist, FreeMaxDist);
                RaycastHit hit;
                if (Physics.SphereCast(pivot, 0.25f, back, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
                    dist = Mathf.Max(FreeMinDist * 0.5f, hit.distance - 0.1f);

                var want = pivot + back * dist;
                _fcPos = _fcHavePos ? Vector3.Lerp(_fcPos, want, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime)) : want;
                _fcHavePos = true;

                // hard leash, whatever smoothing did
                var off = _fcPos - pivot;
                if (off.magnitude > FreeMaxDist) _fcPos = pivot + off.normalized * FreeMaxDist;

                cam.transform.SetPositionAndRotation(_fcPos, Quaternion.LookRotation(pivot - _fcPos, Vector3.up));
            }
            catch { }
        }

        static void DrawFreecamHud()
        {
            if (!FreeActive) return;
            var r = new Rect(Screen.width / 2f - 230, 70, 460, 30);
            G.Fill(r, new Color(0, 0, 0, 0.6f));
            G.Fill(new Rect(r.x, r.y, 3, r.height), G.Accent);
            G.Label(r, "TEAM CAM   " + FreeTargetName + "     Q/E switch   scroll zoom   F6 exit", 13, G.Text, TextAnchor.MiddleCenter, true);
        }
    }
}
