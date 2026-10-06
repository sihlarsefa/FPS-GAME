using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>Bölgenin yankı karakteri.</summary>
    public enum SpaceKind { OpenValley = 0, Forest = 1, VillageStreet = 2, Indoor = 3 }

    /// <summary>Bölge yankı sahnesi (saf veri): AudioMix/reverb göndermesi bu değerleri okur.</summary>
    public struct ReverbScene
    {
        public SpaceKind Kind;
        public float PreDelay;     // sn
        public float DecayTime;    // sn (RT60)
        public float Wet;          // 0..1 reverb gönderme seviyesi
        public float HfDamping;    // 0..1 (1 = tizler çok emilir)
        public float EchoSpacing;  // sn: ayrık yankı aralığı (0 = yaygın kuyruk)
        public float EchoGain;     // 0..1 ayrık yankı seviyesi
    }

    /// <summary>Ses rolü: stereo genişliğini belirler.</summary>
    public enum SpatialRole { AmbientBed = 0, Threat = 1, Gunshot = 2, DistantBattle = 3, Ui = 4 }

    /// <summary>
    /// Biyom ses sahnesi saf kuralları: bölge sınıflama + yankı karakteri, rol bazlı stereo genişlik,
    /// aktif maçta uzak çatışma katmanı (400-900 m, menzile göre alçak geçiren). Unity nesnesi yok.
    /// </summary>
    public static class BiomeAcousticsRules
    {
        public const float FarBattleMinDistance = 400f;
        public const float FarBattleMaxDistance = 900f;
        public const float SabineK = 0.161f;
        public const float DefaultAbsorption = 0.25f;

        /// <summary>
        /// Bölge sınıfı. indoorScore: AcousticsMath.IndoorScore (&gt;=0.5 iç mekân). openness 0..1 (gökyüzü/ufuk açıklığı),
        /// treeDensity 0..1, buildingDensity 0..1.
        /// </summary>
        public static SpaceKind Classify(float indoorScore, float openness, float treeDensity, float buildingDensity)
        {
            if (indoorScore >= 0.5f) return SpaceKind.Indoor;
            if (buildingDensity >= 0.35f && buildingDensity >= treeDensity) return SpaceKind.VillageStreet;
            if (treeDensity >= 0.4f && openness < 0.7f) return SpaceKind.Forest;
            return SpaceKind.OpenValley;
        }

        /// <summary>Dış mekân sahnesi: vadi uzun seyrek yankı, orman kısa yumuşak emilim, köy orta slapback.</summary>
        public static ReverbScene Outdoor(SpaceKind kind)
        {
            switch (kind)
            {
                case SpaceKind.Forest:
                    return new ReverbScene { Kind = kind, PreDelay = 0.015f, DecayTime = 0.45f, Wet = 0.18f, HfDamping = 0.8f, EchoSpacing = 0f, EchoGain = 0.05f };
                case SpaceKind.VillageStreet:
                    return new ReverbScene { Kind = kind, PreDelay = 0.04f, DecayTime = 0.9f, Wet = 0.3f, HfDamping = 0.45f, EchoSpacing = 0.12f, EchoGain = 0.4f };
                default:
                    return new ReverbScene { Kind = SpaceKind.OpenValley, PreDelay = 0.12f, DecayTime = 2.6f, Wet = 0.22f, HfDamping = 0.5f, EchoSpacing = 0.9f, EchoGain = 0.3f };
            }
        }

        /// <summary>
        /// İç mekân sahnesi oda boyutuna göre (Sabine: RT60 = 0,161·V/(S·a)). Boyutlar metre; geçersizse küçük oda varsayılır.
        /// </summary>
        public static ReverbScene Indoor(float width, float depth, float height)
        {
            var w = Mathf.Clamp(width <= 0f ? 4f : width, 1.5f, 40f);
            var d = Mathf.Clamp(depth <= 0f ? 4f : depth, 1.5f, 40f);
            var h = Mathf.Clamp(height <= 0f ? 2.6f : height, 2f, 12f);
            var vol = w * d * h;
            var surf = 2f * (w * d + w * h + d * h);
            var rt = Mathf.Clamp(SabineK * vol / (surf * DefaultAbsorption), 0.2f, 3.5f);
            var size01 = Mathf.Clamp01((Mathf.Pow(vol, 1f / 3f) - 2f) / 10f);
            return new ReverbScene
            {
                Kind = SpaceKind.Indoor,
                PreDelay = Mathf.Clamp(AcousticsMath.Delay(Mathf.Min(w, d)), 0.005f, 0.06f),
                DecayTime = rt,
                Wet = Mathf.Lerp(0.45f, 0.3f, size01),
                HfDamping = 0.35f,
                EchoSpacing = AcousticsMath.SlapbackDelay(Mathf.Min(w, d) * 0.5f),
                EchoGain = 0.35f
            };
        }

        /// <summary>İki sahne arasında yumuşak geçiş (t 0..1).</summary>
        public static ReverbScene Blend(ReverbScene a, ReverbScene b, float t)
        {
            t = Mathf.Clamp01(t);
            return new ReverbScene
            {
                Kind = t < 0.5f ? a.Kind : b.Kind,
                PreDelay = Mathf.Lerp(a.PreDelay, b.PreDelay, t),
                DecayTime = Mathf.Lerp(a.DecayTime, b.DecayTime, t),
                Wet = Mathf.Lerp(a.Wet, b.Wet, t),
                HfDamping = Mathf.Lerp(a.HfDamping, b.HfDamping, t),
                EchoSpacing = Mathf.Lerp(a.EchoSpacing, b.EchoSpacing, t),
                EchoGain = Mathf.Lerp(a.EchoGain, b.EchoGain, t)
            };
        }

        /// <summary>AudioSource.spread (derece): yataklar geniş, tehdit sesleri (adım/silah) nokta.</summary>
        public static float SpreadDegrees(SpatialRole role, float distance)
        {
            switch (role)
            {
                case SpatialRole.AmbientBed: return 180f;
                case SpatialRole.Ui: return 0f;
                case SpatialRole.Threat: return 0f;
                case SpatialRole.Gunshot: return 0f;
                default: // uzak çatışma: mesafeyle hafif genişler ama yönü korur
                    return Mathf.Clamp(Mathf.Lerp(5f, 30f, Mathf.InverseLerp(FarBattleMinDistance, FarBattleMaxDistance, distance)), 5f, 30f);
            }
        }

        /// <summary>Yakın alan (spatialBlend) 1 = tam 3B; yataklar kısmen 2B.</summary>
        public static float SpatialBlend(SpatialRole role)
        {
            switch (role)
            {
                case SpatialRole.AmbientBed: return 0.35f;
                case SpatialRole.Ui: return 0f;
                default: return 1f;
            }
        }

        /// <summary>Uzak çatışma katmanı bu olay için çalınmalı mı: aktif maç + gerçek olay + 400-900 m.</summary>
        public static bool ShouldPlayFarBattle(bool matchActive, bool realEvent, float distance)
        {
            if (!matchActive || !realEvent || float.IsNaN(distance)) return false;
            return distance >= FarBattleMinDistance && distance <= FarBattleMaxDistance;
        }

        /// <summary>Menzile göre alçak geçiren (Hz): 400 m ~4,5 kHz → 900 m ~1,2 kHz; iç mekânda ayrıca kısılır.</summary>
        public static float FarBattleLowpassHz(float distance, bool indoor)
        {
            var t = Mathf.Clamp01((distance - FarBattleMinDistance) / (FarBattleMaxDistance - FarBattleMinDistance));
            var hz = Mathf.Lerp(4500f, 1200f, t);
            return indoor ? hz * 0.5f : hz;
        }

        /// <summary>Uzak çatışma seviyesi (0..~0.4): mesafeyle düşer; sönüm 900 m'de ~0,12.</summary>
        public static float FarBattleVolume(float distance, bool indoor)
        {
            if (distance < FarBattleMinDistance || distance > FarBattleMaxDistance) return 0f;
            var v = 0.4f * FarBattleMinDistance / distance;
            return indoor ? v * 0.4f : v;
        }
    }
}
