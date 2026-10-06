using UnityEngine;

namespace Project.Infrastructure.Weapons.Optics
{
    /// <summary>Retikül odak düzlemi: FFP büyütmeyle birlikte büyür, SFP ekranda sabit kalır.</summary>
    public enum FocalPlane
    {
        Second = 0,
        First = 1
    }

    /// <summary>Kolimatör (kırmızı nokta) sonucu: ekran ofseti (IMGUI pikseli, y aşağı) ve görünürlük 0..1.</summary>
    public readonly struct CollimatorResult
    {
        public readonly Vector2 OffsetPixels;
        public readonly float Visibility;

        public CollimatorResult(Vector2 offsetPixels, float visibility)
        {
            OffsetPixels = offsetPixels;
            Visibility = visibility;
        }
    }

    /// <summary>
    /// Saf optik matematiği (sahne bağımsız, test edilebilir): paralaksız kolimatör retikülü, çıkış gözbebeği/göz kutusu,
    /// büyütmeye bağlı hassasiyet, ADS geçiş eğrileri, FFP/SFP retikül ölçeği ve düşüş (holdover) pikseli.
    /// </summary>
    public static class OpticMath
    {
        private const float Deg2Rad = Mathf.PI / 180f;

        /// <summary>Gözbebeği çapı (mm): çıkış gözbebeğinden küçük kalan pay göz kutusunu belirler.</summary>
        public const float EyePupilMm = 3.5f;

        /// <summary>Oyun hissi için göz kutusu toleransı çarpanı (gerçekte birkaç mm, oyunda daha bağışlayıcı).</summary>
        public const float EyeBoxForgiveness = 4f;

        /// <summary>Verilen FOV'dan büyütme: tan(base/2) / tan(fov/2) (ScopeMath.FovFromMagnification'ın tersi).</summary>
        public static float MagnificationFromFov(float baseFovDegrees, float fovDegrees)
        {
            baseFovDegrees = Mathf.Clamp(ScopeMath.Sanitize(baseFovDegrees, 60f), 1f, 170f);
            fovDegrees = Mathf.Clamp(ScopeMath.Sanitize(fovDegrees, baseFovDegrees), 0.5f, 170f);
            return Mathf.Tan(baseFovDegrees * 0.5f * Deg2Rad) / Mathf.Tan(fovDegrees * 0.5f * Deg2Rad);
        }

        /// <summary>
        /// Büyütmeye göre fare hassasiyeti çarpanı: (tan(zoom/2) / tan(base/2)) ^ match. match=1 ekranda aynı piksel hızı
        /// (1/büyütme), match=0 ham açısal hız. Sonuç 0.02..1.
        /// </summary>
        public static float SensitivityScale(float baseFovDegrees, float zoomFovDegrees, float match = 1f)
        {
            baseFovDegrees = Mathf.Clamp(ScopeMath.Sanitize(baseFovDegrees, 60f), 1f, 170f);
            zoomFovDegrees = Mathf.Clamp(ScopeMath.Sanitize(zoomFovDegrees, baseFovDegrees), 0.5f, baseFovDegrees);
            var ratio = Mathf.Tan(zoomFovDegrees * 0.5f * Deg2Rad) / Mathf.Tan(baseFovDegrees * 0.5f * Deg2Rad);
            return Mathf.Clamp(Mathf.Pow(Mathf.Clamp(ratio, 0.001f, 1f), Mathf.Clamp(match, 0f, 1.5f)), 0.02f, 1f);
        }

        // ------------------------------------------------------------------ ADS geçiş eğrileri

        /// <summary>Silah konumu için eğri: hızlı başlar, oturarak biter (ease-out). 0→0, 1→1.</summary>
        public static float PositionBlend(float t)
        {
            t = Mathf.Clamp01(ScopeMath.Sanitize(t));
            var inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        /// <summary>FOV/büyütme için eğri: yumuşak başlar ve biter (smootherstep). Silah konumunun biraz gerisinden gelir.</summary>
        public static float FovBlend(float t)
        {
            t = Mathf.Clamp01(ScopeMath.Sanitize(t));
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        /// <summary>
        /// ADS sırasındaki dikey FOV: büyütme üstel enterpole edilir (mag^blend), böylece algılanan yakınlaşma hızı sabit kalır
        /// (doğrusal FOV geçişi sonda aniden hızlanır).
        /// </summary>
        public static float AdsFov(float hipFovDegrees, float targetMagnification, float t)
        {
            var mag = Mathf.Max(1f, ScopeMath.Sanitize(targetMagnification, 1f));
            var eff = Mathf.Pow(mag, FovBlend(t));
            return ScopeMath.FovFromMagnification(hipFovDegrees, eff);
        }

        /// <summary>
        /// Dürbün ekran katmanı opaklığı: yüksek büyütmede silah ekseni oturana kadar (geç) belirir, kırmızı noktada erken.
        /// </summary>
        public static float OverlayAlpha(float aimBlend, float magnification)
        {
            aimBlend = Mathf.Clamp01(ScopeMath.Sanitize(aimBlend));
            var k = Mathf.Clamp01((ScopeMath.Sanitize(magnification, 1f) - 1f) / 3f);
            var start = Mathf.Lerp(0.05f, 0.45f, k);
            var end = start + 0.5f;
            var x = Mathf.Clamp01((aimBlend - start) / (end - start));
            return x * x * (3f - 2f * x);
        }

        // ------------------------------------------------------------------ Projeksiyon

        /// <summary>
        /// Kamera uzayındaki yön vektörünün ekran merkezine göre piksel ofseti (IMGUI: x sağ, y aşağı).
        /// Kameranın arkasındaki yön için sıfır döner.
        /// </summary>
        public static Vector2 DirectionToScreenPixels(Vector3 dirCamera, float verticalFovDegrees, float screenHeight)
        {
            if (dirCamera.z < 1e-4f)
                return Vector2.zero;
            var halfTan = Mathf.Tan(Mathf.Clamp(ScopeMath.Sanitize(verticalFovDegrees, 60f), 1f, 170f) * 0.5f * Deg2Rad);
            var pxPerTan = Mathf.Max(1f, screenHeight) * 0.5f / halfTan;
            return new Vector2(dirCamera.x / dirCamera.z * pxPerTan, -dirCamera.y / dirCamera.z * pxPerTan);
        }

        /// <summary>
        /// Paralaksız kolimatör retikülü: nokta sonsuza, namlu yönüne yansıtılır; göz ofsetinden bağımsızdır.
        /// boreInCamera: silahın kameraya göre dönüşü (namlu = yerel +Z). eyeLateral: gözün cam merkezine göre yanal kayması (m,
        /// pencere düzleminde). Pencere yarıçapı dışına çıkan nokta söner.
        /// </summary>
        public static CollimatorResult Collimate(Quaternion boreInCamera, Vector2 eyeLateral, float windowRadius, float eyeRelief, float verticalFovDegrees, float screenHeight)
        {
            var dir = boreInCamera * Vector3.forward;
            var offset = DirectionToScreenPixels(dir, verticalFovDegrees, screenHeight);
            if (dir.z < 1e-4f)
                return new CollimatorResult(Vector2.zero, 0f);

            // Göz ışını pencere düzlemini cam merkezinden şu kadar uzakta keser (açı * göz mesafesi + göz kayması).
            var relief = Mathf.Max(0.01f, eyeRelief);
            var px = eyeLateral.x + dir.x / dir.z * relief;
            var py = eyeLateral.y + dir.y / dir.z * relief;
            var r = Mathf.Sqrt(px * px + py * py);
            var radius = Mathf.Max(1e-4f, windowRadius);
            var vis = 1f - Mathf.Clamp01((r - radius * 0.8f) / (radius * 0.2f));
            return new CollimatorResult(offset, vis);
        }

        // ------------------------------------------------------------------ Göz kutusu / retikül ölçeği

        /// <summary>Çıkış gözbebeği (mm) = objektif çapı / büyütme.</summary>
        public static float ExitPupilMm(float objectiveMm, float magnification)
        {
            return Mathf.Max(0.5f, ScopeMath.Sanitize(objectiveMm, 24f)) / Mathf.Max(1f, ScopeMath.Sanitize(magnification, 1f));
        }

        /// <summary>
        /// Göz yanal kayması (m) ile gölge karartması 0..1: kayma (çıkış gözbebeği - gözbebeği)/2 payını (x tolerans) aşınca
        /// karanlık halka kapanır.
        /// </summary>
        public static float EyeBoxShadow(float lateralMeters, float exitPupilMm)
        {
            var slackMm = Mathf.Max(0.25f, (exitPupilMm - EyePupilMm) * 0.5f) * EyeBoxForgiveness;
            var r = Mathf.Abs(ScopeMath.Sanitize(lateralMeters)) * 1000f / slackMm;
            var x = Mathf.Clamp01((r - 0.5f) / 1f);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Retikül çizim ölçeği: SFP = 1; FFP = mag/minMag (0.5..3 aralığına sınırlı).</summary>
        public static float ReticleScale(FocalPlane plane, float magnification, float minMagnification)
        {
            if (plane == FocalPlane.Second)
                return 1f;
            var min = Mathf.Max(1f, ScopeMath.Sanitize(minMagnification, 1f));
            var mag = Mathf.Max(min, ScopeMath.Sanitize(magnification, min));
            return Mathf.Clamp(mag / min, 0.5f, 3f);
        }

        /// <summary>Mermi düşüşünün (m, hedefte) dürbün FOV'unda ekran pikseli karşılığı (pozitif = aşağı).</summary>
        public static float HoldoverPixels(float dropMeters, float rangeMeters, float zoomedFovDegrees, float screenHeight)
        {
            if (rangeMeters < 1f)
                return 0f;
            var halfTan = Mathf.Tan(Mathf.Clamp(ScopeMath.Sanitize(zoomedFovDegrees, 30f), 0.5f, 170f) * 0.5f * Deg2Rad);
            var tanAng = ScopeMath.Sanitize(dropMeters) / rangeMeters;
            return tanAng / halfTan * Mathf.Max(1f, screenHeight) * 0.5f;
        }

        /// <summary>
        /// ADS'de gez hattına karışan salınım payı: gövde salınımı hâlâ görsel hissi taşır ama gez/nokta eksenden kaçmamalıdır.
        /// Dönüş çarpanı (WeaponViewModel: damp = Lerp(1, AdsSwayFactor(kind), aim)). Eski sabit 0.18, 14 cm'de ~14 mrad kaydırırdı.
        /// </summary>
        public static float AdsSwayFactor(SightKind kind)
        {
            switch (kind)
            {
                case SightKind.Scope: return 0.02f;
                case SightKind.RedDot: return 0.03f;
                case SightKind.Holo: return 0.03f;
                default: return 0.04f;
            }
        }

        /// <summary>Kalan salınım ofsetinin (m) gözden göz mesafesinde yarattığı açısal kayma (mrad).</summary>
        public static float SwayResidualMrad(float swayOffsetMeters, float damping, float eyeRelief)
        {
            return Mathf.Abs(ScopeMath.Sanitize(swayOffsetMeters)) * Mathf.Clamp01(damping) / Mathf.Max(0.01f, eyeRelief) * 1000f;
        }

        /// <summary>IMGUI dikdörtgen merkezini tam piksele yuvarlar (tek sayı çözünürlükte yarım piksel titremesini önler).</summary>
        public static Vector2 SnapToPixel(Vector2 p)
        {
            return new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
        }
    }
}
