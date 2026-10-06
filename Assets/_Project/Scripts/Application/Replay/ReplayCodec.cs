using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Project.Application.Replay
{
    /// <summary>
    /// Tekrar ikili biçimi. Düzen: "HRPL" + sürüm, başlık, oyuncu tablosu, silah tablosu, kareler, olaylar.
    /// Konum float, yön nicemlenmiş (yaw 16 bit, pitch 8 bit); tamsayılar 7-bit değişken uzunlukludur.
    /// Saf mantık: Unity'ye bağımlı değildir.
    /// </summary>
    public static class ReplayCodec
    {
        private const uint Magic = 0x4C505248; // "HRPL" (little-endian)
        private const int MaxCount = 5_000_000;

        public static byte[] ToBytes(ReplayData data, bool gzip = false)
        {
            using (var ms = new MemoryStream())
            {
                Write(data, ms, gzip);
                return ms.ToArray();
            }
        }

        public static ReplayData FromBytes(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
                return Read(ms);
        }

        /// <summary>Yazar; gzip=true ise gövde sıkıştırılır (okuyucu başlıktan otomatik algılar).</summary>
        public static void Write(ReplayData data, Stream output, bool gzip = false)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (output == null) throw new ArgumentNullException(nameof(output));

            if (gzip)
            {
                // 0x1F 0x8B gzip imzası zaten okuyucuda algılanır.
                using (var gz = new GZipStream(output, CompressionMode.Compress, true))
                    WriteRaw(data, gz);
            }
            else
            {
                WriteRaw(data, output);
            }
        }

        public static ReplayData Read(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var first = input.ReadByte();
            if (first < 0)
                throw new InvalidDataException("Tekrar dosyası boş.");

            var buffered = new MemoryStream();
            buffered.WriteByte((byte)first);
            input.CopyTo(buffered);
            buffered.Position = 0;

            if (first == 0x1F)
            {
                using (var gz = new GZipStream(buffered, CompressionMode.Decompress))
                {
                    var raw = new MemoryStream();
                    gz.CopyTo(raw);
                    raw.Position = 0;
                    return ReadRaw(raw);
                }
            }

            return ReadRaw(buffered);
        }

        // ------------------------------------------------------------------ Nicemleme

        public static ushort QuantizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f) yaw += 360f;
            var v = (int)Math.Round(yaw / 360f * 65536f);
            return (ushort)(v & 0xFFFF);
        }

        public static float DequantizeYaw(ushort q) => q * 360f / 65536f;

        public static sbyte QuantizePitch(float pitch)
        {
            if (pitch > 90f) pitch = 90f;
            if (pitch < -90f) pitch = -90f;
            return (sbyte)Math.Round(pitch / 90f * 127f);
        }

        public static float DequantizePitch(sbyte q) => q * 90f / 127f;

        // ------------------------------------------------------------------ Yazma

        private static void WriteRaw(ReplayData d, Stream s)
        {
            using (var w = new BinaryWriter(s, Encoding.UTF8, true))
            {
                w.Write(Magic);
                w.Write((ushort)d.Version);
                WriteString(w, d.MapId);
                w.Write(d.MatchSeed);
                w.Write(d.WorldSeed);
                w.Write(d.LocalPlayerId);
                w.Write(d.SampleInterval);
                w.Write(d.RecordedAtUtcTicks);

                WriteVar(w, d.Players.Count);
                for (var i = 0; i < d.Players.Count; i++)
                {
                    var p = d.Players[i];
                    WriteVar(w, p.Id);
                    WriteString(w, p.Name);
                    WriteVar(w, p.Team);
                    w.Write(p.IsBot);
                }

                WriteVar(w, d.Weapons.Count);
                for (var i = 0; i < d.Weapons.Count; i++)
                    WriteString(w, d.Weapons[i]);

                WriteVar(w, d.Frames.Count);
                for (var i = 0; i < d.Frames.Count; i++)
                {
                    var f = d.Frames[i];
                    w.Write(f.Time);
                    w.Write(f.ZoneX);
                    w.Write(f.ZoneZ);
                    w.Write(f.ZoneRadius);
                    var actors = f.Actors ?? Array.Empty<ReplayActorSample>();
                    WriteVar(w, actors.Length);
                    for (var a = 0; a < actors.Length; a++)
                    {
                        var s2 = actors[a];
                        WriteVar(w, s2.Id);
                        w.Write(s2.X);
                        w.Write(s2.Y);
                        w.Write(s2.Z);
                        w.Write(QuantizeYaw(s2.Yaw));
                        w.Write(QuantizePitch(s2.Pitch));
                        // bit0-1: duruş, bit2: hayatta, bit3: silah var
                        var flags = (byte)((s2.Stance & 3) | (s2.Alive ? 4 : 0) | (s2.Weapon >= 0 ? 8 : 0));
                        w.Write(flags);
                        if (s2.Weapon >= 0)
                            WriteVar(w, s2.Weapon);
                    }
                }

                WriteVar(w, d.Events.Count);
                for (var i = 0; i < d.Events.Count; i++)
                {
                    var e = d.Events[i];
                    w.Write(e.Time);
                    w.Write((byte)e.Type);
                    WriteVar(w, e.Actor + 1);
                    WriteVar(w, e.Target + 1);
                    w.Write(e.X);
                    w.Write(e.Y);
                    w.Write(e.Z);
                    w.Write(e.Value);
                    WriteVar(w, e.Weapon + 1);
                    w.Write(e.Flag);
                }
            }
        }

        // ------------------------------------------------------------------ Okuma

        private static ReplayData ReadRaw(Stream s)
        {
            using (var r = new BinaryReader(s, Encoding.UTF8, true))
            {
                if (r.ReadUInt32() != Magic)
                    throw new InvalidDataException("Geçersiz tekrar dosyası.");
                var version = r.ReadUInt16();
                if (version == 0 || version > ReplayData.CurrentVersion)
                    throw new InvalidDataException("Desteklenmeyen tekrar sürümü: " + version);

                var d = new ReplayData { Version = version };
                d.MapId = ReadString(r);
                d.MatchSeed = r.ReadInt32();
                d.WorldSeed = r.ReadInt32();
                d.LocalPlayerId = r.ReadInt32();
                d.SampleInterval = r.ReadSingle();
                d.RecordedAtUtcTicks = r.ReadInt64();

                var n = ReadCount(r);
                for (var i = 0; i < n; i++)
                    d.Players.Add(new ReplayPlayer { Id = ReadVar(r), Name = ReadString(r), Team = ReadVar(r), IsBot = r.ReadBoolean() });

                n = ReadCount(r);
                for (var i = 0; i < n; i++)
                    d.Weapons.Add(ReadString(r));

                n = ReadCount(r);
                for (var i = 0; i < n; i++)
                {
                    var f = new ReplayFrame { Time = r.ReadSingle(), ZoneX = r.ReadSingle(), ZoneZ = r.ReadSingle(), ZoneRadius = r.ReadSingle() };
                    var ac = ReadCount(r);
                    f.Actors = new ReplayActorSample[ac];
                    for (var a = 0; a < ac; a++)
                    {
                        var smp = new ReplayActorSample
                        {
                            Id = ReadVar(r),
                            X = r.ReadSingle(),
                            Y = r.ReadSingle(),
                            Z = r.ReadSingle(),
                            Yaw = DequantizeYaw(r.ReadUInt16()),
                            Pitch = DequantizePitch(r.ReadSByte())
                        };
                        var flags = r.ReadByte();
                        smp.Stance = (byte)(flags & 3);
                        smp.Alive = (flags & 4) != 0;
                        smp.Weapon = (flags & 8) != 0 ? ReadVar(r) : -1;
                        f.Actors[a] = smp;
                    }
                    d.Frames.Add(f);
                }

                n = ReadCount(r);
                for (var i = 0; i < n; i++)
                {
                    var e = new ReplayEvent
                    {
                        Time = r.ReadSingle(),
                        Type = (ReplayEventType)r.ReadByte(),
                        Actor = ReadVar(r) - 1,
                        Target = ReadVar(r) - 1,
                        X = r.ReadSingle(),
                        Y = r.ReadSingle(),
                        Z = r.ReadSingle(),
                        Value = r.ReadSingle(),
                        Weapon = ReadVar(r) - 1,
                        Flag = r.ReadBoolean()
                    };
                    d.Events.Add(e);
                }

                return d;
            }
        }

        /// <summary>Sayaç okur ve makul aralıkta olduğunu doğrular.</summary>
        private static int ReadCount(BinaryReader r)
        {
            var n = ReadVar(r);
            if (n < 0 || n > MaxCount)
                throw new InvalidDataException("Bozuk tekrar dosyası (sayı aralık dışı).");
            return n;
        }

        // ------------------------------------------------------------------ İlkel yardımcılar

        private static void WriteString(BinaryWriter w, string s) => w.Write(s ?? string.Empty);
        private static string ReadString(BinaryReader r) => r.ReadString();

        /// <summary>Negatif olmayan tamsayıyı 7-bit parçalarla yazar.</summary>
        public static void WriteVar(BinaryWriter w, int value)
        {
            var v = (uint)(value < 0 ? 0 : value);
            while (v >= 0x80)
            {
                w.Write((byte)(v | 0x80));
                v >>= 7;
            }
            w.Write((byte)v);
        }

        public static int ReadVar(BinaryReader r)
        {
            uint result = 0;
            var shift = 0;
            while (true)
            {
                var b = r.ReadByte();
                result |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                    break;
                shift += 7;
                if (shift > 35)
                    throw new InvalidDataException("Bozuk değişken uzunluklu sayı.");
            }
            return (int)result;
        }
    }
}
