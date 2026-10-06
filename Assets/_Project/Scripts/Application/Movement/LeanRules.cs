using System;

namespace Project.Application.Movement
{
    /// <summary>
    /// Yana eğilme (peek) kuralları. Tarkov ~30°/0,3-0,4 m yan ofset; Siege 20°; biz 12°/0,35 m taban (config) üzerine duruşa
    /// ve ADS'ye göre ölçekleriz. Çömelikte omuz yere yakındır (ofset %90), yüzüstüde eğilme yok; ADS'de gövde
    /// biraz daha açılır ama hareket yavaş. Eğilirken yürüme hafif yavaşlar (denge).
    /// </summary>
    public static class LeanRules
    {
        private static float C01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Duruşa göre eğilme genlik çarpanı: 0 ayakta 1.0, 1 çömelik 0.9, 2 yüzüstü 0.</summary>
        public static float StanceScale(int stanceIndex) => stanceIndex == 2 ? 0f : stanceIndex == 1 ? 0.9f : 1f;

        /// <summary>Yan ofset (m): config ofseti * duruş * (ADS'de +%10 görüş için).</summary>
        public static float Offset(float configOffset, int stanceIndex, float adsProgress)
            => Math.Max(0f, configOffset) * StanceScale(stanceIndex) * (1f + 0.10f * C01(adsProgress));

        /// <summary>Gövde yatma açısı (derece).</summary>
        public static float Angle(float configAngle, int stanceIndex, float adsProgress)
            => Math.Max(0f, configAngle) * StanceScale(stanceIndex) * (1f + 0.15f * C01(adsProgress));

        /// <summary>Eğilirken yürüme çarpanı: tam eğilmede %88.</summary>
        public static float MoveSpeedFactor(float lean) => 1f - 0.12f * C01(Math.Abs(lean));

        /// <summary>
        /// Eğilme hızı (birim/s): config hızı, ağır yük ve ADS'de azalır (ADS'de eğilmek daha kontrollü). Dönüş yönü değişimi
        /// (sol->sağ) merkezden geçtiği için merkeze dönüş %30 daha hızlı.
        /// </summary>
        public static float Speed(float configSpeed, float burden, float adsProgress, bool returningToCenter)
        {
            var s = Math.Max(0.1f, configSpeed) * (1f - 0.25f * C01(burden)) * (1f - 0.15f * C01(adsProgress));
            return returningToCenter ? s * 1.3f : s;
        }

        /// <summary>Eğilmede kalkan açıklığı: eğilme genliği sürdükçe açıkta kalan gövde oranı (hit-box ofseti yüzdesi 0..1).</summary>
        public static float ExposedFraction(float lean) => 0.35f * C01(Math.Abs(lean));

        /// <summary>Eğilme geçiş süresi (s): 0 → tam eğilme.</summary>
        public static float TransitionSeconds(float speed) => 1f / Math.Max(0.1f, speed);
    }
}
