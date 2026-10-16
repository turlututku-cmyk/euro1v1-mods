using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Euro3v3.ModsMenu
{
    // "RTX Mode": not ray tracing. Raises URP render quality and adds bloom, tonemapping and color grading.
    public static partial class Features
    {
        static readonly int[] MsaaValues = { 1, 2, 4, 8 };
        static bool _rtxApplied, _rtxErrLogged;
        static float _nextRtx;
        static int _oMsaa, _oCascades;
        static float _oScale, _oShadowDist, _oLod;
        static bool _oHdr, _oSoft;
        static AnisotropicFiltering _oAniso;
        static GameObject _rtxGo;
        static VolumeProfile _rtxProfile;
        static Bloom _bloom;
        static ColorAdjustments _ca;
        static Tonemapping _tm;

        static void UpdateRtx()
        {
            if (!Cfg.RtxOn.Value)
            {
                if (_rtxApplied) RtxRestore();
                return;
            }
            if (Time.unscaledTime < _nextRtx) return;
            _nextRtx = Time.unscaledTime + 0.5f;
            try { RtxApply(); }
            catch (Exception e)
            {
                if (!_rtxErrLogged) { _rtxErrLogged = true; Plugin.Instance.Log.LogError("RTX mode: " + e); }
            }
        }

        static void RtxApply()
        {
            var asset = UniversalRenderPipeline.asset;
            if (!_rtxApplied)
            {
                if (asset != null)
                {
                    _oMsaa = asset.msaaSampleCount; _oScale = asset.renderScale; _oHdr = asset.supportsHDR;
                    _oShadowDist = asset.shadowDistance; _oCascades = asset.shadowCascadeCount; _oSoft = asset.supportsSoftShadows;
                }
                _oAniso = QualitySettings.anisotropicFiltering;
                _oLod = QualitySettings.lodBias;
                Plugin.Instance.Log.LogInfo("RTX mode on (URP asset " + (asset != null ? asset.name : "none") + ")");
            }

            if (asset != null)
            {
                asset.msaaSampleCount = MsaaValues[Mathf.Clamp(Cfg.RtxMsaa.Value, 0, 3)];
                asset.renderScale = Cfg.RtxScale.Value;
                asset.supportsHDR = Cfg.RtxHdr.Value;
                if (Cfg.RtxShadows.Value)
                {
                    asset.shadowDistance = Cfg.RtxShadowDist.Value;
                    asset.shadowCascadeCount = 4;
                    asset.supportsSoftShadows = true;
                }
            }
            if (Cfg.RtxAniso.Value) QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.lodBias = Cfg.RtxLod.Value;

            EnsureRtxVolume();
            _rtxGo.layer = RtxLayer();
            _bloom.intensity.overrideState = true; _bloom.intensity.value = Cfg.RtxBloom.Value;
            _bloom.threshold.overrideState = true; _bloom.threshold.value = 0.9f;
            _bloom.scatter.overrideState = true; _bloom.scatter.value = 0.7f;
            _ca.saturation.overrideState = true; _ca.saturation.value = Cfg.RtxSat.Value;
            _ca.contrast.overrideState = true; _ca.contrast.value = Cfg.RtxContrast.Value;
            _ca.postExposure.overrideState = true; _ca.postExposure.value = Cfg.RtxExposure.Value;
            _tm.mode.overrideState = true; _tm.mode.value = Cfg.RtxTonemap.Value ? TonemappingMode.ACES : TonemappingMode.None;

            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data != null) data.renderPostProcessing = true;
            }
            _rtxApplied = true;
        }

        static int RtxLayer()
        {
            foreach (var cam in Camera.allCameras)
            {
                var d = cam.GetComponent<UniversalAdditionalCameraData>();
                if (d == null || d.renderType != CameraRenderType.Base) continue;
                int m = d.volumeLayerMask.value;
                for (int i = 0; i < 32; i++) if (((m >> i) & 1) == 1) return i;
            }
            return 0;
        }

        static void EnsureRtxVolume()
        {
            if (_rtxGo != null && _rtxProfile != null) return;
            _rtxGo = new GameObject("ModsMenu_RTXVolume");
            UnityEngine.Object.DontDestroyOnLoad(_rtxGo);
            var vol = _rtxGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1000f;
            vol.weight = 1f;
            _rtxProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = _rtxProfile;
            _bloom = _rtxProfile.Add<Bloom>(true);
            _ca = _rtxProfile.Add<ColorAdjustments>(true);
            _tm = _rtxProfile.Add<Tonemapping>(true);
        }

        static void RtxRestore()
        {
            try
            {
                var asset = UniversalRenderPipeline.asset;
                if (asset != null)
                {
                    asset.msaaSampleCount = _oMsaa; asset.renderScale = _oScale; asset.supportsHDR = _oHdr;
                    asset.shadowDistance = _oShadowDist; asset.shadowCascadeCount = _oCascades; asset.supportsSoftShadows = _oSoft;
                }
                QualitySettings.anisotropicFiltering = _oAniso;
                QualitySettings.lodBias = _oLod;
                if (_rtxGo != null) UnityEngine.Object.Destroy(_rtxGo);
                if (_rtxProfile != null) UnityEngine.Object.Destroy(_rtxProfile);
            }
            catch (Exception e) { Plugin.Instance.Log.LogError("RTX restore: " + e.Message); }
            _rtxGo = null; _rtxProfile = null; _bloom = null; _ca = null; _tm = null;
            _rtxApplied = false;
            Plugin.Instance.Log.LogInfo("RTX mode off");
        }
    }
}
