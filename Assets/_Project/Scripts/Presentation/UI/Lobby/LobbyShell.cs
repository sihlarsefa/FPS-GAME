using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>
    /// Lobi kabuğu: vinyet + sekme çubuğu + sekme başına panel (açılış geçişli) + oyuncu kartı.
    /// Panel içerikleri çağıran tarafça doldurulur (<see cref="GetTabPanel"/>). Q/E ile sekme gezinmesi.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyShell : MonoBehaviour
    {
        private readonly RectTransform[] _panels = new RectTransform[LobbyTheme.TabCount];
        private readonly LobbyPanelTransition[] _transitions = new LobbyPanelTransition[LobbyTheme.TabCount];

        public LobbyTabBar Tabs { get; private set; }
        public LobbyPlayerCard Card { get; private set; }
        public LobbyVignette Vignette { get; private set; }
        /// <summary>Sağ alttaki büyük kırmızı OYNA düğmesi.</summary>
        public Button PlayButton { get; private set; }
        private Text _rankText;
        private Text _xpText;

        /// <summary>Üst bar sağ göstergesi (rütbe + deneyim/para).</summary>
        public void SetStatus(string rank, string xp)
        {
            if (_rankText != null) _rankText.text = rank ?? string.Empty;
            if (_xpText != null) _xpText.text = xp ?? string.Empty;
        }

        /// <summary>Verilen sekmeyi seçer.</summary>
        public void SelectTab(int index) { if (Tabs != null) Tabs.Select(index); }

        /// <summary>Verilen sekmenin içerik paneli.</summary>
        public RectTransform GetTabPanel(int index) => _panels[Mathf.Clamp(index, 0, LobbyTheme.TabCount - 1)];

        /// <summary>Kabuğu ebeveynin (tam ekran canvas) altında kurar.</summary>
        public static LobbyShell Create(Transform parent, Action onPlay = null)
        {
            var root = UiFactory.CreateRect("LobbyShell", parent);
            UiFactory.Stretch(root);
            var shell = root.gameObject.AddComponent<LobbyShell>();

            var bg = UiFactory.Image(root, null, new Color(LobbyTheme.Bg.r, LobbyTheme.Bg.g, LobbyTheme.Bg.b, 0.45f));
            bg.raycastTarget = false;
            UiFactory.Stretch(bg);
            shell.Vignette = LobbyVignette.Create(root);

            shell.BuildTopBar(root);
            shell.Tabs = LobbyTabBar.Create(root, 56f);
            var tabRect = (RectTransform)shell.Tabs.transform;
            UiFactory.SetRect(tabRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(360f, -56f), new Vector2(-420f, 0f));

            for (var i = 0; i < LobbyTheme.TabCount; i++)
            {
                var panel = UiFactory.CreateRect("TabPanel_" + i, root);
                UiFactory.SetRect(panel, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -60f));
                shell._panels[i] = panel;
                shell._transitions[i] = LobbyPanelTransition.Attach(panel);
                panel.gameObject.SetActive(i == 0);
            }

            LobbyVitrin.Create(shell._panels[1]); // VİTRİN: kaplama listesi + 3D önizleme + KUŞAN
            shell.Card = LobbyPlayerCard.Create(root);
            UiFactory.Anchor(shell.Card, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-400f, 48f), new Vector2(420f, 150f));
            shell.PlayButton = LobbyPlayButton.Create(root, onPlay);
            shell.Tabs.TabChanged += shell.OnTabChanged;
            return shell;
        }

        private int _current;

        private void BuildTopBar(RectTransform root)
        {
            var bar = UiFactory.Image(root, null, LobbyTheme.Panel);
            bar.gameObject.name = "TopBar";
            bar.raycastTarget = false;
            UiFactory.SetRect(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), Vector2.zero);
            var logo = UiFactory.Label(bar.rectTransform, "HAREKÂT", 34, TextAnchor.MiddleLeft, LobbyTheme.Text, FontStyle.Bold);
            logo.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(logo, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(28f, 0f), new Vector2(340f, 0f));
            var dot = UiFactory.Image(bar.rectTransform, null, LobbyTheme.Red);
            dot.raycastTarget = false;
            UiFactory.SetRect(dot, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 10f), new Vector2(5f, -10f));
            _rankText = UiFactory.Label(bar.rectTransform, string.Empty, 17, TextAnchor.MiddleRight, LobbyTheme.Gold, FontStyle.Bold);
            _rankText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_rankText, new Vector2(1f, 0.5f), new Vector2(1f, 1f), new Vector2(-410f, 0f), new Vector2(-24f, -4f));
            _xpText = UiFactory.Label(bar.rectTransform, string.Empty, 15, TextAnchor.MiddleRight, LobbyTheme.TextDim);
            _xpText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_xpText, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-410f, 4f), new Vector2(-24f, 0f));
        }

        private void OnTabChanged(int index)
        {
            if (index == _current) return;
            _transitions[_current].Hide();
            _transitions[index].Show();
            _current = index;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.qKey.wasPressedThisFrame) Tabs.Step(-1);
            else if (kb.eKey.wasPressedThisFrame) Tabs.Step(1);
        }
    }
}
