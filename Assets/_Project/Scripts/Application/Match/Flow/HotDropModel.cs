using System;
using System.Collections.Generic;

namespace Project.Application.Match.Flow
{
    public enum DropHeat
    {
        /// <summary>Beklenen takım ≤ 0.5: uzak, güvenli ama yavaş gelişim.</summary>
        Cold = 0,
        Warm = 1,
        Hot = 2,
        /// <summary>Kapasitesinin ~2 katı takım: ilk dakika çatışması kesin.</summary>
        Inferno = 3
    }

    public readonly struct DropPoi
    {
        public string Id { get; }
        public float X { get; }
        public float Z { get; }

        /// <summary>Ganimet çekiciliği (örn. bölgedeki bina sayısı × kademe değeri); ≥ 0.</summary>
        public float LootValue { get; }

        /// <summary>Savaşmadan paylaşılabilecek takım sayısı (bina/bölge büyüklüğü).</summary>
        public float Capacity { get; }

        public DropPoi(string id, float x, float z, float lootValue, float capacity)
        {
            Id = id;
            X = x;
            Z = z;
            LootValue = lootValue;
            Capacity = capacity;
        }
    }

    public readonly struct DropContention
    {
        public int PoiIndex { get; }
        public float ExpectedSquads { get; }
        public float Contention { get; }
        public DropHeat Heat { get; }

        public DropContention(int poiIndex, float expectedSquads, float contention, DropHeat heat)
        {
            PoiIndex = poiIndex;
            ExpectedSquads = expectedSquads;
            Contention = contention;
            Heat = heat;
        }
    }

    /// <summary>
    /// Sıcak iniş noktası tahmini (saf). Warzone/PUBG'de takımlar uçak hattına yakın ve ganimeti zengin
    /// noktalara yığılır; çekicilik ≈ değer × exp(−hat uzaklığı / ölçek), süzülme menzili dışı sıfır.
    /// Beklenen takım sayısı çekiciliklerin normalleştirilmiş payıdır; kapasiteye bölünerek "rekabet" bulunur.
    /// Harita tasarımcısı sonucu görüp POI değerini/kapasitesini veya uçak rotası kurallarını ayarlar.
    /// </summary>
    public static class HotDropModel
    {
        /// <summary>Atlayış sonrası yatay süzülme menzili (m): serbest düşüş + paraşüt, PUBG ≈ 1–2 km.</summary>
        public const float DefaultGlideRange = 1100f;

        /// <summary>Hat uzaklığı ölçeği (m): çekicilik her bu kadar mesafede e kat azalır.</summary>
        public const float DefaultDistanceScale = 450f;

        public static float DistanceToRoute(float x, float z, float ax, float az, float bx, float bz)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var lenSq = dx * dx + dz * dz;
            if (lenSq < 1e-6f)
            {
                var ex = x - ax;
                var ez = z - az;
                return (float)Math.Sqrt(ex * ex + ez * ez);
            }

            var t = ((x - ax) * dx + (z - az) * dz) / lenSq;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var px = ax + dx * t - x;
            var pz = az + dz * t - z;
            return (float)Math.Sqrt(px * px + pz * pz);
        }

        public static DropContention[] Evaluate(IReadOnlyList<DropPoi> pois, float routeAx, float routeAz,
            float routeBx, float routeBz, int squadCount, float glideRange, float distanceScale)
        {
            var count = pois != null ? pois.Count : 0;
            var result = new DropContention[count];
            if (count == 0)
                return result;

            if (!(glideRange > 0f))
                glideRange = DefaultGlideRange;
            if (!(distanceScale > 0f))
                distanceScale = DefaultDistanceScale;
            if (squadCount < 0)
                squadCount = 0;

            var attraction = new float[count];
            var sum = 0f;
            for (var i = 0; i < count; i++)
            {
                var p = pois[i];
                var d = DistanceToRoute(p.X, p.Z, routeAx, routeAz, routeBx, routeBz);
                if (d > glideRange || !(p.LootValue > 0f))
                    continue;

                attraction[i] = p.LootValue * (float)Math.Exp(-d / distanceScale);
                sum += attraction[i];
            }

            for (var i = 0; i < count; i++)
            {
                var expected = sum > 0f ? squadCount * attraction[i] / sum : 0f;
                var capacity = pois[i].Capacity > 0.01f ? pois[i].Capacity : 1f;
                var contention = expected / capacity;
                result[i] = new DropContention(i, expected, contention, Classify(contention));
            }

            return result;
        }

        public static DropHeat Classify(float contention)
        {
            if (contention >= 2f) return DropHeat.Inferno;
            if (contention >= 1f) return DropHeat.Hot;
            if (contention >= 0.5f) return DropHeat.Warm;
            return DropHeat.Cold;
        }

        /// <summary>
        /// Ganimet kalite bölgesi: sıcak noktada kademe bir basamak yükselir (risk/ödül), soğukta aynı kalır.
        /// Askeri kademe tavandır; Low (0) kademe hiçbir zaman yükseltilmez (tarlalar boş kalmalı).
        /// </summary>
        public static int PromoteTier(int tier, DropHeat heat)
        {
            if (tier < 0) tier = 0;
            if (tier > 3) tier = 3;
            if (tier == 0)
                return 0;
            return heat >= DropHeat.Hot && tier < 3 ? tier + 1 : tier;
        }
    }
}
