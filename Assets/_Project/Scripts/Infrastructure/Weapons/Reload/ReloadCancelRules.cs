using System;
using Project.Application.Services;

namespace Project.Infrastructure.Weapons.Reload
{
    public enum ReloadCancelReason { Manual, Sprint, WeaponSwap, Fire, Melee, Throw, Damage }

    public enum CancelClass
    {
        /// <summary>Serbest: şarjör hâlâ yuvada, hiçbir şey değişmedi.</summary>
        Free,
        /// <summary>Cezalı: şarjör çıkmış; eski şarjör geri takılırken kısa süre silah kullanılamaz.</summary>
        Penalty,
        /// <summary>Taahhüt: mermi silaha geçti; iptal animasyon kuyruğunu keser, mermi korunur.</summary>
        Committed,
        /// <summary>İptal edilemez bu anda (ör. şarjör yok iken ateş).</summary>
        Blocked
    }

    public readonly struct CancelDecision
    {
        public readonly bool Allowed;
        public readonly CancelClass Class;
        /// <summary>Mermi sayısı değişti (yeni şarjör oturdu / fişek yüklendi).</summary>
        public readonly bool AmmoCommitted;
        /// <summary>İptal geçiş süresi (sn): görsel geri sarma.</summary>
        public readonly float BlendSeconds;
        /// <summary>Geri sarma hedefi (0..1 doldurma zamanı).</summary>
        public readonly float ResumeFraction;
        /// <summary>Silah bu geçişten sonra hemen ateş edebilir mi.</summary>
        public readonly bool CanFireImmediately;

        public CancelDecision(bool allowed, CancelClass cls, bool committed, float blend, float resume, bool fire)
        {
            Allowed = allowed; Class = cls; AmmoCommitted = committed; BlendSeconds = blend; ResumeFraction = resume; CanFireImmediately = fire;
        }

        public static CancelDecision Blocked(float t) => new CancelDecision(false, CancelClass.Blocked, false, 0f, t, false);
    }

    /// <summary>
    /// Doldurma iptal kuralları (saf). CoD/Tarkov mantığı: şarjör çıkmadan iptal bedava; şarjör çıkmışken iptal eski şarjörü
    /// geri takar (ceza); yeni şarjör oturduktan sonra taktik doldurmada ateş doğrudan iptaldir (namluda fişek var).
    /// </summary>
    public static class ReloadCancelRules
    {
        public const float FreeBlendSeconds = 0.12f;
        public const float PenaltyBlendSeconds = 0.32f;
        public const float CommittedBlendSeconds = 0.15f;
        /// <summary>Boş doldurmada kurma evresinin bu oranından sonra ateş edilebilir (sürgü ileri sıçradıktan sonra).</summary>
        public const float ChamberFireFraction = 0.6f;

        public static CancelDecision Evaluate(ReloadPhasePlan plan, float t, ReloadCancelReason reason, int shellsLoaded = 0)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            var phase = plan.PhaseAt(t);

            if (plan.Kind == ReloadKind.Shotgun)
                return EvaluateShotgun(plan, t, phase, reason, shellsLoaded);

            if (phase == ReloadPhase.Recover)
                return new CancelDecision(true, CancelClass.Committed, true, 0.08f, 1f, true);

            if (t < plan.MagOutTime)
                return reason == ReloadCancelReason.Fire && plan.Empty
                    ? CancelDecision.Blocked(t)
                    : new CancelDecision(true, CancelClass.Free, false, FreeBlendSeconds, 0f, !plan.Empty);

            if (t < plan.SeatTime)
            {
                // Şarjör silahta değil: ateş edilemez; diğer iptaller eski şarjörü geri takar.
                if (reason == ReloadCancelReason.Fire) return CancelDecision.Blocked(t);
                var blend = reason == ReloadCancelReason.WeaponSwap || reason == ReloadCancelReason.Sprint ? PenaltyBlendSeconds * 0.75f : PenaltyBlendSeconds;
                return new CancelDecision(true, CancelClass.Penalty, false, blend, plan.MagOutTime, false);
            }

            // Yeni şarjör oturdu: mermi taahhüt edildi.
            if (!plan.Empty)
            {
                var fireOk = reason == ReloadCancelReason.Fire;
                return new CancelDecision(true, CancelClass.Committed, true, fireOk ? 0f : CommittedBlendSeconds, plan.RecoverStart, true);
            }

            // Boş: sürgü bırakılmadan fişek odada değildir.
            if (phase == ReloadPhase.Chamber)
            {
                var prog = plan.PhaseProgress(t);
                if (reason == ReloadCancelReason.Fire && prog < ChamberFireFraction) return CancelDecision.Blocked(t);
                return new CancelDecision(true, CancelClass.Committed, true, CommittedBlendSeconds, plan.RecoverStart, prog >= ChamberFireFraction);
            }

            // Seat evresi, sürgü henüz bırakılmadı: şarjör takılı ama fişek odada yok.
            if (reason == ReloadCancelReason.Fire) return CancelDecision.Blocked(t);
            return new CancelDecision(true, CancelClass.Committed, true, CommittedBlendSeconds, plan.RecoverStart, false);
        }

        private static CancelDecision EvaluateShotgun(ReloadPhasePlan plan, float t, ReloadPhase phase, ReloadCancelReason reason, int shellsLoaded)
        {
            if (phase == ReloadPhase.Prep || phase == ReloadPhase.MagRelease || phase == ReloadPhase.Fetch)
                return new CancelDecision(true, CancelClass.Free, false, FreeBlendSeconds, 0f, !plan.Empty);

            if (phase == ReloadPhase.Insert)
            {
                // Her yüklü fişek kalıcı; en az biri varsa ateş iptaldir (pompa evresi başlamadan).
                var any = shellsLoaded >= 1;
                if (reason == ReloadCancelReason.Fire && !any) return CancelDecision.Blocked(t);
                return new CancelDecision(true, any ? CancelClass.Committed : CancelClass.Free, any, FreeBlendSeconds + 0.08f, plan.MagOutTime, any);
            }

            return new CancelDecision(true, CancelClass.Committed, true, CommittedBlendSeconds, plan.RecoverStart, reason == ReloadCancelReason.Fire && t >= plan.ChamberStart);
        }
    }

    /// <summary>İptal geçişi (saf): dondurulan zamandan hedef zamana yumuşak geri/ileri sarma + poz ağırlığı sönümü.</summary>
    public struct ReloadCancelBlend
    {
        private float _from, _to, _duration, _elapsed;
        public bool Active { get; private set; }

        public static ReloadCancelBlend Start(float fromT, CancelDecision d)
        {
            var dur = Math.Max(0f, d.BlendSeconds);
            return new ReloadCancelBlend { _from = fromT, _to = d.ResumeFraction, _duration = dur, _elapsed = 0f, Active = dur > 1e-4f };
        }

        /// <summary>dt kadar ilerler. t = gösterilecek doldurma zamanı, weight = poz ağırlığı (1 başta, 0 sonda). Bitince false.</summary>
        public bool Step(float dt, out float t, out float weight)
        {
            if (!Active) { t = _to; weight = 0f; return false; }
            _elapsed += Math.Max(0f, dt);
            var p = _duration <= 1e-6f ? 1f : Math.Min(1f, _elapsed / _duration);
            var e = p * p * (3f - 2f * p);
            t = _from + (_to - _from) * e;
            // Ağırlık son %40'ta söner; böylece şarjör önce yerine döner, sonra poz sıfırlanır.
            var w = 1f - Math.Max(0f, (p - 0.6f) / 0.4f);
            weight = w * w * (3f - 2f * w);
            if (p >= 1f) { Active = false; weight = 0f; return false; }
            return true;
        }
    }
}
