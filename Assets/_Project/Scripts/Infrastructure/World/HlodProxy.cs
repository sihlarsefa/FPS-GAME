using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// S6: Yerleşim HLOD proxy'si. Uzaktayken kaynak renderer'lar kapanır, birleşik proxy açılır; histerezisli.
    /// Çarpıştırıcılara dokunulmaz (yalnız Renderer.enabled). Proxy/kaynak yoksa sessizce hiçbir şey yapmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HlodProxy : MonoBehaviour
    {
        private const float TickInterval = 0.3f;

        private static readonly List<HlodProxy> Active = new List<HlodProxy>();
        private static int _tier = 1;
        private static bool _globalEnabled = true;

        [SerializeField] private Renderer[] sources = new Renderer[0];
        [SerializeField] private Renderer[] proxies = new Renderer[0];
        [SerializeField] private float swapDistanceOverride;   // 0 = kademeden

        private bool _proxyOn;
        private bool _applied;
        private float _nextTick;
        private Vector3 _center;

        public bool IsProxyActive => _proxyOn;

        /// <summary>Entegrasyon: kalite kademesi değişince çağrılır (RenderSettingsUtil / PostProcessing).</summary>
        public static void SetTier(int tier)
        {
            _tier = HlodMath.ClampTier(tier);
            for (var i = 0; i < Active.Count; i++)
                if (Active[i] != null) Active[i]._nextTick = 0f;
        }

        public static void SetGlobalEnabled(bool on)
        {
            _globalEnabled = on;
            for (var i = 0; i < Active.Count; i++)
                if (Active[i] != null) Active[i]._nextTick = 0f;
        }

        public void Configure(Renderer[] sourceRenderers, Renderer[] proxyRenderers, Vector3 worldCenter)
        {
            sources = sourceRenderers ?? new Renderer[0];
            proxies = proxyRenderers ?? new Renderer[0];
            _center = worldCenter;
        }

        private void OnEnable()
        {
            if (_center == Vector3.zero) _center = transform.position;
            Active.Add(this);
            _nextTick = Time.time + Random.value * TickInterval;   // kareler arası dağıt
            Apply(false, true);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            Apply(false, true);   // kapanırken kaynağı geri aç
        }

        private void Update()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickInterval;
            var cam = Camera.main;
            if (cam == null) return;

            if (!_globalEnabled || proxies.Length == 0 || sources.Length == 0)
            {
                Apply(false, false);
                return;
            }
            var swap = swapDistanceOverride > 0f ? swapDistanceOverride : HlodMath.SwapDistance(_tier);
            var d = Vector3.Distance(cam.transform.position, _center);
            Apply(HlodMath.ShouldUseProxy(_proxyOn, d, swap), false);
        }

        private void Apply(bool proxyOn, bool force)
        {
            if (_applied && !force && proxyOn == _proxyOn) return;
            _applied = true;
            _proxyOn = proxyOn;
            for (var i = 0; i < sources.Length; i++)
                if (sources[i] != null) sources[i].enabled = !proxyOn;
            for (var i = 0; i < proxies.Length; i++)
                if (proxies[i] != null) proxies[i].enabled = proxyOn;
        }
    }
}
