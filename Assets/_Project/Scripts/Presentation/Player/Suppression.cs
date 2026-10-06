using Project.Application.Combat.Suppression;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>
    /// Bastırma (suppression) durumu, yalnız yerel oyuncu: yakından geçen mermi, yakın patlama ve gelen MG ateşi
    /// 0..1 ölçerini doldurur, zamanla söner. Efektler <see cref="CombatScreenFx"/>, nişan sallantısı
    /// <see cref="SwayMultiplier"/> ile verilir.
    /// </summary>
    public static class Suppression
    {
        public const float DecayPerSecond = 0.28f;

        // Tek kaynak: Application SuppressionState (şiddet + 0.5 sn boğulma). Eski ikinci ölçer kaldırıldı.
        public static readonly SuppressionConfig Config = new SuppressionConfig { DecayPerSecond = DecayPerSecond };
        private static readonly SuppressionState State = new SuppressionState(Config);

        public static float Value => State.Level;
        public static float Concussion { get; private set; }

        /// <summary>0..1 ses boğulma gücü (0.5 sn'de doğrusal söner) — MixerRouting.SetMuffle'a verilir.</summary>
        public static float MuffleAmount => State.MuffleAmount;

        /// <summary>Nişan/silah sallantı çarpanı (1 = normal). Viewmodel/nişan kodu bununla çarpar.</summary>
        public static float SwayMultiplier => CombatScreenFxMath.SwayMultiplier(Value, CombatScreenFx.Intensity);

        /// <summary>Oyuncunun yanından geçen mermi. point: mermi/ıska noktası, caliber: mm, distance: oyuncuya m.</summary>
        public static void ReportNearMiss(Vector3 point, float caliber, float distance)
        {
            var shot = SuppressionRules.ShotIntensity(distance, Config) * CombatScreenFxMath.CaliberFactor(caliber);
            State.AddImpulse(shot);
        }

        /// <summary>Gelen otomatik ateş (MG): ıska olmasa da yakın ateş baskı yapar. Atış başına çağrılabilir.</summary>
        public static void ReportIncomingFire(float distance, float caliber)
        {
            State.AddImpulse(CombatScreenFxMath.IncomingFireImpulse(distance, CombatScreenFxMath.CaliberFactor(caliber)));
        }

        /// <summary>Patlama: yakınlık bastırma + sersemletme (beyaz parlama/bulanıklık) üretir.</summary>
        public static void ReportExplosion(Vector3 point, float radius, float distanceToPlayer)
        {
            var c = CombatScreenFxMath.ConcussionFor(distanceToPlayer, radius);
            if (c <= 0f)
                return;
            Concussion = CombatScreenFxMath.AddConcussion(Concussion, c);
            State.AddImpulse(0.25f + 0.5f * c, true);
        }

        internal static void Tick(float dt)
        {
            State.Tick(dt);
            Concussion = CombatScreenFxMath.DecayConcussion(Concussion, dt);
        }

        public static void Clear()
        {
            State.Reset();
            Concussion = 0f;
            CombatScreenFx.ResetInputs();
        }
    }
}
