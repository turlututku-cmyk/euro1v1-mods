using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Euro3v3.ModsMenu
{
    // Diagnostics: create BepInEx/debug.trigger and the plugin writes debug-state.txt and debug-shot.png next to it.
    public static partial class Features
    {
        static float _nextTriggerCheck;
        static int _shotPending;
        static string _shotSuffix = "";
        static float _autoSnap1, _autoSnap2;

        // Capture the game state a few seconds after the clip viewer opens (the map and player exist then).
        static void ScheduleSnapshots() { _autoSnap1 = Time.unscaledTime + 6f; _autoSnap2 = 0f; }

        // Called when dummy mode becomes active: capture the state a few seconds later, and again later, without anyone triggering it.
    
        static void UpdateDebug()
        {
            if (_autoSnap1 > 0f && Time.unscaledTime >= _autoSnap1) { _autoSnap1 = 0f; _shotSuffix = "-dummy1"; try { DumpState(); } catch { } _shotPending = 3; }
            else if (_autoSnap2 > 0f && Time.unscaledTime >= _autoSnap2) { _autoSnap2 = 0f; _shotSuffix = "-dummy2"; try { DumpState(); } catch { } _shotPending = 3; }
            if (_shotPending > 0 && --_shotPending == 0)
                ScreenCapture.CaptureScreenshot(Path.Combine(Paths.BepInExRootPath, "debug-shot" + _shotSuffix + ".png"));

            if (Time.unscaledTime < _nextTriggerCheck) return;
            _nextTriggerCheck = Time.unscaledTime + 1f;
            var trig = Path.Combine(Paths.BepInExRootPath, "debug.trigger");
            if (!File.Exists(trig)) return;
            try { File.Delete(trig); } catch { }
            _shotSuffix = "";
            try { DumpState(); } catch (Exception e) { File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "debug-state.txt"), e.ToString()); }
            _shotPending = 3;
        }

        static void DumpState()
        {
            var sb = new StringBuilder();
            sb.AppendLine("time " + Time.unscaledTime + "  screen " + Screen.width + "x" + Screen.height + "  cursorLock " + Cursor.lockState);
            sb.AppendLine("BvOn " + Cfg.BvOn.Value + "  Vig " + Cfg.VigOn.Value + "  Neon " + Cfg.NeonOn.Value + "  Part " + Cfg.PartOn.Value +
                          "  Trail " + Cfg.TrailOn.Value + "  Theme " + Cfg.ThemeOn.Value + "  Gun " + Cfg.GunOn.Value + "  Text " + Cfg.TextOn.Value);
            sb.AppendLine("PartMenusOnly " + Cfg.PartMenusOnly.Value + "  InMenu " + InMenu + "  particlesInit " + _partInit);
            sb.AppendLine("music on " + Cfg.MusicOn.Value + " playing " + MusicPlaying + " now '" + NowPlaying + "' sourcePlaying " + (_music != null && _music.isPlaying) + " clipLen " + (_music != null && _music.clip != null ? _music.clip.length.ToString() : "-") + " volume " + (_music != null ? _music.volume.ToString() : "-") + " tracks " + Tracks.Count);
            sb.AppendLine("guns tinted " + _guns.Count + "  texts tracked " + _texts.Count);
            sb.AppendLine("RTX on " + Cfg.RtxOn.Value + "  applied " + _rtxApplied + "  volume " + (_rtxGo != null));

            var pipe = GraphicsSettings.currentRenderPipeline;
            sb.AppendLine("pipeline " + (pipe != null ? pipe.name + " / " + pipe.GetIl2CppType().Name : "builtin"));
            var asset = UniversalRenderPipeline.asset;
            if (asset != null)
                sb.AppendLine("URP asset msaa " + asset.msaaSampleCount + " scale " + asset.renderScale + " hdr " + asset.supportsHDR +
                              " shadowDist " + asset.shadowDistance + " cascades " + asset.shadowCascadeCount);

            try
            {
                var stack = VolumeManager.instance.stack;
                var b = stack.GetComponent<Bloom>();
                sb.AppendLine("stack bloom: " + (b == null ? "null" : "active " + b.active + " intensity " + b.intensity.value + " override " + b.intensity.overrideState));
                var c = stack.GetComponent<ColorAdjustments>();
                sb.AppendLine("stack colorAdj: " + (c == null ? "null" : "active " + c.active + " sat " + c.saturation.value + " contrast " + c.contrast.value));
                var t = stack.GetComponent<Tonemapping>();
                sb.AppendLine("stack tonemap: " + (t == null ? "null" : "active " + t.active + " mode " + t.mode.value));
            }
            catch (Exception e) { sb.AppendLine("stack error " + e.Message); }

            if (_rtxGo != null) sb.AppendLine("rtx volume layer " + _rtxGo.layer + " active " + _rtxGo.activeInHierarchy);

            foreach (var cam in Camera.allCameras)
            {
                var d = cam.GetComponent<UniversalAdditionalCameraData>();
                sb.AppendLine("camera '" + cam.name + "' enabled " + cam.enabled + " depth " + cam.depth + " target " + (cam.targetTexture != null ? "RT" : "screen") +
                              " hdr " + cam.allowHDR + " msaa " + cam.allowMSAA + " fov " + cam.fieldOfView + " mask " + cam.cullingMask + " parent " + (cam.transform.parent != null ? cam.transform.parent.name : "-") +
                              (d != null ? " | postFX " + d.renderPostProcessing + " type " + d.renderType + " volMask " + d.volumeLayerMask.value + " renderer " + d.scriptableRenderer?.GetType().Name : " | no URP data"));
            }

            // audio sources currently playing
            try
            {
                sb.AppendLine("--- playing AudioSources");
                foreach (var a in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                {
                    if (!a.isPlaying) continue;
                    string path = a.gameObject.name; var p = a.transform.parent;
                    for (int i = 0; i < 4 && p != null; i++, p = p.parent) path = p.name + "/" + path;
                    sb.AppendLine("  " + path + " clip=" + (a.clip != null ? a.clip.name : "-") + " loop=" + a.loop + " vol=" + a.volume + " mute=" + a.mute +
                                  " mixer=" + (a.outputAudioMixerGroup != null ? a.outputAudioMixerGroup.name : "-"));
                }
            }
            catch (Exception e) { sb.AppendLine("audio dump error " + e.Message); }

            // UI hierarchy (active canvases, depth <= 4)
            try
            {
                sb.AppendLine("--- UI canvases");
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
                {
                    if (!c.isRootCanvas || !c.gameObject.activeInHierarchy) continue;
                    sb.AppendLine("Canvas " + c.name + " mode " + c.renderMode);
                    DumpUi(c.transform, 1, sb);
                }
            }
            catch (Exception e) { sb.AppendLine("ui dump error " + e.Message); }

            // camera / movement related components (for the freecam mod)
            try
            {
                sb.AppendLine("--- camera & movement components");
                var seen = new Dictionary<string, int>();
                foreach (var mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                {
                    string n = mb.GetIl2CppType().Name;
                    string l = n.ToLowerInvariant();
                    if (!(l.Contains("camera") || l.Contains("freecam") || l.Contains("fly") || l.Contains("movement") || l.Contains("mouselook") || l.Contains("playercontroller") || l.Contains("freelook") || l.Contains("spectat"))) continue;
                    if (l.Contains("cinemachine") && !l.Contains("brain")) { }
                    seen.TryGetValue(n, out int c); seen[n] = c + 1;
                    if (c == 0)
                    {
                        string path = mb.gameObject.name; var p = mb.transform.parent;
                        for (int i = 0; i < 5 && p != null; i++, p = p.parent) path = p.name + "/" + path;
                        sb.AppendLine("  " + n + "  enabled=" + mb.enabled + " activeInHierarchy=" + mb.gameObject.activeInHierarchy + "  e.g. " + path);
                    }
                }
                foreach (var cam in Camera.allCameras)
                {
                    sb.Append("  components on camera '" + cam.name + "': ");
                    foreach (var c in cam.gameObject.GetComponents<Component>()) sb.Append(c.GetIl2CppType().Name + ", ");
                    sb.AppendLine();
                }
            }
            catch (Exception e) { sb.AppendLine("component dump error " + e.Message); }

            // players (for the dummy-mode work)
            try
            {
                sb.AppendLine("--- players");
                var cam0 = Cam();
                foreach (var p in UnityEngine.Object.FindObjectsOfType<PlayerObjectController>())
                {
                    try
                    {
                        var rs = p.GetComponentsInChildren<Renderer>(true);
                        int on = rs.Count(r => r.enabled), vis = rs.Count(r => r.isVisible);
                        string scr = "-";
                        if (cam0 != null) { var sp = cam0.WorldToScreenPoint(p.transform.position); scr = sp.x.ToString("0") + "," + sp.y.ToString("0") + " z=" + sp.z.ToString("0.0"); }
                        sb.AppendLine("  " + p.PlayerName + " local=" + p.isLocalPlayer + " bot=" + p.IsBot + " team=" + p.team + " alive=" + p.alive +
                                      " pos=" + p.transform.position + " active=" + p.gameObject.activeInHierarchy + " renderers=" + rs.Length + "/enabled " + on + "/visible " + vis +
                                      " screen=" + scr + " hp=" + (p.health != null ? p.health.health.ToString("0") : "-") +
                                      " visual=" + p.currentPlayerVisual.ToString() +
                                      " death=" + p.deathVisualActive + " spec=" + p.spectatingVisualActive + " tv=" + p.tvVisualActive + " cut=" + p.cutsceneVisualActive + " title=" + p.titleCardVisualActive);
                    }
                    catch (Exception e) { sb.AppendLine("  player error " + e.Message); }
                }
                if (cam0 != null) sb.AppendLine("  camera pos=" + cam0.transform.position + " forward=" + cam0.transform.forward);
            }
            catch (Exception e) { sb.AppendLine("player dump error " + e.Message); }

            // character model structure (for using the game's own player model in clips)
            try
            {
                sb.AppendLine("--- player model");
                var poc = LocalPlayer();
                if (poc != null)
                {
                    var pm = poc.playerMesh;
                    if (pm != null)
                    {
                        string path = pm.name; var tt = pm.transform.parent;
                        for (int i = 0; i < 8 && tt != null; i++, tt = tt.parent) path = tt.name + "/" + path;
                        sb.AppendLine("  playerMesh: " + path + " enabled=" + pm.enabled + " activeSelf=" + pm.gameObject.activeSelf + " verts=" + (pm.sharedMesh != null ? pm.sharedMesh.vertexCount : -1) +
                                      " bones=" + (pm.bones != null ? pm.bones.Length : -1) + " rootBone=" + (pm.rootBone != null ? pm.rootBone.name : "-") + " materials=" + pm.sharedMaterials.Length);
                    }
                    sb.AppendLine("  blueMat=" + (poc.blueMat != null ? poc.blueMat.name : "-") + " redMat=" + (poc.redMat != null ? poc.redMat.name : "-") + " notAssigned=" + (poc.notAssignedMat != null ? poc.notAssignedMat.name : "-"));
                    foreach (var smr in poc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        string par = smr.transform.parent != null ? smr.transform.parent.name : "-";
                        sb.AppendLine("  SMR " + smr.name + " parent=" + par + " enabled=" + smr.enabled + " active=" + smr.gameObject.activeInHierarchy + " verts=" + (smr.sharedMesh != null ? smr.sharedMesh.vertexCount : -1) + " bones=" + (smr.bones != null ? smr.bones.Length : -1) + " mats=" + string.Join(",", smr.sharedMaterials.Select(m => m != null ? m.name : "null")));
                    }
                    foreach (var an in poc.GetComponentsInChildren<Animator>(true))
                        sb.AppendLine("  Animator on " + an.gameObject.name + " enabled=" + an.enabled + " controller=" + (an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "-"));
                    sb.AppendLine("  outsideVisual count=" + (poc.outsideVisual != null ? poc.outsideVisual.Length : -1));
                    if (poc.outsideVisual != null)
                        foreach (var r in poc.outsideVisual) { try { sb.AppendLine("    outside: " + r.name + " type=" + r.GetIl2CppType().Name + " activeSelf=" + r.activeSelf); } catch { } }
                    sb.AppendLine("  insideVisual count=" + (poc.insideVisual != null ? poc.insideVisual.Length : -1));
                }
            }
            catch (Exception e) { sb.AppendLine("model dump error " + e.Message); }

            // build parts (walls / floors / stairs) and their prefabs
            try
            {
                sb.AppendLine("--- build parts");
                var live = BuildPart.activeParts;
                sb.AppendLine("  live parts: " + (live != null ? live.Count : -1));
                if (live != null)
                    for (int i = 0; i < live.Count && i < 6; i++)
                    {
                        var bp = live[i];
                        var rr = bp.GetComponentsInChildren<Renderer>(true);
                        sb.AppendLine("  live[" + i + "] " + bp.gameObject.name + " type=" + bp.type + " team=" + bp.ownerTeam + " pos=" + bp.transform.position + " rot=" + bp.transform.eulerAngles + " scale=" + bp.transform.lossyScale +
                                      " renderers=" + rr.Length + " " + string.Join(",", rr.Take(3).Select(r => r.name + "/" + (r.sharedMaterial != null ? r.sharedMaterial.name : "-"))));
                    }
                var nm = Mirror.NetworkManager.singleton;
                if (nm != null)
                    foreach (var pf in nm.spawnPrefabs)
                    {
                        try
                        {
                            var bp = pf.GetComponent<BuildPart>();
                            if (bp == null) continue;
                            var rr = pf.GetComponentsInChildren<Renderer>(true);
                            sb.AppendLine("  prefab " + pf.name + " type=" + bp.type + " scale=" + pf.transform.localScale + " renderers=" + rr.Length + " " + string.Join(",", rr.Take(4).Select(r => r.name + "/" + (r.sharedMaterial != null ? r.sharedMaterial.name : "-") + "/" + r.GetIl2CppType().Name)));
                        }
                        catch { }
                    }
                var bc = UnityEngine.Object.FindObjectOfType<BuildController>();
                if (bc != null)
                    sb.AppendLine("  BuildController previews: wall=" + (bc.wallPreviewPrefab != null ? bc.wallPreviewPrefab.name : "-") + " floor=" + (bc.floorPreviewPrefab != null ? bc.floorPreviewPrefab.name : "-") + " stairs=" + (bc.stairPreviewPrefab != null ? bc.stairPreviewPrefab.name : "-"));
            }
            catch (Exception e) { sb.AppendLine("parts dump error " + e.Message); }

            File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "debug-state" + _shotSuffix + ".txt"), sb.ToString());
        }

        static void DumpUi(Transform t, int depth, StringBuilder sb)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (!c.gameObject.activeInHierarchy) continue;
                var txt = c.GetComponent<TMPro.TMP_Text>();
                sb.Append(new string(' ', depth * 2)).Append(c.name);
                if (txt != null && !string.IsNullOrEmpty(txt.text)) sb.Append("  '").Append(txt.text.Replace((char)10, (char)32)).Append("'");
                sb.AppendLine();
                if (depth < 5) DumpUi(c, depth + 1, sb);
            }
        }
    }
}
