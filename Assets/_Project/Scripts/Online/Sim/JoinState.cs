using System;
using System.Collections.Generic;

namespace Project.Online.Sim
{
    /// <summary>
    /// Maça sonradan katılan istemciye gönderilen durum özeti (saf veri): bölge fazı/aşaması/çemberler ve
    /// hayatta kalan oyuncu kimlikleri. İkili kodlama sürümlü ve sınırlıdır (bozuk/kötü niyetli veri güvenle reddedilir).
    /// </summary>
    public sealed class JoinState
    {
        public const byte Version = 1;
        public const int MaxAlive = 128;
        /// <summary>Sabit başlık: sürüm(1) + faz(1) + aşama(1) + 9 float(36) + sayı(2).</summary>
        public const int HeaderBytes = 41;

        public byte ZonePhaseIndex;
        public byte ZoneStage;
        public float StageRemaining;
        public float StageDuration;
        public float CenterX, CenterZ, Radius, Dps;
        public float NextCenterX, NextCenterZ, NextRadius;
        public readonly List<int> AliveIds = new List<int>();

        public int AliveCount => AliveIds.Count;

        public bool IsAlive(int playerId) => AliveIds.Contains(playerId);

        public byte[] Encode()
        {
            var count = Math.Min(AliveIds.Count, MaxAlive);
            var buf = new byte[HeaderBytes + count * 4];
            var o = 0;
            buf[o++] = Version;
            buf[o++] = ZonePhaseIndex;
            buf[o++] = ZoneStage;
            WriteF(buf, ref o, StageRemaining);
            WriteF(buf, ref o, StageDuration);
            WriteF(buf, ref o, CenterX);
            WriteF(buf, ref o, CenterZ);
            WriteF(buf, ref o, Radius);
            WriteF(buf, ref o, Dps);
            WriteF(buf, ref o, NextCenterX);
            WriteF(buf, ref o, NextCenterZ);
            WriteF(buf, ref o, NextRadius);
            buf[o++] = (byte)(count & 0xFF);
            buf[o++] = (byte)((count >> 8) & 0xFF);
            for (var i = 0; i < count; i++)
            {
                var v = AliveIds[i];
                buf[o++] = (byte)v;
                buf[o++] = (byte)(v >> 8);
                buf[o++] = (byte)(v >> 16);
                buf[o++] = (byte)(v >> 24);
            }

            return buf;
        }

        /// <summary>Bozuk, kısa, yanlış sürümlü, sayısı sınırı aşan veya NaN içeren veri false döner.</summary>
        public static bool TryDecode(byte[] data, out JoinState state)
        {
            state = null;
            if (data == null || data.Length < HeaderBytes || data[0] != Version)
                return false;

            var s = new JoinState();
            var o = 1;
            s.ZonePhaseIndex = data[o++];
            s.ZoneStage = data[o++];
            s.StageRemaining = ReadF(data, ref o);
            s.StageDuration = ReadF(data, ref o);
            s.CenterX = ReadF(data, ref o);
            s.CenterZ = ReadF(data, ref o);
            s.Radius = ReadF(data, ref o);
            s.Dps = ReadF(data, ref o);
            s.NextCenterX = ReadF(data, ref o);
            s.NextCenterZ = ReadF(data, ref o);
            s.NextRadius = ReadF(data, ref o);
            var count = data[o] | (data[o + 1] << 8);
            o += 2;
            if (count > MaxAlive || data.Length != HeaderBytes + count * 4)
                return false;

            if (!Finite(s.StageRemaining) || !Finite(s.StageDuration) || !Finite(s.CenterX) || !Finite(s.CenterZ)
                || !Finite(s.Radius) || !Finite(s.Dps) || !Finite(s.NextCenterX) || !Finite(s.NextCenterZ) || !Finite(s.NextRadius)
                || s.Radius < 0f || s.NextRadius < 0f || s.StageRemaining < 0f || s.StageDuration < 0f)
                return false;

            for (var i = 0; i < count; i++)
            {
                s.AliveIds.Add(data[o] | (data[o + 1] << 8) | (data[o + 2] << 16) | (data[o + 3] << 24));
                o += 4;
            }

            state = s;
            return true;
        }

        /// <summary>Kalan aşama süresini RTT/2 kadar azaltır (varış gecikmesi telafisi); 0'ın altına inmez.</summary>
        public void CompensateLatency(float oneWaySeconds)
        {
            if (oneWaySeconds > 0f && ZoneStage >= 1 && ZoneStage <= 2)
                StageRemaining = Math.Max(0f, StageRemaining - oneWaySeconds);
        }

        private static bool Finite(float f) => !(float.IsNaN(f) || float.IsInfinity(f));

        private static void WriteF(byte[] b, ref int o, float f)
        {
            var v = BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
            b[o++] = (byte)v;
            b[o++] = (byte)(v >> 8);
            b[o++] = (byte)(v >> 16);
            b[o++] = (byte)(v >> 24);
        }

        private static float ReadF(byte[] b, ref int o)
        {
            var v = b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
            o += 4;
            return BitConverter.ToSingle(BitConverter.GetBytes(v), 0);
        }
    }
}
