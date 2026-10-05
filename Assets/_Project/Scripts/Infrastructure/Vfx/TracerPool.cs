using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Havuzlanmış LineRenderer mermi izleri (katkılı). Kuyruk süre boyunca başa doğru kısalır ve iz söner;
    /// genişlik kameraya uzaklıkla büyütülür (uzakta piksel altına düşmesin). Kare başına bellek ayırmaz.
    /// </summary>
    internal sealed class TracerPool
    {
        private static readonly Color HeadColor = new Color(1f, 0.86f, 0.55f, 1f);
        private static readonly Color TailColor = new Color(1f, 0.55f, 0.2f, 0.12f);

        private readonly Transform _container;
        private readonly LineRenderer[] _lines;
        private readonly Vector3[] _from;
        private readonly Vector3[] _to;
        private readonly float[] _age;
        private readonly float[] _duration;
        private readonly float[] _width;
        private readonly bool[] _active;
        private int _count;
        private int _activeCount;

        public TracerPool(Transform container, int capacity)
        {
            _container = container;
            Capacity = Mathf.Max(1, capacity);
            _lines = new LineRenderer[Capacity];
            _from = new Vector3[Capacity];
            _to = new Vector3[Capacity];
            _age = new float[Capacity];
            _duration = new float[Capacity];
            _width = new float[Capacity];
            _active = new bool[Capacity];
        }

        public int Capacity { get; }

        public int ActiveCount => _activeCount;

        public void Spawn(Vector3 from, Vector3 to, float duration, float width, bool hasCamera, Vector3 cameraPosition)
        {
            var index = Acquire();
            if (index < 0)
                return;

            _from[index] = from;
            _to[index] = to;
            _age[index] = 0f;
            _duration[index] = duration;
            _width[index] = width;
            if (!_active[index])
            {
                _active[index] = true;
                _activeCount++;
            }

            var line = _lines[index];
            line.enabled = true;
            Apply(index, 0f, hasCamera, cameraPosition);
        }

        public void Tick(float deltaTime, bool hasCamera, Vector3 cameraPosition)
        {
            if (_activeCount == 0)
                return;

            var active = 0;
            for (var i = 0; i < _count; i++)
            {
                if (!_active[i])
                    continue;

                if (_lines[i] == null)
                {
                    _active[i] = false;
                    continue;
                }

                _age[i] += deltaTime;
                var t = _age[i] / _duration[i];
                if (t >= 1f)
                {
                    _active[i] = false;
                    _lines[i].enabled = false;
                    continue;
                }

                Apply(i, t, hasCamera, cameraPosition);
                active++;
            }

            _activeCount = active;
        }

        public void Clear()
        {
            for (var i = 0; i < _count; i++)
            {
                _active[i] = false;
                if (_lines[i] != null)
                    _lines[i].enabled = false;
            }

            _activeCount = 0;
        }

        private void Apply(int index, float t, bool hasCamera, Vector3 cameraPosition)
        {
            var line = _lines[index];
            var from = _from[index];
            var to = _to[index];

            // Kuyruk başa doğru ilerler: iz "uçuyormuş" gibi görünür.
            var tail = Vector3.LerpUnclamped(from, to, t * 0.85f);
            line.SetPosition(0, tail);
            line.SetPosition(1, to);

            var fade = 1f - t;
            var head = HeadColor;
            head.a *= fade;
            var tailColor = TailColor;
            tailColor.a *= fade;
            line.startColor = tailColor;
            line.endColor = head;

            var width = _width[index];
            if (hasCamera)
            {
                line.startWidth = width * DistanceWidthFactor(tail, cameraPosition);
                line.endWidth = width * DistanceWidthFactor(to, cameraPosition);
            }
            else
            {
                line.startWidth = width;
                line.endWidth = width;
            }
        }

        private static float DistanceWidthFactor(Vector3 point, Vector3 cameraPosition)
        {
            var distance = Vector3.Distance(point, cameraPosition);
            return Mathf.Clamp(distance / 25f, 1f, 6f);
        }

        private int Acquire()
        {
            for (var i = 0; i < _count; i++)
            {
                if (_lines[i] == null)
                    return Create(i) ? i : -1;
                if (!_active[i])
                    return i;
            }

            if (_count < Capacity)
            {
                var index = _count;
                if (!Create(index))
                    return -1;
                _count++;
                return index;
            }

            // Hepsi aktif: en yaşlı izi yeniden kullan.
            var oldest = 0;
            var oldestProgress = -1f;
            for (var i = 0; i < _count; i++)
            {
                var progress = _age[i] / Mathf.Max(0.0001f, _duration[i]);
                if (progress > oldestProgress)
                {
                    oldestProgress = progress;
                    oldest = i;
                }
            }

            return oldest;
        }

        private bool Create(int index)
        {
            if (_container == null)
                return false;

            var go = new GameObject("Tracer");
            go.layer = GameLayers.Default;
            go.transform.SetParent(_container, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            line.generateLightingData = false;
            line.sharedMaterial = VfxMaterials.Tracer;
            line.startColor = TailColor;
            line.endColor = HeadColor;
            line.enabled = false;

            _lines[index] = line;
            _active[index] = false;
            return true;
        }
    }
}
