using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// Nişangâh mesh/nokta parametreleri (ince, okunur). WeaponBlueprints'in sight kurucuları bu değerleri kullanmalı.
    /// ENTEGRASYON: WeaponBlueprints.cs: IronFrontPost genişliğini SightVisuals.FrontPostWidth, gez kulaklarını
    /// SightVisuals.RearEarWidth, RedDot nokta kutusunu SightVisuals.DotWorldSize(mesafe, fov, ekranYüksekliği) ile boyutla.
    /// </summary>
    public static class SightVisuals
    {
        /// <summary>Arpacık genişliği (m): ince koyu çubuk (eski blok ~7-8 mm idi).</summary>
        public const float FrontPostWidth = 0.0026f;
        /// <summary>Arpacık kalkan (hood) kalınlığı (m).</summary>
        public const float FrontHoodThickness = 0.0016f;
        /// <summary>Gez kulağı genişliği (m).</summary>
        public const float RearEarWidth = 0.0034f;
        /// <summary>Gez deliği iç/dış oranı: büyük = ince halka.</summary>
        public const float ApertureInnerRatio = 0.82f;

        /// <summary>Kırmızı nokta: hedef ekran çapı (piksel, 1080p).</summary>
        public const float DotPixels = 2.5f;
        /// <summary>Glow halkası çapı / nokta çapı.</summary>
        public const float GlowScale = 3.2f;
        /// <summary>Nokta HDR yoğunluğu (bloom'a girer).</summary>
        public const float DotHdrIntensity = 6f;
        /// <summary>Glow HDR yoğunluğu.</summary>
        public const float GlowHdrIntensity = 1.4f;
        /// <summary>Paralaks stabilizasyonu: noktanın cam içindeki kayma çarpanı (0 = sabit, 1 = ham).</summary>
        public const float ParallaxFactor = 0.12f;

        /// <summary>Verilen mesafedeki noktanın dünya boyutu (m) ki ekranda DotPixels olsun.</summary>
        public static float DotWorldSize(float distanceMeters, float verticalFovDegrees, float screenHeightPx, float pixels = DotPixels)
        {
            var fov = Mathf.Clamp(verticalFovDegrees, 5f, 150f);
            var h = Mathf.Max(100f, screenHeightPx);
            var worldHeight = 2f * Mathf.Max(0.01f, distanceMeters) * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            return worldHeight * Mathf.Max(0.5f, pixels) / h;
        }

        /// <summary>ADS viewmodel dikey FOV'u (ViewmodelDynamics varsayılan 54 * 0.92).</summary>
        public const float AdsVerticalFov = 54f * 0.92f;

        /// <summary>Kırmızı nokta kutusunun dünya boyutu: göz mesafesi + tüp içi derinlik, ADS FOV, 1080p referansı (alt sınır yok: ince kalır).</summary>
        public static float AdsDotWorldSize(float eyeRelief, float depthInsideSight)
        {
            return Mathf.Clamp(DotWorldSize(eyeRelief + Mathf.Max(0f, depthInsideSight), AdsVerticalFov, 1080f), 0.0003f, 0.004f);
        }

        /// <summary>Verilen dünya boyutunun ekrandaki piksel çapı (test/teşhis).</summary>
        public static float DotScreenPixels(float worldSize, float distanceMeters, float verticalFovDegrees, float screenHeightPx)
        {
            var wh = 2f * Mathf.Max(0.01f, distanceMeters) * Mathf.Tan(Mathf.Clamp(verticalFovDegrees, 5f, 150f) * 0.5f * Mathf.Deg2Rad);
            return worldSize / wh * screenHeightPx;
        }

        /// <summary>Paralaks: göz cam ekseninden <paramref name="eyeOffset"/> kadar kaçıksa nokta ofseti (cam uzayı, m).</summary>
        public static Vector2 ParallaxDotOffset(Vector2 eyeOffset)
        {
            return eyeOffset * -ParallaxFactor;
        }

        /// <summary>HDR nokta rengi (alfa 1).</summary>
        public static Color DotColor(Color baseColor)
        {
            return new Color(baseColor.r * DotHdrIntensity, baseColor.g * DotHdrIntensity, baseColor.b * DotHdrIntensity, 1f);
        }

        public static Color GlowColor(Color baseColor)
        {
            return new Color(baseColor.r * GlowHdrIntensity, baseColor.g * GlowHdrIntensity, baseColor.b * GlowHdrIntensity, 0.35f);
        }
    }
}
