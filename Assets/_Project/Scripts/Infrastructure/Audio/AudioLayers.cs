using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>Ortam türü (kuyruk + yankı seçimi).</summary>
    public enum AudioEnvironment { Outdoor, Indoor, Valley }

    /// <summary>C11 ses katmanları: saf mantık (mesafe çapraz geçişi, occlusion hesabı, ray bütçesi, ortam sınıflaması, ses sınırı).</summary>
    public static class AudioLayers
    {
        /// <summary>Eşzamanlı tek seferlik + döngü ses üst sınırı.</summary>
        public const int MaxConcurrentVoices = 48;

        /// <summary>Kare başına en fazla occlusion/ortam ışını.</summary>
        public const int RaysPerFrame = 6;

        public const float CrossfadeNearEnd = 50f;
        public const float CrossfadeMidStart = 40f;
        public const float CrossfadeMidEnd = 90f;
        public const float CrossfadeFarStart = 200f;
        public const float CrossfadeFarEnd = 300f;

        /// <summary>Mesafeye göre yakın/orta/uzak katman ağırlıkları (eşit güçlü çapraz geçiş; yakın+orta ve orta+uzak çakışır).</summary>
        public static void LayerWeights(float distance, out float near, out float mid, out float far)
        {
            if (!(distance >= 0f))
                distance = 0f;
            var n = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CrossfadeMidStart, CrossfadeNearEnd + 20f, distance));
            var mUp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CrossfadeMidStart, CrossfadeMidEnd, distance));
            var mDown = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CrossfadeFarStart, CrossfadeFarEnd, distance));
            var f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CrossfadeFarStart, CrossfadeFarEnd, distance));
            near = n;
            mid = Mathf.Min(mUp, mDown);
            far = f;
        }

        /// <summary>Engelleyici sayısına göre ses çarpanı (1 = engelsiz).</summary>
        public static float OcclusionVolume(int blockers)
        {
            if (blockers <= 0) return 1f;
            return Mathf.Max(0.3f, 1f - 0.25f * blockers);
        }

        /// <summary>Engelleyici sayısına göre alçak geçiren kesim frekansı (Hz); 22000 = süzgeç kapalı.</summary>
        public static float OcclusionCutoff(int blockers)
        {
            if (blockers <= 0) return 22000f;
            return Mathf.Max(900f, 6500f / blockers);
        }

        /// <summary>Tavan/yan ışın sonuçlarından ortam: tavan varsa iç mekân; tavan yok ve ≥3 yan kapalıysa vadi.</summary>
        public static AudioEnvironment Classify(bool ceilingHit, int blockedSides)
        {
            if (ceilingHit) return AudioEnvironment.Indoor;
            return blockedSides >= 3 ? AudioEnvironment.Valley : AudioEnvironment.Outdoor;
        }

        public static SoundId TailFor(AudioEnvironment env)
        {
            switch (env)
            {
                case AudioEnvironment.Indoor: return SoundId.ShotTailIndoor;
                case AudioEnvironment.Valley: return SoundId.ShotTailValley;
                default: return SoundId.ShotTailOutdoor;
            }
        }

        public static SoundId ShellDropFor(Project.Infrastructure.Vfx.SurfaceKind kind)
        {
            switch (kind)
            {
                case Project.Infrastructure.Vfx.SurfaceKind.Metal: return SoundId.ShellDropMetal;
                case Project.Infrastructure.Vfx.SurfaceKind.Concrete: return SoundId.ShellDropConcrete;
                case Project.Infrastructure.Vfx.SurfaceKind.Foliage:
                case Project.Infrastructure.Vfx.SurfaceKind.Dirt: return SoundId.ShellDropGrass;
                default: return SoundId.ShellCasing;
            }
        }

        /// <summary>Ses kimliği önceliği çarpanı: arayüz/oyuncu > silah > çevre.</summary>
        public static float PriorityWeight(SoundId id)
        {
            switch (id)
            {
                case SoundId.ShellCasing:
                case SoundId.ShellDropGrass:
                case SoundId.ShellDropConcrete:
                case SoundId.ShellDropMetal:
                case SoundId.ClothRustle:
                case SoundId.Footstep:
                case SoundId.FootstepGrass:
                case SoundId.FootstepConcrete:
                case SoundId.FootstepWood:
                case SoundId.FootstepMetal:
                case SoundId.FootstepSnow:
                case SoundId.FootstepGravel:
                case SoundId.FootstepMud:
                case SoundId.WoodCreak:
                case SoundId.GearJingle:
                    return 0.5f;
                case SoundId.ShotTailOutdoor:
                case SoundId.ShotTailIndoor:
                case SoundId.ShotTailValley:
                case SoundId.ShotMech:
                case SoundId.ShotDistantMid:
                case SoundId.ShotDistantFar:
                    return 0.7f;
                case SoundId.Explosion:
                case SoundId.Death:
                case SoundId.BulletWhiz:
                case SoundId.BulletCrack:
                    return 1.4f;
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Sınırdaysa yalnızca öncelikle ağırlıklanmış önem eşiği aşarsa kabul edilir (kabul edilen ses en zayıfı çalar).
        /// </summary>
        public static bool NeedsSteal(int activeCount) => activeCount >= MaxConcurrentVoices;

        public static float EffectiveImportance(SoundId id, float importance) => importance * PriorityWeight(id);

        /// <summary>Kare başına ray bütçesi sayacı (ana iş parçacığı).</summary>
        public struct RayBudget
        {
            private int _frame;
            private int _used;

            public bool TryConsume(int frame, int count = 1, int perFrame = RaysPerFrame)
            {
                if (frame != _frame)
                {
                    _frame = frame;
                    _used = 0;
                }

                if (_used + count > perFrame)
                    return false;
                _used += count;
                return true;
            }
        }
    }
}
