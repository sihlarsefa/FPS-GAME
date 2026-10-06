using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Halka tamponlu çıkartma havuzu (mermi deliği, yanık izi). Paylaşılan çift yüzlü quad + paylaşılan malzemeler;
    /// doluysa en eski çıkartma taşınır. Hareketli nesnelere (Rigidbody/araç) bağlanabilir; bağlı olduğu nesne yok
    /// edilirse örnek tembel olarak yeniden kurulur.
    /// </summary>
    internal sealed class DecalPool
    {
        private static Mesh _quad;

        private readonly string _name;
        private readonly Transform _container;
        private readonly Transform[] _items;
        private readonly MeshRenderer[] _renderers;
        private int _count;
        private int _cursor;

        public DecalPool(string name, Transform container, int capacity)
        {
            _name = name;
            _container = container;
            Capacity = Mathf.Max(1, capacity);
            _items = new Transform[Capacity];
            _renderers = new MeshRenderer[Capacity];
        }

        public int Capacity { get; }

        public int Count => _count;

        public void Place(Vector3 point, Vector3 normal, float size, float rollDegrees, Material material, Transform attachTo)
        {
            if (material == null || _container == null)
                return;

            int index;
            if (_count < Capacity)
            {
                index = _count++;
            }
            else
            {
                index = _cursor;
                _cursor = (_cursor + 1) % Capacity;
            }

            var t = _items[index];
            if (t == null && !Create(index))
                return;

            t = _items[index];
            var renderer = _renderers[index];

            if (t.parent != _container)
                t.SetParent(_container, false);

            var rotation = Quaternion.LookRotation(normal) * Quaternion.AngleAxis(rollDegrees, Vector3.forward);
            t.SetPositionAndRotation(point, rotation);
            t.localScale = new Vector3(size, size, size);

            if (renderer.sharedMaterial != material)
                renderer.sharedMaterial = material;
            if (!renderer.enabled)
                renderer.enabled = true;

            if (attachTo != null)
                t.SetParent(attachTo, true);
        }

        /// <summary>Sıradaki <see cref="Place"/> çağrısının yazacağı yuva (halka sırası; FootprintTrail zaman çizelgesi için).</summary>
        public int NextSlot => _count < Capacity ? _count : _cursor;

        /// <summary>Yuvadaki çıkartmayı gizler (solma bitti).</summary>
        public void HideSlot(int slot)
        {
            if (slot >= 0 && slot < Capacity && _renderers[slot] != null)
                _renderers[slot].enabled = false;
        }

        /// <summary>Yuvadaki çıkartmaya örnek başına renk/alfa uygular (paylaşılan malzemeyi bozmaz).</summary>
        public void TintSlot(int slot, MaterialPropertyBlock block)
        {
            if (slot >= 0 && slot < Capacity && _renderers[slot] != null)
                _renderers[slot].SetPropertyBlock(block);
        }

        public void Clear()
        {
            for (var i = 0; i < _count; i++)
            {
                var t = _items[i];
                if (t == null)
                    continue;

                if (t.parent != _container)
                    t.SetParent(_container, false);
                if (_renderers[i] != null)
                    _renderers[i].enabled = false;
            }

            _count = 0;
            _cursor = 0;
        }

        private bool Create(int index)
        {
            if (_container == null)
                return false;

            var go = new GameObject(_name);
            go.layer = GameLayers.Default;
            go.transform.SetParent(_container, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = QuadMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = true;
            renderer.enabled = false;

            _items[index] = go.transform;
            _renderers[index] = renderer;
            return true;
        }

        /// <summary>Yerel XY düzleminde, +Z'ye bakan, iki yüzlü birim quad (köşe rengi beyaz).</summary>
        public static Mesh QuadMesh
        {
            get
            {
                if (_quad != null)
                    return _quad;

                _quad = new Mesh { name = "VFX_DecalQuad", hideFlags = HideFlags.DontSave };
                _quad.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                };
                _quad.uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                };
                _quad.colors32 = new[]
                {
                    new Color32(255, 255, 255, 255),
                    new Color32(255, 255, 255, 255),
                    new Color32(255, 255, 255, 255),
                    new Color32(255, 255, 255, 255)
                };
                _quad.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
                // Her iki sarım yönü: kaplama hangi yönden bakılırsa görünsün.
                _quad.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
                _quad.RecalculateBounds();
                _quad.UploadMeshData(true);
                return _quad;
            }
        }

        public static void ResetCache()
        {
            _quad = null;
        }
    }
}
