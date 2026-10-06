using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Kısa ömürlü nokta ışık havuzu (namlu alevi, patlama). Sabit üst sınır; doluysa en az ömrü kalan ışık yeniden
    /// kullanılır. Gölge yok, yoğunluk karesel söner.
    /// </summary>
    internal sealed class FlashLightPool
    {
        private readonly string _name;
        private readonly Transform _container;
        private readonly Light[] _lights;
        private readonly Transform[] _transforms;
        private readonly float[] _timeLeft;
        private readonly float[] _duration;
        private readonly float[] _intensity;
        private int _count;
        private int _active;

        public FlashLightPool(string name, Transform container, int capacity)
        {
            _name = name;
            _container = container;
            Capacity = Mathf.Max(1, capacity);
            _lights = new Light[Capacity];
            _transforms = new Transform[Capacity];
            _timeLeft = new float[Capacity];
            _duration = new float[Capacity];
            _intensity = new float[Capacity];
        }

        public int Capacity { get; }

        public int ActiveCount => _active;

        public void Flash(Vector3 position, Color color, float intensity, float range, float duration, int maxActive = int.MaxValue)
        {
            if (_container == null || intensity <= 0f || range <= 0f)
                return;

            // Kademe sınırı: doluysa yeni ışık atlanır (ucuz; en eski ışık yeniden kullanılmaz).
            if (_active >= maxActive)
                return;

            var index = Acquire();
            if (index < 0)
                return;

            var light = _lights[index];
            _transforms[index].position = position;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            if (!light.enabled)
            {
                light.enabled = true;
                _active++;
            }

            _intensity[index] = intensity;
            _duration[index] = Mathf.Max(0.01f, duration);
            _timeLeft[index] = _duration[index];
        }

        public void Tick(float deltaTime)
        {
            if (_active == 0)
                return;

            var active = 0;
            for (var i = 0; i < _count; i++)
            {
                var light = _lights[i];
                if (light == null || !light.enabled)
                    continue;

                _timeLeft[i] -= deltaTime;
                if (_timeLeft[i] <= 0f)
                {
                    light.enabled = false;
                    continue;
                }

                var k = _timeLeft[i] / _duration[i];
                light.intensity = _intensity[i] * k * k;
                active++;
            }

            _active = active;
        }

        public void Clear()
        {
            for (var i = 0; i < _count; i++)
            {
                if (_lights[i] != null)
                    _lights[i].enabled = false;
                _timeLeft[i] = 0f;
            }

            _active = 0;
        }

        private int Acquire()
        {
            var best = -1;
            var bestTime = float.MaxValue;
            for (var i = 0; i < _count; i++)
            {
                if (_lights[i] == null)
                    return Create(i) ? i : -1;

                if (!_lights[i].enabled)
                    return i;

                if (_timeLeft[i] < bestTime)
                {
                    bestTime = _timeLeft[i];
                    best = i;
                }
            }

            if (_count < Capacity)
            {
                var index = _count;
                if (!Create(index))
                    return best;
                _count++;
                return index;
            }

            return best;
        }

        private bool Create(int index)
        {
            if (_container == null)
                return false;

            var go = new GameObject(_name);
            go.layer = GameLayers.Default;
            go.transform.SetParent(_container, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            light.bounceIntensity = 0f;
            light.enabled = false;
            _lights[index] = light;
            _transforms[index] = go.transform;
            _timeLeft[index] = 0f;
            return true;
        }
    }
}
