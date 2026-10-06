using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Alttaki kayan haber bandı: metin sağdan girer, soldan çıkar ve döner (RectMask2D içinde).</summary>
    public sealed class MenuTicker : MonoBehaviour
    {
        /// <summary>Bant hızı (referans piksel / sn).</summary>
        public const float Speed = 70f;

        public static readonly string[] Headlines =
        {
            "SEZON 1: KUZGUN HAREKÂTI başladı — görevleri tamamla, apolet ve kamuflaj kazan",
            "YENİ: Mavi Liman ve Kartal Yaylası haritaları tim savaşına açıldı",
            "İPUCU: Sis bombası, yaralı tim arkadaşını kurtarırken görüşü keser — önce sisle, sonra kaldır",
            "İPUCU: Telsizci, topçu desteğini çağırır; Sıhhiyeci, yaralıları ayağa kaldırır",
            "T-70 ile intikal ederken kapıdan sırayla atla — tim dağılmasın",
            "Kirpi zırhlı araç: sürücü, taretçi ve 8 yolcu — birlikte hareket eden tim kazanır",
            "Poligonda eğitimli moda katıl: hareket, atış, siper, eşya ve emir komuta"
        };

        private RectTransform _text;
        private float _contentWidth;
        private float _time;

        public static MenuTicker Create(Transform parent, float height)
        {
            var root = UiFactory.CreateRect("Ticker", parent);
            var back = root.gameObject.AddComponent<Image>();
            back.color = new Color(0.063f, 0.071f, 0.078f, 0.88f);
            back.raycastTarget = false;
            root.gameObject.AddComponent<RectMask2D>();

            var line = UiFactory.Image(root, null, UiKitTokens.Accent);
            line.raycastTarget = false;
            UiFactory.SetRect(line, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), Vector2.zero);

            var text = UiFactory.Label(root, Compose(), UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextDim, FontStyle.Bold);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = (RectTransform)text.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);

            var ticker = root.gameObject.AddComponent<MenuTicker>();
            ticker._text = rt;
            ticker._contentWidth = Mathf.Max(200f, text.preferredWidth);
            rt.sizeDelta = new Vector2(ticker._contentWidth, 0f);
            return ticker;
        }

        /// <summary>Başlıkları ayraçla birleştirir.</summary>
        public static string Compose()
        {
            return string.Join("      //      ", Headlines);
        }

        private void Update()
        {
            if (_text == null)
                return;
            _time += Time.unscaledDeltaTime;
            var view = ((RectTransform)transform).rect.width;
            _text.anchoredPosition = new Vector2(MainMenuMotion.TickerOffset(_time, Speed, _contentWidth, view), 0f);
        }
    }
}
