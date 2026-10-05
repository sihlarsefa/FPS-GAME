using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Prosedürel çizgi (kırık çizgi, isteğe bağlı kesikli) çizen UI grafiği: intikal rotaları, güvenli bölgeye yön
    /// çizgisi. Noktalar grafiğin yerel uzayındadır (pivot = 0,0). Genellikle ebeveyni dolduracak şekilde gerilir; böylece
    /// RectMask2D ayıklaması yanlış gizlemez. Ağ yalnızca noktalar/kalınlık değişince yeniden kurulur.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MapLineGraphic : MaskableGraphic
    {
        private const float ChangeThreshold = 0.25f;
        private const int MaxDashes = 600;

        private readonly List<Vector2> _points = new List<Vector2>(8);
        private float _thickness = 2f;
        private float _dashLength;
        private float _gapLength;

        /// <summary>Çizgi kalınlığı (px).</summary>
        public float Thickness
        {
            get => _thickness;
            set
            {
                value = Mathf.Max(0.5f, value);
                if (Mathf.Approximately(_thickness, value))
                    return;
                _thickness = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Nokta sayısı.</summary>
        public int PointCount => _points.Count;

        /// <summary>
        /// Ebeveyni dolduran (pivot sol-alt) çizgi grafiği oluşturur: noktalar ebeveynin sol-alt köşesine göre px'tir.
        /// </summary>
        public static MapLineGraphic Create(Transform parent, string name, Color color, float thickness)
        {
            var rt = UiFactory.CreateRect(name, parent);
            rt.pivot = Vector2.zero;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var line = rt.gameObject.AddComponent<MapLineGraphic>();
            line.color = color;
            line.raycastTarget = false;
            line._thickness = Mathf.Max(0.5f, thickness);
            return line;
        }

        /// <summary>Kesik deseni (px). <paramref name="dashLength"/> ≤ 0 düz çizgi.</summary>
        public void SetDash(float dashLength, float gapLength)
        {
            dashLength = Mathf.Max(0f, dashLength);
            gapLength = Mathf.Max(0f, gapLength);
            if (Mathf.Approximately(dashLength, _dashLength) && Mathf.Approximately(gapLength, _gapLength))
                return;
            _dashLength = dashLength;
            _gapLength = gapLength;
            SetVerticesDirty();
        }

        /// <summary>Tek parça çizgi (a → b). Değişim eşiğin altındaysa ağ yeniden kurulmaz.</summary>
        public void SetSegment(Vector2 a, Vector2 b)
        {
            if (_points.Count == 2 && (_points[0] - a).sqrMagnitude < ChangeThreshold * ChangeThreshold
                                   && (_points[1] - b).sqrMagnitude < ChangeThreshold * ChangeThreshold)
                return;

            _points.Clear();
            _points.Add(a);
            _points.Add(b);
            SetVerticesDirty();
        }

        /// <summary>Kırık çizgi noktalarını atar (kopyalanır).</summary>
        public void SetPoints(IReadOnlyList<Vector2> points)
        {
            _points.Clear();
            if (points != null)
            {
                for (var i = 0; i < points.Count; i++)
                    _points.Add(points[i]);
            }

            SetVerticesDirty();
        }

        /// <summary>Çizgiyi boşaltır (hiçbir şey çizmez).</summary>
        public void ClearPoints()
        {
            if (_points.Count == 0)
                return;
            _points.Clear();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2)
                return;

            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            var half = _thickness * 0.5f;
            var dashed = _dashLength > 0.5f;
            var dashCount = 0;
            var phase = 0f; // kesik deseni çizgi boyunca sürer

            for (var i = 1; i < _points.Count; i++)
            {
                var a = _points[i - 1];
                var b = _points[i];
                var delta = b - a;
                var length = delta.magnitude;
                if (length < 0.01f)
                    continue;

                var dir = delta / length;
                var normal = new Vector2(-dir.y, dir.x) * half;

                if (!dashed)
                {
                    AddQuad(vh, ref vertex, a, b, normal);
                    continue;
                }

                var period = _dashLength + _gapLength;
                var t = -phase;
                while (t < length && dashCount < MaxDashes)
                {
                    var s = Mathf.Max(0f, t);
                    var e = Mathf.Min(length, t + _dashLength);
                    if (e > s + 0.01f)
                    {
                        AddQuad(vh, ref vertex, a + dir * s, a + dir * e, normal);
                        dashCount++;
                    }

                    t += period;
                }

                phase = period > 0f ? (length + phase) % period : 0f;
            }
        }

        private static void AddQuad(VertexHelper vh, ref UIVertex vertex, Vector2 a, Vector2 b, Vector2 normal)
        {
            var start = vh.currentVertCount;
            vertex.position = a - normal;
            vh.AddVert(vertex);
            vertex.position = a + normal;
            vh.AddVert(vertex);
            vertex.position = b + normal;
            vh.AddVert(vertex);
            vertex.position = b - normal;
            vh.AddVert(vertex);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
