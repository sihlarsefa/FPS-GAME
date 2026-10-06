using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>GPU (VFX Graph) efekt kimlikleri. Resources/VFX/&lt;ResourceName&gt;.vfx ile eşleşir.</summary>
    public enum GpuVfxEffect
    {
        MuzzleRifle = 0,
        MuzzlePistol = 1,
        MuzzleShotgun = 2,
        MuzzleSniper = 3,
        MuzzleSuppressed = 4,
        MuzzleMachineGun = 5,
        Impact = 6,
        Explosion = 7,
        RotorDust = 8,
        Rain = 9,
        Snow = 10,
        SmokeGrenade = 11
    }

    /// <summary>GPU efekt arka ucu. VfxGraph derlemesi (paket varsa) kendini GpuVfx.Backend'e kaydeder.</summary>
    public interface IGpuVfxBackend
    {
        /// <summary>Paket + en az bir .vfx kaynağı yüklenebiliyor mu.</summary>
        bool IsAvailable { get; }

        /// <summary>Verilen efekt için .vfx kaynağı bulundu mu.</summary>
        bool Has(GpuVfxEffect effect);

        /// <summary>Efekti oynatır. false: oynatılamadı (çağıran CPU parçacık yoluna dönmeli).</summary>
        bool TryPlay(GpuVfxEffect effect, Vector3 position, Vector3 direction, float scale, int surface, float duration);

        /// <summary>Sürekli hava efektini (Rain/Snow) kameraya bağlar; intensity 0 = kapat.</summary>
        void SetWeather(GpuVfxEffect effect, Transform follow, float intensity);

        /// <summary>Kalite kademesi 0..3 (Düşük..Ultra).</summary>
        void SetTier(int tier);
    }

    /// <summary>Bütçe sınırları (saf, test edilebilir). Kademe 0..3.</summary>
    public static class GpuVfxBudget
    {
        public static bool Enabled(int tier) => tier >= 1;

        /// <summary>Aynı anda yaşayan örnek sayısı üst sınırı (efekt başına havuz).</summary>
        public static int MaxInstances(GpuVfxEffect effect, int tier)
        {
            tier = Mathf.Clamp(tier, 0, 3);
            if (tier == 0) return 0;
            int baseCount;
            switch (effect)
            {
                case GpuVfxEffect.Impact: baseCount = 16; break;
                case GpuVfxEffect.Explosion: baseCount = 2; break;
                case GpuVfxEffect.SmokeGrenade: baseCount = 3; break;
                case GpuVfxEffect.RotorDust: baseCount = 2; break;
                case GpuVfxEffect.Rain:
                case GpuVfxEffect.Snow: baseCount = 1; break;
                default: baseCount = 6; break;
            }
            if (baseCount == 1) return 1;
            return Mathf.Max(1, baseCount * (tier == 1 ? 1 : tier == 2 ? 2 : 3) / 2);
        }

        /// <summary>Örnek başına parçacık kapasitesi önerisi (VFX Graph 'Capacity').</summary>
        public static int Capacity(GpuVfxEffect effect, int tier)
        {
            tier = Mathf.Clamp(tier, 0, 3);
            if (tier == 0) return 0;
            int[] mul = { 0, 1, 2, 4 };
            int baseCap;
            switch (effect)
            {
                case GpuVfxEffect.Impact: baseCap = 64; break;
                case GpuVfxEffect.Explosion: baseCap = 1500; break;
                case GpuVfxEffect.SmokeGrenade: baseCap = 1000; break;
                case GpuVfxEffect.RotorDust: baseCap = 1000; break;
                case GpuVfxEffect.Rain: baseCap = 5000; break;
                case GpuVfxEffect.Snow: baseCap = 4000; break;
                default: baseCap = 128; break;
            }
            return baseCap * mul[tier];
        }

        /// <summary>Resources/VFX altındaki dosya adı.</summary>
        public static string ResourceName(GpuVfxEffect effect)
        {
            return "VFX_" + effect;
        }
    }

    /// <summary>GameVfx'in GPU efektleri tercih etmesi için statik kanca (paket bağımlılığı yok).</summary>
    public static class GpuVfx
    {
        public static IGpuVfxBackend Backend;
        private static int _tier = 2;

        public static int Tier
        {
            get => _tier;
            set { _tier = Mathf.Clamp(value, 0, 3); Backend?.SetTier(_tier); }
        }

        public static bool Available
        {
            get
            {
                try { return Backend != null && GpuVfxBudget.Enabled(_tier) && Backend.IsAvailable; }
                catch { return false; }
            }
        }

        /// <summary>true dönerse GPU efekti oynatıldı; false ise çağıran CPU yoluna devam etmeli.</summary>
        public static bool TryPlay(GpuVfxEffect effect, Vector3 position, Vector3 direction, float scale = 1f, int surface = 0, float duration = 0f)
        {
            if (!Available) return false;
            try { return Backend.Has(effect) && Backend.TryPlay(effect, position, direction, scale, surface, duration); }
            catch (System.Exception e) { Debug.LogWarning("[GpuVfx] " + e.Message); return false; }
        }

        public static void SetWeather(GpuVfxEffect effect, Transform follow, float intensity)
        {
            if (!Available) return;
            try { Backend.SetWeather(effect, follow, intensity); } catch { }
        }
    }
}
