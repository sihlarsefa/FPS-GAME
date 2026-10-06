using System;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>Far modu: kapalı / normal / karartma (kısık, dar huzmeli, kızılötesi öncesi gece sürüşü).</summary>
    public enum HeadlightMode { Kapali, Acik, Karartma }

    /// <summary>Kirpi hissi için saf matematik (Unity'siz): teker bağımsız yay/sönüm, iç mekan boğuk ses, far modu, toz parametresi.</summary>
    public static class VehicleAmbienceMath
    {
        /// <summary>Tek tekerin görsel yay-sönüm adımı (yarı örtük Euler). x=ofset(m), v=hız(m/s) döner.</summary>
        public static void SpringStep(ref float x, ref float v, float target, float stiffness, float damping, float dt)
        {
            dt = Math.Min(Math.Max(dt, 0f), 0.05f); // büyük kare atlamasında patlamayı önle
            var a = (target - x) * stiffness - v * damping;
            v += a * dt;
            x += v * dt;
        }

        /// <summary>Yay ofsetini görsel sınırda tutar (tekerlek çamurluğa girmesin).</summary>
        public static float ClampTravel(float x, float maxTravel) => x < -maxTravel ? -maxTravel : (x > maxTravel ? maxTravel : x);

        /// <summary>Far modu döngüsü: Kapalı -> Açık -> Karartma -> Kapalı.</summary>
        public static HeadlightMode NextMode(HeadlightMode m)
        {
            switch (m)
            {
                case HeadlightMode.Kapali: return HeadlightMode.Acik;
                case HeadlightMode.Acik: return HeadlightMode.Karartma;
                default: return HeadlightMode.Kapali;
            }
        }

        /// <summary>Far şiddeti çarpanı: karartmada çok kısık.</summary>
        public static float HeadlightIntensity(HeadlightMode m) => m == HeadlightMode.Acik ? 6f : (m == HeadlightMode.Karartma ? 0.9f : 0f);

        /// <summary>Far menzili: karartmada kısa.</summary>
        public static float HeadlightRange(HeadlightMode m) => m == HeadlightMode.Acik ? 48f : (m == HeadlightMode.Karartma ? 14f : 0f);

        /// <summary>Far huzme açısı: karartmada dar.</summary>
        public static float HeadlightAngle(HeadlightMode m) => m == HeadlightMode.Karartma ? 38f : 62f;

        /// <summary>Düşük-geçiren filtre kesim frekansı (Hz): içerideyken boğuk, dışarıda açık.</summary>
        public static float MuffleCutoff(bool inside, float hatchOpen01)
        {
            if (!inside) return 22000f;
            var o = hatchOpen01 < 0f ? 0f : (hatchOpen01 > 1f ? 1f : hatchOpen01);
            return 700f + o * 6000f;
        }

        /// <summary>İç mekanda dış ses seviyesi çarpanı.</summary>
        public static float InsideVolumeScale(bool inside) => inside ? 0.55f : 1f;

        /// <summary>Düşük kalite kademesinde teker tozu aralığı büyür (0..3); kademe dışı değer güvenli kırpılır.</summary>
        public static float DustIntervalScale(int tier)
        {
            if (tier <= 0) return 2.2f;
            if (tier == 1) return 1.4f;
            return 1f;
        }

        /// <summary>Teker izi parçacık ölçeği: hız ve zemin (kar daha kabarık).</summary>
        public static float TrailScale(float speedKmh, float maxKmh, bool snow)
        {
            var f = maxKmh <= 1f ? 0f : Math.Min(1f, Math.Max(0f, speedKmh / maxKmh));
            return (0.5f + 0.9f * f) * (snow ? 1.25f : 1f);
        }

        /// <summary>Boş/negatif/mutlak yay hedefini sıkışmadan görsel ofsete çevirir (m; sıkışma arttıkça teker yukarı).</summary>
        public static float CompressionTarget(float compression01, float travel)
            => (compression01 < 0f ? 0f : (compression01 > 1f ? 1f : compression01)) * travel;
    }
}
