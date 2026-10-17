using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Euro3v3.ModsMenu
{
    // Replay-style clip recorder (like the Minecraft Replay Mod, but for this game's player movement).
    // It keeps the last N seconds of every player's position in memory, saves them to Mods/Clips/*.clip,
    // and the viewer (ClipViewer.cs) replays them inside the same map with a free camera and a timeline.
    // Local and offline: nothing is sent over the network.
    public class ClipPlayer
    {
        public int Key; public string Name; public int Team; public bool IsBot, IsLocal;
        public float Height = 1.8f, CenterOff = 0.9f;
        public float[] X, Y, Z, Yaw, Hp;
        public bool[] Alive, Valid;
    }

    public class ClipEvent { public float T; public int Kind; public string Text; public int Team; } // kind 0 = elimination, 1 = your hit, 2 = marker

    public class ClipPart { public float T0, T1; public int Type, Team; public Vector3 Pos, Scale; public Quaternion Rot; }

    public class CamKey { public float T; public Vector3 Pos; public Quaternion Rot; }

    public class Clip
    {
        public string Name = "", Scene = "", Path = "";
        public DateTime Saved;
        public int LocalTeam;
        public float[] Times = new float[0];
        public List<ClipPlayer> Players = new List<ClipPlayer>();
        public List<ClipEvent> Events = new List<ClipEvent>();
        public List<CamKey> Keys = new List<CamKey>();
        public float[] Boxes = new float[0]; // 10 floats per box: center xyz, size xyz, rotation xyzw
        public List<ClipPart> Parts = new List<ClipPart>();
        public float Duration => Times.Length > 0 ? Times[Times.Length - 1] : 0f;
    }

    public class ClipInfo { public string Path, Name, Scene; public DateTime Saved; public float Duration; public int PlayerCount; }

    public static partial class Features
    {
        struct PSnap
        {
            public int Key; public string Name; public int Team; public bool IsBot, IsLocal, Alive;
            public float X, Y, Z, Yaw, Hp, Height, CenterOff;
        }
        class Frame { public float T; public PSnap[] P; }

        const float ClipRate = 15f;
        static readonly Queue<Frame> _ring = new Queue<Frame>();
        static readonly Queue<ClipEvent> _ringEvents = new Queue<ClipEvent>();
        static readonly Dictionary<int, bool> _wasAlive = new Dictionary<int, bool>();
        static readonly List<PlayerObjectController> _clipPlayers = new List<PlayerObjectController>();
        static float _nextClipSample, _nextClipScan;
        public static readonly List<ClipInfo> Clips = new List<ClipInfo>();
        static bool _clipsScanned;
        public static float BufferSeconds => _ring.Count > 1 ? _ring.Last().T - _ring.Peek().T : 0f;
        public static int BufferPlayers => _ring.Count > 0 ? _ring.Last().P.Length : 0;
        public static bool Recording;
        public static bool ManualRec;
        static float _manualStart;
        public static float ManualElapsed => ManualRec ? Time.unscaledTime - _manualStart : 0f;

        public static void ToggleManualRecord()
        {
            if (!Cfg.ClipOn.Value) { Toast("Turn on Clip Maker first"); return; }
            if (!ManualRec)
            {
                if (!Recording) { Toast("Recording starts once you are in a match"); return; }
                ManualRec = true; _manualStart = Time.unscaledTime;
                Toast("Recording... press Shift+F7 or STOP to save");
            }
            else StopManualRecord();
        }

        static void StopManualRecord()
        {
            float el = ManualElapsed;
            ManualRec = false;
            SaveClipSeconds("Recording " + DateTime.Now.ToString("HH:mm:ss"), el + 0.5f);
        }

        public static string ClipsDir => Path.Combine(Paths.GameRootPath, "Mods", "Clips");

        // ---------------------------------------------------------------- build parts (player-built walls, floors, stairs)

        class PartRec { public int Id, Type, Team; public Vector3 Pos, Scale; public Quaternion Rot; public float TSpawn, TDestroy = -1f; }
        static readonly Dictionary<int, PartRec> _partsAlive = new Dictionary<int, PartRec>();
        static readonly List<PartRec> _partAll = new List<PartRec>();

        static void ScanParts(float now)
        {
            try
            {
                var live = BuildPart.activeParts;
                if (live == null) return;
                var seen = new HashSet<int>();
                for (int i = 0; i < live.Count; i++)
                {
                    var bp = live[i];
                    if (bp == null) continue;
                    int id = bp.GetInstanceID();
                    seen.Add(id);
                    if (_partsAlive.ContainsKey(id)) continue;
                    var t = bp.transform;
                    var rec = new PartRec { Id = id, Type = (int)bp.type, Team = bp.ownerTeam, Pos = t.position, Rot = t.rotation, Scale = t.lossyScale, TSpawn = now };
                    _partsAlive[id] = rec; _partAll.Add(rec);
                }
                foreach (var id in _partsAlive.Keys.ToList())
                    if (!seen.Contains(id)) { _partsAlive[id].TDestroy = now; _partsAlive.Remove(id); }
            }
            catch { }
        }

        static void PrunePartsOlderThan(float cutoff)
        {
            _partAll.RemoveAll(r => r.TDestroy >= 0f && r.TDestroy < cutoff);
        }

        static List<ClipPart> BuildClipParts(float t0, float t1)
        {
            var res = new List<ClipPart>();
            foreach (var r in _partAll)
            {
                if (r.TDestroy >= 0f && r.TDestroy < t0) continue;   // gone before the clip starts
                if (r.TSpawn > t1) continue;
                res.Add(new ClipPart
                {
                    T0 = Mathf.Max(0f, r.TSpawn - t0),
                    T1 = r.TDestroy < 0f ? -1f : r.TDestroy - t0,
                    Type = r.Type, Team = r.Team, Pos = r.Pos, Rot = r.Rot, Scale = r.Scale
                });
            }
            return res;
        }

        // ---------------------------------------------------------------- recording

        static void UpdateClipRecorder()
        {
            var kb = Keyboard.current;
            if (Cfg.ClipOn.Value && (KeyHit(Cfg.KbClipRec.Value) || KeyHit(Cfg.KbClipSave.Value)))
            {
                if (KeyHit(Cfg.KbClipRec.Value)) ToggleManualRecord();
                else SaveClip(null);
            }

            if (!Cfg.ClipOn.Value) { if (_ring.Count > 0) { _ring.Clear(); _ringEvents.Clear(); _wasAlive.Clear(); } Recording = false; return; }
            if (ViewerActive) { return; } // do not record the stand-ins / replay camera

            float now = Time.unscaledTime;
            if (now >= _nextClipScan)
            {
                _nextClipScan = now + 0.5f;
                try
                {
                    _clipPlayers.Clear();
                    foreach (var p in UnityEngine.Object.FindObjectsOfType<PlayerObjectController>()) _clipPlayers.Add(p);
                }
                catch { }
                Recording = _clipPlayers.Count > 0;
                if (Recording) ScanParts(now);
                if (!Recording && _ring.Count > 0)
                {
                    if (ManualRec) StopManualRecord(); // the match ended: keep what was recorded
                    _ring.Clear(); _ringEvents.Clear(); _wasAlive.Clear(); // left the match
                    _partsAlive.Clear(); _partAll.Clear();
                }
            }
            if (!Recording || now < _nextClipSample) return;
            _nextClipSample = now + 1f / ClipRate;

            var snaps = new List<PSnap>(_clipPlayers.Count);
            foreach (var p in _clipPlayers)
            {
                try
                {
                    var pos = p.transform.position;
                    float yaw = p.vc_fps != null ? p.vc_fps.transform.eulerAngles.y : p.transform.eulerAngles.y;
                    float h = 1.8f, off = 0.9f;
                    if (p.capsuleCollider != null)
                    {
                        h = p.capsuleCollider.height * Mathf.Abs(p.transform.lossyScale.y);
                        off = p.capsuleCollider.bounds.center.y - pos.y;
                    }
                    float hp = p.health != null ? p.health.health : 0f;
                    snaps.Add(new PSnap
                    {
                        Key = p.playerIdNumber, Name = p.PlayerName ?? "?", Team = p.team, IsBot = p.IsBot, IsLocal = p.isLocalPlayer, Alive = p.alive,
                        X = pos.x, Y = pos.y, Z = pos.z, Yaw = yaw, Hp = hp, Height = h, CenterOff = off
                    });
                    _wasAlive.TryGetValue(p.playerIdNumber, out bool was);
                    if (was && !p.alive)
                        _ringEvents.Enqueue(new ClipEvent { T = now, Kind = 0, Text = (p.PlayerName ?? "?") + " eliminated", Team = p.team });
                    _wasAlive[p.playerIdNumber] = p.alive;
                }
                catch { }
            }
            _ring.Enqueue(new Frame { T = now, P = snaps.ToArray() });

            float window = Mathf.Max(10f, Cfg.ClipSeconds.Value);
            if (ManualRec) { window = Mathf.Clamp(now - _manualStart + 2f, window, 600f); if (now - _manualStart > 598f) StopManualRecord(); }
            while (_ring.Count > 0 && now - _ring.Peek().T > window) _ring.Dequeue();
            while (_ringEvents.Count > 0 && now - _ringEvents.Peek().T > window) _ringEvents.Dequeue();
            PrunePartsOlderThan(now - window - 5f);
        }

        static void DrawRecIndicator()
        {
            if (!ManualRec) return;
            float t = ManualElapsed;
            bool blink = ((int)(Time.unscaledTime * 2f) & 1) == 0;
            var r = new Rect(Screen.width / 2f - 70f, 108f, 140f, 28f);
            G.Fill(r, new Color(0f, 0f, 0f, 0.65f));
            if (blink) G.Fill(new Rect(r.x + 10f, r.y + 8f, 12f, 12f), new Color(1f, 0.15f, 0.15f));
            G.Label(new Rect(r.x + 28f, r.y, r.width - 30f, r.height), "REC  " + ((int)(t / 60f)).ToString("00") + ":" + ((int)(t % 60f)).ToString("00"), 14, Color.white, TextAnchor.MiddleLeft, true);
        }

        // Called from the hit detector when your hitmarker fires.
        static void ClipNoteHit(bool kill)
        {
            if (!Cfg.ClipOn.Value || !Recording) return;
            _ringEvents.Enqueue(new ClipEvent { T = Time.unscaledTime, Kind = 1, Text = kill ? "Your kill shot" : "Your hit", Team = -1 });
        }

        // ---------------------------------------------------------------- build / save / load

        static Clip BuildClipFromRing(string label, float lastSeconds)
        {
            var frames = _ring.ToArray();
            if (frames.Length < 2) return null;
            float end = frames[frames.Length - 1].T, start = Mathf.Max(frames[0].T, end - lastSeconds);
            frames = frames.Where(f => f.T >= start).ToArray();
            if (frames.Length < 2) return null;

            var clip = new Clip { Name = label ?? ("Clip " + DateTime.Now.ToString("HH:mm:ss")), Saved = DateTime.Now, Scene = SceneNames() };
            var local = LocalPlayer();
            clip.LocalTeam = local != null ? local.team : 0;
            float t0 = frames[0].T;
            int n = frames.Length;
            clip.Times = frames.Select(f => f.T - t0).ToArray();

            var tracks = new Dictionary<int, ClipPlayer>();
            for (int i = 0; i < n; i++)
            {
                foreach (var s in frames[i].P)
                {
                    if (!tracks.TryGetValue(s.Key, out var tr))
                    {
                        tr = new ClipPlayer
                        {
                            Key = s.Key, Name = s.Name, Team = s.Team, IsBot = s.IsBot, IsLocal = s.IsLocal, Height = s.Height, CenterOff = s.CenterOff,
                            X = new float[n], Y = new float[n], Z = new float[n], Yaw = new float[n], Hp = new float[n], Alive = new bool[n], Valid = new bool[n]
                        };
                        tracks[s.Key] = tr;
                    }
                    tr.X[i] = s.X; tr.Y[i] = s.Y; tr.Z[i] = s.Z; tr.Yaw[i] = s.Yaw; tr.Hp[i] = s.Hp; tr.Alive[i] = s.Alive; tr.Valid[i] = true;
                    tr.Team = s.Team; tr.Name = s.Name;
                }
            }
            clip.Players = tracks.Values.ToList();
            clip.Boxes = CaptureBoxes();
            clip.Parts = BuildClipParts(t0, frames[frames.Length - 1].T);
            foreach (var e in _ringEvents.Where(e => e.T >= start))
                clip.Events.Add(new ClipEvent { T = e.T - t0, Kind = e.Kind, Text = e.Text, Team = e.Team });
            return clip;
        }

        static string SceneNames()
        {
            var names = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++) names.Add(SceneManager.GetSceneAt(i).name);
            return string.Join("+", names);
        }

        // Rough blockout of the map: every solid static collider as a box (largest first). Lets a clip be viewed anywhere.
        static float[] CaptureBoxes()
        {
            var items = new List<KeyValuePair<float, float[]>>();
            try
            {
                foreach (var col in UnityEngine.Object.FindObjectsOfType<Collider>())
                {
                    try
                    {
                        if (col.isTrigger || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                        var rb = col.attachedRigidbody;
                        if (rb != null && !rb.isKinematic) continue;
                        var b = col.bounds; var sz = b.size;
                        if (sz.magnitude > 600f || (sz.x < 0.3f && sz.y < 0.3f && sz.z < 0.3f)) continue;
                        if (col.GetComponentInParent<PlayerObjectController>() != null) continue;
                        float[] d;
                        var box = col as BoxCollider;
                        if (box != null)
                        {
                            var t = box.transform; var c = t.TransformPoint(box.center); var ls = t.lossyScale; var r = t.rotation;
                            d = new[] { c.x, c.y, c.z, Mathf.Abs(box.size.x * ls.x), Mathf.Abs(box.size.y * ls.y), Mathf.Abs(box.size.z * ls.z), r.x, r.y, r.z, r.w };
                        }
                        else d = new[] { b.center.x, b.center.y, b.center.z, sz.x, sz.y, sz.z, 0f, 0f, 0f, 1f };
                        items.Add(new KeyValuePair<float, float[]>(sz.x * sz.y * sz.z, d));
                    }
                    catch { }
                }
            }
            catch { }
            var top = items.OrderByDescending(k => k.Key).Take(4000).ToList();
            var res = new float[top.Count * 10];
            for (int i = 0; i < top.Count; i++) Array.Copy(top[i].Value, 0, res, i * 10, 10);
            return res;
        }

        public static void SaveClip(string label) { SaveClipSeconds(label, Cfg.ClipSeconds.Value); }

        public static void SaveClipSeconds(string label, float seconds)
        {
            try
            {
                var clip = BuildClipFromRing(label, seconds);
                if (clip == null) { Toast("Nothing recorded yet. Clips only record during a match"); return; }
                Directory.CreateDirectory(ClipsDir);
                clip.Path = Path.Combine(ClipsDir, "clip_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".clip");
                WriteClip(clip);
                ScanClips();
                Toast("Clip saved (" + Mathf.RoundToInt(clip.Duration) + " s, " + clip.Parts.Count + " build parts)");
                Plugin.Instance.Log.LogInfo("Clip saved: " + clip.Players.Count + " players, " + clip.Parts.Count + " build parts, " + clip.Boxes.Length / 10 + " map boxes");
            }
            catch (Exception e) { Toast("Could not save clip: " + e.Message); Plugin.Instance.Log.LogError("SaveClip: " + e); }
        }

        public static void WriteClip(Clip c)
        {
            using (var bw = new BinaryWriter(File.Create(c.Path)))
            {
                bw.Write(0x434C4950); bw.Write(3);                       // magic 'CLIP', version
                bw.Write(c.Name); bw.Write(c.Scene); bw.Write(c.Saved.ToBinary()); bw.Write(c.LocalTeam);
                int n = c.Times.Length;
                bw.Write(n);
                foreach (var t in c.Times) bw.Write(t);
                bw.Write(c.Players.Count);
                foreach (var p in c.Players)
                {
                    bw.Write(p.Key); bw.Write(p.Name); bw.Write(p.Team); bw.Write(p.IsBot); bw.Write(p.IsLocal); bw.Write(p.Height); bw.Write(p.CenterOff);
                    for (int i = 0; i < n; i++) { bw.Write(p.Valid[i]); if (!p.Valid[i]) continue; bw.Write(p.X[i]); bw.Write(p.Y[i]); bw.Write(p.Z[i]); bw.Write(p.Yaw[i]); bw.Write(p.Hp[i]); bw.Write(p.Alive[i]); }
                }
                bw.Write(c.Events.Count);
                foreach (var e in c.Events) { bw.Write(e.T); bw.Write(e.Kind); bw.Write(e.Text ?? ""); bw.Write(e.Team); }
                bw.Write(c.Keys.Count);
                foreach (var k in c.Keys) { bw.Write(k.T); bw.Write(k.Pos.x); bw.Write(k.Pos.y); bw.Write(k.Pos.z); bw.Write(k.Rot.x); bw.Write(k.Rot.y); bw.Write(k.Rot.z); bw.Write(k.Rot.w); }
                bw.Write(c.Boxes.Length / 10);
                foreach (var f in c.Boxes) bw.Write(f);
                bw.Write(c.Parts.Count);
                foreach (var p in c.Parts) { bw.Write(p.T0); bw.Write(p.T1); bw.Write(p.Type); bw.Write(p.Team); bw.Write(p.Pos.x); bw.Write(p.Pos.y); bw.Write(p.Pos.z); bw.Write(p.Rot.x); bw.Write(p.Rot.y); bw.Write(p.Rot.z); bw.Write(p.Rot.w); bw.Write(p.Scale.x); bw.Write(p.Scale.y); bw.Write(p.Scale.z); }
            }
        }

        public static Clip ReadClip(string path, bool headerOnly = false)
        {
            using (var br = new BinaryReader(File.OpenRead(path)))
            {
                if (br.ReadInt32() != 0x434C4950) throw new InvalidDataException("not a clip file");
                int ver = br.ReadInt32();
                var c = new Clip { Path = path, Name = br.ReadString(), Scene = br.ReadString(), Saved = DateTime.FromBinary(br.ReadInt64()), LocalTeam = br.ReadInt32() };
                int n = br.ReadInt32();
                c.Times = new float[n];
                for (int i = 0; i < n; i++) c.Times[i] = br.ReadSingle();
                int pc = br.ReadInt32();
                if (headerOnly) { for (int i = 0; i < pc; i++) c.Players.Add(new ClipPlayer()); return c; }
                for (int k = 0; k < pc; k++)
                {
                    var p = new ClipPlayer { Key = br.ReadInt32(), Name = br.ReadString(), Team = br.ReadInt32(), IsBot = br.ReadBoolean(), IsLocal = br.ReadBoolean(), Height = br.ReadSingle(), CenterOff = br.ReadSingle() };
                    p.X = new float[n]; p.Y = new float[n]; p.Z = new float[n]; p.Yaw = new float[n]; p.Hp = new float[n]; p.Alive = new bool[n]; p.Valid = new bool[n];
                    for (int i = 0; i < n; i++)
                    {
                        p.Valid[i] = br.ReadBoolean();
                        if (!p.Valid[i]) continue;
                        p.X[i] = br.ReadSingle(); p.Y[i] = br.ReadSingle(); p.Z[i] = br.ReadSingle(); p.Yaw[i] = br.ReadSingle(); p.Hp[i] = br.ReadSingle(); p.Alive[i] = br.ReadBoolean();
                    }
                    c.Players.Add(p);
                }
                int ec = br.ReadInt32();
                for (int i = 0; i < ec; i++) c.Events.Add(new ClipEvent { T = br.ReadSingle(), Kind = br.ReadInt32(), Text = br.ReadString(), Team = br.ReadInt32() });
                int kc = br.ReadInt32();
                for (int i = 0; i < kc; i++)
                    c.Keys.Add(new CamKey { T = br.ReadSingle(), Pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()), Rot = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()) });
                if (ver >= 2)
                {
                    int bc = br.ReadInt32();
                    c.Boxes = new float[bc * 10];
                    for (int i = 0; i < c.Boxes.Length; i++) c.Boxes[i] = br.ReadSingle();
                }
                if (ver >= 3)
                {
                    int pcnt = br.ReadInt32();
                    for (int i = 0; i < pcnt; i++)
                        c.Parts.Add(new ClipPart { T0 = br.ReadSingle(), T1 = br.ReadSingle(), Type = br.ReadInt32(), Team = br.ReadInt32(), Pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()), Rot = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()), Scale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()) });
                }
                return c;
            }
        }

        public static void ScanClips()
        {
            Clips.Clear();
            Directory.CreateDirectory(ClipsDir);
            foreach (var f in Directory.GetFiles(ClipsDir, "*.clip").OrderByDescending(f => File.GetLastWriteTime(f)))
            {
                try
                {
                    var c = ReadClip(f, true);
                    Clips.Add(new ClipInfo { Path = f, Name = c.Name, Scene = c.Scene, Saved = c.Saved, Duration = c.Duration, PlayerCount = c.Players.Count });
                }
                catch { }
            }
            _clipsScanned = true;
        }

        public static void EnsureClipsScanned() { if (!_clipsScanned) ScanClips(); }

        public static void DeleteClip(ClipInfo ci)
        {
            try { File.Delete(ci.Path); } catch (Exception e) { Toast("Could not delete: " + e.Message); }
            ScanClips();
        }
    }
}
