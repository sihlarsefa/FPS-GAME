using System;
using UnityEngine;

namespace Project.Online.Sim
{
    /// <summary>Tek oyuncunun sıkıştırılmış anlık görüntüsü (saf veri; konum cm, açılar 0..65535 kuantize).</summary>
    public struct PlayerSnap : IEquatable<PlayerSnap>
    {
        public int Id;
        public int X, Y, Z;        // santimetre
        public ushort Yaw, Pitch;  // 0..65535 -> 0..360
        public byte Health;        // 0..255
        public byte Flags;         // eğilme, koşma, ölü vb.

        public static int ToCm(float meters) => Mathf.RoundToInt(meters * 100f);
        public static ushort ToAngle(float deg)
        {
            var n = deg % 360f;
            if (n < 0f) n += 360f;
            return (ushort)(Mathf.RoundToInt(n / 360f * 65536f) & 0xFFFF);
        }

        public static PlayerSnap From(int id, Vector3 pos, float yaw, float pitch, float health01, byte flags) => new PlayerSnap
        {
            Id = id, X = ToCm(pos.x), Y = ToCm(pos.y), Z = ToCm(pos.z),
            Yaw = ToAngle(yaw), Pitch = ToAngle(pitch),
            Health = (byte)Mathf.Clamp(Mathf.RoundToInt(health01 * 255f), 0, 255), Flags = flags
        };

        public Vector3 Position => new Vector3(X / 100f, Y / 100f, Z / 100f);
        public float YawDegrees => Yaw / 65536f * 360f;

        public bool Equals(PlayerSnap o) => Id == o.Id && X == o.X && Y == o.Y && Z == o.Z
            && Yaw == o.Yaw && Pitch == o.Pitch && Health == o.Health && Flags == o.Flags;
        public override bool Equals(object obj) => obj is PlayerSnap s && Equals(s);
        public override int GetHashCode() => Id;
    }

    /// <summary>
    /// Delta sıkıştırma: baseline'a göre yalnızca değişen alanlar, bit maskesiyle yazılır.
    /// Maske 0 ise oyuncu için yalnızca 1 bayt (+ id) gider. Bozuk veri güvenle reddedilir.
    /// </summary>
    public static class SnapshotDelta
    {
        public const byte BitPos = 1, BitYaw = 2, BitPitch = 4, BitHealth = 8, BitFlags = 16;
        private const byte AllBits = 31;
        /// <summary>Konum delta'sı sığarsa (|d| &lt;= 127 cm) 3 bayt, aksi halde 12 bayt (BitPosWide).</summary>
        private const byte BitPosWide = 32;
        public const int MaxFullBytes = 4 + 1 + 12 + 2 + 2 + 1 + 1;

        public static byte ComputeMask(in PlayerSnap b, in PlayerSnap c)
        {
            byte m = 0;
            if (b.X != c.X || b.Y != c.Y || b.Z != c.Z) m |= BitPos;
            if (b.Yaw != c.Yaw) m |= BitYaw;
            if (b.Pitch != c.Pitch) m |= BitPitch;
            if (b.Health != c.Health) m |= BitHealth;
            if (b.Flags != c.Flags) m |= BitFlags;
            return m;
        }

        /// <summary>Yazılacak bayt sayısını önceden hesaplar (bant genişliği bütçesi için).</summary>
        public static int EncodedSize(in PlayerSnap b, in PlayerSnap c)
        {
            var m = ComputeMask(b, c);
            var n = 4 + 1;
            if ((m & BitPos) != 0) n += PosFits(b, c) ? 3 : 12;
            if ((m & BitYaw) != 0) n += 2;
            if ((m & BitPitch) != 0) n += 2;
            if ((m & BitHealth) != 0) n++;
            if ((m & BitFlags) != 0) n++;
            return n;
        }

        private static bool PosFits(in PlayerSnap b, in PlayerSnap c) =>
            Math.Abs(c.X - b.X) <= 127 && Math.Abs(c.Y - b.Y) <= 127 && Math.Abs(c.Z - b.Z) <= 127;

        /// <summary>Oyuncuyu buf'a yazar. Baseline yoksa (ilk gönderim) <see cref="WriteFull"/> kullanın.</summary>
        public static void WriteDelta(byte[] buf, ref int o, in PlayerSnap b, in PlayerSnap c)
        {
            var m = ComputeMask(b, c);
            WriteI32(buf, ref o, c.Id);
            var wide = (m & BitPos) != 0 && !PosFits(b, c);
            buf[o++] = (byte)(m | (wide ? BitPosWide : 0));
            if ((m & BitPos) != 0)
            {
                if (wide) { WriteI32(buf, ref o, c.X); WriteI32(buf, ref o, c.Y); WriteI32(buf, ref o, c.Z); }
                else { buf[o++] = (byte)(sbyte)(c.X - b.X); buf[o++] = (byte)(sbyte)(c.Y - b.Y); buf[o++] = (byte)(sbyte)(c.Z - b.Z); }
            }
            if ((m & BitYaw) != 0) WriteU16(buf, ref o, c.Yaw);
            if ((m & BitPitch) != 0) WriteU16(buf, ref o, c.Pitch);
            if ((m & BitHealth) != 0) buf[o++] = c.Health;
            if ((m & BitFlags) != 0) buf[o++] = c.Flags;
        }

        /// <summary>Tüm alanları yazar (baseline sıfır/boş kabul edilir: geniş konum + tüm bitler).</summary>
        public static void WriteFull(byte[] buf, ref int o, in PlayerSnap c)
        {
            WriteI32(buf, ref o, c.Id);
            buf[o++] = (byte)(AllBits | BitPosWide);
            WriteI32(buf, ref o, c.X); WriteI32(buf, ref o, c.Y); WriteI32(buf, ref o, c.Z);
            WriteU16(buf, ref o, c.Yaw); WriteU16(buf, ref o, c.Pitch);
            buf[o++] = c.Health; buf[o++] = c.Flags;
        }

        /// <summary>
        /// Okur: baseline (alıcının bildiği son durum) üzerine uygular. hasBaseline=false ise paket
        /// geniş konum + tüm bitleri içermelidir (tam durum), aksi halde reddedilir.
        /// </summary>
        public static bool TryRead(byte[] buf, ref int o, bool hasBaseline, in PlayerSnap baseline, out PlayerSnap result)
        {
            result = default;
            if (buf == null || o < 0 || buf.Length - o < 5) return false;
            var s = hasBaseline ? baseline : default;
            s.Id = ReadI32(buf, ref o);
            var raw = buf[o++];
            if ((raw & ~(AllBits | BitPosWide)) != 0) return false;
            var wide = (raw & BitPosWide) != 0;
            var m = (byte)(raw & AllBits);
            if (hasBaseline && s.Id != baseline.Id) return false;
            if (!hasBaseline && (m != AllBits || !wide)) return false;
            if (wide && (m & BitPos) == 0) return false;

            var need = 0;
            if ((m & BitPos) != 0) need += wide ? 12 : 3;
            if ((m & BitYaw) != 0) need += 2;
            if ((m & BitPitch) != 0) need += 2;
            if ((m & BitHealth) != 0) need++;
            if ((m & BitFlags) != 0) need++;
            if (buf.Length - o < need) return false;

            if ((m & BitPos) != 0)
            {
                if (wide) { s.X = ReadI32(buf, ref o); s.Y = ReadI32(buf, ref o); s.Z = ReadI32(buf, ref o); }
                else { s.X += (sbyte)buf[o++]; s.Y += (sbyte)buf[o++]; s.Z += (sbyte)buf[o++]; }
            }
            if ((m & BitYaw) != 0) s.Yaw = ReadU16(buf, ref o);
            if ((m & BitPitch) != 0) s.Pitch = ReadU16(buf, ref o);
            if ((m & BitHealth) != 0) s.Health = buf[o++];
            if ((m & BitFlags) != 0) s.Flags = buf[o++];
            result = s;
            return true;
        }

        private static void WriteI32(byte[] b, ref int o, int v)
        {
            b[o++] = (byte)v; b[o++] = (byte)(v >> 8); b[o++] = (byte)(v >> 16); b[o++] = (byte)(v >> 24);
        }
        private static int ReadI32(byte[] b, ref int o)
        {
            var v = b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
            o += 4;
            return v;
        }
        private static void WriteU16(byte[] b, ref int o, ushort v) { b[o++] = (byte)v; b[o++] = (byte)(v >> 8); }
        private static ushort ReadU16(byte[] b, ref int o) { var v = (ushort)(b[o] | (b[o + 1] << 8)); o += 2; return v; }
    }
}
