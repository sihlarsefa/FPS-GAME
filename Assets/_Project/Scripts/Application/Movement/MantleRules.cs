using System;

namespace Project.Application.Movement
{
    public enum MantleKind
    {
        /// <summary>Alçak engel: koşu momentumunu koruyarak aşma (vault).</summary>
        Vault = 0,
        /// <summary>Orta engel: elle tutunup çıkma (mantle).</summary>
        Mantle = 1,
        /// <summary>Yüksek engel: kollarla çekilme (climb), yavaş ve yorucu.</summary>
        Climb = 2,
    }

    /// <summary>Tırmanma planı: tür, süre, stamina bedeli, bitişte ileri hız.</summary>
    public readonly struct MantlePlan
    {
        public MantleKind Kind { get; }
        public float Duration { get; }
        public float StaminaCost { get; }
        public float ExitSpeed { get; }

        public MantlePlan(MantleKind kind, float duration, float staminaCost, float exitSpeed)
        {
            Kind = kind;
            Duration = duration;
            StaminaCost = staminaCost;
            ExitSpeed = exitSpeed;
        }
    }

    /// <summary>
    /// Engel aşma kuralları. Apex/CoD/Battlefield ortak desen: bel altı (~0,9 m) engel koşarken vault (0,4-0,5 s, hız korunur),
    /// bel üstü mantle (0,6-0,8 s), omuz hizası (>1,05 m) climb (0,9-1,2 s, yavaş). Hızlı yaklaşım süreyi %20'ye kadar kısaltır.
    /// </summary>
    public static class MantleRules
    {
        public const float VaultMaxHeight = 0.9f;
        public const float MantleMaxHeight = 1.05f;
        public const float ClimbMaxHeight = 1.4f;

        private static float C01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;

        public static MantleKind Classify(float height)
        {
            if (height <= VaultMaxHeight) return MantleKind.Vault;
            if (height <= MantleMaxHeight) return MantleKind.Mantle;
            return MantleKind.Climb;
        }

        /// <summary>Tür için taban süre (s) ve yükseklikle ince ayar.</summary>
        public static float BaseDuration(MantleKind kind, float height)
        {
            var h = float.IsNaN(height) ? 0f : height;
            switch (kind)
            {
                case MantleKind.Vault: return 0.38f + 0.14f * C01((h - 0.4f) / 0.5f);
                case MantleKind.Mantle: return 0.62f + 0.14f * C01((h - 0.9f) / 0.15f);
                default: return 0.9f + 0.3f * C01((h - 1.05f) / 0.35f);
            }
        }

        /// <summary>
        /// Plan üretir. <paramref name="entrySpeed"/> yaklaşma hızı (m/s), <paramref name="sprintSpeed"/> referans,
        /// <paramref name="burden"/> yük 0..1, <paramref name="exhausted"/> tükenmişlik süreyi uzatır.
        /// </summary>
        public static MantlePlan Plan(float height, float entrySpeed, float sprintSpeed, float burden, bool exhausted)
        {
            var kind = Classify(height);
            var run = C01(entrySpeed / Math.Max(0.1f, sprintSpeed));
            // Hızlı yaklaşan vault/mantle'ı %20'ye kadar hızlı yapar; climb'te etki yarısı (dururken tutunma).
            var speedGain = kind == MantleKind.Climb ? 0.10f : 0.20f;
            var duration = BaseDuration(kind, height) * (1f - speedGain * run) * (1f + 0.35f * C01(burden)) * (exhausted ? 1.2f : 1f);

            float cost;
            switch (kind)
            {
                case MantleKind.Vault: cost = 7f; break;
                case MantleKind.Mantle: cost = 11f; break;
                default: cost = 16f + 10f * C01((height - 1.05f) / 0.35f); break;
            }

            // Vault momentumu korur (koşu hızının %35'i, en çok 4 m/s); mantle/climb durağan çıkar.
            float exit;
            switch (kind)
            {
                case MantleKind.Vault: exit = Math.Min(4f, entrySpeed * 0.35f); break;
                case MantleKind.Mantle: exit = 1.5f; break;
                default: exit = 0.8f; break;
            }

            return new MantlePlan(kind, duration, cost, Math.Max(0f, exit));
        }

        /// <summary>Tırmanma için gereken asgari yaklaşma hızı: climb durağan başlayabilir, vault yürüyüşten başlayabilir.</summary>
        public static bool ApproachAllowed(MantleKind kind, float forwardInput) => forwardInput > (kind == MantleKind.Climb ? 0.1f : 0.3f);

        /// <summary>Tırmanma sırasında silah kullanımı: vault'ta ateş yok; bitişte kalkış süresi (SprintToFireTimer).</summary>
        public static SprintExitKind WeaponExitKind(MantleKind kind) => kind == MantleKind.Vault ? SprintExitKind.Slide : SprintExitKind.Mantle;
    }
}
