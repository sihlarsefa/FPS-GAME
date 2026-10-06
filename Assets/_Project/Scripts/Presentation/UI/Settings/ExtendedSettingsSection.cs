using System;
using System.Collections.Generic;
using Project.Application.Settings;
using Project.Core.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI.Settings
{
    /// <summary>
    /// Ayarlar penceresine eklenen gelişmiş bölümler: ADS zoom başına hassasiyet, ham girdi/ivme, ses çıkış önayarı,
    /// bağımsız ADS FOV'si, renk körlüğü HUD paleti, hareket azaltma, profil paylaşım kodu.
    /// Veri <see cref="ExtendedSettings"/> (saf) üzerinde tutulur; "UYGULA" ile <see cref="Commit"/> kaydeder ve yayınlar.
    /// </summary>
    public sealed class ExtendedSettingsSection
    {
        /// <summary>Panelin ortak satır sistemine (arama, sıfırlama, değişti noktası) kaydedilecek denetim.</summary>
        public sealed class Entry
        {
            public Component Control;
            public Func<bool> Changed;
            public Action Reset;
            public Func<string> Warn;
            public int Tab;
        }

        public const int TabKontrol = 3;
        public const int TabGrafik = 1;
        public const int TabSes = 2;
        public const int TabErisim = 4;

        private static readonly string[] CustomColorNames = { "Mavi", "Turuncu", "Camgöbeği", "Sarı", "Macenta", "Beyaz" };
        private static readonly int[] CustomColorHex = { 0x4DA3FF, 0xFF9F1C, 0x2EE6C8, 0xFFD60A, 0xE040FB, 0xFFFFFF };

        private readonly ISettingsStore _store;
        private readonly Action _markDirty;
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Action> _refreshers = new List<Action>();
        private ExtendedSettings _working;
        private ExtendedSettings _defaults;
        private ExtendedSettings _saved;
        private Text _cm360Text;
        private Text _paletteText;
        private Text _audioText;
        private Text _shareStatus;
        private InputField _shareField;

        public IReadOnlyList<Entry> Entries => _entries;
        public ExtendedSettings Working => _working;
        public bool IsDirty => !_working.SameAs(_saved);

        private ExtendedSettingsSection(ISettingsStore store, Action markDirty)
        {
            _store = store;
            _markDirty = markDirty;
            _saved = ExtendedSettingsStore.Load(store);
            _working = _saved.Clone();
            _defaults = ExtendedSettingsStore.NewDefault();
            ExtendedSettingsHub.Publish(_saved);
        }

        /// <summary>Denetimleri ilgili sekme içeriklerine ekler. Mağaza null ise yalnızca oturum içinde geçerlidir.</summary>
        public static ExtendedSettingsSection Build(RectTransform kontrol, RectTransform grafik, RectTransform ses, RectTransform erisim,
            ISettingsStore store, Action markDirty)
        {
            var s = new ExtendedSettingsSection(store, markDirty);
            s.BuildControls(kontrol);
            s.BuildDisplay(grafik);
            s.BuildAudio(ses);
            s.BuildAccessibility(erisim);
            s.BuildShare(kontrol);
            s.RefreshInfo();
            return s;
        }

        // ------------------------------------------------------------------ Kontrol
        private void BuildControls(RectTransform c)
        {
            var w = _working;
            var d = _defaults;

            Slider(c, TabKontrol, "ADS · Ironsight / kırmızı nokta çarpanı", 0.2f, 2f, () => w.Ads.IronSights, v => w.Ads.IronSights = v, () => d.Ads.IronSights, v => "×" + v.ToString("0.00"));
            Slider(c, TabKontrol, "ADS · Düşük zoom (1.5-3x) çarpanı", 0.2f, 2f, () => w.Ads.LowZoom, v => w.Ads.LowZoom = v, () => d.Ads.LowZoom, v => "×" + v.ToString("0.00"));
            Slider(c, TabKontrol, "ADS · Orta zoom (3-5x) çarpanı", 0.2f, 2f, () => w.Ads.MidZoom, v => w.Ads.MidZoom = v, () => d.Ads.MidZoom, v => "×" + v.ToString("0.00"));
            Slider(c, TabKontrol, "ADS · Yüksek zoom (5x+) çarpanı", 0.2f, 2f, () => w.Ads.HighZoom, v => w.Ads.HighZoom = v, () => d.Ads.HighZoom, v => "×" + v.ToString("0.00"));
            Slider(c, TabKontrol, "ADS · Fiziksel eşleşme (açı ↔ ekran)", 0f, 1f, () => w.Ads.MatchCoefficient, v => w.Ads.MatchCoefficient = v, () => d.Ads.MatchCoefficient,
                v => Mathf.RoundToInt(v * 100f) + "%");

            Toggle(c, TabKontrol, "Ham fare girdisi", () => w.Mouse.RawInput, v => w.Mouse.RawInput = v, () => d.Mouse.RawInput,
                () => w.Mouse.RawInput ? null : "Ham girdi kapalıyken işletim sistemi ivmesi nişanı bozabilir.");
            Toggle(c, TabKontrol, "Fare ivmesi", () => w.Mouse.Acceleration, v => w.Mouse.Acceleration = v, () => d.Mouse.Acceleration,
                () => w.Mouse.Acceleration ? "İvme kas hafızasını bozar; rekabetçi oyunda önerilmez." : null);
            Slider(c, TabKontrol, "Fare ivme gücü", 0f, 1f, () => w.Mouse.AccelStrength, v => w.Mouse.AccelStrength = v, () => d.Mouse.AccelStrength,
                v => Mathf.RoundToInt(v * 100f) + "%");
            Slider(c, TabKontrol, "Fare yumuşatma", 0f, 60f, () => w.Mouse.SmoothingMs, v => w.Mouse.SmoothingMs = v, () => d.Mouse.SmoothingMs,
                v => v < 0.5f ? "Kapalı" : v.ToString("0") + " ms", () => w.Mouse.SmoothingMs > 20f ? "Yüksek yumuşatma girdi gecikmesi ekler." : null);
            Slider(c, TabKontrol, "Dikey / yatay hassasiyet oranı", 0.5f, 1.5f, () => w.Mouse.YxRatio, v => w.Mouse.YxRatio = v, () => d.Mouse.YxRatio, v => "×" + v.ToString("0.00"));
            Slider(c, TabKontrol, "Fare DPI (cm/360 hesabı)", 100f, 6400f, () => w.Mouse.Dpi, v => w.Mouse.Dpi = Mathf.RoundToInt(v / 50f) * 50, () => d.Mouse.Dpi,
                v => Mathf.RoundToInt(v / 50f) * 50 + " DPI", null, true);

            _cm360Text = UiFactory.Label(c, string.Empty, UiTheme.FontNormal - 2, TextAnchor.MiddleLeft, UiTheme.Amber);
            UiFactory.LayoutSize(_cm360Text, -1f, 32f, 1f);
        }

        // ------------------------------------------------------------------ Görüntü
        private void BuildDisplay(RectTransform c)
        {
            var w = _working;
            var d = _defaults;
            Toggle(c, TabGrafik, "Bağımsız ADS görüş alanı", () => w.Display.AdsFovIndependent, v => w.Display.AdsFovIndependent = v, () => d.Display.AdsFovIndependent);
            Slider(c, TabGrafik, "Bağımsız ADS FOV (dikey)", 15f, 60f, () => w.Display.AdsFovDegrees, v => w.Display.AdsFovDegrees = v, () => d.Display.AdsFovDegrees,
                v => v.ToString("0") + "°", () => w.Display.AdsFovIndependent && w.Display.AdsFovDegrees > 55f ? "Yüksek ADS FOV'si yakınlaştırma hissini yok eder." : null, true);
            Slider(c, TabGrafik, "Parlaklık (gama)", 0.7f, 1.4f, () => w.Display.Brightness, v => w.Display.Brightness = v, () => d.Display.Brightness, v => v.ToString("0.00"));
            Slider(c, TabGrafik, "Namlu ateşi / flaş yoğunluğu", 0f, 1f, () => w.Display.FlashIntensity, v => w.Display.FlashIntensity = v, () => d.Display.FlashIntensity,
                v => Mathf.RoundToInt(v * 100f) + "%");
        }

        // ------------------------------------------------------------------ Ses
        private void BuildAudio(RectTransform c)
        {
            var w = _working;
            var d = _defaults;
            Selector(c, TabSes, "Çıkış cihazı önayarı", AudioMixPresets.Names, () => w.Audio.OutputMode, v => w.Audio.OutputMode = v, () => d.Audio.OutputMode);
            Slider(c, TabSes, "Dinamik aralık (gece ↔ geniş)", 0f, 1f, () => w.Audio.DynamicRange, v => w.Audio.DynamicRange = v, () => d.Audio.DynamicRange,
                v => Mathf.RoundToInt(v * 100f) + "%");
            Slider(c, TabSes, "Ayak sesi / yön vurgusu", 0f, 1f, () => w.Audio.FootstepEmphasis, v => w.Audio.FootstepEmphasis = v, () => d.Audio.FootstepEmphasis,
                v => Mathf.RoundToInt(v * 100f) + "%");
            Slider(c, TabSes, "Bas (patlama) yoğunluğu", 0f, 1f, () => w.Audio.LowFrequencyIntensity, v => w.Audio.LowFrequencyIntensity = v, () => d.Audio.LowFrequencyIntensity,
                v => Mathf.RoundToInt(v * 100f) + "%");
            Slider(c, TabSes, "Odak dışında ses seviyesi", 0f, 1f, () => w.Audio.UnfocusedVolume, v => w.Audio.UnfocusedVolume = v, () => d.Audio.UnfocusedVolume,
                v => Mathf.RoundToInt(v * 100f) + "%");
            Toggle(c, TabSes, "Telsiz konuşurken ortamı kıs", () => w.Audio.VoiceDucking, v => w.Audio.VoiceDucking = v, () => d.Audio.VoiceDucking);
            _audioText = UiFactory.Label(c, string.Empty, UiTheme.FontNormal - 2, TextAnchor.MiddleLeft, UiTheme.Amber);
            UiFactory.LayoutSize(_audioText, -1f, 32f, 1f);
        }

        // ------------------------------------------------------------------ Erişilebilirlik
        private void BuildAccessibility(RectTransform c)
        {
            var w = _working;
            var d = _defaults;
            Selector(c, TabErisim, "HUD renk körlüğü paleti", ColorBlindHud.Names, () => w.Visual.Kind, v => w.Visual.Kind = v, () => d.Visual.Kind,
                () => w.Visual.Kind == (int)ColorBlindKind.Ozel
                    ? ColorBlindHud.SeparationWarning(ColorBlindKind.Deuteranopi, w.Visual.Palette()) ?? ColorBlindHud.SeparationWarning(ColorBlindKind.Tritanopi, w.Visual.Palette())
                    : null);
            Selector(c, TabErisim, "Özel palet ana rengi", CustomColorNames, () => Mathf.Max(0, Array.IndexOf(CustomColorHex, w.Visual.CustomColorHex)),
                v => w.Visual.CustomColorHex = CustomColorHex[Mathf.Clamp(v, 0, CustomColorHex.Length - 1)], () => 0);
            Slider(c, TabErisim, "Tam ekran renk düzeltme gücü", 0f, 1f, () => w.Visual.FilterStrength, v => w.Visual.FilterStrength = v, () => d.Visual.FilterStrength,
                v => v < 0.01f ? "Kapalı" : Mathf.RoundToInt(v * 100f) + "%");
            Slider(c, TabErisim, "Hareket azaltma (FOV vuruşu / sarsıntı)", 0f, 1f, () => w.Visual.MotionScale, v => w.Visual.MotionScale = v, () => d.Visual.MotionScale,
                v => Mathf.RoundToInt(v * 100f) + "%", () => w.Visual.MotionScale < 0.01f ? "Tüm kamera hareket efektleri kapalı." : null);
            Slider(c, TabErisim, "Kafa sallanması", 0f, 1f, () => w.Visual.HeadBob, v => w.Visual.HeadBob = v, () => d.Visual.HeadBob, v => Mathf.RoundToInt(v * 100f) + "%");
            Slider(c, TabErisim, "Düşman ana hat vurgusu", 0f, 1f, () => w.Visual.EnemyOutline, v => w.Visual.EnemyOutline = v, () => d.Visual.EnemyOutline,
                v => v < 0.01f ? "Kapalı" : Mathf.RoundToInt(v * 100f) + "%");
            Toggle(c, TabErisim, "Yüksek kontrastlı HUD", () => w.Visual.HighContrastHud, v => w.Visual.HighContrastHud = v, () => d.Visual.HighContrastHud);
            _paletteText = UiFactory.Label(c, string.Empty, UiTheme.FontNormal - 2, TextAnchor.MiddleLeft, UiTheme.Amber);
            UiFactory.LayoutSize(_paletteText, -1f, 32f, 1f);
        }

        // ------------------------------------------------------------------ Profil kodu
        private void BuildShare(RectTransform c)
        {
            var copy = UiFactory.Button(c, "PROFİL KODUNU KOPYALA", CopyCode, UiButtonStyle.Default);
            UiFactory.LayoutSize(copy, 0f, UiTheme.ButtonHeight, 1f);
            var paste = UiFactory.Button(c, "PANODAKİ PROFİL KODUNU YÜKLE", PasteCode, UiButtonStyle.Default);
            UiFactory.LayoutSize(paste, 0f, UiTheme.ButtonHeight, 1f);
            _shareStatus = UiFactory.Label(c, string.Empty, UiTheme.FontNormal - 2, TextAnchor.MiddleLeft, UiTheme.Amber);
            UiFactory.LayoutSize(_shareStatus, -1f, 28f, 1f);
            _entries.Add(new Entry { Control = copy, Tab = TabKontrol });
            _entries.Add(new Entry { Control = paste, Tab = TabKontrol });
        }

        private void CopyCode()
        {
            try
            {
                GUIUtility.systemCopyBuffer = SettingsShareCode.Encode(_working);
                _shareStatus.text = "Kod panoya kopyalandı.";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _shareStatus.text = "Pano kullanılamadı.";
            }
        }

        private void PasteCode()
        {
            string text;
            try { text = GUIUtility.systemCopyBuffer; }
            catch { text = null; }
            if (!SettingsShareCode.TryDecode(text, out var loaded))
            {
                _shareStatus.text = "Panodaki metin geçerli bir profil kodu değil.";
                return;
            }
            CopyInto(loaded, _working);
            Refresh();
            _markDirty?.Invoke();
            _shareStatus.text = "Profil yüklendi — kaydetmek için UYGULA.";
        }

        // ------------------------------------------------------------------ Satır üreticiler
        private void Slider(RectTransform parent, int tab, string label, float min, float max, Func<float> get, Action<float> set, Func<float> def,
            Func<float, string> format, Func<string> warn = null, bool whole = false)
        {
            var slider = UiWidgets.LabeledSlider(parent, label, min, max, Mathf.Clamp(get(), min, max), v =>
            {
                set(v);
                RefreshInfo();
                _markDirty?.Invoke();
            }, format, whole);
            _refreshers.Add(() => slider.value = Mathf.Clamp(get(), min, max));
            _entries.Add(new Entry { Control = slider, Tab = tab, Warn = warn, Changed = () => Mathf.Abs(get() - def()) > 0.0005f, Reset = () => set(def()) });
        }

        private void Toggle(RectTransform parent, int tab, string label, Func<bool> get, Action<bool> set, Func<bool> def, Func<string> warn = null)
        {
            var toggle = UiWidgets.LabeledToggle(parent, label, get(), v =>
            {
                set(v);
                RefreshInfo();
                _markDirty?.Invoke();
            });
            _refreshers.Add(() => toggle.SetIsOnWithoutNotify(get()));
            _entries.Add(new Entry { Control = toggle, Tab = tab, Warn = warn, Changed = () => get() != def(), Reset = () => set(def()) });
        }

        private void Selector(RectTransform parent, int tab, string label, IReadOnlyList<string> options, Func<int> get, Action<int> set, Func<int> def, Func<string> warn = null)
        {
            var sel = UiWidgets.OptionSelector(parent, label, options, Mathf.Clamp(get(), 0, options.Count - 1), i =>
            {
                set(i);
                RefreshInfo();
                _markDirty?.Invoke();
            });
            _refreshers.Add(() => sel.Index = Mathf.Clamp(get(), 0, options.Count - 1));
            _entries.Add(new Entry { Control = sel, Tab = tab, Warn = warn, Changed = () => get() != def(), Reset = () => set(def()) });
        }

        // ------------------------------------------------------------------ Durum
        private void RefreshInfo()
        {
            if (_cm360Text != null)
            {
                // Hassasiyet birimi: GameSettings.MouseSensitivity derece/sayım kabul edilir (0.12 varsayılan).
                var degPerCount = Mathf.Max(0.001f, ReadBaseSensitivity());
                var cm = SensitivityConverter.Cm360(degPerCount, _working.Mouse.Dpi);
                var adsHigh = AdsSensitivityModel.Cm360Ads(cm, _working.Ads.HighZoom);
                _cm360Text.text = "360° dönüş: " + cm.ToString("0.0") + " cm (hipfire)  ·  " + adsHigh.ToString("0.0") + " cm (yüksek zoom ADS)";
            }
            if (_paletteText != null)
            {
                var p = _working.Visual.Palette();
                var sep = ColorBlindHud.EnemyTeamSeparation(ColorBlindKind.Deuteranopi, p);
                _paletteText.text = "Düşman/takım ayrımı (deuteranopi benzetimi): " + sep.ToString("0") + (sep >= 120f ? " — iyi" : " — zayıf");
            }
            if (_audioText != null)
            {
                var prof = _working.Audio.Resolve();
                var range = AudioMixPresets.EffectiveRangeDb(prof, -40f, -6f);
                _audioText.text = "Efektif dinamik aralık: " + range.ToString("0") + " dB  ·  sıkıştırma " + prof.Ratio.ToString("0.0") + ":1";
            }
        }

        /// <summary>Panel, GameSettings.MouseSensitivity'yi buradan günceller (cm/360 gösterimi için).</summary>
        public float BaseSensitivity { get; set; } = 0.12f;
        private float ReadBaseSensitivity() => BaseSensitivity;

        /// <summary>Çalışma kopyasından denetimleri yeniler.</summary>
        public void Refresh()
        {
            foreach (var r in _refreshers)
                r();
            RefreshInfo();
        }

        /// <summary>Çalışma kopyasını varsayılana döndürür.</summary>
        public void ResetAll()
        {
            CopyInto(_defaults, _working);
            Refresh();
        }

        /// <summary>Kaydeder, yayınlar ve çalışma zamanı etkilerini uygular.</summary>
        public void Commit()
        {
            _working.Sanitize();
            ExtendedSettingsStore.Save(_store, _working);
            _saved = _working.Clone();
            ExtendedSettingsHub.Publish(_saved);
            ExtendedSettingsRuntime.Apply(_saved);
        }

        /// <summary>Kaydedilmemiş değişiklikleri atar (pencere GERİ ile kapanırken).</summary>
        public void Revert()
        {
            CopyInto(_saved, _working);
        }

        private static void CopyInto(ExtendedSettings from, ExtendedSettings to)
        {
            var c = from.Clone();
            to.Ads = c.Ads;
            to.Mouse = c.Mouse;
            to.Audio = c.Audio;
            to.Visual = c.Visual;
            to.Display = c.Display;
        }
    }
}
