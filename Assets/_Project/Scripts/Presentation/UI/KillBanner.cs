using Project.Application.Catalogs;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Öldürme anı zincir kuralları (saf mantık). Zaman ölçeğine dokunmaz; unscaled saniye alır.</summary>
    public sealed class KillChainRules
    {
        public const float ChainWindow = 8f;
        public const float BannerSeconds = 1.2f;
        public const float FlashSeconds = 0.08f;

        private int _count;
        private float _last = -999f;

        public int Count => _count;

        /// <summary>Yeni öldürmeyi kaydeder; 8 sn pencere içindeyse zinciri uzatır. Güncel zincir uzunluğunu döndürür.</summary>
        public int RegisterKill(float now)
        {
            _count = now - _last <= ChainWindow && _count > 0 ? _count + 1 : 1;
            _last = now;
            return _count;
        }

        public void Reset() { _count = 0; _last = -999f; }

        /// <summary>Zincir başlığı: 1 -> null, 2 ÇİFTE, 3 ÜÇLÜ, 4+ SERİ HAREKÂT.</summary>
        public static string ChainTitle(int chain)
        {
            if (chain <= 1) return null;
            if (chain == 2) return "ÇİFTE";
            if (chain == 3) return "ÜÇLÜ";
            return "SERİ HAREKÂT";
        }

        /// <summary>Ana başlık: zincir varsa zincir adı, yoksa KAFADAN/LEŞ.</summary>
        public static string Headline(int chain, bool headshot, string victimName, string weaponName)
        {
            var chainTitle = ChainTitle(chain);
            var head = chainTitle ?? (headshot ? "KAFADAN" : "LEŞ");
            var text = head + " — " + (string.IsNullOrEmpty(victimName) ? "Düşman" : victimName);
            if (!string.IsNullOrEmpty(weaponName)) text += "  [" + weaponName + "]";
            return text;
        }

        /// <summary>Zincir yükseldikçe yazı büyür (1..4+).</summary>
        public static int FontSize(int chain) => 28 + Mathf.Clamp(chain - 1, 0, 3) * 6;

        /// <summary>Kamera yumruğu şiddeti (derece): kafa ve zincirle artar, sınırlı.</summary>
        public static float PunchPitch(int chain, bool headshot) => Mathf.Min(1.2f + (headshot ? 0.6f : 0f) + 0.25f * (chain - 1), 2.4f);
    }

    /// <summary>Öldürme afişi: alt-orta kayarak girer, kenar kırmızı parlaması 80 ms, asist daha küçük.</summary>
    public sealed class KillBanner : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);

        private readonly KillChainRules _chain = new KillChainRules();
        private RectTransform _bannerRt;
        private Text _banner;
        private Image _flash;
        private float _bannerStart = -999f;
        private float _bannerLen = KillChainRules.BannerSeconds;
        private float _flashStart = -999f;

        public static KillBanner Create(GameObject host)
        {
            var v = host.AddComponent<KillBanner>();
            v.Build();
            return v;
        }

        private void Build()
        {
            var canvasGo = new GameObject("KillBannerCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 41;
            var root = canvasGo.GetComponent<RectTransform>();

            var fgo = new GameObject("KillFlash", typeof(RectTransform));
            fgo.transform.SetParent(root, false);
            _flash = fgo.AddComponent<Image>();
            _flash.raycastTarget = false;
            _flash.color = Color.clear;
            var frt = _flash.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            var edge = fgo.AddComponent<Outline>();
            edge.effectColor = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.9f);
            edge.effectDistance = new Vector2(24f, 24f);
            edge.enabled = false;

            var bgo = new GameObject("KillBannerText", typeof(RectTransform));
            bgo.transform.SetParent(root, false);
            _banner = bgo.AddComponent<Text>();
            _banner.font = UiTheme.Font;
            _banner.fontStyle = FontStyle.Bold;
            _banner.alignment = TextAnchor.MiddleCenter;
            _banner.raycastTarget = false;
            _banner.horizontalOverflow = HorizontalWrapMode.Overflow;
            _banner.verticalOverflow = VerticalWrapMode.Overflow;
            var o = bgo.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            _bannerRt = _banner.rectTransform;
            _bannerRt.anchorMin = _bannerRt.anchorMax = new Vector2(0.5f, 0.2f);
            _bannerRt.sizeDelta = new Vector2(700f, 56f);
            _banner.gameObject.SetActive(false);
        }

        /// <summary>Yerel oyuncunun öldürmesi. Dönen değer zincir uzunluğu (kamera yumruğu için).</summary>
        public int OnLocalKill(string victimName, string weaponId, bool headshot, float now, string subLabel = null)
        {
            var chain = _chain.RegisterKill(now);
            string weapon = null;
            if (!string.IsNullOrEmpty(weaponId))
            {
                try { weapon = WeaponCatalog.GetDisplayName(weaponId); } catch (System.Exception) { weapon = weaponId; }
            }
            var headline = KillChainRules.Headline(chain, headshot, victimName, weapon);
            if (!string.IsNullOrEmpty(subLabel)) headline += "\n" + subLabel;
            Show(headline,
                KillChainRules.FontSize(chain),
                headshot ? Gold : (chain > 1 ? UiTheme.AccentLight : Color.white), now);
            _flashStart = now;
            return chain;
        }

        public void OnLocalAssist(float now) => Show("ASİST", 20, UiTheme.Amber, now);

        public void ResetChain() => _chain.Reset();

        private void Show(string text, int size, Color color, float now)
        {
            if (_banner == null) return;
            _banner.text = text;
            _banner.fontSize = size;
            _banner.color = color;
            _bannerStart = now;
            _banner.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_banner == null) return;
            var now = Time.unscaledTime;
            var t = now - _bannerStart;
            if (_banner.gameObject.activeSelf)
            {
                if (t > _bannerLen) _banner.gameObject.SetActive(false);
                else
                {
                    var slide = Mathf.Clamp01(t / 0.15f);
                    var ease = 1f - (1f - slide) * (1f - slide);
                    _bannerRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(-60f, 0f, ease));
                    var fade = Mathf.Clamp01((_bannerLen - t) / 0.25f);
                    var c = _banner.color; c.a = Mathf.Min(ease, fade); _banner.color = c;
                }
            }
            var ft = now - _flashStart;
            var on = ft >= 0f && ft < KillChainRules.FlashSeconds;
            var edge = _flash.GetComponent<Outline>();
            if (edge != null && edge.enabled != on) edge.enabled = on;
            _flash.color = on ? new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.12f) : Color.clear;
        }
    }
}
