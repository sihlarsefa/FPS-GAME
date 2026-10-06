using System.Collections.Generic;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Sol alt telsiz altyazısı: "[Rütbe Ad]: metin" (3 sn, sonra solar). Kendi kendini kurar ve
    /// <see cref="RadioChatterSystem.LineSpoken"/> olayını dinler; HUD'a bağımlı değildir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RadioSubtitleView : MonoBehaviour
    {
        private const float FadeSeconds = 0.4f;
        private const int MaxLines = 3;

        private static RadioSubtitleView _instance;

        private sealed class Row
        {
            public Text Label;
            public Image Background;
            public float TimeLeft;
        }

        private readonly List<Row> _rows = new List<Row>(MaxLines);
        private RectTransform _root;

        private static readonly int[] FontSizes = { 18, 22, 28, 34 };

        /// <summary>Altyazı boyut dizinine göre yazı boyutu (0-3, saf).</summary>
        public static int FontSizeFor(int sizeIndex) => FontSizes[Mathf.Clamp(sizeIndex, 0, FontSizes.Length - 1)];

        /// <summary>Satır yüksekliği (yazı boyutu + boşluk).</summary>
        public static float LineHeightFor(int sizeIndex) => FontSizeFor(sizeIndex) + 8f;

        /// <summary>Ayarlar (boyut, HUD ölçeği) değişince çalışan örneği günceller.</summary>
        public static void RefreshStyle()
        {
            if (_instance != null)
                _instance.ApplyStyle();
        }

        private float LineHeight => LineHeightFor(AdvancedDisplay.SubtitleSize);

        private void ApplyStyle()
        {
            if (_root == null)
                return;

            var scale = AdvancedDisplay.HudScale;
            _root.pivot = new Vector2(0f, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Label != null)
                {
                    row.Label.fontSize = FontSizeFor(AdvancedDisplay.SubtitleSize);
                    row.Label.rectTransform.sizeDelta = new Vector2(760f, LineHeight - 2f);
                }
                if (row.Background != null)
                    row.Background.gameObject.SetActive(AdvancedDisplay.SubtitleBackground);
            }
            Layout();
        }

        /// <summary>"[Ad]: metin" biçimi (saf, test edilebilir).</summary>
        public static string Format(string speaker, string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return string.IsNullOrEmpty(speaker) ? text : "[" + speaker + "]: " + text;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
                return;

            var go = new GameObject("RadioSubtitleView");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _instance = go.AddComponent<RadioSubtitleView>();
        }

        private void Awake()
        {
            _root = HudBuild.Rect("RadioSubtitles", transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(32f, 150f), new Vector2(760f, MaxLines * 30f));
            RadioChatterSystem.LineSpoken += OnLine;
            ApplyStyle();
        }

        private void OnDestroy()
        {
            RadioChatterSystem.LineSpoken -= OnLine;
            if (_instance == this)
                _instance = null;
        }

        private void OnLine(RadioSpoken spoken)
        {
            var text = Format(spoken.Speaker, spoken.Text);
            if (string.IsNullOrEmpty(text) || _root == null)
                return;

            if (_rows.Count >= MaxLines)
            {
                DestroyRow(_rows[0]);
                _rows.RemoveAt(0);
            }

            var bg = HudBuild.Image("RadioLineBg", _root, null, new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                Vector2.zero, new Vector2(760f, LineHeight));
            bg.gameObject.SetActive(AdvancedDisplay.SubtitleBackground);
            var label = HudBuild.Text("RadioLine", _root, text, FontSizeFor(AdvancedDisplay.SubtitleSize), TextAnchor.LowerLeft, ToneColor(spoken.Tone),
                FontStyle.Bold, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(760f, LineHeight - 2f));
            _rows.Add(new Row { Label = label, Background = bg, TimeLeft = spoken.Seconds > 0f ? spoken.Seconds : RadioChatterSystem.SubtitleSeconds });
            Layout();
        }

        private void Update()
        {
            if (_rows.Count == 0)
                return;

            var dt = Time.unscaledDeltaTime;
            var changed = false;
            for (var i = _rows.Count - 1; i >= 0; i--)
            {
                var row = _rows[i];
                row.TimeLeft -= dt;
                if (row.TimeLeft <= 0f)
                {
                    DestroyRow(row);
                    _rows.RemoveAt(i);
                    changed = true;
                    continue;
                }

                var fade = Mathf.Clamp01(row.TimeLeft / FadeSeconds);
                if (row.Label != null)
                    HudBuild.SetAlpha(row.Label, fade);
                if (row.Background != null)
                    HudBuild.SetAlpha(row.Background, 0.55f * fade);
            }

            if (changed)
                Layout();
        }

        private static void DestroyRow(Row row)
        {
            if (row.Label != null)
                Destroy(row.Label.gameObject);
            if (row.Background != null)
                Destroy(row.Background.gameObject);
        }

        private void Layout()
        {
            // En yeni satır en altta.
            for (var i = 0; i < _rows.Count; i++)
            {
                var label = _rows[i].Label;
                if (label == null)
                    continue;

                var y = (_rows.Count - 1 - i) * LineHeight;
                label.rectTransform.anchoredPosition = new Vector2(0f, y);
                if (_rows[i].Background != null)
                    _rows[i].Background.rectTransform.anchoredPosition = new Vector2(0f, y);
            }
        }

        private static Color ToneColor(string tone)
        {
            switch (tone)
            {
                case "acil": return new Color(1f, 0.55f, 0.45f, 1f);
                case "gergin": return new Color(1f, 0.82f, 0.45f, 1f);
                case "zafer": return new Color(0.7f, 1f, 0.6f, 1f);
                case "agir": return new Color(0.75f, 0.78f, 0.85f, 1f);
                default: return new Color(0.88f, 0.96f, 0.85f, 1f);
            }
        }
    }
}
