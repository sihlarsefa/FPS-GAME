using System;
using System.Reflection;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Bir kalite kademesinin render hattı ayarları (saf veri).</summary>
    public readonly struct PipelineTier
    {
        public readonly string Name;
        public readonly float RenderScale;
        public readonly bool UseStp;
        public readonly int Msaa;
        public readonly bool Hdr;
        public readonly bool SoftShadows;
        public readonly int SoftShadowQuality;   // 1 Düşük, 2 Orta, 3 Yüksek
        public readonly float ShadowDistance;
        public readonly int ShadowCascades;
        public readonly int ShadowResolution;
        public readonly Vector3 CascadeSplits;   // 4 kademede 3 bölünme oranı
        public readonly bool GpuResidentDrawer;
        public readonly bool GpuOcclusion;
        public readonly bool Ssao;
        public readonly bool Decals;
        public readonly float StreamingMipBudgetMb;
        public readonly bool ForceAniso;   // true: QualitySettings aniso ForceEnable (eğik açıda doku bulanıklığına karşı)
        public readonly int MipmapLimit;   // QualitySettings.globalTextureMipmapLimit (0 = tam çözünürlük doku, 1 = yarım)

        public PipelineTier(string name, float scale, bool stp, int msaa, bool hdr, bool soft, int softQ, float shadowDist,
            int cascades, int shadowRes, Vector3 splits, bool grd, bool occ, bool ssao, bool decals, float mipMb,
            bool forceAniso, int mipLimit)
        {
            Name = name; RenderScale = scale; UseStp = stp; Msaa = msaa; Hdr = hdr; SoftShadows = soft; SoftShadowQuality = softQ;
            ShadowDistance = shadowDist; ShadowCascades = cascades; ShadowResolution = shadowRes; CascadeSplits = splits;
            GpuResidentDrawer = grd; GpuOcclusion = occ; Ssao = ssao; Decals = decals; StreamingMipBudgetMb = mipMb;
            ForceAniso = forceAniso; MipmapLimit = mipLimit;
        }
    }

    /// <summary>
    /// Render hattı kademe tablosu: TEK kaynak (Editor URP üretimi ve çalışma zamanı uygulayıcı aynı tabloyu kullanır).
    /// Forward+, GPU Resident Drawer, GPU occlusion, STP, APV, SRP Batcher. Desteklenmeyen ayar: yansıma + uyarı logu, çökme yok.
    /// </summary>
    public static class PipelineTiers
    {
        public const int Count = 4;

        // URP serileştirme sabitleri (enum değerleri; yansımayla yazılır).
        public const int RenderingModeForwardPlus = 2;
        public const int GpuResidentDrawerInstanced = 1;
        public const int LightProbeSystemProbeVolumes = 1;
        public const int UpscalingStp = 4;
        public const int UpscalingLinear = 1; // UpscalingFilterSelection.Linear (Bilinear)

        // Düşük %67, Orta %77, Yüksek %87, Ultra %100.
        // UseStp=false: Unity 6 STP macOS/Metal'te RenderGraph NRE → siyah ekran (Player.log: STP.Execute).
        // Bilineer yükseltme + kademe AA (FXAA/SMAA/TAA) kullan; STP ancak gürültü dokusu + doğrulanmış platformda açılır.
        // Doku netliği (4K): son iki sütun aniso + mip sınırı. Yüksek/Ultra ForceEnable aniso ve mip sınırı 0 (tam çözünürlük
        // doku, pikselleşme/bulanıklık yok); Düşük'te mip sınırı 1 (yarım çözünürlük, bellek). Akış bütçesi 4K dokulara göre:
        // Yüksek 1024, Ultra 2048 MB (QualityTierApplier'daki MipBudget4K satırı bu değerleri taban alır).
        private static readonly PipelineTier[] Tiers =
        {
            new PipelineTier("Low",    0.67f, false, 2, true, false, 1, 50f,  1, 1024, new Vector3(0.10f, 0.25f, 0.50f), true, false, false, false, 256f,  false, 1),
            new PipelineTier("Medium", 0.77f, false, 2, true, true,  1, 100f, 2, 2048, new Vector3(0.12f, 0.30f, 0.55f), true, true,  true,  true,  512f,  false, 0),
            new PipelineTier("High",   0.87f, false, 4, true, true,  3, 180f, 4, 2048, new Vector3(0.05f, 0.15f, 0.40f), true, true,  true,  true,  1024f, true,  0),
            new PipelineTier("Ultra",  1.00f, false, 4, true, true,  3, 300f, 4, 4096, new Vector3(0.03f, 0.10f, 0.30f), true, true,  true,  true,  2048f, true,  0)
        };

        /// <summary>Gölge bias paketi: derinlik bias, normal bias, ışık near-plane (m).</summary>
        public readonly struct ShadowBias
        {
            public readonly float Depth, Normal, NearPlane;
            public ShadowBias(float depth, float normal, float nearPlane) { Depth = depth; Normal = normal; NearPlane = nearPlane; }
        }

        // Gölge bias tablosu (Düşük/Orta/Yüksek/Ultra). Gerekçe: düşük çözünürlük/uzun mesafede texel büyük -> akne (kendi kendine gölge)
        // için daha büyük bias gerekir, ama bias büyüdükçe peter-panning (gölge nesneden kopar). Yüksek texel yoğunluğunda
        // (Yüksek/Ultra, 2048-4096) küçük bias + normal bias ile denge: derinlik bias küçük tutulur (kopma yok), normal bias
        // yüzeyi normal yönünde iterek aknayı çözer. Near-plane: bina altı ışık sızması için gölge kamerası yakın düzlemi ışığa
        // doğru geri çekilir (0 = tüm örtücüler dahil); büyük değerler çatı/zemin örtücüyü kırpıp altı aydınlatır, bu yüzden düşük tutulur.
        private static readonly ShadowBias[] Biases =
        {
            new ShadowBias(0.07f, 0.45f, 0.25f),
            new ShadowBias(0.06f, 0.38f, 0.20f),
            new ShadowBias(0.04f, 0.30f, 0.10f),
            new ShadowBias(0.03f, 0.25f, 0.05f)
        };

        public static ShadowBias BiasFor(int level) => Biases[Mathf.Clamp(level, 0, Biases.Length - 1)];

        /// <summary>Yönlü ışığa kademenin bias/near-plane değerlerini uygular (null güvenli).</summary>
        public static void ApplyShadowBias(Light light, int level)
        {
            if (light == null)
                return;
            var b = BiasFor(level);
            light.shadowBias = b.Depth;
            light.shadowNormalBias = b.Normal;
            light.shadowNearPlane = b.NearPlane;
        }

        public static PipelineTier Get(int level) => Tiers[Mathf.Clamp(level, 0, Count - 1)];

        // AA sütunları: yöntem + CAS taban gücü tek kaynak AntiAliasingMath (Düşük FXAA, Orta SMAA High, Yüksek TAA/STP, Ultra TAA/STP).
        // Not: Ultra render ölçeği zaten 1,00 (üstü süper-örnekleme = pahalı; ayrı karar). STP, URP'de kamera AA'sını da devralır;
        // Düşük/Orta'da FXAA/SMAA'nın görülmesi için o kademelerde UseStp=false (ölçek bilineer/FSR) gerekir (ENTEGRASYON, tablonun diğer sütunları).

        /// <summary>Kademenin kenar yumuşatma kararı (STP yükselticiyse zamansal mod STP).</summary>
        public static AaTier AaFor(int level) => AntiAliasingMath.ForTier(level, Get(level).UseStp);

        /// <summary>STP açıkken MSAA zorunlu 1; kapalıysa istenen değer (1/2/4/8).</summary>
        public static int EffectiveMsaa(PipelineTier t) => t.UseStp ? 1 : t.Msaa;

        /// <summary>Çalışma zamanı: etkin URP varlığına ve QualitySettings'e kademeyi uygular (null/eksik ayar güvenli).</summary>
        public static void ApplyRuntime(object urpAsset, int level)
        {
            var t = Get(level);
            if (urpAsset != null)
            {
                TrySet(urpAsset, "renderScale", t.RenderScale);
                TrySet(urpAsset, "msaaSampleCount", EffectiveMsaa(t));
                TrySet(urpAsset, "supportsHDR", t.Hdr);
                TrySet(urpAsset, "shadowDistance", t.ShadowDistance);
                TrySet(urpAsset, "shadowCascadeCount", t.ShadowCascades);
                TrySet(urpAsset, "cascade4Split", t.CascadeSplits);
                TrySet(urpAsset, "shadowDepthBias", BiasFor(level).Depth);
                TrySet(urpAsset, "shadowNormalBias", BiasFor(level).Normal);
                TrySet(urpAsset, "useSRPBatcher", true);
                // URP 17+: named upscaler; eski enum yedek. STP kapalıyken her zaman Bilinear (siyah ekran kaçınımı).
                if (t.UseStp)
                {
                    if (!TrySetString(urpAsset, "upscalerName", "Spatial-Temporal Post-Processing"))
                        TrySetEnumByInt(urpAsset, "upscalingFilter", UpscalingStp);
                }
                else
                {
                    if (!TrySetString(urpAsset, "upscalerName", "Bilinear"))
                        TrySetEnumByInt(urpAsset, "upscalingFilter", UpscalingLinear);
                }
            }

            try
            {
                QualitySettings.streamingMipmapsActive = true;
                QualitySettings.streamingMipmapsMemoryBudget = t.StreamingMipBudgetMb;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Mipmap streaming ayarlanamadı: " + e.Message);
            }

            try
            {
                // Doku netliği: Yüksek/Ultra'da anizotropik filtre zorla (ForceEnable) ve mip sınırı 0 (tam çözünürlük);
                // Düşük/Orta doku bazlı aniso (Enable). Pikselleşme/bulanıklık şikâyetinin kalıcı çözümü (tek kaynak: tablo).
                QualitySettings.anisotropicFiltering = t.ForceAniso ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
                QualitySettings.globalTextureMipmapLimit = t.MipmapLimit;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Doku filtreleme/mip sınırı ayarlanamadı: " + e.Message);
            }
        }

        /// <summary>Etkin hattın mevcut değerlerini okunabilir metin olarak raporlar.</summary>
        public static string Report(object urpAsset)
        {
            if (urpAsset == null)
                return "URP etkin değil";
            var sb = new System.Text.StringBuilder("URP:");
            foreach (var p in new[] { "renderScale", "upscalingFilter", "msaaSampleCount", "supportsHDR", "shadowDistance", "shadowCascadeCount", "useSRPBatcher", "gpuResidentDrawerMode", "lightProbeSystem" })
            {
                var prop = urpAsset.GetType().GetProperty(p, BindingFlags.Public | BindingFlags.Instance);
                sb.Append(' ').Append(p).Append('=').Append(prop != null ? Convert.ToString(prop.GetValue(urpAsset, null)) : "?");
            }
            return sb.ToString();
        }

        private static void TrySet(object target, string prop, object value)
        {
            try
            {
                var p = target.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (p == null || !p.CanWrite)
                {
                    Debug.LogWarning("[HAREKÂT] URP ayarı yok/salt okunur: " + prop);
                    return;
                }
                p.SetValue(target, value, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] URP ayarı yazılamadı (" + prop + "): " + e.Message);
            }
        }

        private static void TrySetEnumByInt(object target, string prop, int value)
        {
            try
            {
                var p = target.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (p == null || !p.CanWrite || !p.PropertyType.IsEnum)
                {
                    Debug.LogWarning("[HAREKÂT] URP ayarı yok: " + prop);
                    return;
                }
                p.SetValue(target, Enum.ToObject(p.PropertyType, value), null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] URP ayarı yazılamadı (" + prop + "): " + e.Message);
            }
        }

        private static bool TrySetString(object target, string prop, string value)
        {
            try
            {
                var p = target.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (p == null || !p.CanWrite || p.PropertyType != typeof(string))
                    return false;
                p.SetValue(target, value, null);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] URP ayarı yazılamadı (" + prop + "): " + e.Message);
                return false;
            }
        }
    }
}
