using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Euro3v3.ModsMenu
{
    public static partial class Features
    {
        public static bool DmgActive => Cfg.DmgOn.Value || (Cfg.ClOn.Value && (Cfg.ClDealt.Value || Cfg.ClHeal.Value));

        // The camera the game actually renders the world with (Camera.main is null in some menus).
        public static Camera Cam()
        {
            var c = Camera.main;
            if (c != null) return c;
            try
            {
                var t = TheOnlyCamera.instance;
                if (t != null) { var cc = t.GetComponent<Camera>(); if (cc != null) return cc; }
            }
            catch { }
            Camera first = null;
            foreach (var cam in Camera.allCameras)
            {
                if (cam.name == "THEONLYCAMERA") return cam;
                if (first == null)
                {
                    var d = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (d != null && d.renderType == CameraRenderType.Base) first = cam;
                }
            }
            return first;
        }
    }
}
