using System;
using System.Collections.Generic;

namespace Project.Application.Services
{
    /// <summary>
    /// "Konvoy Koruma" kuralları (saf mantık, Unity'siz): 3 ikmal kamyonu, savunma (tim 0) kamyonları yol güzergâhının
    /// sonuna ulaştırır; saldırı (tim 1) pusu kurar. En az bir kamyon varırsa savunma, tüm kamyonlar yok olursa ya da
    /// savunmacıların hepsi şehit düşerse saldırı kazanır. Süre dolarsa konvoy geciktirildiği için saldırı kazanır.
    /// </summary>
    public sealed class ConvoyRules
    {
        public const int DefenderTeam = 0;
        public const int AttackerTeam = 1;
        public const int TruckCount = 3;
        public const float TruckMaxHp = 1500f;
        public const float RespawnWaveSeconds = 25f;
        public const float DefaultDurationSeconds = 480f;
        public const float TruckSpeed = 4.5f;
        public const float TruckSpacing = 14f;

        /// <summary>Pusudaki bir saldırgan askerin menzildeki kamyona verdiği soyut hasar (sn başına).</summary>
        public const float AmbushDpsPerAttacker = 5f;
        public const float AmbushRange = 45f;

        public enum TruckState { Rolling, Arrived, Destroyed }

        private readonly TruckState[] _state;
        private bool _defendersSeen;

        public ConvoyRules(int truckCount = TruckCount, float durationSeconds = DefaultDurationSeconds)
        {
            _state = new TruckState[Math.Max(1, truckCount)];
            DurationSeconds = Math.Max(1f, durationSeconds);
        }

        public int Trucks => _state.Length;
        public float DurationSeconds { get; }
        public float Elapsed { get; private set; }
        public bool IsOver { get; private set; }

        /// <summary>Kazanan tim (-1 = henüz bitmedi).</summary>
        public int WinnerTeam { get; private set; } = -1;

        public float TimeRemaining => Math.Max(0f, DurationSeconds - Elapsed);
        public int Arrived { get; private set; }
        public int Destroyed { get; private set; }
        public int Rolling => _state.Length - Arrived - Destroyed;

        public TruckState GetState(int index) => index >= 0 && index < _state.Length ? _state[index] : TruckState.Destroyed;

        /// <summary>Kamyon varış noktasına ulaştı; ilk varışta savunma kazanır.</summary>
        public void MarkArrived(int index)
        {
            if (!SetState(index, TruckState.Arrived))
                return;

            Arrived++;
            Finish(DefenderTeam);
        }

        /// <summary>Kamyon yok oldu; hepsi yok olduysa (hiçbiri varmadıysa) saldırı kazanır.</summary>
        public void MarkDestroyed(int index)
        {
            if (!SetState(index, TruckState.Destroyed))
                return;

            Destroyed++;
            if (Destroyed >= _state.Length)
                Finish(AttackerTeam);
        }

        /// <summary>Canlı savunmacı sayısı; ilk kez görüldükten sonra 0'a düşerse saldırı kazanır.</summary>
        public void NotifyDefendersAlive(int alive)
        {
            if (alive > 0)
            {
                _defendersSeen = true;
                return;
            }

            if (_defendersSeen)
                Finish(AttackerTeam);
        }

        /// <summary>Süreyi ilerletir; süre dolarsa saldırı kazanır.</summary>
        public void Tick(float deltaTime)
        {
            if (IsOver || !(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return;

            Elapsed += deltaTime;
            if (Elapsed >= DurationSeconds)
                Finish(AttackerTeam);
        }

        private bool SetState(int index, TruckState state)
        {
            if (IsOver || index < 0 || index >= _state.Length || _state[index] != TruckState.Rolling)
                return false;

            _state[index] = state;
            return true;
        }

        private void Finish(int winner)
        {
            if (IsOver)
                return;

            IsOver = true;
            WinnerTeam = winner;
        }

        /// <summary>Kamyona gelen hasar: savunma timi (dost ateşi) kamyona zarar veremez; geçersiz değerler 0'dır.</summary>
        public static float TruckDamage(float rawDamage, int attackerTeam)
        {
            if (attackerTeam == DefenderTeam || float.IsNaN(rawDamage) || rawDamage <= 0f)
                return 0f;
            return rawDamage;
        }

        /// <summary>Menzildeki saldırgan sayısından soyut pusu hasarı (bu kare için).</summary>
        public static float AmbushDamage(int attackersInRange, float deltaTime)
        {
            if (attackersInRange <= 0 || !(deltaTime > 0f))
                return 0f;
            return attackersInRange * AmbushDpsPerAttacker * deltaTime;
        }
    }

    /// <summary>Düzlemde (x,z) kırık çizgi güzergâhı: uzunluk, mesafeye göre örnekleme ve kesit alma.</summary>
    public static class ConvoyRoute
    {
        public static float Length(IReadOnlyList<float> xs, IReadOnlyList<float> zs)
        {
            var count = Math.Min(xs?.Count ?? 0, zs?.Count ?? 0);
            var total = 0f;
            for (var i = 1; i < count; i++)
                total += Dist(xs[i - 1], zs[i - 1], xs[i], zs[i]);
            return total;
        }

        /// <summary>Başlangıçtan <paramref name="distance"/> metre sonraki nokta ve yönü (derece, +z=0, +x=90). Aralık dışı uçlara sıkıştırılır.</summary>
        public static bool Sample(IReadOnlyList<float> xs, IReadOnlyList<float> zs, float distance, out float x, out float z, out float yawDegrees)
        {
            x = z = yawDegrees = 0f;
            var count = Math.Min(xs?.Count ?? 0, zs?.Count ?? 0);
            if (count == 0)
                return false;
            if (count == 1)
            {
                x = xs[0];
                z = zs[0];
                return true;
            }

            if (float.IsNaN(distance) || distance < 0f)
                distance = 0f;

            var remaining = distance;
            for (var i = 1; i < count; i++)
            {
                var seg = Dist(xs[i - 1], zs[i - 1], xs[i], zs[i]);
                var last = i == count - 1;
                if (remaining <= seg || last)
                {
                    var t = seg > 1e-5f ? Math.Min(1f, remaining / seg) : 1f;
                    x = xs[i - 1] + (xs[i] - xs[i - 1]) * t;
                    z = zs[i - 1] + (zs[i] - zs[i - 1]) * t;
                    yawDegrees = (float)(Math.Atan2(xs[i] - xs[i - 1], zs[i] - zs[i - 1]) * 180.0 / Math.PI);
                    return true;
                }

                remaining -= seg;
            }

            return false;
        }

        /// <summary>Güzergâhın [fromFraction, toFraction] kesitini yeni noktalarla döndürür (uçlar kesit sınırlarına eklenir).</summary>
        public static void Slice(IReadOnlyList<float> xs, IReadOnlyList<float> zs, float fromFraction, float toFraction,
            List<float> outX, List<float> outZ)
        {
            outX.Clear();
            outZ.Clear();
            var total = Length(xs, zs);
            if (total <= 0f)
                return;

            var a = Math.Max(0f, Math.Min(1f, fromFraction)) * total;
            var b = Math.Max(0f, Math.Min(1f, toFraction)) * total;
            if (b <= a)
                return;

            Sample(xs, zs, a, out var sx, out var sz, out _);
            outX.Add(sx);
            outZ.Add(sz);

            var cum = 0f;
            for (var i = 1; i < xs.Count && i < zs.Count; i++)
            {
                cum += Dist(xs[i - 1], zs[i - 1], xs[i], zs[i]);
                if (cum > a && cum < b)
                {
                    outX.Add(xs[i]);
                    outZ.Add(zs[i]);
                }
            }

            Sample(xs, zs, b, out var ex, out var ez, out _);
            outX.Add(ex);
            outZ.Add(ez);
        }

        private static float Dist(float x0, float z0, float x1, float z1)
        {
            var dx = x1 - x0;
            var dz = z1 - z0;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
