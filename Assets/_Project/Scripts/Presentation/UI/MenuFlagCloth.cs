using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Rüzgârda dalgalanan bayrak kumaşı (menü dekoru). Direğe bağlı kenar (yerel x = 0) sabittir; serbest uç doğru
    /// dalga genliği artar, rüzgâr esintisi Perlin gürültüsüyle değişir. Kumaş yerel XY düzlemindedir, +x direkten
    /// uzağa, ön yüz −z yönüne bakar (doku düz okunur). Kare başına bellek ayırmaz (köşe dizisi yeniden kullanılır).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuFlagCloth : MonoBehaviour
    {
        private const int Columns = 18;
        private const int Rows = 11;

        private Mesh _mesh;
        private Vector3[] _rest;
        private Vector3[] _vertices;
        private float _width;
        private float _height;
        private float _phase;
        private bool _visible = true;

        /// <summary>Dalga hızı çarpanı (varsayılan 1).</summary>
        public float WindStrength { get; set; } = 1f;

        /// <summary>
        /// Bayrak kumaşı oluşturur: <paramref name="parent"/> altında, bağlı kenar yerel orijinde ve kumaş aşağı doğru
        /// <paramref name="height"/> kadar uzanır (üst kenar orijinde).
        /// </summary>
        public static MenuFlagCloth Create(Transform parent, float width, float height, Material material)
        {
            var go = new GameObject("BayrakKumaşı");
            go.transform.SetParent(parent, false);
            go.layer = parent != null ? parent.gameObject.layer : 0;

            var cloth = go.AddComponent<MenuFlagCloth>();
            cloth._width = Mathf.Max(0.1f, width);
            cloth._height = Mathf.Max(0.1f, height);
            cloth._phase = Random.Range(0f, 10f);
            cloth.BuildMesh();

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = cloth._mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material != null ? material : SafeFlagMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            cloth.Simulate(0f);
            return cloth;
        }

        private static Material SafeFlagMaterial()
        {
            try
            {
                return MaterialLibrary.Get(MaterialId.TurkishFlag);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MenuFlagCloth] Bayrak malzemesi alınamadı: " + e.Message);
                return null;
            }
        }

        private void BuildMesh()
        {
            var count = (Columns + 1) * (Rows + 1);
            _rest = new Vector3[count];
            _vertices = new Vector3[count];
            var uvs = new Vector2[count];
            for (var r = 0; r <= Rows; r++)
            {
                for (var c = 0; c <= Columns; c++)
                {
                    var i = r * (Columns + 1) + c;
                    var u = c / (float)Columns;
                    var v = r / (float)Rows;
                    _rest[i] = new Vector3(u * _width, -_height + v * _height, 0f);
                    _vertices[i] = _rest[i];
                    uvs[i] = new Vector2(u, v);
                }
            }

            // Ön yüz −z (kameraya) bakar: (sol-alt, sol-üst, sağ-alt) saat yönündedir.
            var triangles = new int[Columns * Rows * 6];
            var t = 0;
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Columns; c++)
                {
                    var v00 = r * (Columns + 1) + c;
                    var v10 = v00 + 1;
                    var v01 = v00 + Columns + 1;
                    var v11 = v01 + 1;
                    triangles[t++] = v00;
                    triangles[t++] = v01;
                    triangles[t++] = v10;
                    triangles[t++] = v10;
                    triangles[t++] = v01;
                    triangles[t++] = v11;
                }
            }

            _mesh = new Mesh { name = "HK_MenuFlag" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            // Dalgalanma sınırları aşmasın diye geniş, sabit sınır kutusu (her kare yeniden hesaplanmaz).
            _mesh.bounds = new Bounds(new Vector3(_width * 0.5f, -_height * 0.5f, 0f), new Vector3(_width * 1.2f, _height * 1.4f, _width * 0.8f));
        }

        private void OnBecameVisible()
        {
            _visible = true;
        }

        private void OnBecameInvisible()
        {
            _visible = false;
        }

        private void Update()
        {
            if (!_visible || _mesh == null)
                return;

            var dt = Time.deltaTime;
            if (dt > 0.1f)
                dt = 0.1f;
            Simulate(dt);
        }

        private void Simulate(float dt)
        {
            var t = Time.time;
            var gust = Mathf.PerlinNoise(t * 0.23f, _phase) * 0.8f + 0.45f;   // 0.45..1.25
            _phase += dt * (3.4f + gust * 1.6f) * Mathf.Max(0.1f, WindStrength);

            var droop = Mathf.Lerp(0.16f, 0.03f, Mathf.Clamp01(gust - 0.45f));
            for (var r = 0; r <= Rows; r++)
            {
                var v = r / (float)Rows;
                for (var c = 0; c <= Columns; c++)
                {
                    var i = r * (Columns + 1) + c;
                    var u = c / (float)Columns;
                    var rest = _rest[i];

                    // Direkten uzaklaştıkça artan genlik; iki dalga + çapraz bileşen.
                    var amplitude = u * (0.1f + 0.1f * gust) * _width * 0.5f;
                    var wave = Mathf.Sin(_phase - u * 7.5f + v * 1.3f) * amplitude
                               + Mathf.Sin(_phase * 1.7f - u * 13f - v * 2.1f) * amplitude * 0.28f;

                    var x = rest.x * (1f - 0.05f * u * (1f - gust * 0.5f));
                    var y = rest.y - u * u * droop * _height + Mathf.Sin(_phase * 0.9f - u * 5f) * u * 0.03f * _height;
                    _vertices[i] = new Vector3(x, y, wave);
                }
            }

            _mesh.SetVertices(_vertices, 0, _vertices.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            _mesh.RecalculateNormals();
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(_mesh);
                else
                    DestroyImmediate(_mesh);
                _mesh = null;
            }
        }
    }
}
