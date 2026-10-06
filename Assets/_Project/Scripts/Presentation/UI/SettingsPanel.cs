using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.HdrMix;
using Project.Infrastructure.Rendering;
using Project.Presentation.Bootstrap;
using Project.Application.Localization;
using Project.Infrastructure.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Ayarlar penceresi (ana menü ve duraklatma menüsü ortak). Ebeveyni karartıp ortada bir pencere açar:
    /// KONTROL (fare hassasiyeti, nişan çarpanı, Y eksenini ters çevir), GÖRÜNTÜ (görüş alanı 60–100, kalite
    /// Düşük/Orta/Yüksek/Ultra, tam ekran, FPS göster), SES (ana ses, ortam sesi).
    /// <para>Değişiklikler bir çalışma kopyasında tutulur; ses seviyeleri anında önizlenir. "UYGULA" ayarları
    /// <see cref="SettingsService.Apply"/> ile kaydeder ve motor etkilerini (ses, kalite, tam ekran) hemen uygular.
    /// "GERİ" uygulanmamış önizlemeleri geri alır. Pencere kapanınca kendini yok eder ve <c>onClose</c> çağrılır.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsPanel : MonoBehaviour
    {
        private const float WindowWidth = 1180f;
        private const float WindowHeight = 820f;
        private const float MaxMenuFieldOfView = 100f;

        private SettingsService _settings;
        private GameSettings _working;
        private Action _onClose;
        private bool _dirty;
        private bool _closed;
        private CanvasGroup _group;
        private float _fade;
        private Text _statusText;
        private Button _applyButton;
        private Button _backButton;

        private UiKitTabBar _tabBar;
        private Text _tabHint;
        private RectTransform[] _tabContents;
        private GameObject[] _tabScrolls;
        private static readonly string[] TabHints =
        {
            "Oyun içi görünüm ve dil tercihleri.",
            "Kalite ve çözünürlük; çoğu görsel ayar UYGULA ile devreye girer.",
            "Ses seviyeleri sürüklerken anında önizlenir.",
            "Fare/gamepad hassasiyeti ve tuş atamaları.",
            "Renk körlüğü paletleri, altyazı ve yardımcı seçenekler."
        };
        private Slider _sensitivity;
        private Slider _ads;
        private Slider _fov;
        private Slider _viewmodelFov;
        private Toggle _adsFovRel;
        private Toggle _volFog;
        private Toggle _contactShadows;
        private Toggle _ssr;
        private Slider _master;
        private Slider _ambient;
        private Toggle _invertY;
        private UiOptionSelector _resolution;
        private static readonly string[] ColorBlindNames = { "Kapalı", "Deuteranopi", "Protanopi", "Tritanopi" };
        private static readonly string[] SubtitleSizeNames = { "Küçük", "Normal", "Büyük", "Çok büyük" };
        private UiOptionSelector _windowMode;
        private UiOptionSelector _frameCap;
        private UiOptionSelector _crossColor;
        private Toggle _vsync;
        private Toggle _motionBlur;
        private UiOptionSelector _colorBlind;
        private UiOptionSelector _subtitleSize;
        private Slider _aimAssist;
        private Toggle _subtitleBg;
        private UiOptionSelector _toggleAds;
        private static readonly string[] AdsModeNames = { "Bas-Tut", "Aç-Kapa" };
        private Toggle _toggleCrouch;
        private Slider _renderScale;
        private Slider _hudScale;
        private Slider _hudOpacity;
        private Slider _cameraShake;
        private Slider _crossSize;
        private Slider _sfx;
        private Slider _music;
        private Slider _voice;
        private Toggle _showFps;
        private UiOptionSelector _quality;
        private UiOptionSelector _language;
        private Button _keysButton;

        // ---- Ayarlar UX katmanı (arama, değişti noktası, satır sıfırlama, önerilen rozetleri, uyarılar, klavye gezinmesi) ----
        private sealed class Row
        {
            public string Name;
            public RectTransform Root;
            public Selectable Nav;
            public int Tab;
            public bool Extra;
            public Func<bool> Changed;
            public Action Reset;
            public Func<string> Warn;
            public Func<bool> Rec;
            public Func<string> RecLabel;
            public LayoutElement Layout;
            public Image Dot;
            public Image Chip;
            public GameObject Strip;
            public Text StripText;
            public GameObject Badge;
            public GameObject ResetButton;
        }

        private static readonly string[] TabNames = { "OYUN", "GRAFİK", "SES", "KONTROLLER", "ERİŞİLEBİLİRLİK" };
        private const float ChromeHeight = 20f;
        private readonly List<Row> _rows = new List<Row>();
        private GameSettings _defaults;
        private int _recQuality = 2;
        private InputField _search;
        private string _query = string.Empty;
        private int[] _counts;
        private Image[] _tabIcons;
        private int _currentTab;
        private float _rowTimer;

        /// <summary>Uygulanmamış değişiklik var mı?</summary>
        public bool IsDirty => _dirty;

        /// <summary>Pencere açık mı?</summary>
        public bool IsOpen => !_closed;

        /// <summary>Ayarlar uygulandığında (kaydedildikten sonra) çağrılır.</summary>
        public event Action<GameSettings> Applied;

        /// <summary>
        /// Ayarlar penceresini oluşturur (ebeveyni doldurur, arkasını karartır).
        /// </summary>
        /// <param name="parent">Tuval veya tam ekran kök.</param>
        /// <param name="settings">Ayar servisi; null ise <see cref="GameSession.Settings"/> kullanılır.</param>
        /// <param name="onClose">Pencere kapandıktan sonra çağrılır.</param>
        public static SettingsPanel Create(Transform parent, SettingsService settings, Action onClose)
        {
            var root = UiFactory.CreateRect("[Ayarlar]", parent);
            root.SetAsLastSibling();

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiTheme.Overlay;
            dim.raycastTarget = true;

            var panel = root.gameObject.AddComponent<SettingsPanel>();
            panel._settings = ResolveSettings(settings);
            panel._onClose = onClose;
            panel._working = panel._settings != null ? panel._settings.Current.Clone() : new GameSettings();
            panel._group = UiFactory.EnsureCanvasGroup(root);
            panel._group.alpha = 0f;
            panel.Build(root);
            return panel;
        }

        /// <summary>Çalışma kopyasını kaydeder ve motor etkilerini uygular (pencere açık kalır).</summary>
        public void Apply()
        {
            if (_closed)
                return;

            var sanitized = SettingsService.Sanitize(_working);
            try
            {
                if (_settings != null)
                {
                    _settings.Apply(sanitized);
                    sanitized = _settings.Current.Clone();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            ApplyImmediateEffects(sanitized);
            _working = sanitized.Clone();
            SetDirty(false);
            ShowStatus("Ayarlar kaydedildi.", UiTheme.Success);

            try
            {
                Applied?.Invoke(sanitized);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Uygular ve kapatır.</summary>
        public void ApplyAndClose()
        {
            Apply();
            Close();
        }

        /// <summary>Uygulanmamış önizlemeleri geri alır ve kapatır.</summary>
        public void Close()
        {
            if (_closed)
                return;

            _closed = true;
            if (_dirty)
                RevertPreview();

            ClearSelection();
            var callback = _onClose;
            _onClose = null;

            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);

            if (callback != null)
            {
                try
                {
                    callback();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>Çalışma kopyasını varsayılan değerlere döndürür (oyun ayarları — tim/zorluk/ad — korunur).</summary>
        public void ResetToDefaults()
        {
            var defaults = new GameSettings
            {
                TeamCount = _working.TeamCount,
                Difficulty = _working.Difficulty,
                Insertion = _working.Insertion,
                PlayerName = _working.PlayerName
            };
            _working = SettingsService.Sanitize(defaults);
            RefreshControls();
            PreviewAudio();
            SetDirty(true);
            ShowStatus("Varsayılan değerler yüklendi — kaydetmek için UYGULA.", UiTheme.Amber);
        }

        /// <summary>
        /// Ayarların motor tarafı etkileri: ses seviyeleri, grafik kalitesi (post-processing + gölge + AA), tam ekran.
        /// Ayar servisinin Changed olayı da bunları uygular; burada tekrar uygulanması zararsızdır (idempotent).
        /// </summary>
        public static void ApplyImmediateEffects(GameSettings settings)
        {
            if (settings == null)
                return;

            Loc.SetLanguage(settings.Language);

            try
            {
                GameAudio.MasterVolume = settings.MasterVolume;
                GameAudio.AmbientVolume = settings.AmbientVolume * Mathf.Clamp01(settings.MusicVolume);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                PostProcessing.ApplyQuality(settings.QualityLevel);
                PostProcessing.SetMotionBlur(settings.MotionBlur);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            AdvancedDisplay.Apply(settings);
        }

        private static SettingsService ResolveSettings(SettingsService settings)
        {
            if (settings != null)
                return settings;

            try
            {
                GameSession.EnsureInitialized();
                return GameSession.Settings;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        private void Build(RectTransform root)
        {
            var parts = UiKitPanel.Window(root, new Vector2(WindowWidth, WindowHeight), Loc.Get("settings.title", "AYARLAR"),
                "Değişiklikler UYGULA ile kaydedilir  ·  Ses seviyeleri sürüklerken anında duyulur");
            var window = parts.Window;
            var dimGraphic = root.GetComponent<Image>();
            if (dimGraphic != null)
                dimGraphic.color = UiKitTokens.Scrim;

            // Sol sekme sütunu + sağ içerik alanı.
            var body = parts.Body;
            var tabHost = UiFactory.CreateRect("Tabs", body);
            UiFactory.SetRect(tabHost, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 84f), new Vector2(236f, 0f));
            var tabNames = TabNames;
            _tabBar = UiKitTabBar.Create(tabHost, tabNames, true, SelectTab, 236f, 58f);
            UiFactory.Stretch(_tabBar.GetComponent<RectTransform>());

            var pane = UiKitPanel.Card(body, UiKitTokens.Surface, 12);
            UiFactory.SetRect(pane, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(252f, 84f), Vector2.zero);

            _tabHint = UiFactory.Label(pane, string.Empty, UiKitTokens.FontCaption + 1, TextAnchor.MiddleLeft, UiKitTokens.Sand);
            _tabHint.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_tabHint, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -44f), new Vector2(-350f, -8f));

            _tabContents = new RectTransform[tabNames.Length];
            _tabScrolls = new GameObject[tabNames.Length];
            for (var i = 0; i < tabNames.Length; i++)
            {
                var sc = UiKitScrollFade.VerticalList(pane, out _tabContents[i], UiKitTokens.Surface, 12f, 6);
                UiFactory.SetRect(sc, Vector2.zero, Vector2.one, new Vector2(18f, 10f), new Vector2(-12f, -50f));
                _tabScrolls[i] = sc.gameObject;
                _tabScrolls[i].SetActive(false);
            }

            var tabOyun = _tabContents[0];
            var tabGrafik = _tabContents[1];
            var tabSes = _tabContents[2];
            var tabKontrol = _tabContents[3];
            var tabErisim = _tabContents[4];
            RectTransform content;

            // ---------------------------------------------------------------- KONTROL
            content = tabKontrol;
            _sensitivity = UiWidgets.LabeledSlider(content, "Fare hassasiyeti", SettingsService.MinMouseSensitivity, SettingsService.MaxMouseSensitivity,
                _working.MouseSensitivity, v =>
                {
                    _working.MouseSensitivity = v;
                    SetDirty(true);
                }, v => v.ToString("0.00"));
            _ads = UiWidgets.LabeledSlider(content, "Nişan (ADS) çarpanı", SettingsService.MinAdsMultiplier, SettingsService.MaxAdsMultiplier,
                _working.AdsSensitivityMultiplier, v =>
                {
                    _working.AdsSensitivityMultiplier = v;
                    SetDirty(true);
                }, v => "×" + v.ToString("0.00"));
            _adsFovRel = UiWidgets.LabeledToggle(content, "ADS hassasiyeti FOV'ye göre ölçeklensin", _working.AdsFovRelativeSensitivity, v =>
            {
                _working.AdsFovRelativeSensitivity = v;
                SetDirty(true);
            });
            _invertY = UiWidgets.LabeledToggle(content, "Y eksenini ters çevir", _working.InvertY, v =>
            {
                _working.InvertY = v;
                SetDirty(true);
            });
            UiWidgets.LabeledToggle(content, "Hasar sayıları", DamageNumbersView.Enabled, v => DamageNumbersView.Enabled = v);

            // ---------------------------------------------------------------- GRAFİK
            content = tabGrafik;
            var fovMax = Mathf.Min(MaxMenuFieldOfView, SettingsService.MaxFieldOfView);
            _fov = UiWidgets.LabeledSlider(content, "Görüş alanı (FOV)", SettingsService.MinFieldOfView, fovMax,
                Mathf.Clamp(_working.FieldOfView, SettingsService.MinFieldOfView, fovMax), v =>
                {
                    _working.FieldOfView = v;
                    SetDirty(true);
                }, v => v.ToString("0") + "°", true);
            _viewmodelFov = UiWidgets.LabeledSlider(content, "Silah görüş açısı", SettingsService.MinViewmodelFov, SettingsService.MaxViewmodelFov,
                Mathf.Clamp(_working.ViewmodelFov, SettingsService.MinViewmodelFov, SettingsService.MaxViewmodelFov), v =>
                {
                    _working.ViewmodelFov = v;
                    SetDirty(true);
                }, v => v.ToString("0") + "°", true);
            _quality = UiWidgets.OptionSelector(content, "Grafik kalitesi", MenuText.QualityNames,
                Mathf.Clamp(_working.QualityLevel, 0, MenuText.QualityNames.Length - 1), i =>
                {
                    _working.QualityLevel = i;
                    SetDirty(true);
                });
            content = tabOyun;
            _language = UiWidgets.OptionSelector(content, Loc.Get("settings.language", "Dil"), LocalizationTable.LanguageNames,
                LocalizationTable.IndexOf(_working.Language), i =>
                {
                    _working.Language = LocalizationTable.Languages[Mathf.Clamp(i, 0, LocalizationTable.Languages.Length - 1)];
                    SetDirty(true);
                });
            content = tabGrafik;
            var resolutions = AdvancedDisplay.ResolutionOptions();
            var resLabels = new string[resolutions.Count];
            var resIndex = 0;
            for (var i = 0; i < resolutions.Count; i++)
            {
                resLabels[i] = AdvancedDisplay.ResolutionLabel(resolutions[i]);
                if (resolutions[i].x == _working.ResolutionWidth && resolutions[i].y == _working.ResolutionHeight)
                    resIndex = i;
            }
            _resolution = UiWidgets.OptionSelector(content, Loc.Get("settings.resolution", "Çözünürlük"), resLabels, resIndex, i =>
            {
                var r = resolutions[Mathf.Clamp(i, 0, resolutions.Count - 1)];
                _working.ResolutionWidth = r.x;
                _working.ResolutionHeight = r.y;
                SetDirty(true);
            });
            _windowMode = UiWidgets.OptionSelector(content, Loc.Get("settings.windowMode", "Pencere modu"), AdvancedDisplay.WindowModeNames,
                Mathf.Clamp(_working.WindowMode, 0, 2), i =>
                {
                    _working.WindowMode = i;
                    SetDirty(true);
                });
            _vsync = UiWidgets.LabeledToggle(content, Loc.Get("settings.vsync", "Dikey senkronizasyon (VSync)"), _working.VSync, v =>
            {
                _working.VSync = v;
                SetDirty(true);
            });
            var caps = SettingsService.FrameRateCaps;
            var capLabels = new string[caps.Length];
            var capIndex = 0;
            for (var i = 0; i < caps.Length; i++)
            {
                capLabels[i] = AdvancedDisplay.FrameCapLabel(caps[i]);
                if (caps[i] == _working.FrameRateCap)
                    capIndex = i;
            }
            _frameCap = UiWidgets.OptionSelector(content, Loc.Get("settings.fpsCap", "Kare sınırı"), capLabels, capIndex, i =>
            {
                _working.FrameRateCap = caps[Mathf.Clamp(i, 0, caps.Length - 1)];
                SetDirty(true);
            });
            _renderScale = UiWidgets.LabeledSlider(content, Loc.Get("settings.renderScale", "Render ölçeği"), 0.5f, 1f, _working.RenderScale, v =>
            {
                _working.RenderScale = v;
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));
            _motionBlur = UiWidgets.LabeledToggle(content, Loc.Get("settings.motionBlur", "Hareket bulanıklığı"), _working.MotionBlur, v =>
            {
                _working.MotionBlur = v;
                SetDirty(true);
            });
            _volFog = UiWidgets.LabeledToggle(content, Loc.Get("settings.volFog", "Hacimsel sis"), _working.VolumetricFog, v =>
            {
                _working.VolumetricFog = v;
                SetDirty(true);
            });
            _contactShadows = UiWidgets.LabeledToggle(content, Loc.Get("settings.contactShadows", "Temas gölgeleri"), _working.ContactShadows, v =>
            {
                _working.ContactShadows = v;
                SetDirty(true);
            });
            _ssr = UiWidgets.LabeledToggle(content, Loc.Get("settings.ssr", "Ekran-uzayı yansımalar (SSR)"), _working.ScreenSpaceReflections, v =>
            {
                _working.ScreenSpaceReflections = v;
                SetDirty(true);
            });
            UiWidgets.LabeledToggle(content, Loc.Get("settings.dof", "Alan derinliği (nişan bulanıklığı)"), PostProcessing.DepthOfFieldEnabled,
                v => PostProcessing.SetDepthOfFieldEnabled(v));
            UiWidgets.LabeledToggle(content, Loc.Get("settings.grain", "Film greni"), PostProcessing.FilmGrainEnabled,
                v => PostProcessing.SetFilmGrainEnabled(v));
            UiWidgets.LabeledSlider(content, Loc.Get("settings.sharpness", "Keskinlik"), 0f, 1f,
                Project.Infrastructure.Rendering.Features.SharpenSettings.UserSharpness,
                v => Project.Infrastructure.Rendering.Features.SharpenSettings.UserSharpness = v, v => MenuText.FormatPercent(v));
            content = tabErisim;
            _colorBlind = UiWidgets.OptionSelector(content, Loc.Get("settings.colorBlind", "Renk körlüğü paleti"), ColorBlindNames,
                Mathf.Clamp(_working.ColorBlindPalette, 0, ColorBlindNames.Length - 1), i =>
                {
                    _working.ColorBlindPalette = i;
                    _working.ColorBlindMode = i != 0;
                    SetDirty(true);
                });
            _subtitleSize = UiWidgets.OptionSelector(content, Loc.Get("settings.subtitleSize", "Telsiz altyazı boyutu"), SubtitleSizeNames,
                Mathf.Clamp(_working.SubtitleSize, 0, SubtitleSizeNames.Length - 1), i =>
                {
                    _working.SubtitleSize = i;
                    SetDirty(true);
                });
            _subtitleBg = UiWidgets.LabeledToggle(content, Loc.Get("settings.subtitleBg", "Altyazı arka planı"), _working.SubtitleBackground, v =>
            {
                _working.SubtitleBackground = v;
                SetDirty(true);
            });
            content = tabKontrol;
            _aimAssist = UiWidgets.LabeledSlider(content, Loc.Get("settings.aimAssist", "Gamepad nişan yardımı"), 0f, 100f, _working.AimAssistStrength, v =>
            {
                _working.AimAssistStrength = Mathf.RoundToInt(v);
                SetDirty(true);
            }, v => Mathf.RoundToInt(v) + "%", true);
            content = tabOyun;
            _toggleAds = UiWidgets.OptionSelector(content, Loc.Get("settings.toggleAds", "Nişan"), AdsModeNames,
                _working.ToggleAds ? 1 : 0, i =>
                {
                    _working.ToggleAds = i == 1;
                    SetDirty(true);
                });
            content = tabKontrol;
            _toggleCrouch = UiWidgets.LabeledToggle(content, Loc.Get("settings.toggleCrouch", "Eğilmeyi aç/kapa (basılı tutma yerine)"), _working.ToggleCrouch, v =>
            {
                _working.ToggleCrouch = v;
                SetDirty(true);
            });
            content = tabOyun;
            _cameraShake = UiWidgets.LabeledSlider(content, Loc.Get("settings.cameraShake", "Kamera sarsıntısı"), 0f, 1.5f, _working.CameraShakeIntensity, v =>
            {
                _working.CameraShakeIntensity = v;
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));
            _hudScale = UiWidgets.LabeledSlider(content, Loc.Get("settings.hudScale", "HUD ölçeği"), 0.8f, 1.2f, _working.HudScale, v =>
            {
                _working.HudScale = v;
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));
            _hudOpacity = UiWidgets.LabeledSlider(content, Loc.Get("settings.hudOpacity", "HUD opaklığı"), 0.3f, 1f, _working.HudOpacity, v =>
            {
                _working.HudOpacity = v;
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));
            _crossColor = UiWidgets.OptionSelector(content, Loc.Get("settings.crosshairColor", "Nişangâh rengi"), AdvancedDisplay.CrosshairColorNames,
                Mathf.Clamp(_working.CrosshairColor, 0, AdvancedDisplay.CrosshairColorNames.Length - 1), i =>
                {
                    _working.CrosshairColor = i;
                    SetDirty(true);
                });
            _crossSize = UiWidgets.LabeledSlider(content, Loc.Get("settings.crosshairSize", "Nişangâh boyutu"), 0.5f, 2f, _working.CrosshairSize, v =>
            {
                _working.CrosshairSize = v;
                SetDirty(true);
            }, v => "×" + v.ToString("0.00"));
            _showFps = UiWidgets.LabeledToggle(content, "FPS göster", _working.ShowFps, v =>
            {
                _working.ShowFps = v;
                SetDirty(true);
            });

            content = tabKontrol;
            var keysButton = _keysButton = UiFactory.Button(content, Loc.Get("settings.btn.keybindings", "TUŞ ATAMALARI"), () => KeyBindingsPanel.Create(transform, null), UiButtonStyle.Default);
            UiFactory.LayoutSize(keysButton, 0f, UiTheme.ButtonHeight, 1f);

            // ---------------------------------------------------------------- SES
            content = tabSes;
            _master = UiWidgets.LabeledSlider(content, "Ana ses", 0f, 1f, _working.MasterVolume, v =>
            {
                _working.MasterVolume = v;
                PreviewAudio();
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));
            _ambient = UiWidgets.LabeledSlider(content, "Ortam sesi", 0f, 1f, _working.AmbientVolume, v =>
            {
                _working.AmbientVolume = v;
                PreviewAudio();
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));
            _sfx = UiWidgets.LabeledSlider(content, Loc.Get("settings.sfxVolume", "Efekt sesi"), 0f, 1f, _working.SfxVolume, v =>
            {
                _working.SfxVolume = v;
                PreviewAudio();
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));
            _music = UiWidgets.LabeledSlider(content, Loc.Get("settings.musicVolume", "Müzik / ortam seviyesi"), 0f, 1f, _working.MusicVolume, v =>
            {
                _working.MusicVolume = v;
                PreviewAudio();
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));
            _voice = UiWidgets.LabeledSlider(content, Loc.Get("settings.voiceVolume", "Telsiz / ses"), 0f, 1f, _working.VoiceVolume, v =>
            {
                _working.VoiceVolume = v;
                PreviewAudio();
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));

            // ---------------------------------------------------------------- Alt çubuk
            _statusText = UiFactory.Label(body, string.Empty, UiKitTokens.FontCaption + 1, TextAnchor.MiddleLeft, UiKitTokens.TextMuted);
            _statusText.gameObject.name = "Status";
            UiFactory.SetRect(_statusText, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 62f), new Vector2(0f, 82f));

            var row = UiFactory.HorizontalList(body, 14f, 0, TextAnchor.MiddleRight);
            row.gameObject.name = "Buttons";
            UiFactory.SetRect(row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 56f));
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            var defaults = UiKitButton.Create(row, Loc.Get("settings.btn.defaults", "VARSAYILAN"), ResetToDefaults, UiKitButtonKind.Ghost, 200f, 52f);
            UiFactory.FlexibleSpacer(row);
            _backButton = UiKitButton.Create(row, Loc.Get("common.back", "GERİ"), Close, UiKitButtonKind.Default, 180f, 52f);
            _applyButton = UiKitButton.Create(row, Loc.Get("settings.btn.apply", "UYGULA"), ApplyAndClose, UiKitButtonKind.Primary, 220f, 52f);

            AttachHints();
            UiKitRestyle.All(window);
            BuildTabGlyphs();
            BuildSearchBox(pane);
            RegisterRows();
            SelectTab(0);
            _tabBar.Select(0, false);

            ShowStatus(_settings == null ? "Ayar servisi bulunamadı — değişiklikler yalnızca bu oturumda geçerli." : string.Empty, UiTheme.Amber);
        }

        private void SelectTab(int index)
        {
            if (_tabScrolls == null)
                return;
            for (var i = 0; i < _tabScrolls.Length; i++)
                _tabScrolls[i].SetActive(i == index);
            _currentTab = Mathf.Clamp(index, 0, _tabScrolls.Length - 1);
            if (_tabIcons != null)
            {
                for (var i = 0; i < _tabIcons.Length; i++)
                    if (_tabIcons[i] != null)
                        _tabIcons[i].color = i == index ? UiKitTokens.Accent : UiKitTokens.TextDim;
            }
            UpdateHint();
        }

        private void AttachHints()
        {
            UiKitTooltip.Attach(_renderScale, "Düşük değer FPS'i artırır, görüntüyü yumuşatır.");
            UiKitTooltip.Attach(_fov, "Geniş görüş alanı çevreyi daha çok gösterir.");
            UiKitTooltip.Attach(_hudScale, "Arayüz ve HUD boyutu (%80 – %120).");
            UiKitTooltip.Attach(_master, "Tüm seslerin genel seviyesi; önizleme anlık.");
            UiKitTooltip.Attach(_sensitivity, "Fare hassasiyeti; nişanda ayrı çarpan uygulanır.");
            UiKitTooltip.Attach(_aimAssist, "Yalnızca gamepad kullanırken etkilidir.");
        }

        private void Start()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && _sensitivity != null && _sensitivity.gameObject.activeInHierarchy)
                eventSystem.SetSelectedGameObject(_sensitivity.gameObject);
        }

        private void Update()
        {
            if (_group != null && _fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
                _group.alpha = _fade;
            }

            if (_closed)
                return;

            _rowTimer -= Time.unscaledDeltaTime;
            if (_rowTimer <= 0f)
            {
                _rowTimer = 0.12f;
                RefreshRows();
            }

            HandleKeys();
        }

        private void RefreshControls()
        {
            if (_sensitivity != null)
                _sensitivity.value = _working.MouseSensitivity;   // Değer yazısı onValueChanged ile güncellenir.
            if (_ads != null)
                _ads.value = _working.AdsSensitivityMultiplier;
            if (_fov != null)
                _fov.value = Mathf.Clamp(_working.FieldOfView, _fov.minValue, _fov.maxValue);
            if (_viewmodelFov != null)
                _viewmodelFov.value = Mathf.Clamp(_working.ViewmodelFov, _viewmodelFov.minValue, _viewmodelFov.maxValue);
            if (_adsFovRel != null)
                _adsFovRel.SetIsOnWithoutNotify(_working.AdsFovRelativeSensitivity);
            if (_volFog != null)
                _volFog.SetIsOnWithoutNotify(_working.VolumetricFog);
            if (_contactShadows != null)
                _contactShadows.SetIsOnWithoutNotify(_working.ContactShadows);
            if (_ssr != null)
                _ssr.SetIsOnWithoutNotify(_working.ScreenSpaceReflections);
            if (_master != null)
                _master.value = _working.MasterVolume;
            if (_ambient != null)
                _ambient.value = _working.AmbientVolume;
            if (_invertY != null)
                _invertY.SetIsOnWithoutNotify(_working.InvertY);
            if (_vsync != null)
                _vsync.SetIsOnWithoutNotify(_working.VSync);
            if (_motionBlur != null)
                _motionBlur.SetIsOnWithoutNotify(_working.MotionBlur);
            if (_colorBlind != null)
                _colorBlind.Index = Mathf.Clamp(_working.ColorBlindPalette, 0, ColorBlindNames.Length - 1);
            if (_subtitleSize != null)
                _subtitleSize.Index = Mathf.Clamp(_working.SubtitleSize, 0, SubtitleSizeNames.Length - 1);
            if (_subtitleBg != null)
                _subtitleBg.SetIsOnWithoutNotify(_working.SubtitleBackground);
            if (_aimAssist != null)
                _aimAssist.value = _working.AimAssistStrength;
            if (_toggleAds != null)
                _toggleAds.Index = _working.ToggleAds ? 1 : 0;
            if (_toggleCrouch != null)
                _toggleCrouch.SetIsOnWithoutNotify(_working.ToggleCrouch);
            if (_renderScale != null)
                _renderScale.value = _working.RenderScale;
            if (_cameraShake != null)
                _cameraShake.value = _working.CameraShakeIntensity;
            if (_hudScale != null)
                _hudScale.value = _working.HudScale;
            if (_hudOpacity != null)
                _hudOpacity.value = _working.HudOpacity;
            if (_crossSize != null)
                _crossSize.value = _working.CrosshairSize;
            if (_sfx != null)
                _sfx.value = _working.SfxVolume;
            if (_music != null)
                _music.value = _working.MusicVolume;
            if (_voice != null)
                _voice.value = _working.VoiceVolume;
            if (_windowMode != null)
                _windowMode.Index = Mathf.Clamp(_working.WindowMode, 0, 2);
            if (_crossColor != null)
                _crossColor.Index = Mathf.Clamp(_working.CrosshairColor, 0, AdvancedDisplay.CrosshairColorNames.Length - 1);
            if (_frameCap != null)
                _frameCap.Index = Mathf.Max(0, Array.IndexOf(SettingsService.FrameRateCaps, _working.FrameRateCap));
            if (_resolution != null)
            {
                var opts = AdvancedDisplay.ResolutionOptions();
                var idx = opts.FindIndex(o => o.x == _working.ResolutionWidth && o.y == _working.ResolutionHeight);
                _resolution.Index = Mathf.Max(0, idx);
            }
            if (_showFps != null)
                _showFps.SetIsOnWithoutNotify(_working.ShowFps);
            if (_language != null)
                _language.Index = LocalizationTable.IndexOf(_working.Language);
            if (_quality != null)
                _quality.Index = Mathf.Clamp(_working.QualityLevel, 0, MenuText.QualityNames.Length - 1);
        }

        private void PreviewAudio()
        {
            try
            {
                GameAudio.MasterVolume = Mathf.Clamp01(_working.MasterVolume);
                GameAudio.AmbientVolume = Mathf.Clamp01(_working.AmbientVolume) * Mathf.Clamp01(_working.MusicVolume);
                MixerRouting.ApplySettings(_working.SfxVolume, _working.MusicVolume, _working.VoiceVolume); // mikser önizlemesi (yoksa no-op)
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ================================================================ UX katmanı

        private GameSettings BuildDefaults()
        {
            return SettingsService.Sanitize(new GameSettings
            {
                TeamCount = _working.TeamCount,
                Difficulty = _working.Difficulty,
                Insertion = _working.Insertion,
                PlayerName = _working.PlayerName
            });
        }

        private static bool Same<T>(T a, T b)
        {
            if (a is float fa && b is float fb)
                return Mathf.Abs(fa - fb) < 0.0005f;
            return EqualityComparer<T>.Default.Equals(a, b);
        }

        private static RectTransform RowRoot(Component control)
        {
            if (control is Slider)
                return control.transform.parent as RectTransform;
            return control.transform as RectTransform;
        }

        private static string RowName(Component control, RectTransform root)
        {
            if (control is UiOptionSelector sel && sel.Label != null)
                return sel.Label.text;
            var t = root.Find("Label");
            if (t == null && root.childCount > 0)
                t = root.GetChild(0);
            var text = t != null ? t.GetComponent<Text>() : null;
            return text != null ? text.text : root.name;
        }

        private void RegisterRows()
        {
            _defaults = BuildDefaults();
            _recQuality = Mathf.Clamp(SettingsUxRules.RecommendedQuality(), 0, MenuText.QualityNames.Length - 1);
            var map = new Dictionary<Transform, Row>();

            void R<T>(Component c, Func<GameSettings, T> get, Action<GameSettings, T> set,
                Func<string> warn = null, Func<bool> rec = null, Func<string> recLabel = null)
            {
                if (c == null)
                    return;
                var root = RowRoot(c);
                if (root == null)
                    return;
                var row = new Row
                {
                    Name = RowName(c, root),
                    Root = root,
                    Nav = c is UiOptionSelector os ? os.Selectable : c as Selectable,
                    Changed = () => !Same(get(_working), get(_defaults)),
                    Reset = () => set(_working, get(_defaults)),
                    Warn = warn,
                    Rec = rec,
                    RecLabel = recLabel
                };
                map[root] = row;
            }

            R(_sensitivity, s => s.MouseSensitivity, (s, v) => s.MouseSensitivity = v);
            R(_ads, s => s.AdsSensitivityMultiplier, (s, v) => s.AdsSensitivityMultiplier = v);
            R(_adsFovRel, s => s.AdsFovRelativeSensitivity, (s, v) => s.AdsFovRelativeSensitivity = v);
            R(_invertY, s => s.InvertY, (s, v) => s.InvertY = v);
            R(_fov, s => s.FieldOfView, (s, v) => s.FieldOfView = v, () => SettingsUxRules.FovWarning(_working.FieldOfView));
            R(_viewmodelFov, s => s.ViewmodelFov, (s, v) => s.ViewmodelFov = v,
                () => SettingsUxRules.ViewmodelFovWarning(_working.ViewmodelFov, SettingsService.MinViewmodelFov, SettingsService.MaxViewmodelFov));
            R(_quality, s => s.QualityLevel, (s, v) => s.QualityLevel = v, null,
                () => _working.QualityLevel == _recQuality, () => MenuText.QualityNames[_recQuality].ToUpperInvariant());
            R(_language, s => s.Language, (s, v) => s.Language = v);
            R(_resolution, s => new Vector2Int(s.ResolutionWidth, s.ResolutionHeight), (s, v) => { s.ResolutionWidth = v.x; s.ResolutionHeight = v.y; });
            R(_windowMode, s => s.WindowMode, (s, v) => s.WindowMode = v);
            R(_vsync, s => s.VSync, (s, v) => s.VSync = v);
            R(_frameCap, s => s.FrameRateCap, (s, v) => s.FrameRateCap = v);
            R(_renderScale, s => s.RenderScale, (s, v) => s.RenderScale = v, () => SettingsUxRules.RenderScaleWarning(_working.RenderScale),
                () => Mathf.Abs(_working.RenderScale - RecommendedScale()) < 0.06f, () => MenuText.FormatPercent(RecommendedScale()));
            R(_motionBlur, s => s.MotionBlur, (s, v) => s.MotionBlur = v);
            R(_volFog, s => s.VolumetricFog, (s, v) => s.VolumetricFog = v);
            R(_contactShadows, s => s.ContactShadows, (s, v) => s.ContactShadows = v);
            R(_ssr, s => s.ScreenSpaceReflections, (s, v) => s.ScreenSpaceReflections = v);
            R(_colorBlind, s => s.ColorBlindPalette, (s, v) => { s.ColorBlindPalette = v; s.ColorBlindMode = v != 0; });
            R(_subtitleSize, s => s.SubtitleSize, (s, v) => s.SubtitleSize = v);
            R(_subtitleBg, s => s.SubtitleBackground, (s, v) => s.SubtitleBackground = v);
            R(_aimAssist, s => s.AimAssistStrength, (s, v) => s.AimAssistStrength = v);
            R(_toggleAds, s => s.ToggleAds, (s, v) => s.ToggleAds = v);
            R(_toggleCrouch, s => s.ToggleCrouch, (s, v) => s.ToggleCrouch = v);
            R(_cameraShake, s => s.CameraShakeIntensity, (s, v) => s.CameraShakeIntensity = v, () => SettingsUxRules.ShakeWarning(_working.CameraShakeIntensity));
            R(_hudScale, s => s.HudScale, (s, v) => s.HudScale = v);
            R(_hudOpacity, s => s.HudOpacity, (s, v) => s.HudOpacity = v, () => SettingsUxRules.HudOpacityWarning(_working.HudOpacity));
            R(_crossColor, s => s.CrosshairColor, (s, v) => s.CrosshairColor = v);
            R(_crossSize, s => s.CrosshairSize, (s, v) => s.CrosshairSize = v);
            R(_showFps, s => s.ShowFps, (s, v) => s.ShowFps = v);
            R(_master, s => s.MasterVolume, (s, v) => s.MasterVolume = v);
            R(_ambient, s => s.AmbientVolume, (s, v) => s.AmbientVolume = v);
            R(_sfx, s => s.SfxVolume, (s, v) => s.SfxVolume = v);
            R(_music, s => s.MusicVolume, (s, v) => s.MusicVolume = v);
            R(_voice, s => s.VoiceVolume, (s, v) => s.VoiceVolume = v);

            // Bölüm sıfırlama düğmeleri (her sekmenin sonunda) + tuş atamaları düğmesi gezinmeye dahil.
            for (var t = 0; t < _tabContents.Length; t++)
            {
                var tab = t;
                var btn = UiKitButton.Create(_tabContents[t], "BU BÖLÜMÜ SIFIRLA", () => ResetTab(tab), UiKitButtonKind.Ghost, 260f, 40f);
                UiFactory.LayoutSize(btn, 260f, 40f);
                map[btn.transform] = new Row { Name = "bölümü sıfırla", Root = (RectTransform)btn.transform, Nav = btn, Extra = true };
            }
            if (_keysButton != null)
                map[_keysButton.transform] = new Row { Name = "tuş atamaları", Root = (RectTransform)_keysButton.transform, Nav = _keysButton, Extra = true };

            _rows.Clear();
            for (var t = 0; t < _tabContents.Length; t++)
            {
                for (var i = 0; i < _tabContents[t].childCount; i++)
                {
                    if (!map.TryGetValue(_tabContents[t].GetChild(i), out var row))
                        continue;
                    row.Tab = t;
                    _rows.Add(row);
                    if (row.Nav != null)
                        row.Nav.navigation = new Navigation { mode = Navigation.Mode.None };   // Yukarı/aşağı gezinmeyi biz yönetiriz.
                    if (!row.Extra)
                        BuildChrome(row);
                }
            }

            _counts = new int[_tabContents.Length];
            RefreshRows();
        }

        private float RecommendedScale() => _recQuality >= 2 ? 1f : _recQuality == 1 ? 0.85f : 0.7f;

        private void BuildChrome(Row r)
        {
            r.Layout = r.Root.GetComponent<LayoutElement>();

            // Değişti noktası (satırın sol kenarı).
            r.Dot = UiFactory.Image(r.Root, UiSprites.Circle, UiKitTokens.Accent);
            r.Dot.gameObject.name = "ChangedDot";
            UiFactory.SetRect(r.Dot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-5f, -3f), new Vector2(1f, 3f));
            r.Dot.enabled = false;

            // Kaydırıcı değer çipi.
            var slider = r.Nav as Slider;
            if (slider != null && r.Root.childCount >= 2)
            {
                var valueText = r.Root.GetChild(1).GetComponent<Text>();
                if (valueText != null)
                {
                    r.Chip = UiFactory.Image(r.Root, UiSprites.GetRoundedRect(8), UiKitTokens.SurfaceRaised);
                    r.Chip.type = Image.Type.Sliced;
                    r.Chip.gameObject.name = "ValueChip";
                    UiFactory.SetRect(r.Chip, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-84f, -13f), Vector2.zero + new Vector2(0f, 13f));
                    r.Chip.transform.SetSiblingIndex(valueText.transform.GetSiblingIndex());
                    valueText.rectTransform.offsetMin = new Vector2(-84f, 0f);
                    valueText.rectTransform.offsetMax = new Vector2(-8f, 0f);
                }
            }

            // Alt şerit: uyarı / önerilen yazısı, ÖNERİLEN rozeti, VARSAYILANA DÖN.
            var strip = UiFactory.CreateRect("Chrome", r.Root);
            UiFactory.SetRect(strip, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 15f));
            r.Strip = strip.gameObject;

            r.StripText = UiFactory.Label(strip, string.Empty, 12, TextAnchor.MiddleLeft, UiKitTokens.Ember);
            r.StripText.horizontalOverflow = HorizontalWrapMode.Overflow;
            r.StripText.raycastTarget = false;
            UiFactory.SetRect(r.StripText, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-270f, 0f));

            var badge = UiFactory.Image(strip, UiSprites.GetRoundedRect(6), UiKitTokens.AccentDeep);
            badge.type = Image.Type.Sliced;
            badge.gameObject.name = "Recommended";
            UiFactory.SetRect(badge, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-264f, -7f), new Vector2(-180f, 7f));
            var badgeText = UiFactory.Label(badge.transform, "ÖNERİLEN", 11, TextAnchor.MiddleCenter, UiKitTokens.Sand, FontStyle.Bold);
            badgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(badgeText);
            r.Badge = badge.gameObject;
            r.Badge.SetActive(false);

            var resetImg = UiFactory.Image(strip, UiSprites.GetRoundedRect(6), UiKitTokens.SurfaceHover);
            resetImg.type = Image.Type.Sliced;
            resetImg.raycastTarget = true;
            resetImg.gameObject.name = "RowReset";
            UiFactory.SetRect(resetImg, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-174f, -7f), new Vector2(-4f, 7f));
            var resetBtn = resetImg.gameObject.AddComponent<Button>();
            resetBtn.targetGraphic = resetImg;
            resetBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            var row = r;
            resetBtn.onClick.AddListener(() => ResetRow(row));
            var resetText = UiFactory.Label(resetImg.transform, "VARSAYILANA DÖN", 11, TextAnchor.MiddleCenter, UiKitTokens.Text, FontStyle.Bold);
            resetText.horizontalOverflow = HorizontalWrapMode.Overflow;
            resetText.raycastTarget = false;
            UiFactory.Stretch(resetText);
            r.ResetButton = resetImg.gameObject;
            r.ResetButton.SetActive(false);
            r.Strip.SetActive(false);
        }

        private void ResetRow(Row r)
        {
            if (r == null || r.Reset == null)
                return;
            r.Reset();
            RefreshControls();
            PreviewAudio();
            SetDirty(true);
            RefreshRows();
            ShowStatus("\"" + r.Name + "\" varsayılana döndü — kaydetmek için UYGULA.", UiTheme.Amber);
        }

        private void ResetTab(int tab)
        {
            var n = 0;
            foreach (var r in _rows)
            {
                if (r.Tab != tab || r.Extra || r.Changed == null || !r.Changed())
                    continue;
                r.Reset();
                n++;
            }
            if (n == 0)
            {
                ShowStatus("Bu bölümde değiştirilmiş ayar yok.", UiTheme.Amber);
                return;
            }
            RefreshControls();
            PreviewAudio();
            SetDirty(true);
            RefreshRows();
            ShowStatus(TabNames[Mathf.Clamp(tab, 0, TabNames.Length - 1)] + ": " + n + " ayar varsayılana döndü — kaydetmek için UYGULA.", UiTheme.Amber);
        }

        private void RefreshRows()
        {
            if (_closed || _rows.Count == 0 || _counts == null)
                return;

            Array.Clear(_counts, 0, _counts.Length);
            var q = _query;
            foreach (var r in _rows)
            {
                if (r.Root == null)
                    continue;
                var match = r.Extra ? string.IsNullOrEmpty(q) : SettingsUxRules.Matches(r.Name, q);
                if (match && !r.Extra)
                    _counts[r.Tab]++;
                if (r.Root.gameObject.activeSelf != match)
                    r.Root.gameObject.SetActive(match);
                if (r.Extra || r.Strip == null)
                    continue;

                var changed = r.Changed != null && r.Changed();
                var warn = r.Warn != null ? r.Warn() : string.Empty;
                var hasRec = r.Rec != null;
                var isRec = hasRec && r.Rec();

                if (r.Dot.enabled != changed)
                    r.Dot.enabled = changed;
                if (r.Chip != null)
                {
                    var chipColor = warn.Length > 0 ? UiKitTokens.DangerDark : UiKitTokens.SurfaceRaised;
                    if (r.Chip.color != chipColor)
                        r.Chip.color = chipColor;
                }

                var stripText = warn.Length > 0 ? warn : (hasRec && !isRec ? "Önerilen: " + r.RecLabel() : string.Empty);
                if (r.StripText.text != stripText)
                    r.StripText.text = stripText;
                var stripColor = warn.Length > 0 ? UiKitTokens.Ember : UiKitTokens.Sand;
                if (r.StripText.color != stripColor)
                    r.StripText.color = stripColor;

                var showStrip = changed || warn.Length > 0 || hasRec;
                if (r.Strip.activeSelf != showStrip)
                    r.Strip.SetActive(showStrip);
                if (r.Badge.activeSelf != isRec)
                    r.Badge.SetActive(isRec);
                if (r.ResetButton.activeSelf != changed)
                    r.ResetButton.SetActive(changed);
                if (r.Layout != null)
                {
                    var h = showStrip ? UiTheme.RowHeight + ChromeHeight : UiTheme.RowHeight;
                    if (!Mathf.Approximately(r.Layout.preferredHeight, h))
                    {
                        r.Layout.preferredHeight = h;
                        r.Layout.minHeight = h;
                    }
                }
            }
            UpdateHint();
        }

        private void UpdateHint()
        {
            if (_tabHint == null)
                return;
            if (string.IsNullOrEmpty(_query) || _counts == null)
            {
                _tabHint.text = TabHints[Mathf.Clamp(_currentTab, 0, TabHints.Length - 1)];
                return;
            }

            var total = 0;
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] <= 0)
                    continue;
                total += _counts[i];
                if (sb.Length > 0)
                    sb.Append("  ·  ");
                sb.Append(TabNames[i]).Append(' ').Append(_counts[i]);
            }
            _tabHint.text = total == 0 ? "Eşleşen ayar yok." : total + " sonuç  —  " + sb;
        }

        private void OnSearchChanged(string text)
        {
            _query = text ?? string.Empty;
            RefreshRows();
            if (string.IsNullOrWhiteSpace(_query) || _counts == null)
                return;
            if (_counts[_currentTab] > 0)
                return;
            for (var i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] > 0)
                {
                    _tabBar.Select(i);
                    break;
                }
            }
        }

        private void BuildSearchBox(RectTransform pane)
        {
            var box = UiFactory.CreateRect("Search", pane);
            UiFactory.SetRect(box, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-336f, -42f), new Vector2(-24f, -8f));
            var bg = box.gameObject.AddComponent<Image>();
            bg.sprite = UiSprites.GetRoundedRect(8);
            bg.type = Image.Type.Sliced;
            bg.color = UiKitTokens.SurfaceRaised;

            var lens = UiFactory.Image(box, SettingsGlyphs.Get(5), UiKitTokens.TextMuted);
            UiFactory.SetRect(lens, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(9f, -9f), new Vector2(27f, 9f));

            var text = UiFactory.Label(box, string.Empty, UiKitTokens.FontCaption + 1, TextAnchor.MiddleLeft, UiKitTokens.Text);
            UiFactory.Stretch(text, 36f, 2f, 10f, 2f);
            text.supportRichText = false;
            var placeholder = UiFactory.Label(box, "Ayar ara…  (Ctrl+F)", UiKitTokens.FontCaption + 1, TextAnchor.MiddleLeft, UiKitTokens.TextMuted, FontStyle.Italic);
            UiFactory.Stretch(placeholder, 36f, 2f, 10f, 2f);

            _search = box.gameObject.AddComponent<InputField>();
            _search.targetGraphic = bg;
            _search.textComponent = text;
            _search.placeholder = placeholder;
            _search.lineType = InputField.LineType.SingleLine;
            _search.characterLimit = 32;
            _search.onValueChanged.AddListener(OnSearchChanged);
            _search.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private void BuildTabGlyphs()
        {
            if (_tabBar == null)
                return;
            var host = _tabBar.transform;
            _tabIcons = new Image[Mathf.Min(host.childCount, TabNames.Length)];
            for (var i = 0; i < _tabIcons.Length; i++)
            {
                var tab = host.GetChild(i);
                var icon = UiFactory.Image(tab, SettingsGlyphs.Get(i), UiKitTokens.TextDim);
                icon.gameObject.name = "Glyph";
                UiFactory.SetRect(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, -12f), new Vector2(38f, 12f));
                _tabIcons[i] = icon;
                var label = tab.GetComponentInChildren<Text>();
                if (label != null)
                    label.rectTransform.offsetMin = new Vector2(48f, label.rectTransform.offsetMin.y);
            }
        }

        private void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null)
                return;

            var es = EventSystem.current;
            var searchFocused = _search != null && _search.isFocused;

            if (kb.ctrlKey.isPressed && kb.fKey.wasPressedThisFrame && _search != null)
            {
                if (es != null)
                    es.SetSelectedGameObject(_search.gameObject);
                _search.ActivateInputField();
                return;
            }

            var down = kb.downArrowKey.wasPressedThisFrame;
            var up = kb.upArrowKey.wasPressedThisFrame;
            var enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            if (!down && !up && !enter)
                return;

            var visible = new List<Row>();
            foreach (var r in _rows)
            {
                if (r.Tab == _currentTab && r.Nav != null && r.Root != null && r.Root.gameObject.activeInHierarchy)
                    visible.Add(r);
            }

            if (searchFocused)
            {
                if (down && visible.Count > 0)
                {
                    _search.DeactivateInputField();
                    FocusRow(visible[0]);
                }
                return;
            }

            var selected = es != null ? es.currentSelectedGameObject : null;
            var index = -1;
            var inRows = false;
            if (selected != null)
            {
                for (var i = 0; i < visible.Count; i++)
                {
                    if (selected == visible[i].Nav.gameObject || selected.transform.IsChildOf(visible[i].Root))
                    {
                        index = i;
                        inRows = true;
                        break;
                    }
                }
            }

            if (enter)
            {
                if (inRows)
                {
                    var option = visible[index].Root.GetComponent<UiOptionSelector>();
                    if (option != null)
                        option.Next();
                }
                return;
            }

            if (selected != null && !inRows)
                return;   // Alt düğmeler vb. Unity'nin kendi gezinmesine kalır.
            if (visible.Count == 0)
                return;

            if (up && index == 0 && _search != null)
            {
                if (es != null)
                    es.SetSelectedGameObject(_search.gameObject);
                _search.ActivateInputField();
                return;
            }

            var next = index < 0 ? (down ? 0 : visible.Count - 1) : Mathf.Clamp(index + (down ? 1 : -1), 0, visible.Count - 1);
            FocusRow(visible[next]);
        }

        private void FocusRow(Row r)
        {
            var es = EventSystem.current;
            if (es != null)
                es.SetSelectedGameObject(r.Nav.gameObject);
            ScrollIntoView(r.Tab, r.Root);
        }

        private void ScrollIntoView(int tab, RectTransform row)
        {
            if (tab < 0 || tab >= _tabScrolls.Length || row == null)
                return;
            var sr = _tabScrolls[tab].GetComponentInChildren<ScrollRect>(true);
            if (sr == null || sr.content == null)
                return;
            var viewport = sr.viewport != null ? sr.viewport : sr.transform as RectTransform;
            if (viewport == null)
                return;
            Canvas.ForceUpdateCanvases();
            var b = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row);
            var rect = viewport.rect;
            var delta = 0f;
            if (b.max.y > rect.yMax)
                delta = b.max.y - rect.yMax;
            else if (b.min.y < rect.yMin)
                delta = b.min.y - rect.yMin;
            if (Mathf.Abs(delta) > 0.01f)
                sr.content.anchoredPosition -= new Vector2(0f, delta);
        }

        private void RevertPreview()
        {
            var current = _settings != null ? _settings.Current : null;
            if (current == null)
                return;

            try
            {
                GameAudio.MasterVolume = current.MasterVolume;
                GameAudio.AmbientVolume = current.AmbientVolume * Mathf.Clamp01(current.MusicVolume);
                MixerRouting.ApplySettings(current.SfxVolume, current.MusicVolume, current.VoiceVolume);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void SetDirty(bool dirty)
        {
            if (_dirty == dirty)
                return;

            _dirty = dirty;
            if (dirty)
                ShowStatus("Kaydedilmemiş değişiklikler var.", UiTheme.Amber);
        }

        private void ShowStatus(string text, Color color)
        {
            if (_statusText == null)
                return;
            _statusText.text = text ?? string.Empty;
            _statusText.color = color;
        }

        private void ClearSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null &&
                eventSystem.currentSelectedGameObject.transform.IsChildOf(transform))
                eventSystem.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            // Pencere dışarıdan yok edilirse (sahne değişimi, üst menü kapanışı) önizlemeler geri alınır.
            if (!_closed && _dirty)
                RevertPreview();
            _closed = true;
            Applied = null;
        }
    }

    /// <summary>Ayarlar sekme ve arama glifleri: prosedürel, tek renkli (Image.color ile boyanır), 48 px.</summary>
    internal static class SettingsGlyphs
    {
        private const int N = 48;
        private static readonly Sprite[] Cache = new Sprite[6];

        /// <summary>0 oyun (nişan), 1 grafik (monitör), 2 ses (hoparlör), 3 kontroller (fare), 4 erişilebilirlik (göz), 5 arama (büyüteç).</summary>
        public static Sprite Get(int kind)
        {
            kind = Mathf.Clamp(kind, 0, Cache.Length - 1);
            if (Cache[kind] != null)
                return Cache[kind];
            var tex = Build(kind);
            Cache[kind] = Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), 100f);
            Cache[kind].name = "SettingsGlyph" + kind;
            return Cache[kind];
        }

        private static Texture2D Build(int kind)
        {
            var strokes = new List<Vector2[]>();
            var fills = new List<Vector3>();   // x, y, yarıçap
            switch (kind)
            {
                case 0:
                    strokes.Add(Arc(24, 24, 14, 0f, 6.2832f));
                    strokes.Add(new[] { new Vector2(24, 3), new Vector2(24, 12) });
                    strokes.Add(new[] { new Vector2(24, 36), new Vector2(24, 45) });
                    strokes.Add(new[] { new Vector2(3, 24), new Vector2(12, 24) });
                    strokes.Add(new[] { new Vector2(36, 24), new Vector2(45, 24) });
                    fills.Add(new Vector3(24, 24, 2.6f));
                    break;
                case 1:
                    strokes.Add(new[] { new Vector2(8, 17), new Vector2(40, 17), new Vector2(40, 38), new Vector2(8, 38), new Vector2(8, 17) });
                    strokes.Add(new[] { new Vector2(24, 17), new Vector2(24, 9) });
                    strokes.Add(new[] { new Vector2(15, 9), new Vector2(33, 9) });
                    strokes.Add(new[] { new Vector2(14, 23), new Vector2(20, 29), new Vector2(26, 25), new Vector2(34, 32) });
                    break;
                case 2:
                    strokes.Add(new[] { new Vector2(6, 19), new Vector2(14, 19), new Vector2(25, 9), new Vector2(25, 39), new Vector2(14, 29), new Vector2(6, 29), new Vector2(6, 19) });
                    strokes.Add(Arc(27, 24, 8, -0.85f, 0.85f));
                    strokes.Add(Arc(27, 24, 14, -0.85f, 0.85f));
                    break;
                case 3:
                {
                    var loop = new List<Vector2>();
                    loop.AddRange(Arc(24, 32, 11, 0f, 3.1416f));
                    loop.AddRange(Arc(24, 16, 11, 3.1416f, 6.2832f));
                    loop.Add(loop[0]);
                    strokes.Add(loop.ToArray());
                    strokes.Add(new[] { new Vector2(13, 30), new Vector2(35, 30) });
                    strokes.Add(new[] { new Vector2(24, 30), new Vector2(24, 43) });
                    fills.Add(new Vector3(24, 36, 1.8f));
                    break;
                }
                case 4:
                {
                    var upper = new List<Vector2>();
                    var lower = new List<Vector2>();
                    for (var i = 0; i <= 20; i++)
                    {
                        var x = 4f + 40f * i / 20f;
                        var t = (x - 24f) / 20f;
                        var h = 12f * (1f - t * t);
                        upper.Add(new Vector2(x, 24f + h));
                        lower.Add(new Vector2(x, 24f - h));
                    }
                    strokes.Add(upper.ToArray());
                    strokes.Add(lower.ToArray());
                    strokes.Add(Arc(24, 24, 6, 0f, 6.2832f));
                    fills.Add(new Vector3(24, 24, 2.6f));
                    break;
                }
                default:
                    strokes.Add(Arc(21, 27, 11, 0f, 6.2832f));
                    strokes.Add(new[] { new Vector2(29, 19), new Vector2(42, 6) });
                    break;
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "SettingsGlyphTex" + kind
            };
            var px = new Color32[N * N];
            for (var y = 0; y < N; y++)
            {
                for (var x = 0; x < N; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var cov = 0f;
                    foreach (var s in strokes)
                        cov = Mathf.Max(cov, Mathf.Clamp01(1.7f + 0.5f - PolyDist(p, s)));
                    foreach (var f in fills)
                        cov = Mathf.Max(cov, Mathf.Clamp01(f.z + 0.5f - Vector2.Distance(p, new Vector2(f.x, f.y))));
                    px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cov * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Vector2[] Arc(float cx, float cy, float r, float a0, float a1)
        {
            var steps = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(a1 - a0) * 8f));
            var pts = new Vector2[steps + 1];
            for (var i = 0; i <= steps; i++)
            {
                var a = Mathf.Lerp(a0, a1, i / (float)steps);
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        private static float PolyDist(Vector2 p, Vector2[] pts)
        {
            var best = float.MaxValue;
            for (var i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i];
                var ab = pts[i + 1] - a;
                var len2 = ab.sqrMagnitude;
                var t = len2 <= 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }
    }
}
