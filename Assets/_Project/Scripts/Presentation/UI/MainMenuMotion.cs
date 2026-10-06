using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Ana menü hareket matematiği (saf, EditMode'da sınanır): yumuşatma eğrileri, sayfa geçişi (0.15-0.25 sn),
    /// üstel yaklaşma, döngüsel indeks, haber bandı kayması, sayaç animasyonu ve ışık süpürmesi.
    /// </summary>
    public static class MainMenuMotion
    {
        /// <summary>Sayfa girişi süresi (sn).</summary>
        public const float PageInSeconds = 0.22f;

        /// <summary>Sayfa çıkışı süresi (sn).</summary>
        public const float PageOutSeconds = 0.15f;

        /// <summary>Sayfanın yatay kayma mesafesi (referans piksel).</summary>
        public const float PageSlideDistance = 44f;

        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            var u = 1f - t;
            return 1f - u * u * u;
        }

        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>Hafif aşıp yerine oturan eğri (kartların seçilmesi için).</summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Kare hızından bağımsız üstel yaklaşma (rate büyüdükçe hızlanır).</summary>
        public static float Approach(float current, float target, float rate, float dt)
        {
            if (dt <= 0f || rate <= 0f)
                return current;
            var k = 1f - Mathf.Exp(-rate * dt);
            return current + (target - current) * k;
        }

        /// <summary>Sayfa ilerlemesini (0 gizli, 1 görünür) hedefe doğru süreye göre ilerletir.</summary>
        public static float AdvancePage(float progress, bool visible, float dt)
        {
            var duration = visible ? PageInSeconds : PageOutSeconds;
            var step = dt / Mathf.Max(0.01f, duration);
            return visible ? Mathf.Min(1f, progress + step) : Mathf.Max(0f, progress - step);
        }

        /// <summary>Sayfa saydamlığı.</summary>
        public static float PageAlpha(float progress) => EaseOutCubic(progress);

        /// <summary>
        /// Sayfa yatay kayması: girişte sağdan (+) gelir, çıkışta sola (-) kayar. sign = +1 giriş, -1 çıkış.
        /// </summary>
        public static float PageOffset(float progress, float sign) => (1f - EaseOutCubic(progress)) * PageSlideDistance * sign;

        /// <summary>Döngüsel indeks (negatif dahil).</summary>
        public static int Wrap(int index, int count)
        {
            if (count <= 0)
                return 0;
            var m = index % count;
            return m < 0 ? m + count : m;
        }

        /// <summary>
        /// Haber bandı kayma konumu (x): içerik görünüm genişliğinin dışından girer, tamamen çıkınca başa döner.
        /// </summary>
        public static float TickerOffset(float elapsed, float speed, float contentWidth, float viewWidth)
        {
            var span = Mathf.Max(1f, contentWidth + viewWidth);
            var travelled = Mathf.Max(0f, elapsed) * Mathf.Max(0f, speed);
            return viewWidth - travelled % span;
        }

        /// <summary>Sayaç animasyonu: 0..hedef (t = 0..1, yavaşlayarak).</summary>
        public static int CountUp(int target, float t)
        {
            if (target <= 0)
                return 0;
            return Mathf.Clamp(Mathf.RoundToInt(target * EaseOutCubic(t)), 0, target);
        }

        /// <summary>Arka plan ışık süpürmesinin yatay konumu (0..1, ileri-geri yavaş).</summary>
        public static float SweepPosition(float time, float period)
        {
            if (period <= 0f)
                return 0f;
            var phase = Mathf.Repeat(time / period, 1f);
            return phase < 0.5f ? EaseInOutCubic(phase * 2f) : EaseInOutCubic((1f - phase) * 2f);
        }

        /// <summary>Vinyet saydamlığının yavaş nabzı.</summary>
        public static float VignettePulse(float time, float baseAlpha, float amplitude)
            => Mathf.Clamp01(baseAlpha + Mathf.Sin(time * 0.45f) * amplitude);

        /// <summary>Çubuk dolgusu: değer 0..max → 0..1 (taşma ve negatif güvenli).</summary>
        public static float Fill(float value, float max) => max <= 0f ? 0f : Mathf.Clamp01(value / max);

        /// <summary>Önizleme dönüş açısı: otomatik dönüş + sürükleme (derece, 0..360).</summary>
        public static float PreviewYaw(float current, float degreesPerSecond, float dt, bool dragging)
            => dragging ? current : Mathf.Repeat(current + degreesPerSecond * dt, 360f);
    }
}
