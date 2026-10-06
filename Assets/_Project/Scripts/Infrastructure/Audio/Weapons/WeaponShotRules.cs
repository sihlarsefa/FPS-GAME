using System;

namespace Project.Infrastructure.Audio.Weapons
{
    /// <summary>Atış sınıfı (gövde/kuyruk karakteri).</summary>
    public enum ShotClass { Pistol = 0, Smg, Rifle, Sniper, Shotgun }

    /// <summary>Kuyruk mesafe kademesi.</summary>
    public enum TailDistance { Near = 0, Mid = 1, Far = 2 }

    /// <summary>Şarjör değişim foley adımı (zaman çizelgesi).</summary>
    public struct ReloadBeat
    {
        public float Time;
        public string Layer;   // FoleyNaming katman adlarıyla uyumlu
        public float Gain;
    }

    /// <summary>Saf kurallar: mesafe kademesi, kuyruk süresi/kesimi, son mermi seçimi, şarjör değişim zinciri.</summary>
    public static class WeaponShotRules
    {
        public const float NearMax = 40f;
        public const float MidMax = 150f;

        public static TailDistance Bucket(float meters)
        {
            if (meters < NearMax) return TailDistance.Near;
            return meters < MidMax ? TailDistance.Mid : TailDistance.Far;
        }

        /// <summary>Kuyruk alçak geçiren kesimi: uzaklaştıkça boğuklaşır.</summary>
        public static float TailCutoffHz(TailDistance d, bool indoor)
        {
            var hz = d == TailDistance.Near ? 6500f : d == TailDistance.Mid ? 3200f : 1400f;
            return indoor ? hz * 0.8f : hz;
        }

        /// <summary>Kuyruk süresi (sn).</summary>
        public static float TailSeconds(TailDistance d, bool indoor, ShotClass c)
        {
            var baseSec = d == TailDistance.Near ? 0.45f : d == TailDistance.Mid ? 0.9f : 1.6f;
            if (c == ShotClass.Sniper || c == ShotClass.Shotgun) baseSec *= 1.25f;
            if (c == ShotClass.Pistol) baseSec *= 0.8f;
            return indoor ? baseSec * 0.65f : baseSec;
        }

        /// <summary>Mermi sayacına göre son mermi mi (ateş sonrası kalan 0).</summary>
        public static bool IsLastRound(int roundsLeftAfterShot) => roundsLeftAfterShot <= 0;

        /// <summary>Şarjör değişim zinciri; boş şarjörde (tactical=false) sürgü/şarj kolu eklenir.</summary>
        public static ReloadBeat[] ReloadChain(bool tactical, float speed = 1f)
        {
            var s = speed <= 0.1f ? 1f : speed;
            var list = new System.Collections.Generic.List<ReloadBeat>(8)
            {
                new ReloadBeat { Time = 0f, Layer = "magrelease", Gain = 0.8f },
                new ReloadBeat { Time = 0.12f / s, Layer = "magout", Gain = 0.9f },
                new ReloadBeat { Time = 0.55f / s, Layer = "pouch", Gain = 0.6f },
                new ReloadBeat { Time = 0.95f / s, Layer = "magin", Gain = 1f },
                new ReloadBeat { Time = 1.10f / s, Layer = "slap", Gain = 0.7f },
            };
            if (!tactical)
            {
                list.Add(new ReloadBeat { Time = 1.55f / s, Layer = "chargehandle", Gain = 0.95f });
                list.Add(new ReloadBeat { Time = 1.72f / s, Layer = "bolt", Gain = 0.85f });
            }
            return list.ToArray();
        }

        public static float ChainDuration(ReloadBeat[] chain)
        {
            var t = 0f;
            if (chain == null) return t;
            for (var i = 0; i < chain.Length; i++) t = Math.Max(t, chain[i].Time);
            return t + 0.25f;
        }
    }
}
