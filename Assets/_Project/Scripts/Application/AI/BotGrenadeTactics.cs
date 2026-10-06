using System;

namespace Project.Application.AI
{
    public enum GrenadeIntent
    {
        None = 0,
        /// <summary>Siperden çıkarma (frag, siperin arkasına).</summary>
        Flush = 1,
        /// <summary>Kapalı mekân/kapı temizleme (içeri at, bekle).</summary>
        Clear = 2,
        /// <summary>Duman: ilerleme/kurtarma/gözü kapatma.</summary>
        Smoke = 3
    }

    /// <summary>
    /// El bombası taktikleri (saf mantık): niyet seçimi, öldürücülük tahmini, pişirme, düşen bombaya tepki (kaç / geri at) ve
    /// atış sonrası takip. Insurgency'de botlar siperdeki oyuncuya bomba/molotof ile baskı kurar, bombayı fark edince kaçar.
    /// </summary>
    public static class BotGrenadeTactics
    {
        public const float FragLethalRadius = 4.5f;
        public const float FragMaxRadius = 9f;
        public const float FuseSeconds = 3.6f;
        public const float HumanReactionToGrenade = 0.35f;

        /// <summary>Patlamanın hedefe öldürücülük olasılığı 0..1 (mesafe düşüşü, siper azaltımı).</summary>
        public static float Lethality(float blastDistance, bool targetBehindHardCover, bool targetProne)
        {
            if (blastDistance >= FragMaxRadius)
                return 0f;
            float p;
            if (blastDistance <= 1.5f) p = 1f;
            else if (blastDistance <= FragLethalRadius) p = 1f - (blastDistance - 1.5f) / (FragLethalRadius - 1.5f) * 0.55f;
            else p = 0.45f * (1f - (blastDistance - FragLethalRadius) / (FragMaxRadius - FragLethalRadius));
            if (targetBehindHardCover) p *= 0.35f;
            if (targetProne) p *= 0.6f;
            return Math.Max(0f, Math.Min(1f, p));
        }

        /// <summary>Niyet seç. enclosed: hedef kapalı mekânda; hasFrag/hasSmoke envanter.</summary>
        public static GrenadeIntent ChooseIntent(bool hasFrag, bool hasSmoke, float targetDistance, bool targetInCover,
            bool enclosed, float nearestAllyToTarget, float secondsPinned, bool wantsToAdvanceOrRevive, float aggression01)
        {
            if (hasSmoke && wantsToAdvanceOrRevive && targetDistance > 12f)
                return GrenadeIntent.Smoke;
            if (!hasFrag)
                return GrenadeIntent.None;
            if (nearestAllyToTarget < FragLethalRadius + 1.5f)
                return GrenadeIntent.None;
            if (enclosed && targetDistance >= 4f && targetDistance <= 18f)
                return GrenadeIntent.Clear;
            if (targetInCover && targetDistance >= 12f && targetDistance <= 32f && (secondsPinned > 3f || aggression01 > 0.55f))
                return GrenadeIntent.Flush;
            return GrenadeIntent.None;
        }

        /// <summary>
        /// Pişirme süresi (sn): havada patlama (air-burst) için hedefe uçuş süresi hesaba katılır;
        /// kapalı mekânda daha az pişir (kaçışa izin verme), açıkta hedef kaçmasın diye 1.0–1.6 sn pişir.
        /// </summary>
        public static float CookSeconds(GrenadeIntent intent, float flightSeconds, float skill01)
        {
            if (intent == GrenadeIntent.Smoke || intent == GrenadeIntent.None) return 0f;
            var target = intent == GrenadeIntent.Flush ? 1.0f + 0.6f * Clamp01(skill01) : 0.4f;
            // Toplam sigorta içinde: pişirme + uçuş < sigorta - 0.5 sn güvenlik payı.
            var maxCook = Math.Max(0f, FuseSeconds - flightSeconds - 0.5f);
            return Math.Min(target, maxCook);
        }

        /// <summary>Bomba atış sonrası: bot ne kadar süre siperde kalmalı (kendi bombasının parçalarından korun).</summary>
        public static float HoldAfterThrow(float distance, float cookSeconds, float flightSeconds)
        {
            var remain = Math.Max(0f, FuseSeconds - cookSeconds - flightSeconds);
            return remain + 0.6f + (distance < 14f ? 0.5f : 0f);
        }

        /// <summary>Düşen bombaya tepki gecikmesi (sn): beceri ve açı (görüş dışı = daha geç).</summary>
        public static float DodgeReactionSeconds(float skill01, float offAxisDegrees, float rng01)
        {
            var t = HumanReactionToGrenade * (1.25f - 0.5f * Clamp01(skill01));
            t += Clamp01(Math.Abs(offAxisDegrees) / 120f) * 0.25f;
            t *= 0.85f + Clamp01(rng01) * 0.4f;
            return Math.Max(0.12f, t);
        }

        public enum DodgeAction { Ignore = 0, Flee = 1, KickBack = 2, DropProne = 3 }

        /// <summary>
        /// Tepki kararı: bomba yarıçapında değilse yoksay; çok yakın ve kısa fitil → yere yat / geri tekme (yalnız yakında ve çok iyi bot);
        /// aksi halde kaç (flee). fuseRemaining: kalan sigorta; canOutrun: kaçış mesafesi kaçmaya yetiyor mu.
        /// </summary>
        public static DodgeAction DecideDodge(float distanceToGrenade, float fuseRemaining, float reactionSeconds,
            bool canOutrun, bool hasCoverBetween, float skill01)
        {
            if (distanceToGrenade > FragMaxRadius + 1f)
                return DodgeAction.Ignore;
            var timeLeft = fuseRemaining - reactionSeconds;
            if (timeLeft <= 0.1f)
                return DodgeAction.DropProne;
            if (hasCoverBetween && distanceToGrenade > FragLethalRadius)
                return DodgeAction.Ignore;
            if (distanceToGrenade < 2.2f && timeLeft > 0.6f && skill01 > 0.7f)
                return DodgeAction.KickBack;
            if (canOutrun)
                return DodgeAction.Flee;
            return hasCoverBetween ? DodgeAction.Ignore : DodgeAction.DropProne;
        }

        /// <summary>Bombadan kaçış hedef mesafesi (m) ve gereken koşu hızı: kalan süre içinde güvenli yarıçapı aşmak.</summary>
        public static float RequiredEscapeSpeed(float distanceToGrenade, float timeLeft)
        {
            var need = Math.Max(0f, FragMaxRadius + 0.5f - distanceToGrenade);
            return timeLeft <= 0.05f ? float.MaxValue : need / timeLeft;
        }

        public static bool CanOutrun(float distanceToGrenade, float timeLeft, float maxSpeed)
        {
            return RequiredEscapeSpeed(distanceToGrenade, timeLeft) <= maxSpeed;
        }

        /// <summary>Duvardan sektirme: hedef duvar/siperin arkasında ve doğrudan atış engelli → sektir (yalnız yakın mesafede).</summary>
        public static bool ShouldBounceThrow(bool directBlocked, bool wallBehindTarget, float distance)
        {
            return directBlocked && wallBehindTarget && distance < 22f;
        }

        /// <summary>Bomba sonrası saldırı penceresi: patlamadan sonra ilerlemek için bekleme (sn) — duman/şok dağılsın, kanatçı hazır.</summary>
        public static float FollowUpPushDelay(GrenadeIntent intent)
        {
            switch (intent)
            {
                case GrenadeIntent.Clear: return 0.25f; // kapı: patlar patlamaz gir
                case GrenadeIntent.Flush: return 0.8f;
                case GrenadeIntent.Smoke: return 1.2f;
                default: return 0f;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
