using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Kompakt 6 çubuklu istatistik bloğu (vitrin gibi dar alanlar için): etiket + dolgu + harf notu.
    /// Dolgu rengi nota göre (S/A yeşil, B sarı, C/D kırmızı). Hesap LoadoutStats/LoadoutStatCompare'dadır.
    /// </summary>
    public sealed class LoadoutMiniBars : MonoBehaviour
    {
        private const float Row = 26f;
        private readonly Image[] _fills = new Image[LoadoutStats.BarCount];
        private readonly Text[] _grades = new Text[LoadoutStats.BarCount];
        private readonly float[] _target = new float[LoadoutStats.BarCount];
        private readonly float[] _shown = new float[LoadoutStats.BarCount];
        private Text _overall;
        private Color _label, _track;

        /// <summary>Verilen alana (sol-alt köşeye hizalı) kurar; renkler çağıran temasından gelir.</summary>
        public static LoadoutMiniBars Create(RectTransform parent, Vector2 bottomLeft, float width, Color label, Color track)
        {
            var root = UiFactory.CreateRect("MiniBars", parent);
            UiFactory.SetRect(root, new Vector2(0f, 0f), new Vector2(0f, 0f), bottomLeft, bottomLeft + new Vector2(width, Row * (LoadoutStats.BarCount + 1)));
            var bars = root.gameObject.AddComponent<LoadoutMiniBars>();
            bars._label = label;
            bars._track = track;
            bars.Build(root);
            return bars;
        }

        private void Build(RectTransform root)
        {
            _overall = UiFactory.Label(root, string.Empty, 18, TextAnchor.MiddleLeft, _label, FontStyle.Bold);
            _overall.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_overall, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -Row), new Vector2(0f, 0f));
            for (var i = 0; i < LoadoutStats.BarCount; i++)
            {
                var top = -Row * (i + 1);
                var cap = UiFactory.Label(root, LoadoutStats.Labels[i], 14, TextAnchor.MiddleLeft, _label, FontStyle.Bold);
                cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(cap, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, top - Row), new Vector2(84f, top));
                var track = UiFactory.Image(root, UiSprites.White, _track);
                track.raycastTarget = false;
                UiFactory.SetRect(track, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(88f, top - Row * 0.72f), new Vector2(-34f, top - Row * 0.28f));
                var fill = UiFactory.Image(track.transform, UiSprites.White, Color.white);
                fill.raycastTarget = false;
                UiFactory.SetRect(fill, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                _fills[i] = fill;
                var g = UiFactory.Label(root, string.Empty, 15, TextAnchor.MiddleRight, _label, FontStyle.Bold);
                UiFactory.SetRect(g, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, top - Row), new Vector2(0f, top));
                _grades[i] = g;
            }
        }

        /// <summary>Silahın eklentisiz değerlerini gösterir (null = gizli/boş).</summary>
        public void SetWeapon(Project.Core.Domain.WeaponDefinitionData weapon)
        {
            if (weapon == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            var set = LoadoutStats.Compute(weapon);
            var overall = LoadoutStatCompare.Overall(set, weapon.Category);
            _overall.text = "GENEL  " + overall + "  (" + LoadoutStatCompare.GradeFromPoints(overall) + ")";
            for (var i = 0; i < LoadoutStats.BarCount; i++)
            {
                var v = Mathf.Clamp01(LoadoutStats.Value(set, i));
                _target[i] = v;
                _grades[i].text = LoadoutStatCompare.Grade(v);
                _fills[i].color = GradeColor(v);
            }
        }

        public static Color GradeColor(float v)
        {
            if (v >= 0.70f) return UiTheme.Success;
            if (v >= 0.50f) return UiTheme.Amber;
            return UiTheme.Danger;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            for (var i = 0; i < _fills.Length; i++)
            {
                if (_fills[i] == null)
                    continue;
                _shown[i] = MainMenuMotion.Approach(_shown[i], _target[i], 9f, dt);
                _fills[i].rectTransform.anchorMax = new Vector2(_shown[i], 1f);
            }
        }
    }
}
