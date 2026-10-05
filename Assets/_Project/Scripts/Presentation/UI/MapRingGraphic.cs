using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Prosedürel halka (annulus) çizen UI grafiği: harita bölge çemberleri (sabit piksel kalınlıkta kenar), bölge dışı
    /// mavi tonlama (iç yarıçaptan dış yarıçapa dolgu) ve kesikli çemberler. Sprite ölçeklemenin aksine kenar kalınlığı
    /// yarıçaptan bağımsızdır. Ağ yalnızca yarıçap/kalınlık anlamlı ölçüde değişince yeniden kurulur.
    /// RectTransform boyutu dış çapa eşitlenir (RectMask2D ayıklaması doğru çalışsın diye).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MapRingGraphic : MaskableGraphic
    {
        private const float RebuildThreshold = 0.35f;
        private const int MinSegments = 24;
        private const int MaxSegments = 160;

        private float _innerRadius;
        private float _outerRadius = 1f;
        private int _dashCount;
        private int _segmentsOverride;

        /// <summary>İç yarıçap (px).</summary>
        public float InnerRadius => _innerRadius;

        /// <summary>Dış yarıçap (px).</summary>
        public float OuterRadius => _outerRadius;

        /// <summary>Kesik sayısı (0 = düz çizgi). Çevre bu sayıda dolu/boş çifte bölünür.</summary>
        public int DashCount
        {
            get => _dashCount;
            set
            {
                value = Mathf.Clamp(value, 0, 96);
                if (_dashCount == value)
                    return;
                _dashCount = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Sabit dilim sayısı (0 = yarıçapa göre otomatik).</summary>
        public int Segments
        {
            get => _segmentsOverride;
            set
            {
                value = value <= 0 ? 0 : Mathf.Clamp(value, MinSegments, MaxSegments);
                if (_segmentsOverride == value)
                    return;
                _segmentsOverride = value;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// Yeni halka grafiği oluşturur (ışın hedefi kapalı). Konum çapalarla verilir; boyut <see cref="SetRing"/> ile atanır.
        /// </summary>
        public static MapRingGraphic Create(Transform parent, string name, Color color)
        {
            var rt = UiFactory.CreateRect(name, parent);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * 2f;
            var ring = rt.gameObject.AddComponent<MapRingGraphic>();
            ring.color = color;
            ring.raycastTarget = false;
            return ring;
        }

        /// <summary>Merkez yarıçapı ve kenar kalınlığıyla (px) halka çizgisi.</summary>
        public void SetStroke(float radius, float thickness)
        {
            radius = Mathf.Max(0f, radius);
            thickness = Mathf.Max(0.5f, thickness);
            SetRing(Mathf.Max(0f, radius - thickness * 0.5f), radius + thickness * 0.5f);
        }

        /// <summary>İç ve dış yarıçapı (px) atar. Değişim eşiğin altındaysa ağ yeniden kurulmaz.</summary>
        public void SetRing(float innerRadius, float outerRadius)
        {
            if (float.IsNaN(innerRadius) || float.IsNaN(outerRadius))
                return;

            innerRadius = Mathf.Max(0f, innerRadius);
            outerRadius = Mathf.Max(innerRadius + 0.5f, outerRadius);
            if (Mathf.Abs(innerRadius - _innerRadius) < RebuildThreshold && Mathf.Abs(outerRadius - _outerRadius) < RebuildThreshold)
                return;

            _innerRadius = innerRadius;
            _outerRadius = outerRadius;
            var size = outerRadius * 2f;
            var rt = rectTransform;
            if (Mathf.Abs(rt.sizeDelta.x - size) > RebuildThreshold || Mathf.Abs(rt.sizeDelta.y - size) > RebuildThreshold)
                rt.sizeDelta = new Vector2(size, size);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_outerRadius <= 0.01f)
                return;

            var center = rectTransform.rect.center;
            var segments = _segmentsOverride > 0
                ? _segmentsOverride
                : Mathf.Clamp(Mathf.CeilToInt(_outerRadius * 0.35f), MinSegments, MaxSegments);
            if (_dashCount > 0)
                segments = Mathf.Max(segments, _dashCount * 4) / (_dashCount * 2) * (_dashCount * 2);

            var c = (Color32)color;
            var vertex = UIVertex.simpleVert;
            vertex.color = c;
            var step = Mathf.PI * 2f / segments;
            var dashSegments = _dashCount > 0 ? segments / (_dashCount * 2) : 0;

            if (dashSegments <= 0)
            {
                for (var i = 0; i <= segments; i++)
                {
                    var a = i * step;
                    var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                    vertex.position = center + dir * _innerRadius;
                    vh.AddVert(vertex);
                    vertex.position = center + dir * _outerRadius;
                    vh.AddVert(vertex);
                    if (i > 0)
                    {
                        var b = i * 2;
                        vh.AddTriangle(b - 2, b - 1, b + 1);
                        vh.AddTriangle(b - 2, b + 1, b);
                    }
                }

                return;
            }

            for (var i = 0; i < segments; i++)
            {
                if ((i / dashSegments) % 2 == 1)
                    continue;

                var a0 = i * step;
                var a1 = (i + 1) * step;
                var d0 = new Vector2(Mathf.Sin(a0), Mathf.Cos(a0));
                var d1 = new Vector2(Mathf.Sin(a1), Mathf.Cos(a1));
                var start = vh.currentVertCount;
                vertex.position = center + d0 * _innerRadius;
                vh.AddVert(vertex);
                vertex.position = center + d0 * _outerRadius;
                vh.AddVert(vertex);
                vertex.position = center + d1 * _outerRadius;
                vh.AddVert(vertex);
                vertex.position = center + d1 * _innerRadius;
                vh.AddVert(vertex);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
