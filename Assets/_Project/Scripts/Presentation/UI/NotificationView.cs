using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Bildirim türü (renk ve vurgu şeridi).</summary>
    public enum HudNoticeKind
    {
        /// <summary>Genel bilgi (beyaz).</summary>
        Info,
        /// <summary>Uyarı (amber): bölge daralıyor.</summary>
        Warning,
        /// <summary>Tehlike (kırmızı): düşman topçusu, alan dışı.</summary>
        Danger,
        /// <summary>Telsiz / komuta (mavi): komuta devri, topçu isteği.</summary>
        Radio,
        /// <summary>Tim emri (yeşil).</summary>
        Order
    }

    /// <summary>
    /// Ekran bildirimleri: (1) büyük merkez mesajı (<see cref="ShowCenter"/>: "HAREKÂT BAŞLADI", "ŞEHİT DÜŞTÜN"),
    /// (2) üst ortada yığılan kısa bildirimler (faz/bölge değişimi, komuta devri, topçu, tim emirleri — en fazla 4,
    /// yenisi üstte), (3) alt ortada etkisiz hâle getirme şeridi. Havuzlu; yalnızca yeni bildirimde metin atanır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NotificationView : MonoBehaviour
    {
        private const int MaxToasts = 4;
        private const float ToastHeight = 34f;
        private const float ToastSpacing = 4f;
        private const float ToastWidth = 760f;
        private const float ToastTop = 176f;
        private const float DefaultToastSeconds = 4f;
        private const float FadeIn = 0.15f;
        private const float FadeOut = 0.45f;

        private sealed class Toast
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Background;
            public Image Accent;
            public Text Label;
            public float TimeLeft;
            public float Duration;
            public string Text;
        }

        private RectTransform _centerRoot;
        private CanvasGroup _centerGroup;
        private Image _centerBackdrop;
        private Text _centerText;
        private float _centerTimeLeft;
        private float _centerDuration;

        private RectTransform _toastRoot;
        private readonly Toast[] _toasts = new Toast[MaxToasts];
        private int _toastCount;

        private RectTransform _killRoot;
        private CanvasGroup _killGroup;
        private Text _killTitle;
        private Text _killName;
        private Text _killCount;
        private float _killTimeLeft;
        private float _killDuration;

        public RectTransform Root { get; private set; }

        public static NotificationView Create(RectTransform parent)
        {
            var root = HudBuild.Fill("Notifications", parent);
            var view = root.gameObject.AddComponent<NotificationView>();
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);

            // Merkez mesajı.
            _centerRoot = HudBuild.Rect("Center", Root, HudBuild.Center, HudBuild.Center, new Vector2(0f, 190f), new Vector2(1200f, 64f));
            _centerGroup = HudBuild.PassiveGroup(_centerRoot, 0f);
            _centerBackdrop = HudBuild.Image("Backdrop", _centerRoot, HudBuild.Banner, UiTheme.WithAlpha(Color.black, 0.55f),
                Vector2.zero, new Vector2(900f, 64f));
            _centerText = HudBuild.Text("Text", _centerRoot, string.Empty, UiTheme.FontLarge, TextAnchor.MiddleCenter, UiTheme.Text,
                FontStyle.Bold, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(1200f, 64f));
            HudBuild.SetActive(_centerRoot, false);

            // Üst bildirim yığını.
            _toastRoot = HudBuild.Rect("Toasts", Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -ToastTop),
                new Vector2(ToastWidth, MaxToasts * (ToastHeight + ToastSpacing)));
            for (var i = 0; i < MaxToasts; i++)
            {
                var toast = new Toast();
                toast.Rect = HudBuild.Rect("Toast" + i, _toastRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                    new Vector2(ToastWidth, ToastHeight));
                toast.Group = HudBuild.PassiveGroup(toast.Rect, 0f);
                toast.Background = HudBuild.Image("Bg", toast.Rect, HudBuild.Banner, UiTheme.WithAlpha(Color.black, 0.5f),
                    Vector2.zero, new Vector2(ToastWidth, ToastHeight));
                toast.Accent = HudBuild.Image("Accent", toast.Rect, UiSprites.White, UiTheme.Amber, HudBuild.Center,
                    new Vector2(1f, 0.5f), new Vector2(-200f, 0f), new Vector2(4f, ToastHeight - 8f));
                toast.Label = HudBuild.Text("Text", toast.Rect, string.Empty, UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.Text,
                    FontStyle.Bold, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(ToastWidth, ToastHeight));
                HudBuild.SetActive(toast.Rect, false);
                _toasts[i] = toast;
            }

            // Etkisiz hâle getirme şeridi (nişangâhın altında).
            _killRoot = HudBuild.Rect("Kill", Root, HudBuild.Center, HudBuild.Center, new Vector2(0f, -210f), new Vector2(700f, 70f));
            _killGroup = HudBuild.PassiveGroup(_killRoot, 0f);
            _killTitle = HudBuild.Text("Title", _killRoot, "ETKİSİZ HÂLE GETİRİLDİ", UiTheme.FontSmall, TextAnchor.MiddleCenter,
                UiTheme.EnemyRed, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(700f, 22f));
            _killName = HudBuild.Text("Name", _killRoot, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.Text,
                FontStyle.Bold, HudBuild.Center, HudBuild.Center, new Vector2(0f, -4f), new Vector2(700f, 30f));
            _killCount = HudBuild.Text("Count", _killRoot, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.TextDim,
                FontStyle.Bold, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(700f, 18f));
            HudBuild.SetActive(_killRoot, false);
        }

        /// <summary>Büyük merkez mesajı (öncekinin yerine geçer). seconds ≤ 0 ise 3 sn.</summary>
        public void ShowCenter(string text, float seconds)
        {
            ShowCenter(text, seconds, UiTheme.Text);
        }

        /// <summary>Renkli büyük merkez mesajı.</summary>
        public void ShowCenter(string text, float seconds, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                _centerTimeLeft = 0f;
                return;
            }

            if (float.IsNaN(seconds) || seconds <= 0f)
                seconds = 3f;

            _centerText.text = text;
            _centerText.color = color;
            var width = Mathf.Clamp(_centerText.preferredWidth + 160f, 360f, 1700f);
            _centerBackdrop.rectTransform.sizeDelta = new Vector2(width, 64f);
            _centerDuration = seconds;
            _centerTimeLeft = seconds;
            HudBuild.SetActive(_centerRoot, true);
        }

        /// <summary>Üst ortaya kısa bildirim ekler. Aynı metin zaten görünüyorsa süresini tazeler.</summary>
        public void Push(string text, HudNoticeKind kind, float seconds = DefaultToastSeconds)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (float.IsNaN(seconds) || seconds <= 0f)
                seconds = DefaultToastSeconds;

            for (var i = 0; i < _toastCount; i++)
            {
                var existing = _toasts[i];
                if (existing.TimeLeft > 0f && HudFormat.Same(existing.Text, text))
                {
                    existing.TimeLeft = Mathf.Max(existing.TimeLeft, seconds);
                    existing.Duration = Mathf.Max(existing.Duration, existing.TimeLeft);
                    return;
                }
            }

            // En eskiyi yeniden kullan, diziyi bir kaydır (yeni olan üstte).
            var toast = _toasts[MaxToasts - 1];
            for (var i = MaxToasts - 1; i > 0; i--)
                _toasts[i] = _toasts[i - 1];
            _toasts[0] = toast;
            _toastCount = Mathf.Min(_toastCount + 1, MaxToasts);

            var color = KindColor(kind);
            toast.Text = text;
            toast.Label.text = text;
            toast.Label.color = kind == HudNoticeKind.Info ? UiTheme.Text : UiTheme.Lighten(color, 0.25f);
            toast.Accent.color = color;
            var textWidth = Mathf.Min(toast.Label.preferredWidth, ToastWidth - 60f);
            toast.Accent.rectTransform.anchoredPosition = new Vector2(-textWidth * 0.5f - 14f, 0f);
            toast.Background.rectTransform.sizeDelta = new Vector2(Mathf.Min(ToastWidth, textWidth + 140f), ToastHeight);
            toast.Duration = seconds;
            toast.TimeLeft = seconds;
            toast.Group.alpha = 0f;
            HudBuild.SetPosition(toast.Rect, new Vector2(0f, ToastHeight * 0.6f));
            HudBuild.SetActive(toast.Rect, true);
        }

        /// <summary>Yerel oyuncunun etkisiz hâle getirdiği asker (kafadan ise belirtilir).</summary>
        public void ShowKill(string victimName, bool headshot, int totalKills)
        {
            _killTitle.text = headshot ? "KAFADAN — ETKİSİZ HÂLE GETİRİLDİ" : "ETKİSİZ HÂLE GETİRİLDİ";
            _killTitle.color = headshot ? UiTheme.Amber : UiTheme.EnemyRed;
            _killName.text = string.IsNullOrEmpty(victimName) ? "Düşman asker" : victimName;
            _killCount.text = totalKills > 0 ? UiWidgets.Number(totalKills) + " ÖLDÜRME" : string.Empty;
            _killDuration = 2.6f;
            _killTimeLeft = _killDuration;
            HudBuild.SetActive(_killRoot, true);
        }

        /// <summary>Tüm bildirimleri kapatır.</summary>
        public void ClearAll()
        {
            _centerTimeLeft = 0f;
            _killTimeLeft = 0f;
            for (var i = 0; i < _toasts.Length; i++)
            {
                _toasts[i].TimeLeft = 0f;
                HudBuild.SetActive(_toasts[i].Rect, false);
            }

            _toastCount = 0;
            HudBuild.SetActive(_centerRoot, false);
            HudBuild.SetActive(_killRoot, false);
        }

        private static Color KindColor(HudNoticeKind kind)
        {
            switch (kind)
            {
                case HudNoticeKind.Warning: return UiTheme.Amber;
                case HudNoticeKind.Danger: return UiTheme.EnemyRed;
                case HudNoticeKind.Radio: return UiTheme.AllyBlue;
                case HudNoticeKind.Order: return UiTheme.Success;
                default: return UiTheme.Khaki;
            }
        }

        /// <summary>HUD denetleyicisi her karede çağırır (ölçeksiz zamanla — duraklatmada da söner).</summary>
        public void Tick(float deltaTime)
        {
            // Merkez mesajı.
            if (_centerTimeLeft > 0f)
            {
                _centerTimeLeft -= deltaTime;
                var age = _centerDuration - _centerTimeLeft;
                var alpha = Mathf.Min(Mathf.Clamp01(age / FadeIn), Mathf.Clamp01(_centerTimeLeft / FadeOut));
                HudBuild.SetAlpha(_centerGroup, alpha);
                var pop = Mathf.Lerp(1.08f, 1f, Mathf.Clamp01(age / 0.2f));
                HudBuild.SetScale(_centerRoot, pop);
                if (_centerTimeLeft <= 0f)
                    HudBuild.SetActive(_centerRoot, false);
            }

            // Etkisiz hâle getirme şeridi.
            if (_killTimeLeft > 0f)
            {
                _killTimeLeft -= deltaTime;
                var age = _killDuration - _killTimeLeft;
                var alpha = Mathf.Min(Mathf.Clamp01(age / 0.1f), Mathf.Clamp01(_killTimeLeft / 0.5f));
                HudBuild.SetAlpha(_killGroup, alpha);
                HudBuild.SetScale(_killRoot, Mathf.Lerp(1.12f, 1f, Mathf.Clamp01(age / 0.18f)));
                if (_killTimeLeft <= 0f)
                    HudBuild.SetActive(_killRoot, false);
            }

            // Bildirim yığını.
            if (_toastCount == 0)
                return;

            var active = 0;
            for (var i = 0; i < _toastCount; i++)
            {
                var toast = _toasts[i];
                if (toast.TimeLeft <= 0f)
                {
                    HudBuild.SetActive(toast.Rect, false);
                    continue;
                }

                toast.TimeLeft -= deltaTime;
                var targetY = -i * (ToastHeight + ToastSpacing);
                var y = Mathf.Lerp(toast.Rect.anchoredPosition.y, targetY, 1f - Mathf.Exp(-deltaTime * 12f));
                HudBuild.SetPosition(toast.Rect, new Vector2(0f, y));

                var age = toast.Duration - toast.TimeLeft;
                var alpha = Mathf.Min(Mathf.Clamp01(age / FadeIn), Mathf.Clamp01(toast.TimeLeft / FadeOut));
                HudBuild.SetAlpha(toast.Group, alpha);

                if (toast.TimeLeft <= 0f)
                    HudBuild.SetActive(toast.Rect, false);
                else
                    active = i + 1;
            }

            _toastCount = active;
        }
    }
}
