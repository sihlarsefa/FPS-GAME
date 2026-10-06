using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// Uzaktaki düşmanlara görünen dürbün parıltısı: kameraya dönük additive sprite.
    /// Bot algısı için <see cref="IntensityAt"/> statik olarak kullanılabilir.
    /// </summary>
    public sealed class ScopeGlint : MonoBehaviour
    {
        private static Texture2D _tex;
        private Transform _quad;
        private MeshRenderer _renderer;
        private Material _mat;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public static Vector3 SunDirection()
        {
            var sun = RenderSettings.sun;
            return sun != null ? -sun.transform.forward : new Vector3(0.4f, 0.8f, 0.3f).normalized;
        }

        /// <summary>Gözlemci konumundan bu dürbün parıltısının şiddeti (0..1).</summary>
        public static float IntensityAt(Vector3 scopePos, Vector3 scopeForward, Vector3 observerPos)
        {
            var to = observerPos - scopePos;
            var dist = to.magnitude;
            if (dist < 0.01f)
                return 0f;
            to /= dist;
            var f = scopeForward.normalized;
            var sun = SunDirection();
            return ScopeMath.GlintIntensity(f.x, f.y, f.z, to.x, to.y, to.z, sun.x, sun.y, sun.z, dist);
        }

        /// <summary>Her kare: dürbün dünya konumu/yönü ve gözlemci (ör. en yakın düşman ya da ana kamera).</summary>
        public void Tick(bool visibleTier, bool scoped, Vector3 scopePos, Vector3 scopeForward, Vector3 observerPos)
        {
            if (!visibleTier || !scoped)
            {
                SetVisible(false);
                return;
            }

            Ensure();
            var i = IntensityAt(scopePos, scopeForward, observerPos);
            if (i <= 0.01f)
            {
                SetVisible(false);
                return;
            }

            var dist = Vector3.Distance(scopePos, observerPos);
            _quad.position = scopePos + (observerPos - scopePos).normalized * 0.1f;
            _quad.rotation = Quaternion.LookRotation(scopePos - observerPos, Vector3.up);
            // Mesafeyle ekranda sabit büyüklükte kalacak şekilde ölçek.
            var size = Mathf.Clamp(dist * 0.012f, 0.15f, 4f) * (0.6f + i);
            _quad.localScale = Vector3.one * size;
            if (_mat != null)
                _mat.SetColor(BaseColor, new Color(1f, 0.96f, 0.85f, i));
            SetVisible(true);
        }

        private void SetVisible(bool v)
        {
            if (_renderer != null && _renderer.enabled != v)
                _renderer.enabled = v;
        }

        private void Ensure()
        {
            if (_quad != null)
                return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ScopeGlintSprite";
            var col = go.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            go.transform.SetParent(transform, false);
            _quad = go.transform;
            _renderer = go.GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _mat = new Material(sh) { name = "ScopeGlint", hideFlags = HideFlags.DontSave };
                if (_mat.HasProperty("_Surface"))
                {
                    _mat.SetFloat("_Surface", 1f);
                    _mat.SetFloat("_Blend", 2f);
                    _mat.SetFloat("_SrcBlend", 1f);
                    _mat.SetFloat("_DstBlend", 1f);
                    _mat.SetFloat("_ZWrite", 0f);
                    _mat.renderQueue = 3100;
                    _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }

                _mat.mainTexture = GlintTexture();
                if (_mat.HasProperty("_BaseMap"))
                    _mat.SetTexture("_BaseMap", GlintTexture());
                _renderer.sharedMaterial = _mat;
            }

            _renderer.enabled = false;
        }

        public static Texture2D GlintTexture()
        {
            if (_tex != null)
                return _tex;
            const int n = 64;
            var px = new Color32[n * n];
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    var dx = (x + 0.5f) / n * 2f - 1f;
                    var dy = (y + 0.5f) / n * 2f - 1f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var core = Mathf.Clamp01(1f - d * 2.2f);
                    var star = Mathf.Clamp01(1f - Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy)) * 9f) * Mathf.Clamp01(1f - d);
                    var a = Mathf.Clamp01(core * core + star * 0.8f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            _tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            _tex.SetPixels32(px);
            _tex.Apply(false, true);
            return _tex;
        }

        private void OnDestroy()
        {
            if (_mat != null)
                Destroy(_mat);
        }
    }
}
