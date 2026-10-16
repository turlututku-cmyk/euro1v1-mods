using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Minimal MJPEG-in-AVI writer: every frame is a JPEG. Plays in VLC, Windows players and video editors, no extra codecs needed.
    public class AviMjpegWriter : IDisposable
    {
        readonly FileStream _fs;
        readonly BinaryWriter _bw;
        readonly int _w, _h, _fps;
        readonly List<int[]> _index = new List<int[]>(); // offset (from 'movi' fourcc), size
        long _riffSizePos, _moviSizePos, _moviFourccPos, _avihFramesPos, _strhLengthPos, _maxChunk;
        int _frames;
        bool _closed;

        public int Frames => _frames;

        public AviMjpegWriter(string path, int w, int h, int fps)
        {
            _w = w; _h = h; _fps = fps;
            _fs = File.Create(path);
            _bw = new BinaryWriter(_fs);
            WriteHeader();
        }

        void Four(string s) { _bw.Write((byte)s[0]); _bw.Write((byte)s[1]); _bw.Write((byte)s[2]); _bw.Write((byte)s[3]); }

        void WriteHeader()
        {
            Four("RIFF"); _riffSizePos = _fs.Position; _bw.Write(0); Four("AVI ");
            Four("LIST"); long hdrlSizePos = _fs.Position; _bw.Write(0); Four("hdrl");
            Four("avih"); _bw.Write(56);
            _bw.Write(1000000 / _fps); _bw.Write(0); _bw.Write(0); _bw.Write(0x10);
            _avihFramesPos = _fs.Position; _bw.Write(0);
            _bw.Write(0); _bw.Write(1); _bw.Write(1 << 20); _bw.Write(_w); _bw.Write(_h);
            _bw.Write(0); _bw.Write(0); _bw.Write(0); _bw.Write(0);
            Four("LIST"); long strlSizePos = _fs.Position; _bw.Write(0); Four("strl");
            Four("strh"); _bw.Write(56);
            Four("vids"); Four("MJPG"); _bw.Write(0); _bw.Write((short)0); _bw.Write((short)0); _bw.Write(0);
            _bw.Write(1); _bw.Write(_fps); _bw.Write(0);
            _strhLengthPos = _fs.Position; _bw.Write(0);
            _bw.Write(1 << 20); _bw.Write(-1); _bw.Write(0);
            _bw.Write((short)0); _bw.Write((short)0); _bw.Write((short)_w); _bw.Write((short)_h);
            Four("strf"); _bw.Write(40);
            _bw.Write(40); _bw.Write(_w); _bw.Write(_h); _bw.Write((short)1); _bw.Write((short)24);
            Four("MJPG"); _bw.Write(_w * _h * 3); _bw.Write(0); _bw.Write(0); _bw.Write(0); _bw.Write(0);
            long strlEnd = _fs.Position;
            long hdrlEnd = _fs.Position;
            Patch(strlSizePos, (int)(strlEnd - strlSizePos - 4));
            Patch(hdrlSizePos, (int)(hdrlEnd - hdrlSizePos - 4));
            Four("LIST"); _moviSizePos = _fs.Position; _bw.Write(0); _moviFourccPos = _fs.Position; Four("movi");
        }

        void Patch(long pos, int value)
        {
            long cur = _fs.Position;
            _fs.Position = pos; _bw.Write(value); _fs.Position = cur;
        }

        public void AddFrame(byte[] jpeg)
        {
            long chunkPos = _fs.Position;
            Four("00dc"); _bw.Write(jpeg.Length); _bw.Write(jpeg);
            if ((jpeg.Length & 1) == 1) _bw.Write((byte)0);
            _index.Add(new[] { (int)(chunkPos - _moviFourccPos), jpeg.Length });
            if (jpeg.Length > _maxChunk) _maxChunk = jpeg.Length;
            _frames++;
        }

        public void Dispose() { Close(); }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            long moviEnd = _fs.Position;
            Patch(_moviSizePos, (int)(moviEnd - _moviSizePos - 4));
            Four("idx1"); _bw.Write(_index.Count * 16);
            foreach (var e in _index) { Four("00dc"); _bw.Write(0x10); _bw.Write(e[0]); _bw.Write(e[1]); }
            Patch(_riffSizePos, (int)(_fs.Position - 8));
            Patch(_avihFramesPos, _frames);
            Patch(_strhLengthPos, _frames);
            _bw.Flush(); _fs.Close();
        }
    }

    // Renders the clip viewer's camera (trim range, camera path and all) to a video file, one frame at a time so it never stutters.
    public static partial class Features
    {
        public static bool Rendering;
        static int _rFrame, _rTotal;
        static float _rStep;
        static bool _rPending;
        static RenderTexture _rRt;
        static Texture2D _rTex;
        static AviMjpegWriter _rAvi;
        static string _rPath = "";
        static Camera _rCam;
        const int RenderFps = 30;

        public static string VideosDir => Path.Combine(Paths.GameRootPath, "Mods", "Videos");
        public static float RenderProgress => _rTotal > 0 ? Mathf.Clamp01(_rFrame / (float)_rTotal) : 0f;
        public static string RenderPath => _rPath;

        public static void StartRender()
        {
            if (!ViewerActive || _vc == null) return;
            if (Rendering) return;
            try
            {
                var cam = Cam();
                if (cam == null) { Toast("No camera to render"); return; }
                float aspect = Screen.width / (float)Screen.height;
                int w = Mathf.Min(Screen.width, 1920), h = Mathf.RoundToInt(w / aspect);
                w &= ~1; h &= ~1;
                _rStep = Mathf.Max(0.05f, _vSpeed) / RenderFps;
                _rTotal = Mathf.Max(2, Mathf.CeilToInt((_vOut - _vIn) / _rStep));
                Directory.CreateDirectory(VideosDir);
                _rPath = Path.Combine(VideosDir, "clip_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".avi");
                _rAvi = new AviMjpegWriter(_rPath, w, h, RenderFps);
                _rRt = new RenderTexture(w, h, 24);
                _rTex = new Texture2D(w, h, TextureFormat.RGB24, false);
                _rCam = cam;
                cam.targetTexture = _rRt;
                _vPlaying = false;
                _rFrame = 0; _rPending = false;
                Rendering = true;
                Toast("Rendering video... Backspace cancels");
            }
            catch (Exception e)
            {
                Toast("Could not start rendering: " + e.Message);
                Plugin.Instance.Log.LogError("StartRender: " + e);
                CancelRender(true);
            }
        }

        static void RenderTick()
        {
            if (!Rendering) return;
            try
            {
                if (_rPending)
                {
                    RenderTexture.active = _rRt;
                    _rTex.ReadPixels(new Rect(0, 0, _rRt.width, _rRt.height), 0, 0, false);
                    _rTex.Apply(false);
                    RenderTexture.active = null;
                    byte[] jpg = ImageConversion.EncodeToJPG(_rTex, 90);
                    _rAvi.AddFrame(jpg);
                    _rPending = false;
                }
                if (_rFrame >= _rTotal) { FinishRender(); return; }
                _vt = Mathf.Min(_vc.Duration, _vIn + _rFrame * _rStep);
                _rFrame++;
                _rPending = true;
            }
            catch (Exception e)
            {
                Plugin.Instance.Log.LogError("RenderTick: " + e);
                Toast("Rendering failed: " + e.Message);
                CancelRender(true);
            }
        }

        static void CleanupRender()
        {
            try { if (_rCam != null) _rCam.targetTexture = null; } catch { }
            try { RenderTexture.active = null; } catch { }
            try { if (_rRt != null) { _rRt.Release(); UnityEngine.Object.Destroy(_rRt); } } catch { }
            try { if (_rTex != null) UnityEngine.Object.Destroy(_rTex); } catch { }
            _rRt = null; _rTex = null; _rCam = null;
            Rendering = false;
        }

        static void FinishRender()
        {
            int frames = _rAvi != null ? _rAvi.Frames : 0;
            try { if (_rAvi != null) _rAvi.Close(); } catch { }
            _rAvi = null;
            CleanupRender();
            Toast("Video saved (" + frames + " frames): Mods\\Videos\\" + Path.GetFileName(_rPath));
            Plugin.Instance.Log.LogInfo("Rendered " + frames + " frames to " + _rPath);
        }

        public static void CancelRender(bool deleteFile = true)
        {
            try { if (_rAvi != null) _rAvi.Close(); } catch { }
            _rAvi = null;
            CleanupRender();
            if (deleteFile) { try { if (File.Exists(_rPath)) File.Delete(_rPath); } catch { } }
        }

        static void DrawRenderOverlay()
        {
            if (!Rendering) return;
            float W = Screen.width, H = Screen.height;
            G.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
            var r = new Rect(W / 2f - 260f, H / 2f - 70f, 520f, 140f);
            G.Fill(r, new Color(0.06f, 0.07f, 0.09f, 0.97f));
            G.Fill(new Rect(r.x, r.y, r.width, 3f), G.Accent);
            G.Label(new Rect(r.x + 20f, r.y + 10f, r.width - 40f, 30f), "RENDERING VIDEO", 18, G.Accent, TextAnchor.MiddleLeft, true);
            var bar = new Rect(r.x + 20f, r.y + 56f, r.width - 40f, 18f);
            G.Fill(bar, new Color(1, 1, 1, 0.12f));
            G.Fill(new Rect(bar.x, bar.y, bar.width * RenderProgress, bar.height), G.Accent);
            G.Label(new Rect(r.x + 20f, r.y + 80f, r.width - 40f, 22f), _rFrame + " / " + _rTotal + " frames   (" + Mathf.RoundToInt(RenderProgress * 100f) + "%)", 13, G.Text, TextAnchor.MiddleLeft);
            if (VBtn(new Rect(r.xMax - 120f, r.yMax - 40f, 100f, 28f), "CANCEL")) { CancelRender(true); Toast("Rendering cancelled"); }
        }
    }
}

namespace Euro3v3.ModsMenu
{
    public static partial class Features
    {
        static void UpdatePostViewerFix()
        {
            if (_closePauseUntil <= 0f) return;
            if (Time.unscaledTime > _closePauseUntil) { _closePauseUntil = 0f; return; }
            if (ViewerActive) return;
            try { if (FindPause() && ClickPauseButton("resume")) { _closePauseUntil = 0f; } } catch { }
        }
    }
}
