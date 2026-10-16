using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Euro3v3.ModsMenu
{
    // 3D replay viewer: plays a saved clip back inside the live map with stand-in avatars, a free camera, a timeline,
    // trim in/out, camera keyframes (camera path) and "save edit".
    //
    // Fairness rules (so it can't be used to scout a live match):
    //  - the clip's map must be the one that is loaded right now
    //  - alive in a live match with other human players: not allowed
    //  - dead in a live match: allowed, but only YOUR team's tracks are shown
    //  - offline / bots only, or the match is over (EndingState): everyone is shown
    public static partial class Features
    {
        class Actor { public GameObject Root, Body, Face; public ClipPlayer P; public Renderer[] Rends; }

        public static bool ViewerActive;
        static Clip _vc;
        static float _vt, _vSpeed = 1f, _vIn, _vOut, _nextVCheck;
        static bool _vPlaying, _vShowEnemies, _vUi, _vPath, _vFollow;
        static int _vFollowIdx;
        static readonly List<Actor> _actors = new List<Actor>();
        static CinemachineBrain _vBrain; static Camera _vWeaponCam;
        static readonly List<Behaviour> _vDisabled = new List<Behaviour>();
        static float _vYaw, _vPitch, _vDist = 4f, _vFlySpeed = 6f;
        static Vector3 _vPos;
        static Rect _vBar;
        static string _vNote = "";
        static float _closePauseUntil;

        public static string ViewerClipName => _vc != null ? _vc.Name : "";

        // ---------------------------------------------------------------- rules

        static bool MatchEnded()
        {
            try
            {
                var gm = GameModeManager.instance;
                if (gm == null || gm._state == null) return false;
                var o = new Il2CppSystem.Object(Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtr(gm._state));
                return o.GetType().Name == "EndingState";
            }
            catch { return false; }
        }

        static bool ViewerAllowed(Clip c, out string why, out bool showEnemies)
        {
            why = ""; showEnemies = true;
            var all = UnityEngine.Object.FindObjectsOfType<PlayerObjectController>();
            PlayerObjectController me = null;
            foreach (var p in all) if (p.isLocalPlayer) { me = p; break; }

            bool live = false;
            try
            {
                var gm = GameModeManager.instance;
                if (me != null && gm != null && gm._state != null) live = !MatchEnded();
            }
            catch { }
            bool otherHumans = me != null && all.Any(p => p != me && !p.IsBot);

            // menus, lobby, finished matches and bot/solo matches: everything may be shown
            if (!live || !otherHumans) return true;

            // live match against real players
            if (me.alive) { why = "Clips can't be viewed while you are alive in a live match"; return false; }
            showEnemies = false; // dead: your team only
            return true;
        }

        // ---- map blockout (built from the boxes stored in the clip) and menu hiding
        static GameObject _mapGo;
        static readonly List<Canvas> _hiddenCanvases = new List<Canvas>();

        static void BuildMap(Clip c, PlayerObjectController me)
        {
            DestroyMap();
            if (me != null) return; // a match scene is loaded: the real map is already on screen
            int n = c.Boxes.Length / 10;
            if (n == 0) return;
            var verts = new Vector3[n * 24];
            var tris = new int[n * 36];
            Vector3[] axisA = { Vector3.up, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right, Vector3.up };
            Vector3[] axisB = { Vector3.forward, Vector3.up, Vector3.right, Vector3.forward, Vector3.up, Vector3.right };
            Vector3[] nrm = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            // a x b must equal the face normal: +x:(y,z) -x:(z,y) +y:(z,x) -y:(x,z) +z:(x,y) -z:(y,x)
            for (int i = 0; i < n; i++)
            {
                int o = i * 10;
                var center = new Vector3(c.Boxes[o], c.Boxes[o + 1], c.Boxes[o + 2]);
                var half = new Vector3(c.Boxes[o + 3], c.Boxes[o + 4], c.Boxes[o + 5]) * 0.5f;
                var q = new Quaternion(c.Boxes[o + 6], c.Boxes[o + 7], c.Boxes[o + 8], c.Boxes[o + 9]);
                for (int f = 0; f < 6; f++)
                {
                    var nf = nrm[f]; var a = axisA[f]; var b = axisB[f];
                    Vector3 Local(float sa, float sb) => Vector3.Scale(nf + a * sa + b * sb, half);
                    int vi = i * 24 + f * 4;
                    verts[vi] = center + q * Local(-1f, -1f);
                    verts[vi + 1] = center + q * Local(1f, -1f);
                    verts[vi + 2] = center + q * Local(1f, 1f);
                    verts[vi + 3] = center + q * Local(-1f, 1f);
                    int ti = i * 36 + f * 6;
                    tris[ti] = vi; tris[ti + 1] = vi + 2; tris[ti + 2] = vi + 1;
                    tris[ti + 3] = vi; tris[ti + 4] = vi + 3; tris[ti + 5] = vi + 2;
                }
            }
            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Material mat = null;
            try { mat = me != null ? me.notAssignedMat : null; } catch { }
            if (mat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Sprites/Default");
                if (sh == null) { Plugin.Instance.Log.LogWarning("Clip viewer: no shader for the map blockout"); return; }
                mat = new Material(sh);
                var grey = new Color(0.55f, 0.58f, 0.62f, 1f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", grey);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", grey);
            }
            _mapGo = new GameObject("ClipMap");
            _mapGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            _mapGo.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ---- player-built parts (walls / floors / stairs), copied from the game's own part prefabs
        static GameObject _partsRoot;
        static readonly List<KeyValuePair<ClipPart, GameObject>> _partObjs = new List<KeyValuePair<ClipPart, GameObject>>();
        static readonly List<bool> _partShown = new List<bool>();

        static void BuildParts(Clip c)
        {
            DestroyParts();
            if (c.Parts.Count == 0) return;
            try
            {
                var prefabs = new Dictionary<int, GameObject>();
                var nm = Mirror.NetworkManager.singleton;
                if (nm != null)
                    foreach (var pf in nm.spawnPrefabs)
                    {
                        try { var bp = pf.GetComponent<BuildPart>(); if (bp != null && !prefabs.ContainsKey((int)bp.type)) prefabs[(int)bp.type] = pf; } catch { }
                    }
                if (prefabs.Count == 0) { Plugin.Instance.Log.LogWarning("Clip viewer: no build-part prefabs found"); return; }

                _partsRoot = new GameObject("ClipParts");
                var holder = new GameObject("ClipPartsHolder");
                holder.SetActive(false);   // clones stay inactive while we strip their scripts
                int made = 0;
                foreach (var part in c.Parts)
                {
                    if (!prefabs.TryGetValue(part.Type, out var prefab)) continue;
                    var go = UnityEngine.Object.Instantiate(prefab, holder.transform);
                    go.name = "Part";
                    foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) { try { mb.enabled = false; } catch { } }
                    foreach (var col in go.GetComponentsInChildren<Collider>(true)) { try { UnityEngine.Object.Destroy(col); } catch { } }
                    foreach (var ni in go.GetComponentsInChildren<Mirror.NetworkIdentity>(true)) { try { UnityEngine.Object.Destroy(ni); } catch { } }
                    // show the finished part, not the build-up animation
                    var bu = go.GetComponentInChildren<BuildPartVisualBuildUp>(true);
                    if (bu != null)
                    {
                        if (bu.finalVisual != null) bu.finalVisual.SetActive(true);
                        if (bu.animVisual != null) bu.animVisual.SetActive(false);
                    }
                    go.SetActive(false);
                    go.transform.SetParent(_partsRoot.transform, false);
                    go.transform.SetPositionAndRotation(part.Pos, part.Rot);
                    go.transform.localScale = part.Scale;
                    _partObjs.Add(new KeyValuePair<ClipPart, GameObject>(part, go));
                    _partShown.Add(false);
                    made++;
                }
                UnityEngine.Object.Destroy(holder);
                Plugin.Instance.Log.LogInfo("Clip viewer: built " + made + " of " + c.Parts.Count + " build parts");
            }
            catch (Exception e) { Plugin.Instance.Log.LogWarning("Clip viewer: could not build the parts: " + e.Message); }
        }

        static void UpdatePartVisibility()
        {
            for (int i = 0; i < _partObjs.Count; i++)
            {
                var part = _partObjs[i].Key;
                bool want = _vt >= part.T0 && (part.T1 < 0f || _vt < part.T1);
                if (want == _partShown[i]) continue;
                _partShown[i] = want;
                try { _partObjs[i].Value.SetActive(want); } catch { }
            }
        }

        static void DestroyParts()
        {
            try { if (_partsRoot != null) UnityEngine.Object.Destroy(_partsRoot); } catch { }
            _partsRoot = null; _partObjs.Clear(); _partShown.Clear();
        }

        static void DestroyMap()
        {
            try { if (_mapGo != null) UnityEngine.Object.Destroy(_mapGo); } catch { }
            _mapGo = null;
        }

        static void HideGameUi()
        {
            _hiddenCanvases.Clear();
            try
            {
                foreach (var cv in UnityEngine.Object.FindObjectsOfType<Canvas>())
                {
                    if (!cv.isRootCanvas || !cv.enabled || cv.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    cv.enabled = false;
                    _hiddenCanvases.Add(cv);
                }
            }
            catch { }
        }

        static void ShowGameUi()
        {
            foreach (var cv in _hiddenCanvases) { try { cv.enabled = true; } catch { } }
            _hiddenCanvases.Clear();
        }

        // ---------------------------------------------------------------- start / stop

        public static void StartViewer(ClipInfo info)
        {
            if (Cfg.ClipAutoMap.Value && LocalPlayer() == null) { OpenClipInMap(info); return; }
            StartViewerNow(info);
        }

        static void StartViewerNow(ClipInfo info)
        {
            try
            {
                if (ViewerActive) StopViewer();
                if (FreeActive) StopFree();
                var c = ReadClip(info.Path);
                if (!ViewerAllowed(c, out var why, out var showEnemies)) { Toast(why); return; }
                var cam = Cam();
                if (cam == null) { Toast("No camera"); return; }

                _vc = c; _vShowEnemies = showEnemies;
                _vBrain = cam.GetComponent<CinemachineBrain>();
                if (_vBrain != null) _vBrain.enabled = false;
                var wc = TheOnlyCamera.instance != null ? TheOnlyCamera.instance.weaponCamera : null;
                _vWeaponCam = wc != null ? wc.GetComponent<Camera>() : null;
                if (_vWeaponCam != null) _vWeaponCam.enabled = false;
                _vDisabled.Clear();
                var me = LocalPlayer();
                if (me != null)
                {
                    if (me.playerMovementController != null && me.playerMovementController.enabled) { me.playerMovementController.enabled = false; _vDisabled.Add(me.playerMovementController); }
                    var look = me.GetComponent<PlayerCameraController>();
                    if (look != null && look.enabled) { look.enabled = false; _vDisabled.Add(look); }
                }

                BuildActors(me);
                BuildMap(c, me);
                BuildParts(c);
                HideGameUi();
                _vt = 0f; _vIn = 0f; _vOut = c.Duration; _vSpeed = 1f; _vPlaying = true; _vFollow = false; _vPath = false; _vUi = true; _vFollowIdx = 0;
                var e = cam.transform.eulerAngles;
                _vYaw = e.y; _vPitch = e.x > 180f ? e.x - 360f : e.x;
                _vPos = cam.transform.position;
                ViewerActive = true;
                ScheduleSnapshots();
                Toast("Clip viewer: " + (_vShowEnemies ? "all players" : "your team only"));
            }
            catch (Exception e)
            {
                Plugin.Instance.Log.LogError("StartViewer: " + e);
                Toast("Could not open clip: " + e.Message);
                StopViewer();
            }
        }

        // The game's own third-person character: the "NewOutsideVisual" rig (mesh + 52 bones + animator) of the local player.
        static int _modelLogged;

        static GameObject FindModelDonor(PlayerObjectController me)
        {
            try
            {
                if (me == null) return null;
                foreach (var s in me.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (s.transform.parent != null && s.transform.parent.name == "NewOutsideVisual") return s.transform.parent.gameObject;
            }
            catch { }
            return null;
        }

        static bool TryBuildModelActor(ClipPlayer p, GameObject root, GameObject donor, PlayerObjectController me, bool mine, out GameObject body, out GameObject face)
        {
            body = null; face = null;
            try
            {
                var clone = UnityEngine.Object.Instantiate(donor, root.transform);   // donor is inactive, so nothing runs until we switch it on
                clone.name = "Model";
                foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true)) { try { mb.enabled = false; } catch { } }
                // keep the donor's size, offset and rotation relative to the player root (the rig is authored in centimetres under a scaled parent)
                var rel = me.transform;
                clone.transform.localScale = donor.transform.lossyScale;
                clone.transform.localPosition = rel.InverseTransformPoint(donor.transform.position);
                clone.transform.localRotation = Quaternion.Inverse(rel.rotation) * donor.transform.rotation;
                // the owner's own copy has its mesh and skeleton switched off (first-person), so switch them on in the clone
                foreach (var tr in clone.GetComponentsInChildren<Transform>(true))
                {
                    string tn = tr.name;
                    if (tn == "SkinnedMeshRenderer" || tn == "Hips" || tn == "Root") tr.gameObject.SetActive(true);
                    tr.gameObject.layer = 0;
                }

                var smr = clone.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null)
                {
                    var mats = smr.sharedMaterials;
                    var team = mine ? me.blueMat : me.redMat;
                    if (team != null && mats.Length > 0) { mats[0] = team; smr.sharedMaterials = mats; }
                    smr.updateWhenOffscreen = true;
                    smr.gameObject.SetActive(true);
                    smr.enabled = true;
                }
                var an = clone.GetComponentInChildren<Animator>(true);
                if (an != null) { an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
                clone.SetActive(true);
                if (an != null) { try { an.Rebind(); an.Update(0f); } catch { } }
                if (_modelLogged++ < 2) Plugin.Instance.Log.LogInfo("Clip viewer: copied the player model (smr " + (smr != null) + ", active " + (smr != null && smr.gameObject.activeInHierarchy) + ", bounds " + (smr != null ? smr.bounds.size.ToString() : "-") + ")");

                body = clone;
                face = new GameObject("NoFace");
                face.transform.SetParent(root.transform, false);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Instance.Log.LogWarning("Clip viewer: could not copy the player model: " + e.Message);
                return false;
            }
        }

        static Material _matSuitOwn, _matSuitEnemy, _matHead, _matDark;

        static Material MakeMat(Color c, float smooth = 0.25f)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Sprites/Default");
            if (sh == null) return null;
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>(); if (col != null) UnityEngine.Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return go;
        }

        static void BuildActors(PlayerObjectController me)
        {
            DestroyActors();
            _matSuitOwn = MakeMat(new Color(0.25f, 0.55f, 1f));
            _matSuitEnemy = MakeMat(new Color(1f, 0.32f, 0.3f));
            _matHead = MakeMat(new Color(0.9f, 0.92f, 0.95f), 0.5f);
            _matDark = MakeMat(new Color(0.07f, 0.08f, 0.1f), 0.8f);

            foreach (var p in _vc.Players)
            {
                bool mine = p.Team == _vc.LocalTeam || p.IsLocal;
                if (!mine && !_vShowEnemies) continue;
                float H = Mathf.Clamp(p.Height, 1.2f, 2.6f), s = H / 1.8f;
                var suit = mine ? _matSuitOwn : _matSuitEnemy;

                var root = new GameObject("ClipActor_" + p.Name);
                var donorModel = FindModelDonor(me);
                if (donorModel != null && TryBuildModelActor(p, root, donorModel, me, mine, out var mBody, out var mFace))
                {
                    _actors.Add(new Actor { Root = root, Body = mBody, Face = mFace, P = p, Rends = new Renderer[0] });
                    continue;
                }
                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);

                // robot astronaut: suit, round head, dark visor, backpack, gun
                Part(body.transform, PrimitiveType.Capsule, new Vector3(0f, 0.30f * H, 0f), new Vector3(0.52f * s, 0.30f * H, 0.46f * s), suit, "Suit");
                Part(body.transform, PrimitiveType.Sphere, new Vector3(0f, 0.74f * H, 0f), new Vector3(0.46f * s, 0.46f * s, 0.46f * s), _matHead, "Head");
                var visor = Part(body.transform, PrimitiveType.Sphere, new Vector3(0f, 0.75f * H, 0.14f * s), new Vector3(0.34f * s, 0.2f * s, 0.2f * s), _matDark, "Visor");
                Part(body.transform, PrimitiveType.Cube, new Vector3(0f, 0.42f * H, -0.27f * s), new Vector3(0.40f * s, 0.42f * s, 0.16f * s), _matDark, "Pack");
                Part(body.transform, PrimitiveType.Cube, new Vector3(0.27f * s, 0.44f * H, 0.32f * s), new Vector3(0.10f * s, 0.14f * s, 0.46f * s), _matDark, "Gun");

                _actors.Add(new Actor { Root = root, Body = body, Face = visor, P = p, Rends = new Renderer[0] });
            }
        }

        static void DestroyActors()
        {
            foreach (var a in _actors) { try { if (a.Root != null) UnityEngine.Object.Destroy(a.Root); } catch { } }
            _actors.Clear();
        }

        public static void StopViewer()
        {
            if (Rendering) CancelRender(true);
            DestroyActors();
            DestroyParts();
            DestroyMap();
            ShowGameUi();
            try { if (_vBrain != null) _vBrain.enabled = true; } catch { }
            try { if (_vWeaponCam != null) _vWeaponCam.enabled = true; } catch { }
            foreach (var b in _vDisabled) { try { b.enabled = true; } catch { } }
            _vDisabled.Clear();
            _vBrain = null; _vWeaponCam = null; _vc = null;
            if (ViewerActive) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            ViewerActive = false;
        }

        // ---------------------------------------------------------------- sampling

        static void TimeIdx(float[] times, float t, out int i0, out int i1, out float a)
        {
            int n = times.Length;
            if (n == 0) { i0 = i1 = 0; a = 0f; return; }
            if (t <= times[0]) { i0 = i1 = 0; a = 0f; return; }
            if (t >= times[n - 1]) { i0 = i1 = n - 1; a = 0f; return; }
            int i = Array.BinarySearch(times, t);
            if (i < 0) i = ~i - 1;
            i0 = i; i1 = Mathf.Min(i + 1, n - 1);
            a = i1 > i0 ? (t - times[i0]) / (times[i1] - times[i0]) : 0f;
        }

        static bool SampleTrack(ClipPlayer p, float t, out Vector3 pos, out float yaw, out bool alive, out float hp)
        {
            pos = default; yaw = 0f; alive = false; hp = 0f;
            TimeIdx(_vc.Times, t, out int i0, out int i1, out float a);
            if (p.Valid == null || i0 >= p.Valid.Length) return false;
            bool v0 = p.Valid[i0], v1 = p.Valid[i1];
            if (!v0 && !v1) return false;
            if (!v0) { i0 = i1; a = 0f; } else if (!v1) { i1 = i0; a = 0f; }
            pos = Vector3.Lerp(new Vector3(p.X[i0], p.Y[i0], p.Z[i0]), new Vector3(p.X[i1], p.Y[i1], p.Z[i1]), a);
            yaw = Mathf.LerpAngle(p.Yaw[i0], p.Yaw[i1], a);
            alive = p.Alive[i0];
            hp = Mathf.Lerp(p.Hp[i0], p.Hp[i1], a);
            return true;
        }

        // ---------------------------------------------------------------- camera path

        static bool EvalPath(float t, out Vector3 pos, out Quaternion rot)
        {
            pos = default; rot = Quaternion.identity;
            var k = _vc.Keys;
            if (k.Count == 0) return false;
            if (k.Count == 1 || t <= k[0].T) { pos = k[0].Pos; rot = k[0].Rot; return true; }
            if (t >= k[k.Count - 1].T) { pos = k[k.Count - 1].Pos; rot = k[k.Count - 1].Rot; return true; }
            int i = 0;
            while (i < k.Count - 2 && t > k[i + 1].T) i++;
            float a = Mathf.InverseLerp(k[i].T, k[i + 1].T, t);
            a = a * a * (3f - 2f * a); // smoothstep
            var p0 = k[Mathf.Max(i - 1, 0)].Pos; var p1 = k[i].Pos; var p2 = k[i + 1].Pos; var p3 = k[Mathf.Min(i + 2, k.Count - 1)].Pos;
            pos = 0.5f * (2f * p1 + (-p0 + p2) * a + (2f * p0 - 5f * p1 + 4f * p2 - p3) * a * a + (-p0 + 3f * p1 - 3f * p2 + p3) * a * a * a);
            rot = Quaternion.Slerp(k[i].Rot, k[i + 1].Rot, a);
            return true;
        }

        // ---------------------------------------------------------------- update

        static void UpdateViewer()
        {
            if (!ViewerActive || _vc == null) return;
            var kb = Keyboard.current; var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;
            if (Rendering) RenderTick();

            if (Time.unscaledTime >= _nextVCheck)
            {
                _nextVCheck = Time.unscaledTime + 0.5f;
                if (!ViewerAllowed(_vc, out var why, out var showEnemies) || (showEnemies != _vShowEnemies && !showEnemies))
                { Toast(why != "" ? why : "Clip viewer closed (match rules changed)"); StopViewer(); return; }
            }

            if (kb != null)
            {
                if (kb.backspaceKey.wasPressedThisFrame) { if (Rendering) { CancelRender(true); Toast("Rendering cancelled"); } else StopViewer(); return; }
                if (kb.escapeKey.wasPressedThisFrame) { _closePauseUntil = Time.unscaledTime + 1.2f; if (Rendering) CancelRender(true); StopViewer(); return; }
                if (kb.tabKey.wasPressedThisFrame) _vUi = !_vUi;
                if (kb.enterKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) _vPlaying = !_vPlaying;
                if (kb.commaKey.wasPressedThisFrame) { _vPlaying = false; _vt = Mathf.Max(_vIn, _vt - 1f / ClipRate); }
                if (kb.periodKey.wasPressedThisFrame) { _vPlaying = false; _vt = Mathf.Min(_vOut, _vt + 1f / ClipRate); }
                if (kb.leftBracketKey.wasPressedThisFrame) _vSpeed = Mathf.Max(0.125f, _vSpeed * 0.5f);
                if (kb.rightBracketKey.wasPressedThisFrame) _vSpeed = Mathf.Min(8f, _vSpeed * 2f);
                if (kb.homeKey.wasPressedThisFrame) _vt = _vIn;
                if (kb.endKey.wasPressedThisFrame) _vt = _vOut;
                if (kb.kKey.wasPressedThisFrame) AddKey();
                if (_vFollow)
                {
                    if (kb.eKey.wasPressedThisFrame && _actors.Count > 0) _vFollowIdx = (_vFollowIdx + 1) % _actors.Count;
                    if (kb.qKey.wasPressedThisFrame && _actors.Count > 0) _vFollowIdx = (_vFollowIdx - 1 + _actors.Count) % _actors.Count;
                }
            }

            if (_vPlaying && !Rendering)
            {
                _vt += dt * _vSpeed;
                if (_vt >= _vOut) { _vt = _vIn; }
            }
            _vt = Mathf.Clamp(_vt, 0f, _vc.Duration);

            // cursor is FREE by default so the timeline can be clicked; hold the right mouse button to look around (Tab locks the mouse for continuous look)
            bool lookNow = !_vUi || (mouse != null && mouse.rightButton.isPressed);
            Cursor.lockState = lookNow ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lookNow;

            if (mouse != null)
            {
                if (lookNow)
                {
                    var d = mouse.delta.ReadValue();
                    float sens = 0.12f;
                    _vYaw += d.x * sens;
                    _vPitch = Mathf.Clamp(_vPitch - d.y * sens, -89f, 89f);
                }
                float sc = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(sc) > 0.01f)
                {
                    if (_vFollow) _vDist = Mathf.Clamp(_vDist - Mathf.Sign(sc) * 0.5f, 1.2f, 30f);
                    else _vFlySpeed = Mathf.Clamp(_vFlySpeed * (sc > 0 ? 1.25f : 0.8f), 0.5f, 60f);
                }
            }
            if (!_vFollow && kb != null && !(_vPath && _vc.Keys.Count > 1))
            {
                var rot = Quaternion.Euler(_vPitch, _vYaw, 0f);
                Vector3 mv = Vector3.zero;
                if (kb.wKey.isPressed) mv += Vector3.forward;
                if (kb.sKey.isPressed) mv += Vector3.back;
                if (kb.aKey.isPressed) mv += Vector3.left;
                if (kb.dKey.isPressed) mv += Vector3.right;
                Vector3 world = rot * mv;
                if (kb.spaceKey.isPressed) world += Vector3.up;
                if (kb.leftCtrlKey.isPressed) world += Vector3.down;
                float boost = kb.leftShiftKey.isPressed ? 3f : 1f;
                if (world.sqrMagnitude > 0f) _vPos += world.normalized * _vFlySpeed * boost * dt;
            }

            UpdatePartVisibility();

            // stand-ins
            foreach (var a in _actors)
            {
                try
                {
                    if (!SampleTrack(a.P, _vt, out var pos, out var yaw, out var alive, out var hp)) { a.Root.SetActive(false); continue; }
                    a.Root.SetActive(true);
                    a.Root.transform.position = pos;
                    a.Root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    a.Body.transform.localRotation = alive ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
                    a.Body.transform.localPosition = alive ? Vector3.zero : new Vector3(0f, 0.25f, 0f);
                    a.Face.SetActive(alive);
                }
                catch { }
            }
        }

        static void ViewerLateUpdate()
        {
            if (!ViewerActive || _vc == null) return;
            var cam = Cam();
            if (cam == null) return;
            try
            {
                if (_vPath && _vc.Keys.Count > 0 && EvalPath(_vt, out var pp, out var pr))
                {
                    cam.transform.SetPositionAndRotation(pp, pr);
                    return;
                }
                if (_vFollow && _actors.Count > 0)
                {
                    var a = _actors[Mathf.Clamp(_vFollowIdx, 0, _actors.Count - 1)];
                    var pivot = a.Root.transform.position + Vector3.up * (a.P.Height * 0.8f);
                    var rot = Quaternion.Euler(_vPitch, _vYaw, 0f);
                    _vPos = pivot + rot * Vector3.back * _vDist;
                    cam.transform.SetPositionAndRotation(_vPos, rot);
                    return;
                }
                cam.transform.SetPositionAndRotation(_vPos, Quaternion.Euler(_vPitch, _vYaw, 0f));
            }
            catch { }
        }

        static void AddKey()
        {
            var cam = Cam();
            if (cam == null || _vc == null) return;
            _vc.Keys.RemoveAll(k => Mathf.Abs(k.T - _vt) < 0.05f);
            _vc.Keys.Add(new CamKey { T = _vt, Pos = cam.transform.position, Rot = cam.transform.rotation });
            _vc.Keys.Sort((a, b) => a.T.CompareTo(b.T));
            Toast("Camera keyframe " + _vc.Keys.Count + " added at " + _vt.ToString("0.0") + " s");
        }

        // ---------------------------------------------------------------- save edit

        static void SaveEdit()
        {
            try
            {
                var c = _vc;
                int a = 0, b = c.Times.Length - 1;
                while (a < c.Times.Length && c.Times[a] < _vIn) a++;
                while (b > 0 && c.Times[b] > _vOut) b--;
                if (b - a < 2) { Toast("Trim range is too short"); return; }
                int n = b - a + 1;
                float t0 = c.Times[a];
                var e = new Clip { Name = c.Name + " (edit)", Scene = c.Scene, Saved = DateTime.Now, LocalTeam = c.LocalTeam };
                e.Boxes = c.Boxes;
                e.Times = c.Times.Skip(a).Take(n).Select(t => t - t0).ToArray();
                foreach (var p in c.Players)
                {
                    e.Players.Add(new ClipPlayer
                    {
                        Key = p.Key, Name = p.Name, Team = p.Team, IsBot = p.IsBot, IsLocal = p.IsLocal, Height = p.Height, CenterOff = p.CenterOff,
                        X = p.X.Skip(a).Take(n).ToArray(), Y = p.Y.Skip(a).Take(n).ToArray(), Z = p.Z.Skip(a).Take(n).ToArray(), Yaw = p.Yaw.Skip(a).Take(n).ToArray(),
                        Hp = p.Hp.Skip(a).Take(n).ToArray(), Alive = p.Alive.Skip(a).Take(n).ToArray(), Valid = p.Valid.Skip(a).Take(n).ToArray()
                    });
                }
                foreach (var ev in c.Events.Where(x => x.T >= c.Times[a] && x.T <= c.Times[b])) e.Events.Add(new ClipEvent { T = ev.T - t0, Kind = ev.Kind, Text = ev.Text, Team = ev.Team });
                foreach (var k in c.Keys.Where(x => x.T >= c.Times[a] && x.T <= c.Times[b])) e.Keys.Add(new CamKey { T = k.T - t0, Pos = k.Pos, Rot = k.Rot });
                foreach (var pt in c.Parts.Where(x => x.T1 < 0f || x.T1 >= c.Times[a])) e.Parts.Add(new ClipPart { T0 = Mathf.Max(0f, pt.T0 - t0), T1 = pt.T1 < 0f ? -1f : pt.T1 - t0, Type = pt.Type, Team = pt.Team, Pos = pt.Pos, Rot = pt.Rot, Scale = pt.Scale });
                e.Path = System.IO.Path.Combine(ClipsDir, "clip_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_edit.clip");
                WriteClip(e);
                ScanClips();
                Toast("Edit saved (" + Mathf.RoundToInt(e.Duration) + " s, " + e.Keys.Count + " camera keys)");
            }
            catch (Exception ex) { Toast("Could not save edit: " + ex.Message); Plugin.Instance.Log.LogError("SaveEdit: " + ex); }
        }

        // ---------------------------------------------------------------- UI (called every OnGUI event so buttons work)

        static bool VBtn(Rect r, string text, bool on = false, int size = 12)
        {
            G.Fill(r, on ? G.Dark : (G.Hover(r) ? G.RowHover : G.Row));
            if (on) G.Fill(new Rect(r.x, r.yMax - 2, r.width, 2), G.Accent);
            G.Label(r, text, size, G.Text, TextAnchor.MiddleCenter, true);
            return G.Click(r);
        }

        static string Clock(float t) { int m = (int)(t / 60f); return m.ToString("00") + ":" + (t - m * 60f).ToString("00.0"); }

        public static void DrawClipUi()
        {
            if (!ViewerActive || _vc == null) return;
            float W = Screen.width, H = Screen.height;
            float dur = Mathf.Max(0.1f, _vc.Duration);

            // names above heads (repaint only)
            if (Event.current.type == EventType.Repaint)
            {
                var cam = Cam();
                if (cam != null)
                {
                    foreach (var a in _actors)
                    {
                        try
                        {
                            if (!a.Root.activeSelf) continue;
                            var head = a.Root.transform.position + Vector3.up * (a.P.Height + 0.25f);
                            var sp = cam.WorldToScreenPoint(head);
                            if (sp.z <= 0f) continue;
                            bool mine = a.P.Team == _vc.LocalTeam || a.P.IsLocal;
                            SampleTrack(a.P, _vt, out _, out _, out bool alive, out float hp);
                            var col = mine ? new Color(0.5f, 0.8f, 1f) : new Color(1f, 0.5f, 0.5f);
                            var r = new Rect(sp.x - 90f, H - sp.y - 22f, 180f, 20f);
                            string nm = a.P.Name.Length > 18 ? a.P.Name.Substring(0, 17) + "..." : a.P.Name;
                            G.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), nm + (alive ? "" : "  (down)"), 13, new Color(0, 0, 0, 0.9f), TextAnchor.MiddleCenter, true);
                            G.Label(r, nm + (alive ? "" : "  (down)"), 13, col, TextAnchor.MiddleCenter, true);
                            if (alive)
                            {
                                var bar = new Rect(sp.x - 24f, H - sp.y, 48f, 4f);
                                G.Fill(bar, new Color(0, 0, 0, 0.7f));
                                G.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(hp / 100f), bar.height), Color.Lerp(Color.red, Color.green, Mathf.Clamp01(hp / 100f)));
                            }
                        }
                        catch { }
                    }
                }
                // elimination flash
                foreach (var e in _vc.Events)
                {
                    float age = _vt - e.T;
                    if (age < 0f || age > 2.5f || e.Kind != 0) continue;
                    float al = Mathf.Clamp01(1.2f - age / 2.5f);
                    G.Label(new Rect(W / 2f - 200, 90f + (Mathf.Abs(e.GetHashCode()) % 3) * 22f, 400f, 22f), e.Text, 15, new Color(1f, 0.4f, 0.4f, al), TextAnchor.MiddleCenter, true);
                }
            }

            // timeline bar
            _vBar = new Rect(0, H - 150f, W, 150f);
            G.Fill(_vBar, new Color(0.04f, 0.045f, 0.055f, 0.93f));
            G.Fill(new Rect(0, _vBar.y, W, 2f), G.Accent);

            var track = new Rect(24f, _vBar.y + 24f, W - 48f, 30f);
            float trackW = track.width - 56f - 8f;
            // trim region + events behind the slider
            G.Fill(new Rect(track.x, track.y + 12f, trackW, 6f), new Color(1, 1, 1, 0.06f));
            G.Fill(new Rect(track.x + trackW * (_vIn / dur), track.y + 10f, trackW * ((_vOut - _vIn) / dur), 10f), new Color(G.Accent.r, G.Accent.g, G.Accent.b, 0.25f));
            foreach (var e in _vc.Events)
                G.Fill(new Rect(track.x + trackW * (e.T / dur) - 1f, track.y - 8f, 3f, 10f), e.Kind == 0 ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.85f, 0.2f));
            foreach (var k in _vc.Keys)
                G.Fill(new Rect(track.x + trackW * (k.T / dur) - 3f, track.yMax - 2f, 7f, 7f), new Color(0.4f, 0.8f, 1f));
            float nt = G.Slider("clipt", track, _vt, 0f, dur, "0.0");
            if (Mathf.Abs(nt - _vt) > 0.001f) { _vt = nt; _vPlaying = false; }

            float y = _vBar.y + 64f, x = 24f;
            Func<float, float> adv = w => { float cur = x; x += w + 6f; return cur; };
            if (VBtn(new Rect(adv(44f), y, 44f, 32f), "|<")) _vt = _vIn;
            if (VBtn(new Rect(adv(90f), y, 90f, 32f), _vPlaying ? "PAUSE" : "PLAY", _vPlaying)) _vPlaying = !_vPlaying;
            foreach (var sp in new[] { 0.25f, 0.5f, 1f, 2f, 4f })
                if (VBtn(new Rect(adv(54f), y, 54f, 32f), sp + "x", Mathf.Approximately(_vSpeed, sp))) _vSpeed = sp;
            x += 10f;
            if (VBtn(new Rect(adv(54f), y, 54f, 32f), "IN [")) _vIn = Mathf.Min(_vt, _vOut - 0.5f);
            if (VBtn(new Rect(adv(54f), y, 54f, 32f), "OUT ]")) _vOut = Mathf.Max(_vt, _vIn + 0.5f);
            if (VBtn(new Rect(adv(54f), y, 54f, 32f), "RESET")) { _vIn = 0f; _vOut = _vc.Duration; }
            x += 10f;
            if (VBtn(new Rect(adv(74f), y, 74f, 32f), "+ KEY (K)")) AddKey();
            if (VBtn(new Rect(adv(64f), y, 64f, 32f), "PATH", _vPath)) { _vPath = !_vPath; if (_vPath && _vc.Keys.Count < 2) { Toast("Add at least 2 camera keys first"); _vPath = false; } }
            if (VBtn(new Rect(adv(68f), y, 68f, 32f), "CLEAR KEYS")) _vc.Keys.Clear();
            if (VBtn(new Rect(adv(74f), y, 74f, 32f), _vFollow ? "FOLLOW" : "FREE", _vFollow)) _vFollow = !_vFollow;
            if (VBtn(new Rect(adv(100f), y, 100f, 32f), "SAVE EDIT")) SaveEdit();
            if (VBtn(new Rect(adv(120f), y, 120f, 32f), "RENDER VIDEO", Rendering)) StartRender();
            if (VBtn(new Rect(W - 24f - 70f, y, 70f, 32f), "EXIT")) { StopViewer(); return; }
            DrawRenderOverlay();

            string txt = Clock(_vt) + " / " + Clock(_vc.Duration) + "     trim " + Clock(_vIn) + " - " + Clock(_vOut) + "     keys " + _vc.Keys.Count + "     " + (_vShowEnemies ? "all players" : "your team only");
            G.Label(new Rect(24f, _vBar.y + 104f, W - 48f, 20f), txt, 13, G.Text, TextAnchor.MiddleLeft, true);
            G.Label(new Rect(24f, _vBar.y + 124f, W - 48f, 20f),
                "WASD fly   Space/Ctrl up-down   Shift fast   scroll speed   hold right mouse to look   Tab lock mouse   Enter play   , . step   [ ] speed   K camera key   Q/E next player (follow)   Backspace exit",
                12, G.Dim);
        }
    }
}
