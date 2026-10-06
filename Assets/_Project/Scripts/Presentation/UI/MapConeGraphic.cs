using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Görüş konisi (yelpaze) çizen UI grafiği: tim arkadaşlarının ve oyuncunun bakış alanı. Uç yukarıyı (+Y) gösterir;
    /// bakış yönü RectTransform Z dönüşüyle verilir. Merkez daha opak, kenar şeffaftır. Ağ yalnızca yarıçap/açı
    /// değişince yeniden kurulur. RectTransform boyutu yarıçapa eşitlenir (maske ayıklaması doğru çalışsın diye).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MapConeGraphic : MaskableGraphic
    {
        private const int Slices = 10;

        private float _radius = 10f;
        private float _angle = 70f;

        /// <summary>Koni yarıçapı (px).</summary>
        public float Radius => _radius;

        /// <summary>Toplam görüş açısı (derece).</summary>
        public float Angle => _angle;

        /// <summary>Yeni koni oluşturur (ışın hedefi kapalı; merkez = üst nesnenin merkezi).</summary>
        public static MapConeGraphic Create(Transform parent, string name, Color color)
        {
            var rt = UiFactory.CreateRect(name, parent);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * 20f;
            var cone = rt.gameObject.AddComponent<MapConeGraphic>();
            cone.color = color;
            cone.raycastTarget = false;
            return cone;
        }

        /// <summary>Yarıçap (px) ve toplam açı (derece, 5..180) atar.</summary>
        public void SetShape(float radius, float angleDegrees)
        {
            radius = Mathf.Max(2f, radius);
            angleDegrees = Mathf.Clamp(angleDegrees, 5f, 180f);
            if (Mathf.Abs(radius - _radius) < 0.35f && Mathf.Abs(angleDegrees - _angle) < 0.1f)
                return;

            _radius = radius;
            _angle = angleDegrees;
            rectTransform.sizeDelta = Vector2.one * (radius * 2f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var center = color;
            var edge = color;
            edge.a = 0f;
            center.a = color.a;

            vh.AddVert(Vector3.zero, center, Vector2.zero);
            var half = _angle * 0.5f;
            for (var i = 0; i <= Slices; i++)
            {
                var a = Mathf.Lerp(-half, half, i / (float)Slices) * Mathf.Deg2Rad;
                vh.AddVert(new Vector3(Mathf.Sin(a) * _radius, Mathf.Cos(a) * _radius, 0f), edge, Vector2.zero);
            }

            for (var i = 1; i <= Slices; i++)
                vh.AddTriangle(0, i, i + 1);
        }
    }
}
