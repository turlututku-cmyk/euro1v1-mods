using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using Il2CppInterop.Runtime.Runtime;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // Plays your own mp3/wav/ogg files from Mods/Music. Local audio only.
    // Decoding is done in managed code (NLayer for mp3, NVorbis for ogg) because UnityWebRequestMultimedia
    // is stripped from this game build.
    public static partial class Features
    {
        public static readonly List<string> Tracks = new List<string>();
        public static int MusicCur = -1;
        public static bool MusicPlaying;
        public static string NowPlaying = "";

        class Decoded { public float[] Data; public int Channels, Rate; public string Error; public int Index; }

        static AudioSource _music;
        static Task<Decoded> _decode;
        static bool _autoPaused, _musicStarted, _musicScanned;
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

        static void EnsureMusicSource()
        {
            if (_music != null) return;
            var go = new GameObject("ModsMenu_Music");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _music = go.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = false;
            _music.spatialBlend = 0f;
        }

        // ---------- decoding (background thread, managed code only) ----------

        static Decoded DecodeFile(string path, int index)
        {
            var res = new Decoded { Index = index };
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".mp3")
                {
                    using (var f = new NLayer.MpegFile(path))
                    {
                        res.Channels = f.Channels; res.Rate = f.SampleRate;
                        long total = Math.Min(f.Length / 4 + 8192, (long)res.Channels * res.Rate * 1200);
                        res.Data = FillArray(total, (b, o, c) => f.ReadSamples(b, o, c), res.Channels);
                    }
                }
                else if (ext == ".ogg")
                {
                    using (var v = new NVorbis.VorbisReader(path))
                    {
                        res.Channels = v.Channels; res.Rate = v.SampleRate;
                        long total = Math.Min(v.TotalSamples * v.Channels + 8192, (long)res.Channels * res.Rate * 1200);
                        res.Data = FillArray(total, (b, o, c) => v.ReadSamples(b, o, c), res.Channels);
                    }
                }
                else
                {
                    var list = new List<float>(1 << 20);
                    DecodeWav(path, list, res);
                    res.Data = list.ToArray();
                }
                if (res.Data.Length == 0 || res.Channels <= 0) res.Error = "empty or unsupported file";
            }
            catch (Exception e) { res.Error = e.Message; }
            return res;
        }

        // Reads straight into one exactly-sized array (no List growth), keeping peak memory low.
        static float[] FillArray(long capacity, Func<float[], int, int, int> read, int channels)
        {
            var arr = new float[Math.Max(capacity, channels * 4096L)];
            var buf = new float[channels * 4096];
            int pos = 0, n;
            while ((n = read(buf, 0, buf.Length)) > 0)
            {
                if (pos + n > arr.Length) break;
                Array.Copy(buf, 0, arr, pos, n);
                pos += n;
            }
            if (pos != arr.Length) Array.Resize(ref arr, pos);
            return arr;
        }

        static void DecodeWav(string path, List<float> list, Decoded res)
        {
            using (var br = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(br.ReadChars(4)) != "RIFF") throw new InvalidDataException("not a RIFF wav");
                br.ReadInt32();
                if (new string(br.ReadChars(4)) != "WAVE") throw new InvalidDataException("not a WAVE file");
                int tag = 0, bits = 0;
                while (br.BaseStream.Position + 8 <= br.BaseStream.Length)
                {
                    var id = new string(br.ReadChars(4));
                    int size = br.ReadInt32();
                    long next = br.BaseStream.Position + size + (size & 1);
                    if (id == "fmt ")
                    {
                        tag = br.ReadUInt16(); res.Channels = br.ReadUInt16(); res.Rate = br.ReadInt32();
                        br.ReadInt32(); br.ReadUInt16(); bits = br.ReadUInt16();
                    }
                    else if (id == "data")
                    {
                        int bytes = bits / 8;
                        if (bytes == 0) throw new InvalidDataException("missing fmt chunk");
                        int count = size / bytes;
                        for (int i = 0; i < count; i++)
                        {
                            float s;
                            if (tag == 3 && bits == 32) s = br.ReadSingle();
                            else if (bits == 16) s = br.ReadInt16() / 32768f;
                            else if (bits == 8) s = (br.ReadByte() - 128) / 128f;
                            else if (bits == 24) { int b0 = br.ReadByte(), b1 = br.ReadByte(), b2 = (sbyte)br.ReadByte(); s = ((b2 << 16) | (b1 << 8) | b0) / 8388608f; }
                            else if (bits == 32) s = br.ReadInt32() / 2147483648f;
                            else throw new InvalidDataException(bits + "-bit wav not supported");
                            list.Add(s);
                        }
                        return;
                    }
                    br.BaseStream.Position = next;
                }
                throw new InvalidDataException("no audio data in wav");
            }
        }

        // ---------- playback control ----------

        public static void MusicPlay(int i)
        {
            if (Tracks.Count == 0) return;
            i = ((i % Tracks.Count) + Tracks.Count) % Tracks.Count;
            EnsureMusicSource();
            var path = Tracks[i];
            MusicCur = i;
            MusicPlaying = true;
            _autoPaused = false;
            NowPlaying = Path.GetFileNameWithoutExtension(path) + " (loading...)";
            _music.Stop();
            _decode = Task.Run(() => DecodeFile(path, i));
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
            if (_music == null || _music.clip == null)
            {
                MusicPlay(MusicCur < 0 ? 0 : MusicCur);
                return;
            }
            if (MusicPlaying) { _music.Pause(); MusicPlaying = false; }
            else { _music.UnPause(); MusicPlaying = true; _autoPaused = false; }
        }

        static void UpdateMusic()
        {
            if (!Cfg.MusicOn.Value)
            {
                if (_music != null && (_music.isPlaying || MusicPlaying)) { _music.Stop(); MusicPlaying = false; _musicStarted = false; }
                UnmuteGameMusic();
                return;
            }
            if (!_musicScanned) MusicScan();
            EnsureMusicSource();
            _music.volume = Cfg.MusicVol.Value;

            if (!_musicStarted && Cfg.MusicAuto.Value && Tracks.Count > 0 && Time.unscaledTime > 25f)
            {
                _musicStarted = true;
                MusicPlay(Cfg.MusicShuffle.Value ? UnityEngine.Random.Range(0, Tracks.Count) : 0);
            }

            if (_decode != null && _decode.IsCompleted)
            {
                var d = _decode.IsFaulted ? new Decoded { Error = _decode.Exception?.GetBaseException().Message } : _decode.Result;
                _decode = null;
                if (d.Error != null)
                {
                    NowPlaying = "Could not play track";
                    MusicPlaying = false;
                    Toast("Could not load track: " + d.Error);
                    Plugin.Instance.Log.LogWarning("Music: " + d.Error);
                }
                else if (d.Index == MusicCur)
                {
                    try
                    {
                        int frames = d.Data.Length / d.Channels;
                        var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(Tracks[MusicCur]), frames, d.Channels, d.Rate, false);
                        clip.SetData(d.Data, 0);
                        var old = _music.clip;
                        _music.clip = clip;
                        _music.Play();
                        if (old != null) UnityEngine.Object.Destroy(old);
                        NowPlaying = Path.GetFileNameWithoutExtension(Tracks[MusicCur]);
                    }
                    catch (Exception e)
                    {
                        Plugin.Instance.Log.LogError("Music clip failed: " + e);
                        NowPlaying = "Could not play track";
                        MusicPlaying = false;
                    }
                }
            }

            // menus only: pause while playing a match (cursor locked), resume in menus
            if (Cfg.MusicMenusOnly.Value)
            {
                if (!InMenu && _music.isPlaying) { _music.Pause(); _autoPaused = true; }
                else if (InMenu && _autoPaused && MusicPlaying) { _music.UnPause(); _autoPaused = false; }
            }

            // next track when the current one ends
            if (MusicPlaying && !_autoPaused && _decode == null && _music.clip != null && !_music.isPlaying)
                MusicNext();

            bool muteNow = Cfg.MusicMuteGame.Value && MusicPlaying && !_autoPaused;
            if (muteNow && Time.unscaledTime >= _nextMuteScan)
            {
                _nextMuteScan = Time.unscaledTime + 0.4f;
                try
                {
                    foreach (var a in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                    {
                        if (a == _music) continue;
                        if (!IsGameMusic(a)) continue;
                        if (!a.mute) { a.mute = true; _muted.Add(a); }
                    }
                }
                catch { }
            }
            else if (!muteNow) UnmuteGameMusic();
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
