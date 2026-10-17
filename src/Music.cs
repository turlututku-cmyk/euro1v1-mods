using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Plays your own mp3/wav/ogg files from Mods/Music. Local audio only.
    // Decoding is done in managed code (NLayer for mp3, NVorbis for ogg) because UnityWebRequestMultimedia
    // is stripped from this game build.
    //
    // The track is NOT decoded into one giant array any more. A background thread decodes about 10 seconds at a time into small
    // chunks and the main thread plays them back to back on two audio sources (the next chunk is scheduled on the other source
    // just before the current one ends). Peak memory stays around 10 MB instead of ~170 MB for a 4 minute song.
    public static partial class Features
    {
        public static readonly List<string> Tracks = new List<string>();
        public static int MusicCur = -1;
        public static bool MusicPlaying;
        public static string NowPlaying = "";

        const float ChunkSeconds = 10f, ScheduleAhead = 1.5f;
        const int MaxQueuedChunks = 3;

        static Func<float[], int, int, int> OpenReader(string path, out IDisposable file, out int channels, out int rate)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".mp3") { var f = new NLayer.MpegFile(path); file = f; channels = f.Channels; rate = f.SampleRate; return (b, o, c) => f.ReadSamples(b, o, c); }
            if (ext == ".ogg") { var v = new NVorbis.VorbisReader(path); file = v; channels = v.Channels; rate = v.SampleRate; return (b, o, c) => v.ReadSamples(b, o, c); }
            var w = new WavStream(path); file = w; channels = w.Channels; rate = w.Rate; return (b, o, c) => w.ReadSamples(b, o, c);
        }

        class Decoded { public float[] Data; public int Channels, Rate; public string Error; public int Index; }

        // Whole-file decode for SHORT sound effects only (hit sounds). Capped at 30 seconds so it can never allocate much.
        static Decoded DecodeFile(string path, int index)
        {
            var res = new Decoded { Index = index };
            IDisposable file = null;
            try
            {
                var read = OpenReader(path, out file, out res.Channels, out res.Rate);
                if (res.Channels <= 0 || res.Rate <= 0) throw new InvalidDataException("unsupported file");
                int cap = res.Rate * res.Channels * 30, pos = 0, n = 1;
                var arr = new float[res.Rate * res.Channels * 2];
                while (n > 0 && pos < cap)
                {
                    if (pos + 4096 * res.Channels > arr.Length) Array.Resize(ref arr, Math.Min(cap, arr.Length * 2));
                    n = read(arr, pos, Math.Min(4096 * res.Channels, arr.Length - pos));
                    if (n > 0) pos += n;
                }
                pos -= pos % res.Channels;
                Array.Resize(ref arr, pos);
                res.Data = arr;
                if (pos == 0) res.Error = "empty or unsupported file";
            }
            catch (Exception e) { res.Error = e.Message; }
            finally { try { file?.Dispose(); } catch { } }
            return res;
        }

        class MusicStream
        {
            public int Index, Channels, Rate;
            public volatile string Error;
            public volatile bool Done, Cancel;
            public readonly ConcurrentQueue<float[]> Chunks = new ConcurrentQueue<float[]>();
            public int Queued;

            // runs on a background thread
            public void Run(string path)
            {
                IDisposable file = null;
                try
                {
                    var read = OpenReader(path, out file, out Channels, out Rate);
                    if (Channels <= 0 || Rate <= 0) throw new InvalidDataException("unsupported file");

                    int chunkFloats = (int)(Rate * ChunkSeconds) * Channels, step = 4096 * Channels;
                    while (!Cancel)
                    {
                        if (Volatile.Read(ref Queued) >= MaxQueuedChunks) { Thread.Sleep(40); continue; }
                        var buf = new float[chunkFloats];
                        int pos = 0, n = 1;
                        while (pos < chunkFloats && n > 0 && !Cancel)
                        {
                            n = read(buf, pos, Math.Min(step, chunkFloats - pos));
                            if (n > 0) pos += n;
                        }
                        pos -= pos % Channels;
                        if (pos <= 0) break;
                        if (pos < chunkFloats) Array.Resize(ref buf, pos);
                        Chunks.Enqueue(buf);
                        Interlocked.Increment(ref Queued);
                        if (pos < chunkFloats) break;     // end of file
                    }
                }
                catch (Exception e) { Error = e.Message; }
                finally
                {
                    try { file?.Dispose(); } catch { }
                    Done = true;
                }
            }
        }

        // Minimal streaming WAV reader (PCM 8/16/24/32 bit and 32 bit float).
        class WavStream : IDisposable
        {
            readonly BinaryReader _br;
            readonly int _tag, _bits;
            long _left;
            public int Channels, Rate;

            public WavStream(string path)
            {
                _br = new BinaryReader(File.OpenRead(path));
                if (new string(_br.ReadChars(4)) != "RIFF") throw new InvalidDataException("not a RIFF wav");
                _br.ReadInt32();
                if (new string(_br.ReadChars(4)) != "WAVE") throw new InvalidDataException("not a WAVE file");
                int tag = 0, bits = 0; bool found = false;
                while (_br.BaseStream.Position + 8 <= _br.BaseStream.Length)
                {
                    var id = new string(_br.ReadChars(4));
                    int size = _br.ReadInt32();
                    long next = _br.BaseStream.Position + size + (size & 1);
                    if (id == "fmt ")
                    {
                        tag = _br.ReadUInt16(); Channels = _br.ReadUInt16(); Rate = _br.ReadInt32();
                        _br.ReadInt32(); _br.ReadUInt16(); bits = _br.ReadUInt16();
                    }
                    else if (id == "data")
                    {
                        if (bits == 0) throw new InvalidDataException("missing fmt chunk");
                        _left = size / (bits / 8); found = true; break;
                    }
                    _br.BaseStream.Position = next;
                }
                if (!found) throw new InvalidDataException("no audio data in wav");
                _tag = tag; _bits = bits;
                if (!(bits == 8 || bits == 16 || bits == 24 || bits == 32)) throw new InvalidDataException(bits + "-bit wav not supported");
            }

            public int ReadSamples(float[] buf, int off, int count)
            {
                int n = (int)Math.Min(count, _left), i = 0;
                for (; i < n; i++)
                {
                    float s;
                    if (_tag == 3 && _bits == 32) s = _br.ReadSingle();
                    else if (_bits == 16) s = _br.ReadInt16() / 32768f;
                    else if (_bits == 8) s = (_br.ReadByte() - 128) / 128f;
                    else if (_bits == 24) { int b0 = _br.ReadByte(), b1 = _br.ReadByte(), b2 = (sbyte)_br.ReadByte(); s = ((b2 << 16) | (b1 << 8) | b0) / 8388608f; }
                    else s = _br.ReadInt32() / 2147483648f;
                    buf[off + i] = s;
                }
                _left -= n;
                return n;
            }

            public void Dispose() { _br.Dispose(); }
        }

        static AudioSource _music, _music2;
        static MusicStream _ms;
        static int _curSrc = -1;              // which source plays the current chunk, -1 = nothing yet
        static bool _queuedNext, _held, _autoPaused, _musicStarted, _musicScanned;   // _held: the next chunk is decoded and waiting in the other slot
        static AudioClip _clipA, _clipB;      // clip on source 0 / source 1
        static float _nextMuteScan;
        static readonly List<AudioSource> _muted = new List<AudioSource>();

        public static string MusicDir => Path.Combine(Paths.GameRootPath, "Mods", "Music");

        public static void MusicScan()
        {
            Directory.CreateDirectory(MusicDir);
            Tracks.Clear();
            foreach (var f in Directory.GetFiles(MusicDir))
            {
                var e = Path.GetExtension(f).ToLowerInvariant();
                if (e == ".mp3" || e == ".wav" || e == ".ogg") Tracks.Add(f);
            }
            Tracks.Sort(StringComparer.OrdinalIgnoreCase);
            _musicScanned = true;
        }

        static AudioSource MakeMusicSource(string name)
        {
            var go = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f;
            return s;
        }

        static void EnsureMusicSource()
        {
            if (_music == null) _music = MakeMusicSource("ModsMenu_Music");
            if (_music2 == null) _music2 = MakeMusicSource("ModsMenu_Music2");
        }

        static AudioSource MusicSrc(int i) => i == 0 ? _music : _music2;
        static AudioSource CurSrc() => _curSrc < 0 ? null : MusicSrc(_curSrc);

        static AudioClip ClipOn(int i) => i == 0 ? _clipA : _clipB;
        static void SetClip(int i, AudioClip c) { if (i == 0) _clipA = c; else _clipB = c; }

        static void DestroyClip(int i)
        {
            var c = ClipOn(i);
            SetClip(i, null);
            if (c != null) { try { UnityEngine.Object.Destroy(c); } catch { } }
        }

        // Stops everything and frees all decoded audio.
        static void MusicStopAll()
        {
            var ms = _ms; _ms = null;
            if (ms != null) ms.Cancel = true;
            try { if (_music != null) { _music.Stop(); _music.clip = null; } } catch { }
            try { if (_music2 != null) { _music2.Stop(); _music2.clip = null; } } catch { }
            DestroyClip(0); DestroyClip(1);
            _curSrc = -1; _queuedNext = false; _held = false;
        }

        // Plays one decoded chunk on source i. start = dsp time to begin at (0 = now).
        static AudioClip MakeChunkClip(MusicStream ms, float[] chunk, int index)
        {
            int frames = chunk.Length / ms.Channels;
            var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(Tracks[Math.Max(0, Math.Min(MusicCur, Tracks.Count - 1))]) + "_" + index, frames, ms.Channels, ms.Rate, false);
            clip.SetData(chunk, 0);
            return clip;
        }

        static int _chunkNo;

        static bool TakeChunk(MusicStream ms, out float[] chunk)
        {
            if (ms.Chunks.TryDequeue(out chunk)) { Interlocked.Decrement(ref ms.Queued); return true; }
            return false;
        }

        static void MusicPauseNow()
        {
            var s = CurSrc();
            if (s != null) s.Pause();
            if (_queuedNext)
            {
                // the scheduled chunk would start while we are paused: un-schedule it but keep the decoded clip for later
                var o = MusicSrc(1 - _curSrc);
                try { o.Stop(); o.clip = null; } catch { }
                _queuedNext = false;
                _held = true;
            }
        }

        static void MusicResumeNow()
        {
            var s = CurSrc();
            if (s != null) s.UnPause();
        }

        // ---------- playback control ----------

        public static void MusicPlay(int i)
        {
            if (Tracks.Count == 0) return;
            i = ((i % Tracks.Count) + Tracks.Count) % Tracks.Count;
            EnsureMusicSource();
            var path = Tracks[i];
            MusicStopAll();
            MusicCur = i;
            MusicPlaying = true;
            _autoPaused = false;
            _chunkNo = 0;
            NowPlaying = Path.GetFileNameWithoutExtension(path) + " (loading...)";
            var ms = new MusicStream { Index = i };
            _ms = ms;
            Task.Run(() => ms.Run(path));
        }

        public static void MusicNext(int dir = 1)
        {
            if (Tracks.Count == 0) return;
            int n = Cfg.MusicShuffle.Value && Tracks.Count > 1 && dir > 0 ? UnityEngine.Random.Range(0, Tracks.Count) : MusicCur + dir;
            if (n == MusicCur && Tracks.Count > 1) n = MusicCur + 1;
            MusicPlay(n);
        }

        public static void MusicToggle()
        {
            if (_ms == null && _curSrc < 0)
            {
                MusicPlay(MusicCur < 0 ? 0 : MusicCur);
                return;
            }
            if (MusicPlaying) { MusicPauseNow(); MusicPlaying = false; }
            else { MusicResumeNow(); MusicPlaying = true; _autoPaused = false; }
        }

        static void UpdateMusic()
        {
            if (!Cfg.MusicOn.Value)
            {
                if (_ms != null || _curSrc >= 0 || MusicPlaying) { MusicStopAll(); MusicPlaying = false; _musicStarted = false; }
                UnmuteGameMusic();
                return;
            }
            if (!_musicScanned) MusicScan();
            EnsureMusicSource();
            _music.volume = Cfg.MusicVol.Value;
            _music2.volume = Cfg.MusicVol.Value;

            if (!_musicStarted && Cfg.MusicAuto.Value && Tracks.Count > 0 && Time.unscaledTime > 25f)
            {
                _musicStarted = true;
                MusicPlay(Cfg.MusicShuffle.Value ? UnityEngine.Random.Range(0, Tracks.Count) : 0);
            }

            var ms = _ms;
            if (ms != null)
            {
                if (ms.Error != null)
                {
                    string err = ms.Error;
                    MusicStopAll();
                    NowPlaying = "Could not play track";
                    MusicPlaying = false;
                    Toast("Could not load track: " + err);
                    Plugin.Instance.Log.LogWarning("Music: " + err);
                }
                else
                {
                    try { MusicFeed(ms); }
                    catch (Exception e)
                    {
                        Plugin.Instance.Log.LogError("Music playback failed: " + e);
                        MusicStopAll();
                        NowPlaying = "Could not play track";
                        MusicPlaying = false;
                    }
                }
            }

            // menus only: pause while playing a match (cursor locked), resume in menus
            if (Cfg.MusicMenusOnly.Value)
            {
                var cs = CurSrc();
                if (!InMenu && MusicPlaying && !_autoPaused && cs != null && cs.isPlaying) { MusicPauseNow(); _autoPaused = true; }
                else if (InMenu && _autoPaused && MusicPlaying) { MusicResumeNow(); _autoPaused = false; }
            }

            bool muteNow = Cfg.MusicMuteGame.Value && MusicPlaying && !_autoPaused;
            if (muteNow && Time.unscaledTime >= _nextMuteScan)
            {
                _nextMuteScan = Time.unscaledTime + 0.4f;
                try
                {
                    foreach (var a in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                    {
                        if (a == _music || a == _music2) continue;
                        if (!IsGameMusic(a)) continue;
                        if (!a.mute) { a.mute = true; _muted.Add(a); }
                    }
                }
                catch { }
            }
            else if (!muteNow) UnmuteGameMusic();
        }

        // Starts the first chunk, schedules the next one just before the current ends, and moves on to the next track at the end.
        static void MusicFeed(MusicStream ms)
        {
            if (!MusicPlaying || _autoPaused) return;
            bool menuOk = !Cfg.MusicMenusOnly.Value || InMenu;

            if (_curSrc < 0)
            {
                if (!menuOk) return;
                if (!TakeChunk(ms, out var first)) return;
                var clip = MakeChunkClip(ms, first, _chunkNo++);
                SetClip(0, clip);
                _music.clip = clip; _music.Play();
                _curSrc = 0; _queuedNext = false;
                NowPlaying = Path.GetFileNameWithoutExtension(Tracks[MusicCur]);
                return;
            }

            var cur = MusicSrc(_curSrc);
            var curClip = ClipOn(_curSrc);
            if (curClip == null) { MusicNext(); return; }

            if (cur.isPlaying)
            {
                double remaining = (curClip.samples - cur.timeSamples) / (double)curClip.frequency;
                if (!_queuedNext && remaining < ScheduleAhead)
                {
                    int other = 1 - _curSrc;
                    AudioClip clip = null;
                    if (_held && ClipOn(other) != null) { clip = ClipOn(other); _held = false; }
                    else if (TakeChunk(ms, out var chunk))
                    {
                        DestroyClip(other);
                        clip = MakeChunkClip(ms, chunk, _chunkNo++);
                        SetClip(other, clip);
                    }
                    if (clip != null)
                    {
                        var o = MusicSrc(other);
                        o.clip = clip;
                        o.PlayScheduled(UnityEngine.AudioSettings.dspTime + Math.Max(0.0, remaining));
                        _queuedNext = true;
                    }
                }
                return;
            }

            // the current chunk has ended (we are not paused here)
            if (_queuedNext)
            {
                int old = _curSrc;
                _curSrc = 1 - _curSrc; _queuedNext = false;
                try { MusicSrc(old).clip = null; } catch { }
                DestroyClip(old);
                return;
            }
            if (_held && ClipOn(1 - _curSrc) != null)
            {
                // a chunk was un-scheduled by a pause: play it now
                int old = _curSrc, next = 1 - _curSrc;
                var o = MusicSrc(next);
                o.clip = ClipOn(next); o.Play();
                _curSrc = next; _held = false;
                try { MusicSrc(old).clip = null; } catch { }
                DestroyClip(old);
                return;
            }
            if (TakeChunk(ms, out var late))
            {
                // the decoder fell behind or playback was resumed after a pause: continue on the same source
                int old = _curSrc;
                int next = 1 - _curSrc;
                DestroyClip(next);
                var clip = MakeChunkClip(ms, late, _chunkNo++);
                SetClip(next, clip);
                var o = MusicSrc(next);
                o.clip = clip; o.Play();
                _curSrc = next;
                try { MusicSrc(old).clip = null; } catch { }
                DestroyClip(old);
                return;
            }
            if (ms.Done && ms.Chunks.IsEmpty) MusicNext();      // track finished
        }

        static bool IsGameMusic(AudioSource a)
        {
            var g = a.outputAudioMixerGroup;
            if (g != null && g.name.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var n = a.gameObject.name.ToLowerInvariant();
            return n.Contains("music") || n.Contains("theme");
        }

        static void UnmuteGameMusic()
        {
            if (_muted.Count == 0) return;
            foreach (var a in _muted) { try { a.mute = false; } catch { } }
            _muted.Clear();
        }
    }
}
