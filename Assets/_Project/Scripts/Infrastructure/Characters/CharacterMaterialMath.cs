using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>Karakter malzeme türleri (kumaş, ten, sert ekipman alt türleri, gece görüş lensi).</summary>
    public enum CharacterMaterialKind
    {
        Fabric = 0,
        Skin = 1,
        HelmetPaint = 2,
        Cordura = 3,
        Rubber = 4,
        Leather = 5,
        NvgLens = 6
    }

    /// <summary>
    /// Karakter shader'larının (HAREKAT/Character/*) saf matematik aynası: wrap-diffuse, kir gradyanı, speküler oklüzyon,
    /// ıslaklık çarpanları ve tür başına varsayılan parametreler. Shader'daki HKC_* fonksiyonlarıyla birebir aynıdır (EditMode testli).
    /// </summary>
    public static class CharacterMaterialMath
    {
        /// <summary>Wrap diffuse: (NdL + w) / (1 + w), 0..1. w = 0 klasik Lambert.</summary>
        public static float WrapDiffuse(float ndl, float wrap)
        {
            return Mathf.Clamp01((ndl + wrap) / (1f + Mathf.Max(0f, wrap)));
        }

        /// <summary>Alt-yüzey ek ışığı: wrap − Lambert (her zaman ≥ 0); terminatör bölgesinde ten yumuşar.</summary>
        public static float WrapExtra(float ndl, float wrap)
        {
            return WrapDiffuse(ndl, wrap) - Mathf.Clamp01(ndl);
        }

        /// <summary>Nesne-uzayı yükseklikten kir miktarı: start altında artar, span kadar aşağıda 1'e ulaşır.</summary>
        public static float DirtGradient(float objectY, float start, float span)
        {
            return Mathf.Clamp01((start - objectY) / Mathf.Max(span, 0.01f));
        }

        /// <summary>Lagarde speküler oklüzyon: ao ve bakış açısından yansımanın ne kadarı görünür (0..1).</summary>
        public static float SpecularOcclusion(float ndv, float ao, float smoothness)
        {
            var rough = 1f - Mathf.Clamp01(smoothness);
            var exp = Mathf.Pow(2f, -16f * rough - 1f);
            return Mathf.Clamp01(Mathf.Pow(Mathf.Abs(ndv + ao), exp) - 1f + ao);
        }

        /// <summary>Islaklık 0..1 = küresel ıslaklık × duyarlılık.</summary>
        public static float Wet(float globalWetness, float response)
        {
            return Mathf.Clamp01(globalWetness * response);
        }

        /// <summary>Islak albedo çarpanı: kuru 1 → darken (kumaş suyu çeker, koyulaşır).</summary>
        public static float WetAlbedoScale(float wet, float darken)
        {
            return Mathf.Lerp(1f, darken, Mathf.Clamp01(wet));
        }

        /// <summary>Islak pürüzsüzlük: kumaş hedefi 0,62 (üst sınır, wet·0,65 karışım); sert yüzeyler en az 0,78'e (wet·0,8) çıkar.</summary>
        public static float WetSmoothness(float smoothness, float wet, bool fabric)
        {
            wet = Mathf.Clamp01(wet);
            return fabric
                ? Mathf.Lerp(smoothness, 0.62f, wet * 0.65f)
                : Mathf.Lerp(smoothness, Mathf.Max(smoothness, 0.78f), wet * 0.8f);
        }

        /// <summary>Kenar aşınması maskesi: eğim (normal XY uzunluğu) × makro gürültü × güç. 0..1.</summary>
        public static float EdgeWear(float normalSlope, float macro, float strength)
        {
            var edge = Mathf.Clamp01(normalSlope * 2.2f) * Mathf.Clamp01(macro * 1.5f);
            return Mathf.Clamp01(edge * strength * 2f);
        }

        /// <summary>Toplam kir: gradyan × makro (0,55..1,45) + yukarı bakan toz, × güç. 0..1.</summary>
        public static float Dirt(float gradient, float macro, float normalUp, float dust, float strength)
        {
            var d = gradient * Mathf.Lerp(0.55f, 1.45f, Mathf.Clamp01(macro));
            d += Mathf.Clamp01(normalUp) * dust * macro;
            return Mathf.Clamp01(d * strength);
        }

        /// <summary>Tür başına shader parametreleri (varsayılanlar).</summary>
        public struct Params
        {
            public float Smoothness, Metallic, BumpScale, DetailTiling, MacroTiling;
            public float SheenStrength, SheenPower, Wrap, SssStrength;
            public float DirtStrength, DirtStart, DirtSpan, Dust;
            public float WetResponse, WetDarken, EdgeWear, LensEmission;
        }

        /// <summary>Tür için makul varsayılanlar (testli aralıklar).</summary>
        public static Params Defaults(CharacterMaterialKind kind)
        {
            var p = new Params
            {
                Smoothness = 0.22f, BumpScale = 1f, DetailTiling = 45f, MacroTiling = 3f,
                SheenStrength = 0f, SheenPower = 3f, Wrap = 0.4f, SssStrength = 0f,
                DirtStrength = 0.4f, DirtStart = -0.05f, DirtSpan = 0.4f, Dust = 0.15f,
                WetResponse = 0.8f, WetDarken = 0.62f, EdgeWear = 0f, LensEmission = 0f
            };
            switch (kind)
            {
                case CharacterMaterialKind.Fabric:
                    p.Smoothness = 0.14f; p.SheenStrength = 0.35f; p.SheenPower = 3f; p.DirtStrength = 0.6f; p.DirtSpan = 0.45f; p.Dust = 0.25f;
                    break;
                case CharacterMaterialKind.Skin:
                    p.Smoothness = 0.34f; p.BumpScale = 0.7f; p.DetailTiling = 28f; p.Wrap = 0.5f; p.SssStrength = 0.7f;
                    p.DirtStrength = 0.12f; p.Dust = 0.05f; p.WetResponse = 0.5f; p.WetDarken = 0.85f;
                    break;
                case CharacterMaterialKind.HelmetPaint:
                    p.Smoothness = 0.2f; p.DetailTiling = 22f; p.DirtStrength = 0.3f; p.Dust = 0.3f; p.EdgeWear = 0.45f;
                    p.WetResponse = 0.5f; p.WetDarken = 0.8f;
                    break;
                case CharacterMaterialKind.Cordura:
                    p.Smoothness = 0.1f; p.BumpScale = 1.2f; p.DetailTiling = 60f; p.SheenStrength = 0.12f; p.DirtStrength = 0.45f; p.DirtSpan = 0.5f; p.Dust = 0.2f;
                    break;
                case CharacterMaterialKind.Rubber:
                    p.Smoothness = 0.3f; p.DetailTiling = 70f; p.DirtStrength = 0.5f; p.WetResponse = 0.4f; p.WetDarken = 0.75f;
                    break;
                case CharacterMaterialKind.Leather:
                    p.Smoothness = 0.28f; p.DetailTiling = 50f; p.DirtStrength = 0.85f; p.DirtStart = 0.03f; p.DirtSpan = 0.12f; p.Dust = 0.2f;
                    p.WetResponse = 0.7f; p.WetDarken = 0.55f;
                    break;
                case CharacterMaterialKind.NvgLens:
                    p.Smoothness = 0.96f; p.Metallic = 0.2f; p.DirtStrength = 0f; p.Dust = 0f; p.LensEmission = 0.35f;
                    p.WetResponse = 0f; p.WetDarken = 1f;
                    break;
            }

            return p;
        }
    }
}
