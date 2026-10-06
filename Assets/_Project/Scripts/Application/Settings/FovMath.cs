using System;

namespace Project.Application.Settings
{
    /// <summary>
    /// Görüş açısı matematiği (saf). Oyuncular FOV'yi yatay (Hor+) ya da dikey cinsten konuşur; CS2/Valorant 4:3 tabanlı
    /// yatay, CoD/Tarkov 16:9 yatay, Unity ise dikey FOV kullanır. Dönüşümler tan(yarım açı) oranıyla yapılır.
    /// </summary>
    public static class FovMath
    {
        public const float DegToRad = (float)(Math.PI / 180.0);
        public const float RadToDeg = (float)(180.0 / Math.PI);
        public const float MinFov = 20f;
        public const float MaxFov = 150f;

        /// <summary>Dikey FOV (derece) + en/boy oranından yatay FOV.</summary>
        public static float VerticalToHorizontal(float verticalDeg, float aspect)
        {
            aspect = Math.Max(0.1f, aspect);
            var v = Clamp(verticalDeg, 1f, 170f);
            return 2f * RadToDeg * (float)Math.Atan(Math.Tan(v * DegToRad * 0.5) * aspect);
        }

        /// <summary>Yatay FOV (derece) + en/boy oranından dikey FOV (Unity Camera.fieldOfView).</summary>
        public static float HorizontalToVertical(float horizontalDeg, float aspect)
        {
            aspect = Math.Max(0.1f, aspect);
            var h = Clamp(horizontalDeg, 1f, 170f);
            return 2f * RadToDeg * (float)Math.Atan(Math.Tan(h * DegToRad * 0.5) / aspect);
        }

        /// <summary>Bir en/boy oranında yatay FOV'yi başka bir orandaki yataya çevirir (aynı dikey açı korunur).</summary>
        public static float ConvertHorizontal(float horizontalDeg, float fromAspect, float toAspect)
        {
            return VerticalToHorizontal(HorizontalToVertical(horizontalDeg, fromAspect), toAspect);
        }

        /// <summary>
        /// Oyun içinde ayar olarak sunulan değer (bu projede 4:3 tabanlı yatay değil, 16:9 yatay) geniş ekranda
        /// Hor+ ile genişler. Ultra geniş (21:9) ekranda dikey açıyı koruyarak yatayı hesaplar.
        /// </summary>
        public static float HorPlus(float horizontalAt16x9, float actualAspect)
        {
            return ConvertHorizontal(horizontalAt16x9, 16f / 9f, actualAspect);
        }

        /// <summary>
        /// Nişan (ADS) FOV'si. "Etkilenen" kipte yakınlaştırma tan oranıyla uygulanır (FOV değiştikçe ADS de değişir);
        /// "Bağımsız" kipte ADS FOV'si sabit derece olarak verilir.
        /// </summary>
        public static float AdsFov(float hipFov, float zoom, bool independent, float independentDeg)
        {
            if (independent)
                return Clamp(independentDeg, 5f, MaxFov);
            return ZoomedFov(hipFov, zoom);
        }

        /// <summary>Verilen yakınlaştırma çarpanında (1 = yok) FOV: tan(f/2) = tan(f0/2) / zoom.</summary>
        public static float ZoomedFov(float hipFov, float zoom)
        {
            zoom = Math.Max(1f, zoom);
            var half = Math.Atan(Math.Tan(Clamp(hipFov, 1f, 170f) * DegToRad * 0.5) / zoom);
            return 2f * RadToDeg * (float)half;
        }

        /// <summary>FOV'den yakınlaştırma çarpanı (tan oranı); 1'den küçükse 1'e kırpılır.</summary>
        public static float ZoomFromFov(float hipFov, float adsFov)
        {
            var a = Math.Tan(Clamp(adsFov, 1f, 170f) * DegToRad * 0.5);
            var h = Math.Tan(Clamp(hipFov, 1f, 170f) * DegToRad * 0.5);
            return (float)Math.Max(1.0, h / a);
        }

        /// <summary>
        /// Viewmodel FOV'si ayrı çizildiği için dünya FOV'si büyüdükçe silah ekranda küçülmez; ancak çok yüksek
        /// viewmodel FOV'si kolu ve silahı kameraya yaklaştırıp uzatır. Bu çarpan ekran üzerindeki göreli silah boyunu verir
        /// (1 = referans 54 derece).
        /// </summary>
        public static float ViewmodelApparentScale(float viewmodelFov, float referenceFov = 54f)
        {
            var r = Math.Tan(Clamp(referenceFov, 10f, 120f) * DegToRad * 0.5);
            var v = Math.Tan(Clamp(viewmodelFov, 10f, 120f) * DegToRad * 0.5);
            return (float)(r / v);
        }

        /// <summary>
        /// Görüş alanının getirdiği tahmini piksel yükü: dünya FOV'si yükselince görünür alan (ve çizilen nesne) artar.
        /// 1 = 64 derece referans; yaklaşık görünür alan oranı (tan^2).
        /// </summary>
        public static float RelativeVisibleArea(float verticalFov, float referenceFov = 64f)
        {
            var r = Math.Tan(Clamp(referenceFov, 10f, 150f) * DegToRad * 0.5);
            var v = Math.Tan(Clamp(verticalFov, 10f, 150f) * DegToRad * 0.5);
            return (float)((v * v) / (r * r));
        }

        /// <summary>Sprint/hız FOV vuruşu: baz FOV'ye eklenecek derece (reduce-motion çarpanı uygulanır).</summary>
        public static float KickedFov(float baseFov, float kickDegrees, float motionScale)
        {
            return Clamp(baseFov + kickDegrees * Clamp(motionScale, 0f, 1f), MinFov, MaxFov);
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
