using System;
using System.IO;

namespace Project.Infrastructure.Audio.Music
{
    /// <summary>Üretilen menü temasını persistentDataPath/Music altında 16-bit WAV olarak önbellekler.</summary>
    public static class MusicCache
    {
        public static string Directory => Path.Combine(UnityEngine.Application.persistentDataPath, "Music");

        public static string FileName => "harekat_menu_theme_v" + MusicScore.Version + ".wav";

        /// <summary>Önbellekten okur; yoksa/bozuksa üretip yazar. İş parçacığından çağrılabilir (dizin ana iş parçacığında alınmalı).</summary>
        public static StereoBuffer LoadOrRender(string directory)
        {
            var expected = (int)Math.Round(MusicScore.LoopSeconds * MusicSynth.SampleRate);
            var path = Path.Combine(directory, FileName);
            try
            {
                if (File.Exists(path))
                {
                    var cached = ReadWav(File.ReadAllBytes(path));
                    if (cached != null && cached.Frames == expected && cached.SampleRate == MusicSynth.SampleRate)
                        return cached;
                }
            }
            catch (Exception) { /* bozuk önbellek: yeniden üret */ }

            var rendered = MusicSynth.RenderMenuLoop();
            try
            {
                System.IO.Directory.CreateDirectory(directory);
                var tmp = path + ".tmp";
                File.WriteAllBytes(tmp, WriteWav(rendered));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception) { /* yazılamıyorsa bellek içi çalışmaya devam */ }
            return rendered;
        }

        public static byte[] WriteWav(StereoBuffer b)
        {
            var frames = b.Frames;
            var dataBytes = frames * 4;
            using (var ms = new MemoryStream(44 + dataBytes))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)2);
                w.Write(b.SampleRate);
                w.Write(b.SampleRate * 4);
                w.Write((short)4);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                for (var i = 0; i < frames; i++)
                {
                    w.Write(ToShort(b.Left[i]));
                    w.Write(ToShort(b.Right[i]));
                }
                return ms.ToArray();
            }
        }

        public static StereoBuffer ReadWav(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44) return null;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (new string(r.ReadChars(4)) != "RIFF") return null;
                r.ReadInt32();
                if (new string(r.ReadChars(4)) != "WAVE") return null;
                if (new string(r.ReadChars(4)) != "fmt ") return null;
                var fmtSize = r.ReadInt32();
                var tag = r.ReadInt16();
                var ch = r.ReadInt16();
                var rate = r.ReadInt32();
                r.ReadInt32(); r.ReadInt16();
                var bits = r.ReadInt16();
                if (tag != 1 || ch != 2 || bits != 16) return null;
                r.ReadBytes(fmtSize - 16);
                if (new string(r.ReadChars(4)) != "data") return null;
                var size = r.ReadInt32();
                var frames = Math.Min(size, bytes.Length - (int)r.BaseStream.Position) / 4;
                var res = new StereoBuffer { SampleRate = rate, Left = new float[frames], Right = new float[frames] };
                for (var i = 0; i < frames; i++)
                {
                    res.Left[i] = r.ReadInt16() / 32768f;
                    res.Right[i] = r.ReadInt16() / 32768f;
                }
                return res;
            }
        }

        private static short ToShort(float v)
        {
            if (v > 1f) v = 1f; else if (v < -1f) v = -1f;
            return (short)Math.Round(v * 32767f);
        }
    }
}
