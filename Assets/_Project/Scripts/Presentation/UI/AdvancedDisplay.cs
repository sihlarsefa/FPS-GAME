using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Localization;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.HdrMix;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Gelişmiş ayarların motor tarafı uygulayıcısı: çözünürlük/pencere modu, VSync, kare sınırı, render ölçeği,
    /// ayrı ses seviyeleri (efekt, müzik/ortam, telsiz), nişangâh rengi/boyutu, HUD ölçeği, renk körlüğü paleti.
    /// Tümü idempotenttir ve hata fırlatmaz.
    /// </summary>
    public static class AdvancedDisplay
    {
        public static string[] WindowModeNames => new[]
        {
            Loc.Get("settings.window.fullscreen", "Tam ekran"),
            Loc.Get("settings.window.borderless", "Çerçevesiz pencere"),
            Loc.Get("settings.window.windowed", "Pencere"),
        };
        public static string[] CrosshairColorNames => new[]
        {
            Loc.Get("settings.crosshair.white", "Beyaz"),
            Loc.Get("settings.crosshair.green", "Yeşil"),
            Loc.Get("settings.crosshair.yellow", "Sarı"),
            Loc.Get("settings.crosshair.cyan", "Camgöbeği"),
            Loc.Get("settings.crosshair.red", "Kırmızı"),
        };
        public static readonly Color[] CrosshairColors =
        {
            Color.white, new Color(0.3f, 1f, 0.3f), new Color(1f, 0.92f, 0.2f), new Color(0.2f, 0.95f, 1f), new Color(1f, 0.25f, 0.2f)
        };

        public static float SfxVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 1f;
        /// <summary>Telsiz/ses (komut ve telsiz konuşmaları) seviyesi; ses sistemi bunu çarpan olarak okuyabilir.</summary>
        public static float VoiceVolume { get; private set; } = 1f;
        public static float CrosshairSize { get; private set; } = 1f;
        public static float HudScale { get; private set; } = 1f;
        public static bool MotionBlur { get; private set; }
        /// <summary>Gamepad nişan yardımı gücü 0-100 (yalnızca gamepad).</summary>
        public static int AimAssistStrength { get; private set; } = 50;
        /// <summary>Telsiz altyazı boyutu dizini 0-3.</summary>
        public static int SubtitleSize { get; private set; } = 1;
        public static bool SubtitleBackground { get; private set; }
        public static bool ToggleAds { get; private set; }
        public static bool ToggleCrouch { get; private set; }
        public static Color CrosshairTint { get; private set; } = Color.white;

        public static string FrameCapLabel(int cap) => cap <= 0 ? "Sınırsız" : cap + " FPS";

        /// <summary>Çözünürlük seçenekleri (ilk eleman "Yerel"); (0,0) = yerel.</summary>
        public static List<Vector2Int> ResolutionOptions()
        {
            var list = new List<Vector2Int> { Vector2Int.zero };
            try
            {
                foreach (var r in Screen.resolutions)
                {
                    var v = new Vector2Int(r.width, r.height);
                    if (r.width >= 640 && !list.Contains(v))
                        list.Add(v);
                }
            }
            catch (Exception)
            {
                // Çözünürlük listesi alınamadı: yalnızca yerel.
            }
            return list;
        }

        public static string ResolutionLabel(Vector2Int v) => v.x <= 0 ? "Yerel" : v.x + "×" + v.y;

        public static void Apply(GameSettings s)
        {
            if (s == null)
                return;

            UiTheme.ColorBlindPalette = s.ColorBlindPalette != 0 ? s.ColorBlindPalette : (s.ColorBlindMode ? 1 : 0);
            SfxVolume = Mathf.Clamp01(s.SfxVolume);
            MusicVolume = Mathf.Clamp01(s.MusicVolume);
            VoiceVolume = Mathf.Clamp01(s.VoiceVolume);
            CrosshairSize = Mathf.Clamp(s.CrosshairSize, 0.5f, 2f);
            HudScale = Mathf.Clamp(s.HudScale, 0.8f, 1.2f);
            HudStyle.Opacity = s.HudOpacity;
            MotionBlur = s.MotionBlur;
            AimAssistStrength = Mathf.Clamp(s.AimAssistStrength, 0, 100);
            SubtitleSize = Mathf.Clamp(s.SubtitleSize, 0, 3);
            SubtitleBackground = s.SubtitleBackground;
            ToggleAds = s.ToggleAds;
            ToggleCrouch = s.ToggleCrouch;
            CrosshairTint = CrosshairColors[Mathf.Clamp(s.CrosshairColor, 0, CrosshairColors.Length - 1)];

            try
            {
                GameAudio.AmbientVolume = Mathf.Clamp01(s.AmbientVolume) * MusicVolume;
            }
            catch (Exception e) { Debug.LogException(e); }

            // Gerçek mikser: efekt/müzik/telsiz seviyeleri açık dB parametrelerine yazılır (mixer yoksa no-op).
            try
            {
                MixerRouting.ApplySettings(SfxVolume, MusicVolume, VoiceVolume);
            }
            catch (Exception e) { Debug.LogException(e); }

            try
            {
                QualitySettings.vSyncCount = s.VSync ? 1 : 0;
                Application_SetTargetFps(s.VSync ? -1 : (s.FrameRateCap <= 0 ? -1 : s.FrameRateCap));
                ApplyRenderScale(s.RenderScale);
#if !UNITY_EDITOR && !UNITY_SERVER
                var mode = s.WindowMode == 0 ? FullScreenMode.ExclusiveFullScreen : s.WindowMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                var w = s.ResolutionWidth > 0 ? s.ResolutionWidth : Screen.currentResolution.width;
                var h = s.ResolutionHeight > 0 ? s.ResolutionHeight : Screen.currentResolution.height;
                if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode)
                    Screen.SetResolution(w, h, mode);
#endif
            }
            catch (Exception e) { Debug.LogException(e); }

            try
            {
                var xhair = UnityEngine.Object.FindFirstObjectByType<CrosshairView>(FindObjectsInactive.Include);
                if (xhair != null)
                    xhair.ApplyAppearance();
                ApplyHudScale(UnityEngine.Object.FindFirstObjectByType<HudController>(FindObjectsInactive.Include));
                RadioSubtitleView.RefreshStyle();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static void ApplyHudScale(HudController hud)
        {
            if (hud != null && hud.Root != null)
            {
                hud.Root.localScale = new Vector3(HudScale, HudScale, 1f);
                try
                {
                    // Güvenli alan (çentik/TV taşması): masaüstünde sıfır, kök kenar boşluğu olarak uygulanır.
                    var m = HudStyle.SafeMargin();
                    hud.Root.offsetMin = new Vector2(m.x, m.y);
                    hud.Root.offsetMax = new Vector2(-m.x, -m.y);
                }
                catch (Exception) { /* güvenli alan okunamadı */ }
            }
        }

        private static void Application_SetTargetFps(int fps) => UnityEngine.Application.targetFrameRate = fps;

        private static void ApplyRenderScale(float scale)
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null)
                return;
            var prop = asset.GetType().GetProperty("renderScale");
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(float))
                prop.SetValue(asset, Mathf.Clamp(scale, 0.5f, 1f));
        }
    }

    /// <summary>
    /// Ayarlar penceresi UX kuralları (saf): arama eşleşmesi (Türkçe harf duyarsız), tehlikeli değer uyarıları,
    /// önerilen kademe. Ayar anahtarlarının davranışını değiştirmez; yalnız sunum kararları verir.
    /// </summary>
    public static class SettingsUxRules
    {
        /// <summary>Türkçe harfleri ASCII'ye indirip küçültür ("Çözünürlük" -> "cozunurluk").</summary>
        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (var raw in text)
            {
                var c = raw;
                switch (c)
                {
                    case 'İ': case 'I': case 'ı': c = 'i'; break;
                    case 'Ş': case 'ş': c = 's'; break;
                    case 'Ğ': case 'ğ': c = 'g'; break;
                    case 'Ü': case 'ü': c = 'u'; break;
                    case 'Ö': case 'ö': c = 'o'; break;
                    case 'Ç': case 'ç': c = 'c'; break;
                    case 'Â': case 'â': case 'Î': case 'î': case 'Û': case 'û': c = char.ToLowerInvariant(c == 'Â' || c == 'â' ? 'a' : c == 'Î' || c == 'î' ? 'i' : 'u'); break;
                    default: c = char.ToLowerInvariant(c); break;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Boş sorgu her şeyi eşler; aksi halde boşlukla ayrılmış her sözcük ad içinde geçmeli.</summary>
        public static bool Matches(string name, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            var hay = Fold(name);
            foreach (var word in Fold(query).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (hay.IndexOf(word, StringComparison.Ordinal) < 0)
                    return false;
            }
            return true;
        }

        /// <summary>Görüş alanı uç değer uyarısı (boş = uyarı yok).</summary>
        public static string FovWarning(float fov)
        {
            if (fov <= 65f)
                return "Çok dar görüş alanı çevreyi görmeyi zorlaştırır.";
            if (fov >= 100f)
                return "Çok geniş görüş alanı kenarlarda bozulma ve FPS kaybı yapabilir.";
            return string.Empty;
        }

        /// <summary>Silah görüş açısı uç değer uyarısı.</summary>
        public static string ViewmodelFovWarning(float fov, float min, float max)
        {
            var span = Mathf.Max(0.0001f, max - min);
            var t = (fov - min) / span;
            if (t <= 0.08f)
                return "Silah çok yakın görünür; ekranı kaplayabilir.";
            if (t >= 0.92f)
                return "Silah çok uzak ve küçük görünür.";
            return string.Empty;
        }

        public static string RenderScaleWarning(float scale) =>
            scale < 0.7f ? "Düşük ölçek görüntüyü belirgin biçimde yumuşatır." : string.Empty;

        public static string ShakeWarning(float intensity) =>
            intensity > 1.2f ? "Yüksek sarsıntı mide bulantısı yapabilir." : string.Empty;

        public static string HudOpacityWarning(float opacity) =>
            opacity < 0.45f ? "Çok düşük opaklıkta HUD okunmaz." : string.Empty;

        /// <summary>Donanıma göre önerilen grafik kademesi 0..3 (AutoQualityRules ile aynı kural; ölçüm yoksa donanım puanı).</summary>
        public static int RecommendedQuality()
        {
            try
            {
                return Project.Infrastructure.Rendering.AutoQualityRules.ChooseTier(
                    SystemInfo.graphicsDeviceName, SystemInfo.graphicsMemorySize, SystemInfo.processorCount, 0f);
            }
            catch (Exception)
            {
                return 2;
            }
        }
    }
}
