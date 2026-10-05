using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// <see cref="LoadingScreen"/> örtüsünün görünümü: koyu zemin, silik Türk bayrağı, "HAREKÂT" başlığı, nokta animasyonlu
    /// ileti, kayan/dolan ilerleme çubuğu, nabız atan şerit işaretleri ve dönen ipuçları. Ölçeksiz zamanla çalışır;
    /// gizliyken tuval kapalıdır ve kare başına iş yapmaz. Kare başına bellek ayırmaz (nokta metinleri önceden üretilir).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingScreenView : MonoBehaviour
    {
        private const float FadeInSeconds = 0.2f;
        private const float FadeOutSeconds = 0.35f;
        private const float DotInterval = 0.4f;
        private const float TipInterval = 6f;
        private const int AutoHideFrames = 3;
        private const float SafetyTimeoutSeconds = 90f;

        private static readonly string[] Tips =
        {
            "İpucu: F1 takip, F2 mevzi tut, F3 nişan noktasına taarruz, F4 toplan emirlerini verir.",
            "İpucu: Telsizci ya da tim komutanıysan V tuşuyla nişan noktasına topçu atışı isteyebilirsin.",
            "İpucu: Harekât alanı daralır — mavi bölgenin dışında kalan asker sürekli hasar alır.",
            "İpucu: Komutan şehit düşerse komuta en kıdemli askere geçer; tim harekâta devam eder.",
            "İpucu: Q ve E ile siperin arkasından yana eğilerek ateş edebilirsin.",
            "İpucu: H ile yaralarını sar, J ile takviye kullan. İyileşirken hareket yavaşlar.",
            "İpucu: Kafadan isabetler kask seviyesine göre çok daha ölümcüldür.",
            "İpucu: Kirpi zırhlı aracına F ile binebilir, haritada hızla yer değiştirebilirsin.",
            "İpucu: M ile tam haritayı aç; işaretlediğin nokta tim emirlerinde hedef olur.",
            "İpucu: Z ile yüzüstü yatmak seni uzak mesafeden görünmez kılar."
        };

        private Canvas _canvas;
        private CanvasGroup _group;
        private Text _message;
        private Text _tip;
        private Image _barFill;
        private RectTransform _barRunner;
        private RectTransform _barTrack;
        private Image[] _pulses;
        private readonly string[] _dotVariants = new string[4];

        private bool _showing;
        private float _alpha;
        private float _shownAt;
        private float _dotTimer;
        private int _dotIndex;
        private float _tipTimer;
        private int _tipIndex;
        private float _progress = -1f;
        private int _framesSinceTouch;

        /// <summary>Örtü gösterilmek üzere mi (solma sürüyor olabilir)?</summary>
        public bool IsShowing => _showing;

        internal static LoadingScreenView Create(int sortOrder)
        {
            var go = new GameObject("[Yükleme Ekranı]");
            go.layer = UiTheme.UiLayer;
            Object.DontDestroyOnLoad(go);

            // Olay sistemi gerektirmeyen kendi tuvali (ışın engelleme için GraphicRaycaster yeterli).
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiFactory.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = UiFactory.MatchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();

            var view = go.AddComponent<LoadingScreenView>();
            view._canvas = canvas;
            view.Build((RectTransform)go.transform);
            view._group.alpha = 0f;
            view._canvas.enabled = false;
            view.enabled = false;
            return view;
        }

        internal void Show(string message)
        {
            SetMessage(message);
            _framesSinceTouch = 0;

            if (_showing)
                return;

            _showing = true;
            _shownAt = Time.unscaledTime;
            _progress = -1f;
            UpdateProgressVisual();
            _tipIndex = Random.Range(0, Tips.Length);
            _tipTimer = 0f;
            if (_tip != null)
                _tip.text = Tips[_tipIndex];

            _canvas.enabled = true;
            _group.blocksRaycasts = true;
            enabled = true;
        }

        internal void SetMessage(string message)
        {
            if (message == null)
                message = string.Empty;

            if (_dotVariants[0] != message)
            {
                _dotVariants[0] = message;
                _dotVariants[1] = message + ".";
                _dotVariants[2] = message + "..";
                _dotVariants[3] = message + "...";
            }

            if (_message != null)
                _message.text = _dotVariants[_dotIndex];
        }

        internal void SetProgress(float progress01)
        {
            _framesSinceTouch = 0;
            _progress = progress01 < 0f ? -1f : Mathf.Clamp01(progress01);
            UpdateProgressVisual();
        }

        internal void Hide(bool immediate)
        {
            if (!_showing && !immediate)
                return;

            _showing = false;
            _group.blocksRaycasts = false;
            if (immediate)
            {
                _alpha = 0f;
                _group.alpha = 0f;
                _canvas.enabled = false;
                enabled = false;
            }
            else
            {
                enabled = true;
            }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (dt > 0.1f)
                dt = 0.1f;   // Uzun kurulum karelerinde animasyon zıplamasın.

            // Solma.
            if (_showing)
                _alpha = Mathf.Min(1f, _alpha + dt / FadeInSeconds);
            else
                _alpha = Mathf.Max(0f, _alpha - dt / FadeOutSeconds);
            _group.alpha = _alpha;

            if (!_showing && _alpha <= 0f)
            {
                _canvas.enabled = false;
                enabled = false;
                return;
            }

            Animate(dt);

            if (_showing)
                CheckAutoHide();
        }

        private void CheckAutoHide()
        {
            var visibleFor = Time.unscaledTime - _shownAt;
            if (visibleFor > SafetyTimeoutSeconds)
            {
                Debug.LogWarning("[LoadingScreen] Yükleme ekranı uzun süredir açık; güvenlik için kapatılıyor.");
                Hide(false);
                return;
            }

            if (!LoadingScreen.AutoHide)
                return;

            if (GameSession.IsLoading)
            {
                _framesSinceTouch = 0;
                return;
            }

            _framesSinceTouch++;
            if (_framesSinceTouch >= AutoHideFrames && visibleFor >= LoadingScreen.MinimumVisibleSeconds)
                Hide(false);
        }

        private void Animate(float dt)
        {
            _dotTimer += dt;
            if (_dotTimer >= DotInterval)
            {
                _dotTimer -= DotInterval;
                _dotIndex = (_dotIndex + 1) % _dotVariants.Length;
                if (_message != null && _dotVariants[_dotIndex] != null)
                    _message.text = _dotVariants[_dotIndex];
            }

            _tipTimer += dt;
            if (_tipTimer >= TipInterval && _tip != null)
            {
                _tipTimer = 0f;
                _tipIndex = (_tipIndex + 1) % Tips.Length;
                _tip.text = Tips[_tipIndex];
            }

            var t = Time.unscaledTime;
            if (_progress < 0f && _barRunner != null && _barTrack != null)
            {
                var width = _barTrack.rect.width;
                var runnerWidth = _barRunner.rect.width;
                var phase = Mathf.Repeat(t * 0.55f, 1f);
                var eased = phase * phase * (3f - 2f * phase);
                var pos = _barRunner.anchoredPosition;
                pos.x = Mathf.Lerp(-runnerWidth * 0.5f, width + runnerWidth * 0.5f, eased);
                _barRunner.anchoredPosition = pos;
            }

            if (_pulses != null)
            {
                for (var i = 0; i < _pulses.Length; i++)
                {
                    var image = _pulses[i];
                    if (image == null)
                        continue;
                    var wave = 0.5f + 0.5f * Mathf.Sin(t * 5f - i * 0.9f);
                    var c = image.color;
                    c.a = Mathf.Lerp(0.15f, 1f, wave * wave);
                    image.color = c;
                }
            }
        }

        private void UpdateProgressVisual()
        {
            var determinate = _progress >= 0f;
            if (_barFill != null)
            {
                _barFill.enabled = determinate;
                _barFill.fillAmount = determinate ? _progress : 0f;
            }

            if (_barRunner != null)
                _barRunner.gameObject.SetActive(!determinate);
        }

        private void Build(RectTransform root)
        {
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;

            var background = UiFactory.Panel(root, UiTheme.Background);
            background.gameObject.name = "Background";

            // Silik bayrak (sağda) ve alttan karartma.
            var flag = UiFactory.Image(background, UiSprites.TurkishFlag, new Color(1f, 1f, 1f, 0.07f));
            flag.gameObject.name = "Flag";
            flag.preserveAspect = true;
            UiFactory.Anchor(flag, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(120f, 60f), new Vector2(1200f, 800f));

            var vignette = UiFactory.Image(background, UiSprites.Vignette, new Color(0f, 0f, 0f, 0.85f));
            vignette.gameObject.name = "Vignette";
            UiFactory.Stretch(vignette);

            var shade = UiFactory.Image(background, UiSprites.VerticalGradient, new Color(0f, 0f, 0f, 0.7f));
            shade.gameObject.name = "BottomShade";
            UiFactory.SetRect(shade, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 420f));

            // Üst/alt kırmızı şeritler.
            var topStripe = UiFactory.Image(background, null, UiTheme.Accent);
            topStripe.gameObject.name = "TopStripe";
            UiFactory.SetRect(topStripe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), Vector2.zero);

            // Başlık bloğu (sol alt).
            var title = UiFactory.Label(background, "HAREKÂT", 96, TextAnchor.LowerLeft, UiTheme.Text, FontStyle.Bold);
            title.gameObject.name = "Title";
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(title, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(140f, 300f), new Vector2(-140f, 420f));
            UiFactory.AddShadow(title, UiTheme.TextShadow, new Vector2(3f, -3f));

            var underline = UiFactory.Image(background, null, UiTheme.Accent);
            underline.gameObject.name = "Underline";
            UiFactory.SetRect(underline, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(144f, 284f), new Vector2(144f + 260f, 292f));

            _message = UiFactory.Label(background, string.Empty, UiTheme.FontLarge, TextAnchor.UpperLeft, UiTheme.Amber, FontStyle.Bold);
            _message.gameObject.name = "Message";
            _message.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_message, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(144f, 210f), new Vector2(-140f, 268f));
            UiFactory.AddShadow(_message, UiTheme.TextShadow, new Vector2(2f, -2f));

            // Nabız atan şerit işaretleri (››› benzeri).
            var pulseRow = UiFactory.HorizontalList(background, 6f, 0, TextAnchor.MiddleLeft);
            pulseRow.gameObject.name = "Pulses";
            UiFactory.SetRect(pulseRow, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(144f, 150f), new Vector2(144f + 160f, 182f));
            pulseRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            _pulses = new Image[5];
            for (var i = 0; i < _pulses.Length; i++)
            {
                var chevron = UiFactory.Image(pulseRow, UiSprites.Chevron, UiTheme.Accent);
                chevron.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
                UiFactory.LayoutSize(chevron, 26f, 26f);
                _pulses[i] = chevron;
            }

            // İlerleme çubuğu (tam genişlik, altta).
            var track = UiFactory.Panel(background, UiTheme.Track, UiSprites.GetRoundedRect(3));
            track.gameObject.name = "ProgressTrack";
            track.GetComponent<Image>().raycastTarget = false;
            UiFactory.SetRect(track, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(144f, 120f), new Vector2(-144f, 128f));
            track.gameObject.AddComponent<RectMask2D>();
            _barTrack = track;

            _barFill = UiFactory.Image(track, UiSprites.White, UiTheme.Accent);
            _barFill.gameObject.name = "Fill";
            UiFactory.Stretch(_barFill);
            _barFill.type = Image.Type.Filled;
            _barFill.fillMethod = Image.FillMethod.Horizontal;
            _barFill.fillOrigin = 0;
            _barFill.fillAmount = 0f;

            var runner = UiFactory.Image(track, UiSprites.HorizontalGradient, UiTheme.AccentLight);
            runner.gameObject.name = "Runner";
            runner.rectTransform.localScale = new Vector3(-1f, 1f, 1f);   // Parlak uç önde (sağda).
            UiFactory.Anchor(runner, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-210f, 0f), new Vector2(420f, 8f));
            _barRunner = runner.rectTransform;

            // İpucu (alt).
            _tip = UiFactory.Label(background, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextDim);
            _tip.gameObject.name = "Tip";
            UiFactory.SetRect(_tip, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(144f, 56f), new Vector2(-144f, 100f));

            var footer = UiFactory.Label(background, "Kuzgun Vadisi · Tim Battle Royale", UiTheme.FontTiny, TextAnchor.MiddleRight, UiTheme.TextMuted);
            footer.gameObject.name = "Footer";
            UiFactory.SetRect(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(144f, 56f), new Vector2(-144f, 100f));
        }

        private void OnDestroy()
        {
            LoadingScreen.NotifyDestroyed(this);
        }
    }
}
