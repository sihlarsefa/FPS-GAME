using System.IO;
using Project.Infrastructure;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Lobideki oyuncu (tim komutanı). Gemini'den gelen tam boy kesiti kamp ateşinin başında,
    /// 1.81 m, kameraya döner. Blok adamın yerine bu yüz ve bu kıyafet durur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyCommanderStand : MonoBehaviour
    {
        private const float HeightMeters = 1.81f;
        private const string BodyRelative = "Lobby/commander_body.png";

        private Texture2D _texture;
        private Material _material;
        private Mesh _mesh;
        private Vector3 _baseLocal;

        /// <summary>Yüz veya gövde dokusunu StreamingAssets'ten okur. Yoksa null.</summary>
        public static Texture2D LoadTexture(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            var path = Path.Combine(UnityEngine.Application.streamingAssetsPath, relativePath);
            if (!File.Exists(path))
                return null;

            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "HK_" + Path.GetFileNameWithoutExtension(relativePath),
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                if (!tex.LoadImage(bytes, true) || tex.width < 2)
                {
                    Destroy(tex);
                    return null;
                }

                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Lobby] Karakter görseli okunamadı: " + e.Message);
                return null;
            }
        }

        /// <summary>Komutan kesitini dekor köküne koyar. Dosya yoksa false; çağıran blok askere döner.</summary>
        public static bool TryPlace(Transform root, Vector3 localPosition)
        {
            if (root == null || UnityEngine.Application.isBatchMode)
                return false;

            var tex = LoadTexture(BodyRelative);
            if (tex == null)
                return false;

            var aspect = tex.width / (float)tex.height;
            var width = HeightMeters * aspect;
            var mesh = BuildQuad(width, HeightMeters);
            var mat = CreateCutoutMaterial(tex);
            if (mat == null)
            {
                Destroy(tex);
                Destroy(mesh);
                return false;
            }

            var go = new GameObject("OyuncuKomutan");
            go.layer = GameLayers.Default;
            var t = go.transform;
            t.SetParent(root, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var stand = go.AddComponent<LobbyCommanderStand>();
            stand._texture = tex;
            stand._material = mat;
            stand._mesh = mesh;
            stand._baseLocal = localPosition;
            return true;
        }

        private void LateUpdate()
        {
            var cam = MenuBackdrop.Current != null ? MenuBackdrop.Current.Camera : null;
            if (cam != null)
            {
                var dir = cam.transform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.04f)
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }

            var local = _baseLocal;
            local.y += Mathf.Sin(Time.time * 1.15f) * 0.012f;
            transform.localPosition = local;
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
            if (_texture != null)
                Destroy(_texture);
            if (_mesh != null)
                Destroy(_mesh);
        }

        private static Mesh BuildQuad(float width, float height)
        {
            var mesh = new Mesh { name = "HK_CommanderLikeness" };
            var hx = width * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hx, 0f, 0f),
                new Vector3(hx, 0f, 0f),
                new Vector3(-hx, height, 0f),
                new Vector3(hx, height, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateCutoutMaterial(Texture2D texture)
        {
            var shader = RenderPipelineInfo.FindFirst(RenderPipelineInfo.UrpUnlit, "Unlit/Transparent", "Sprites/Default");
            if (shader == null)
                return null;

            var mat = new Material(shader) { name = "HK_CommanderLikeness" };
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", texture);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", Color.white);

            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Cull"))
                mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }
    }
}
