using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Rendering;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const float WindowWidth = 1000f;
        private const float WindowHeight = 860f;
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

        private Slider _sensitivity;
        private Slider _ads;
        private Slider _fov;
        private Slider _master;
        private Slider _ambient;
        private Toggle _invertY;
        private Toggle _fullscreen;
        private Toggle _showFps;
        private UiOptionSelector _quality;

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

            try
            {
                GameAudio.MasterVolume = settings.MasterVolume;
                GameAudio.AmbientVolume = settings.AmbientVolume;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                PostProcessing.ApplyQuality(settings.QualityLevel);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

#if !UNITY_EDITOR && !UNITY_SERVER
            try
            {
                if (Screen.fullScreen != settings.Fullscreen)
                    Screen.fullScreen = settings.Fullscreen;
            }
            catch (Exception)
            {
                // Bazı platformlarda tam ekran değiştirilemez.
            }
#endif
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
            var window = UiFactory.Panel(root, UiTheme.Panel, UiSprites.ChamferRect);
            window.gameObject.name = "Window";
            UiFactory.Anchor(window, UiAnchor.Center, Vector2.zero, new Vector2(WindowWidth, WindowHeight));

            var border = UiFactory.Image(window, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.PanelBorder);
            UiFactory.Stretch(border);

            var stripe = UiFactory.Image(window, null, UiTheme.Accent);
            stripe.gameObject.name = "Stripe";
            UiFactory.SetRect(stripe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), Vector2.zero);

            var title = UiFactory.Label(window, "AYARLAR", UiTheme.FontTitle, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            title.gameObject.name = "Title";
            UiFactory.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -96f), new Vector2(-40f, -22f));
            UiFactory.AddShadow(title, UiTheme.TextShadow, new Vector2(2f, -2f));

            var scroll = UiWidgets.ScrollList(window, out var content, 10f, 4);
            UiFactory.SetRect(scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 120f), new Vector2(-28f, -110f));

            // ---------------------------------------------------------------- KONTROL
            UiWidgets.Header(content, "KONTROL", UiTheme.FontMedium);
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
            _invertY = UiWidgets.LabeledToggle(content, "Y eksenini ters çevir", _working.InvertY, v =>
            {
                _working.InvertY = v;
                SetDirty(true);
            });

            UiFactory.Spacer(content, 8f);

            // ---------------------------------------------------------------- GÖRÜNTÜ
            UiWidgets.Header(content, "GÖRÜNTÜ", UiTheme.FontMedium);
            var fovMax = Mathf.Min(MaxMenuFieldOfView, SettingsService.MaxFieldOfView);
            _fov = UiWidgets.LabeledSlider(content, "Görüş alanı (FOV)", SettingsService.MinFieldOfView, fovMax,
                Mathf.Clamp(_working.FieldOfView, SettingsService.MinFieldOfView, fovMax), v =>
                {
                    _working.FieldOfView = v;
                    SetDirty(true);
                }, v => v.ToString("0") + "°", true);
            _quality = UiWidgets.OptionSelector(content, "Grafik kalitesi", MenuText.QualityNames,
                Mathf.Clamp(_working.QualityLevel, 0, MenuText.QualityNames.Length - 1), i =>
                {
                    _working.QualityLevel = i;
                    SetDirty(true);
                });
            _fullscreen = UiWidgets.LabeledToggle(content, "Tam ekran", _working.Fullscreen, v =>
            {
                _working.Fullscreen = v;
                SetDirty(true);
            });
            _showFps = UiWidgets.LabeledToggle(content, "FPS göster", _working.ShowFps, v =>
            {
                _working.ShowFps = v;
                SetDirty(true);
            });

            UiFactory.Spacer(content, 8f);

            // ---------------------------------------------------------------- SES
            UiWidgets.Header(content, "SES", UiTheme.FontMedium);
            _master = UiWidgets.LabeledSlider(content, "Ana ses", 0f, 1f, _working.MasterVolume, v =>
            {
                _working.MasterVolume = v;
                PreviewAudio();
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));
            _ambient = UiWidgets.LabeledSlider(content, "Ortam sesi ve müzik", 0f, 1f, _working.AmbientVolume, v =>
            {
                _working.AmbientVolume = v;
                PreviewAudio();
                SetDirty(true);
            }, v => MenuText.FormatPercent(v));

            // ---------------------------------------------------------------- Alt çubuk
            _statusText = UiFactory.Label(window, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextMuted);
            _statusText.gameObject.name = "Status";
            UiFactory.SetRect(_statusText, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 92f), new Vector2(-40f, 116f));

            var row = UiFactory.HorizontalList(window, 14f, 0, TextAnchor.MiddleRight);
            row.gameObject.name = "Buttons";
            UiFactory.SetRect(row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 26f), new Vector2(-40f, 26f + UiTheme.ButtonHeight));
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            var defaults = UiFactory.Button(row, "VARSAYILAN", ResetToDefaults, UiButtonStyle.Ghost);
            UiFactory.LayoutSize(defaults, 230f, UiTheme.ButtonHeight);
            UiFactory.FlexibleSpacer(row);
            _backButton = UiFactory.Button(row, "GERİ", Close, UiButtonStyle.Default);
            UiFactory.LayoutSize(_backButton, 210f, UiTheme.ButtonHeight);
            _applyButton = UiFactory.Button(row, "UYGULA", ApplyAndClose, UiButtonStyle.Primary);
            UiFactory.LayoutSize(_applyButton, 250f, UiTheme.ButtonHeight);

            ShowStatus(_settings == null ? "Ayar servisi bulunamadı — değişiklikler yalnızca bu oturumda geçerli." : string.Empty, UiTheme.Amber);
        }

        private void Start()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && _sensitivity != null)
                eventSystem.SetSelectedGameObject(_sensitivity.gameObject);
        }

        private void Update()
        {
            if (_group == null || _fade >= 1f)
                return;

            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
            _group.alpha = _fade;
        }

        private void RefreshControls()
        {
            if (_sensitivity != null)
                _sensitivity.value = _working.MouseSensitivity;   // Değer yazısı onValueChanged ile güncellenir.
            if (_ads != null)
                _ads.value = _working.AdsSensitivityMultiplier;
            if (_fov != null)
                _fov.value = Mathf.Clamp(_working.FieldOfView, _fov.minValue, _fov.maxValue);
            if (_master != null)
                _master.value = _working.MasterVolume;
            if (_ambient != null)
                _ambient.value = _working.AmbientVolume;
            if (_invertY != null)
                _invertY.SetIsOnWithoutNotify(_working.InvertY);
            if (_fullscreen != null)
                _fullscreen.SetIsOnWithoutNotify(_working.Fullscreen);
            if (_showFps != null)
                _showFps.SetIsOnWithoutNotify(_working.ShowFps);
            if (_quality != null)
                _quality.Index = Mathf.Clamp(_working.QualityLevel, 0, MenuText.QualityNames.Length - 1);
        }

        private void PreviewAudio()
        {
            try
            {
                GameAudio.MasterVolume = Mathf.Clamp01(_working.MasterVolume);
                GameAudio.AmbientVolume = Mathf.Clamp01(_working.AmbientVolume);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void RevertPreview()
        {
            var current = _settings != null ? _settings.Current : null;
            if (current == null)
                return;

            try
            {
                GameAudio.MasterVolume = current.MasterVolume;
                GameAudio.AmbientVolume = current.AmbientVolume;
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
}
