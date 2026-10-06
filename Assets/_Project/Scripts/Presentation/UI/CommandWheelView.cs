using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Radyal komut çarkı arayüzü (kendi ekran tuvali). Sapma imleci fare deltasıyla sürülür; seçim bırakınca yapılır.
    /// Mantık <see cref="CommandWheelMath"/>'te; bu sınıf yalnızca çizer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommandWheelView : MonoBehaviour
    {
        public const float Radius = 210f;
        public const float DeadZone = 0.28f;

        private static CommandWheelView _instance;

        private GameObject _rootGo;
        private RectTransform _cursor;
        private readonly Image[] _slices = new Image[CommandWheelMath.ItemCount];
        private readonly Text[] _labels = new Text[CommandWheelMath.ItemCount];
        private Text _center;
        private int _selected = -2;

        public static CommandWheelView Current => _instance;

        public bool IsOpen => _rootGo != null && _rootGo.activeSelf;

        public static CommandWheelView EnsureExists()
        {
            if (_instance != null)
                return _instance;

            var canvas = UiFactory.CreateCanvas("CommandWheelCanvas", 60);
            var view = canvas.gameObject.AddComponent<CommandWheelView>();
            view.Build(canvas.GetComponent<RectTransform>());
            _instance = view;
            return view;
        }

        private void Build(RectTransform root)
        {
            var group = HudBuild.Fill("Wheel", root);
            _rootGo = group.gameObject;
            HudBuild.PassiveGroup(group);
            HudBuild.FillImage("Dim", group, UiSprites.White, new Color(0f, 0f, 0f, 0.35f));
            HudBuild.Image("Ring", group, UiSprites.ThinRing, UiTheme.WithAlpha(UiTheme.Khaki, 0.55f), Vector2.zero,
                new Vector2(Radius * 2f + 60f, Radius * 2f + 60f));
            _center = HudBuild.Text("Center", group, "KOMUT", 22, TextAnchor.MiddleCenter, UiTheme.Amber, FontStyle.Bold,
                HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(200f, 40f));

            for (var i = 0; i < CommandWheelMath.ItemCount; i++)
            {
                var pos = CommandWheelMath.SliceDirection(i, CommandWheelMath.ItemCount) * Radius;
                _slices[i] = HudBuild.Image("Slice" + i, group, UiSprites.RoundedRect, UiTheme.PanelDark, HudBuild.Center, HudBuild.Center,
                    pos, new Vector2(190f, 58f));
                _labels[i] = HudBuild.Text("Label", _slices[i].transform, CommandWheelMath.Label((CommandWheelItem)i), 22,
                    TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold, HudBuild.Center, HudBuild.Center, Vector2.zero,
                    new Vector2(180f, 40f));
            }

            var dot = HudBuild.Image("Cursor", group, UiSprites.Circle, UiTheme.Amber, Vector2.zero, new Vector2(14f, 14f));
            _cursor = dot.rectTransform;
            _rootGo.SetActive(false);
        }

        public void Open()
        {
            if (_rootGo == null)
                return;

            _selected = -2;
            SetSelection(-1, Vector2.zero);
            _rootGo.SetActive(true);
        }

        public void Close()
        {
            if (_rootGo != null)
                _rootGo.SetActive(false);
        }

        /// <summary>Seçili dilimi ve imleç noktasını (−1..1 sapma) günceller.</summary>
        public void SetSelection(int index, Vector2 normalizedOffset)
        {
            if (_cursor != null)
                _cursor.anchoredPosition = normalizedOffset * (Radius * 0.7f);

            if (index == _selected)
                return;

            _selected = index;
            for (var i = 0; i < _slices.Length; i++)
            {
                var on = i == index;
                _slices[i].color = on ? UiTheme.WithAlpha(UiTheme.AccentDark, 0.95f) : UiTheme.PanelDark;
                _slices[i].rectTransform.localScale = on ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;
                _labels[i].color = on ? Color.white : UiTheme.Text;
            }

            _center.text = index >= 0 ? CommandWheelMath.Label((CommandWheelItem)index) : "KOMUT";
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
