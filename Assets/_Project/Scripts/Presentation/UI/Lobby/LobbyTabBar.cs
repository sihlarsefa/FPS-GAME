using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>
    /// Lobi sekme çubuğu: ANA ÜSSÜ / VİTRİN / GÖREVLER / AYARLAR. Eşit genişlikte 4 düğme, altta kayan kırmızı gösterge.
    /// Q/E (ya da <see cref="Step"/>) ile dairesel gezinme; seçim değişince <see cref="TabChanged"/> tetiklenir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyTabBar : MonoBehaviour
    {
        private readonly Text[] _labels = new Text[LobbyTheme.TabCount];
        private RectTransform _indicator;
        private float _from;
        private float _to;
        private float _t = 1f;

        /// <summary>Seçili sekme indeksi.</summary>
        public int Selected { get; private set; }

        /// <summary>Seçim değiştiğinde (yeni indeks).</summary>
        public event Action<int> TabChanged;

        /// <summary>Çubuğu ebeveynin altında kurar.</summary>
        public static LobbyTabBar Create(Transform parent, float height = 56f)
        {
            var root = UiFactory.CreateRect("LobbyTabBar", parent);
            var bg = UiFactory.Image(root, null, LobbyTheme.Panel);
            bg.raycastTarget = false;
            UiFactory.Stretch(bg);
            var bar = root.gameObject.AddComponent<LobbyTabBar>();
            for (var i = 0; i < LobbyTheme.TabCount; i++) bar.BuildTab(root, i);

            var line = UiFactory.Image(root, null, LobbyTheme.Border);
            line.raycastTarget = false;
            UiFactory.SetRect(line, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));

            var ind = UiFactory.Image(root, null, LobbyTheme.Red);
            ind.raycastTarget = false;
            ind.gameObject.name = "Indicator";
            bar._indicator = ind.rectTransform;
            bar._from = bar._to = LobbyTheme.TabCenter01(0);
            bar.PlaceIndicator(bar._from);
            bar.Refresh();
            return bar;
        }

        private void BuildTab(RectTransform root, int index)
        {
            var slot = UiFactory.CreateRect("Tab_" + index, root);
            var w = 1f / LobbyTheme.TabCount;
            UiFactory.SetRect(slot, new Vector2(w * index, 0f), new Vector2(w * (index + 1), 1f), Vector2.zero, Vector2.zero);
            var hit = UiFactory.Image(slot, null, new Color(1f, 1f, 1f, 0f));
            UiFactory.Stretch(hit);
            var label = UiFactory.Label(slot, LobbyTheme.TabNames[index], 22, TextAnchor.MiddleCenter, LobbyTheme.TextDim, FontStyle.Bold);
            UiFactory.Stretch(label);
            _labels[index] = label;
            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            var captured = index;
            button.onClick.AddListener(() => Select(captured));
            LobbyGlowButton.Attach(button);
        }

        /// <summary>Sekmeyi seçer (sınır dışı değerler kıstırılır).</summary>
        public void Select(int index)
        {
            index = Mathf.Clamp(index, 0, LobbyTheme.TabCount - 1);
            if (index == Selected) return;
            _from = Mathf.Lerp(_from, _to, LobbyTheme.EaseInOutCubic(_t));
            _to = LobbyTheme.TabCenter01(index);
            _t = 0f;
            Selected = index;
            Refresh();
            TabChanged?.Invoke(index);
        }

        /// <summary>Dairesel +1 / -1 gezinme.</summary>
        public void Step(int delta) => Select(LobbyTheme.WrapTab(Selected, delta));

        private void Update()
        {
            if (_t < 1f)
            {
                _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / LobbyTheme.TabSlideSeconds);
                PlaceIndicator(Mathf.Lerp(_from, _to, LobbyTheme.EaseInOutCubic(_t)));
            }
        }

        private void PlaceIndicator(float center01)
        {
            if (_indicator == null) return;
            var half = 0.5f / LobbyTheme.TabCount * 0.7f;
            _indicator.anchorMin = new Vector2(center01 - half, 0f);
            _indicator.anchorMax = new Vector2(center01 + half, 0f);
            _indicator.offsetMin = Vector2.zero;
            _indicator.offsetMax = new Vector2(0f, 3f);
        }

        private void Refresh()
        {
            for (var i = 0; i < _labels.Length; i++)
                if (_labels[i] != null) _labels[i].color = i == Selected ? LobbyTheme.Text : LobbyTheme.TextDim;
        }
    }
}
