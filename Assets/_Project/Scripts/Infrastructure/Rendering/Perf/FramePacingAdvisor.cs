using System;

namespace Project.Infrastructure.Rendering.Perf
{
    public enum PacingAdvice { Hold, ReduceLoad, RestoreLoad }

    /// <summary>
    /// Kare zamanlama danışmanı: kuyruk gecikmesi (p95) ve jitter'a göre yük azalt/geri ver önerir.
    /// Histerezis: azaltmak hızlı (3 ardışık kötü değerlendirme), geri vermek yavaş (10 ardışık iyi),
    /// aralarında bekleme süresi var; böylece kalite "salınım" yapmaz. Saf mantık, Unity bağımsız.
    /// </summary>
    public sealed class FramePacingAdvisor
    {
        public const float ReduceFactor = 1.25f;   // p95 > hedef*1.25 => kötü
        public const float RestoreFactor = 1.05f;  // p95 < hedef*1.05 => iyi
        public const float MaxJitterRatio = 0.35f; // jitter > hedef*0.35 => kötü
        public const int BadEvalsToReduce = 3;
        public const int GoodEvalsToRestore = 10;
        public const int CooldownEvals = 4;

        private int _bad, _good, _cooldown;
        private float _budget = 1f;

        /// <summary>0.4..1: çağıranların parçacık/dekal/ses yoğunluğunu çarpacağı yumuşak bütçe.</summary>
        public float BudgetScale => _budget;
        public const float MinBudget = 0.4f;
        public const float Step = 0.15f;

        public static float TargetMs(int targetFps) => 1000f / Math.Max(15, Math.Min(240, targetFps));

        /// <summary>Bir değerlendirme turu (örn. saniyede bir). Öneriyi ve bütçeyi günceller.</summary>
        public PacingAdvice Evaluate(in FrameStats stats, int targetFps)
        {
            if (stats.Samples < 60) return PacingAdvice.Hold; // yeterli veri yok
            var target = TargetMs(targetFps);
            var bad = stats.P95Ms > target * ReduceFactor || stats.JitterMs > target * MaxJitterRatio;
            var good = stats.P95Ms < target * RestoreFactor && stats.JitterMs < target * MaxJitterRatio * 0.6f;

            if (_cooldown > 0) _cooldown--;

            if (bad) { _bad++; _good = 0; }
            else if (good) { _good++; _bad = 0; }
            else { _bad = 0; _good = 0; }

            if (_cooldown == 0 && _bad >= BadEvalsToReduce)
            {
                _bad = 0; _cooldown = CooldownEvals;
                _budget = Math.Max(MinBudget, _budget - Step);
                return PacingAdvice.ReduceLoad;
            }
            if (_cooldown == 0 && _good >= GoodEvalsToRestore && _budget < 1f)
            {
                _good = 0; _cooldown = CooldownEvals;
                _budget = Math.Min(1f, _budget + Step * 0.5f);
                return PacingAdvice.RestoreLoad;
            }
            return PacingAdvice.Hold;
        }

        public void Reset() { _bad = 0; _good = 0; _cooldown = 0; _budget = 1f; }
    }
}
