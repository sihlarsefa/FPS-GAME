using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Sis hacmi ışık saçılımı sahtesi: aktif sis bulutları kaydedilir; yakınlarında namlu alevi/patlama olunca bulutun
    /// içinde kısa süreli sıcak katkılı bir parıltı (billboard) ve düşük şiddetli bir nokta ışık yanar. Sabit havuz, ayırma yok.
    /// </summary>
    internal sealed class SmokeGlowPool
    {
        private const int MaxClouds = 6;
        private const int MaxGlows = 3;
        private const float GlowDuration = 0.22f;
        private const float MinGapPerCloud = 0.1f;

        private static readonly Color GlowColor = new Color(1f, 0.62f, 0.3f, 1f);

        private readonly Transform _container;
        private readonly Vector3[] _cloudPos = new Vector3[MaxClouds];
        private readonly float[] _cloudRadius = new float[MaxClouds];
        private readonly float[] _cloudExpire = new float[MaxClouds];
        private readonly float[] _cloudNext = new float[MaxClouds];
        private int _cloudCount;

        private readonly MeshRenderer[] _quads = new MeshRenderer[MaxGlows];
        private readonly Transform[] _quadTransforms = new Transform[MaxGlows];
        private readonly Light[] _lights = new Light[MaxGlows];
        private readonly float[] _left = new float[MaxGlows];
        private readonly float[] _strength = new float[MaxGlows];
        private readonly float[] _size = new float[MaxGlows];
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Mesh _mesh;
        private int _active;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public SmokeGlowPool(Transform container)
        {
            _container = container;
        }

        public int CloudCount => _cloudCount;

        public int ActiveCount => _active;

        public void RegisterCloud(Vector3 position, float radius, float duration, float now)
        {
            if (radius <= 0f || duration <= 0f)
                return;

            var slot = -1;
            for (var i = 0; i < _cloudCount; i++)
            {
                if (_cloudExpire[i] <= now)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0)
            {
                if (_cloudCount < MaxClouds)
                {
                    slot = _cloudCount++;
                }
                else
                {
                    slot = 0;
                    for (var i = 1; i < _cloudCount; i++)
                        if (_cloudExpire[i] < _cloudExpire[slot])
                            slot = i;
                }
            }

            _cloudPos[slot] = position + Vector3.up * (radius * 0.35f);
            _cloudRadius[slot] = radius;
            _cloudExpire[slot] = now + duration;
            _cloudNext[slot] = 0f;
        }

        /// <summary>Bir parlama (namlu/patlama) oldu: yakındaki sis bulutlarını parlat.</summary>
        public void NotifyFlash(Vector3 flashPosition, float strength01, float night, float now)
        {
            for (var i = 0; i < _cloudCount; i++)
            {
                if (_cloudExpire[i] <= now || now < _cloudNext[i])
                    continue;

                var distance = Vector3.Distance(flashPosition, _cloudPos[i]);
                var glow = VfxNightRules.SmokeGlow(distance, _cloudRadius[i], strength01, night);
                if (glow < 0.04f)
                    continue;

                _cloudNext[i] = now + MinGapPerCloud;
                Show(_cloudPos[i], _cloudRadius[i], glow);
            }
        }

        public void Tick(float deltaTime, bool hasCamera, Vector3 cameraPosition, float now)
        {
            if (_active == 0)
                return;

            var active = 0;
            for (var i = 0; i < MaxGlows; i++)
            {
                if (_left[i] <= 0f || _quads[i] == null)
                    continue;

                _left[i] -= deltaTime;
                if (_left[i] <= 0f)
                {
                    _quads[i].enabled = false;
                    if (_lights[i] != null)
                        _lights[i].enabled = false;
                    continue;
                }

                var k = _left[i] / GlowDuration;
                var strength = _strength[i] * k * k;
                if (hasCamera)
                {
                    var toCamera = cameraPosition - _quadTransforms[i].position;
                    if (toCamera.sqrMagnitude > 1e-4f)
                        _quadTransforms[i].rotation = Quaternion.LookRotation(-toCamera.normalized);
                }

                var c = GlowColor * (strength * 0.9f);
                c.a = strength;
                _block.SetColor(BaseColorId, c);
                _block.SetColor(ColorId, c);
                _quads[i].SetPropertyBlock(_block);
                if (_lights[i] != null)
                    _lights[i].intensity = 2.5f * strength;
                active++;
            }

            _active = active;
        }

        public void Clear()
        {
            for (var i = 0; i < MaxGlows; i++)
            {
                _left[i] = 0f;
                if (_quads[i] != null)
                    _quads[i].enabled = false;
                if (_lights[i] != null)
                    _lights[i].enabled = false;
            }

            _active = 0;
            _cloudCount = 0;
        }

        private void Show(Vector3 position, float radius, float strength)
        {
            var index = -1;
            var weakest = float.MaxValue;
            for (var i = 0; i < MaxGlows; i++)
            {
                if (_left[i] <= 0f)
                {
                    index = i;
                    break;
                }

                if (_left[i] < weakest)
                {
                    weakest = _left[i];
                    index = i;
                }
            }

            if (index < 0 || !EnsureSlot(index))
                return;

            if (_left[index] <= 0f)
                _active++;
            _left[index] = GlowDuration;
            _strength[index] = strength;
            _size[index] = radius * 1.6f;
            var t = _quadTransforms[index];
            t.position = position;
            t.localScale = new Vector3(_size[index], _size[index], 1f);
            _quads[index].enabled = true;
            var light = _lights[index];
            if (light != null)
            {
                light.transform.position = position;
                light.range = radius * 1.4f + 3f;
                light.color = GlowColor;
                light.intensity = 2.5f * strength;
                light.enabled = true;
            }
        }

        private bool EnsureSlot(int index)
        {
            if (_quads[index] != null)
                return true;
            if (_container == null)
                return false;

            var material = VfxMaterials.AdditiveGlow;
            if (material == null)
                return false;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "VFX_SmokeGlowQuad", hideFlags = HideFlags.DontSave };
                _mesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                };
                _mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                _mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                _mesh.RecalculateBounds();
            }

            var go = new GameObject("SmokeGlow");
            go.layer = GameLayers.Default;
            go.transform.SetParent(_container, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;

            var lightObject = new GameObject("SmokeGlowLight");
            lightObject.layer = GameLayers.Default;
            lightObject.transform.SetParent(_container, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.bounceIntensity = 0f;
            light.enabled = false;

            _quads[index] = renderer;
            _quadTransforms[index] = go.transform;
            _lights[index] = light;
            return true;
        }
    }
}
