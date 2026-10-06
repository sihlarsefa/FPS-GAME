using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>Bot yetenek kademesi (zorluk + rütbe + bireysel farktan türer; belgeleme ve davranış anahtarı).</summary>
    public enum BotSkillTier
    {
        Acemi = 0,
        Muvazzaf = 1,
        Kidemli = 2,
        Seckin = 3
    }

    /// <summary>
    /// "Aimbot değil asker" insansı nişan/algı kuralları — saf (Physics'siz, GC'siz) ve test edilebilir.
    /// Tüm değerler skill01 (0 = acemi, 1 = seçkin) üzerinden bağlanır:
    ///  • Tepki süresi 180–450 ms (rütbe/zorluk/bireysel),
    ///  • İlk atış hatası zamanla yakınsar (üstel), hedef değişiminde yeniden açılır,
    ///  • Geri tepme kontrolü kusurludur (atış başına rastgele + seri uzadıkça bozulur),
    ///  • Hedef değiştirme gecikmesi (açıya bağlı),
    ///  • Baskı (suppression) altında isabet ve siperden çıkma isteği düşer.
    /// </summary>
    public static class BotSkill
    {
        public const float ReactionMin = 0.18f;
        public const float ReactionMax = 0.45f;

        /// <summary>Hedefe yerleşmiş (yakınsamış) hata çarpanı (eski kararlı değer).</summary>
        public const float SettledAimFactor = 0.65f;

        /// <summary>Beceri 0..1: zorluk tabanı + rütbe bonusu + bireysel sapma (individual01 0..1).</summary>
        public static float Skill01(BotDifficulty difficulty, MilitaryRank rank, float individual01)
        {
            float baseSkill;
            switch (difficulty)
            {
                case BotDifficulty.Easy: baseSkill = 0.2f; break;
                case BotDifficulty.Hard: baseSkill = 0.78f; break;
                default: baseSkill = 0.5f; break;
            }

            var rankBonus = Mathf.Clamp01((int)rank / 12f) * 0.15f;
            var individual = (Mathf.Clamp01(individual01) - 0.5f) * 0.12f;
            return Mathf.Clamp01(baseSkill + rankBonus + individual);
        }

        public static BotSkillTier TierOf(float skill01)
        {
            if (skill01 < 0.3f) return BotSkillTier.Acemi;
            if (skill01 < 0.6f) return BotSkillTier.Muvazzaf;
            if (skill01 < 0.85f) return BotSkillTier.Kidemli;
            return BotSkillTier.Seckin;
        }

        public static string TierName(BotSkillTier tier)
        {
            switch (tier)
            {
                case BotSkillTier.Acemi: return "Acemi";
                case BotSkillTier.Muvazzaf: return "Muvazzaf";
                case BotSkillTier.Kidemli: return "Kıdemli";
                default: return "Seçkin";
            }
        }

        /// <summary>
        /// Zorluğa saygılı tepki aralığı (BotDifficultyProfile ile uyumlu): Kolay 0.35–0.9, Normal 0.25–0.6, Zor 0.18–0.45 sn.
        /// Seed determinizmi: yalnız <paramref name="rng01"/> (bot başına tek Rand() çekimi) kullanılır; sıra değişmez.
        /// </summary>
        public static void ReactionRange(BotDifficulty difficulty, out float min, out float max)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy: min = 0.35f; max = 0.9f; break;
                case BotDifficulty.Hard: min = ReactionMin; max = ReactionMax; break;
                default: min = 0.25f; max = 0.6f; break;
            }
        }

        public static float ReactionSeconds(BotDifficulty difficulty, float skill01, float rng01)
        {
            ReactionRange(difficulty, out var min, out var max);
            var baseSeconds = Mathf.Lerp(max, min + 0.02f, Mathf.Clamp01(skill01));
            var jitter = Mathf.Lerp(0.92f, 1.1f, Mathf.Clamp01(rng01));
            return Mathf.Clamp(baseSeconds * jitter, min, max);
        }

        /// <summary>Tepki gecikmesi (sn): 0.18–0.45; beceri yüksek = kısa. rng01 küçük bireysel oynaklık.</summary>
        public static float ReactionSeconds(float skill01, float rng01)
        {
            var baseSeconds = Mathf.Lerp(ReactionMax, ReactionMin + 0.02f, Mathf.Clamp01(skill01));
            var jitter = Mathf.Lerp(0.92f, 1.1f, Mathf.Clamp01(rng01));
            return Mathf.Clamp(baseSeconds * jitter, ReactionMin, ReactionMax);
        }

        /// <summary>
        /// İlk atış hatası çarpanı: hedefi edindikten <paramref name="secondsSinceAcquired"/> sn sonra.
        /// Başlangıç = 0.65 + extra (acemi ~2.2, seçkin ~1.35), sonra üstel olarak 0.65'e yakınsar.
        /// </summary>
        public static float AimSettle(float secondsSinceAcquired, float skill01)
        {
            var s = Mathf.Clamp01(skill01);
            var extra = Mathf.Lerp(1.55f, 0.7f, s);
            var tau = Mathf.Lerp(1.15f, 0.5f, s);
            var t = Mathf.Max(0f, secondsSinceAcquired);
            return SettledAimFactor + extra * Mathf.Exp(-t / tau);
        }

        /// <summary>
        /// Geri tepme telafisi (düşük = iyi): uygulanan tepme oranı. Beceri, baskı, atış başına rastgele ve seri uzunluğu etkiler.
        /// </summary>
        public static float RecoilControl(float skill01, float suppression01, float rng01, int shotIndexInBurst)
        {
            var baseControl = Mathf.Lerp(0.8f, 0.3f, Mathf.Clamp01(skill01));
            var randomFactor = Mathf.Lerp(0.8f, 1.3f, Mathf.Clamp01(rng01));
            var fatigue = 1f + Mathf.Clamp(shotIndexInBurst, 0, 20) * 0.035f;
            var suppression = 1f + Mathf.Clamp01(suppression01) * 0.6f;
            return Mathf.Clamp(baseControl * randomFactor * fatigue * suppression, 0.2f, 1.2f);
        }

        /// <summary>
        /// Hedef değiştirme gecikmesi (sn): beceri ve dönüş açısı (derece). Ölü hedeften sonra (dead=true) kısalır.
        /// </summary>
        public static float TargetSwitchDelay(float skill01, float angleDegrees, bool previousDead, float rng01)
        {
            var delay = Mathf.Lerp(0.55f, 0.2f, Mathf.Clamp01(skill01));
            delay += Mathf.Clamp01(Mathf.Abs(angleDegrees) / 180f) * 0.25f;
            delay *= Mathf.Lerp(0.85f, 1.2f, Mathf.Clamp01(rng01));
            if (previousDead)
                delay *= 0.6f;
            return Mathf.Max(0.12f, delay);
        }

        /// <summary>Baskı etkisi beceriyle azalır (kıdemli asker ateş altında daha soğukkanlı).</summary>
        public static float EffectiveSuppression(float suppression01, float skill01)
        {
            return Mathf.Clamp01(suppression01) * Mathf.Lerp(1.15f, 0.6f, Mathf.Clamp01(skill01));
        }

        /// <summary>Baskı altında nişan hatası çarpanı (1 = etkisiz).</summary>
        public static float SuppressedAimMultiplier(float effectiveSuppression01)
        {
            return 1f + Mathf.Clamp01(effectiveSuppression01) * 1.3f;
        }

        /// <summary>Baskı altında ateşte seri uzunluğu çarpanı (kör ateş: kısa seriler).</summary>
        public static float SuppressedBurstScale(float effectiveSuppression01)
        {
            return Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(effectiveSuppression01));
        }
    }

    /// <summary>
    /// Baskı (suppression) durumu: yakından geçen mermi / yakın düşman ateşi / hasar biriktirir, zamanla söner.
    /// Değer 0..1 (bastırılmış). Yapı değer tipi; bot başına tek örnek, kare başına bellek ayırmaz.
    /// </summary>
    public struct BotSuppression
    {
        public const float DecayPerSecond = 0.28f;
        public const float DamageImpulse = 0.22f;
        public const float NearMissImpulse = 0.14f;
        public const float NearbyFireImpulse = 0.05f;

        private float _level;

        public float Level => _level;

        public void Add(float amount)
        {
            if (amount <= 0f)
                return;
            _level = Mathf.Min(1f, _level + amount);
        }

        public void Tick(float dt)
        {
            if (_level <= 0f)
                return;
            _level = Mathf.Max(0f, _level - DecayPerSecond * dt);
        }

        public void Reset()
        {
            _level = 0f;
        }

        /// <summary>
        /// Geçen merminin baskı katkısı: ışın boyunca hedefe en yakın geçiş uzaklığı (m); 3 m'den yakınsa 0..NearMissImpulse.
        /// </summary>
        public static float NearMissAmount(float missDistance, float caliberFactor)
        {
            const float radius = 3f;
            if (missDistance >= radius)
                return 0f;
            var t = 1f - Mathf.Max(0f, missDistance) / radius;
            return NearMissImpulse * t * Mathf.Clamp(caliberFactor, 0.5f, 2f);
        }

        /// <summary>Yakındaki düşman atışı (sesle duyulan) için katkı; 35 m içinde.</summary>
        public static float NearbyFireAmount(float distance)
        {
            const float radius = 35f;
            if (distance >= radius)
                return 0f;
            return NearbyFireImpulse * (1f - Mathf.Max(0f, distance) / radius);
        }
    }
}
