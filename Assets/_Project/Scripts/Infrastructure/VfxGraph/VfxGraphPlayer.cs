using UnityEngine;
using Project.Infrastructure.Vfx;
#if HAREKAT_VFXGRAPH
using System.Collections.Generic;
using UnityEngine.VFX;
#endif

namespace Project.Infrastructure.VfxGraph
{
#if HAREKAT_VFXGRAPH
    /// <summary>
    /// Resources/VFX/VFX_&lt;Efekt&gt;.vfx kaynaklarını halka havuzla oynatır. Kaynak yoksa Has=false döner,
    /// çağıran CPU parçacık yoluna düşer. Dışa açık özellik adları Docs/VFX_GRAPH_SPEC.md'de.
    /// </summary>
    public sealed class VfxGraphPlayer : IGpuVfxBackend
    {
        private sealed class Pool
        {
            public VisualEffectAsset Asset;
            public VisualEffect[] Items;
            public int Next;
        }

        private readonly Dictionary<GpuVfxEffect, Pool> _pools = new Dictionary<GpuVfxEffect, Pool>();
        private readonly HashSet<GpuVfxEffect> _missing = new HashSet<GpuVfxEffect>();
        private readonly Dictionary<GpuVfxEffect, VisualEffect> _weather = new Dictionary<GpuVfxEffect, VisualEffect>();
        private Transform _root;
        private int _tier = 2;
        private bool _warned;

        public bool IsAvailable => SystemInfo.supportsComputeShaders;

        public void SetTier(int tier) { _tier = Mathf.Clamp(tier, 0, 3); }

        public bool Has(GpuVfxEffect effect)
        {
            if (_missing.Contains(effect)) return false;
            if (_pools.ContainsKey(effect)) return true;
            var asset = Resources.Load<VisualEffectAsset>("VFX/" + GpuVfxBudget.ResourceName(effect));
            if (asset == null)
            {
                _missing.Add(effect);
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[VfxGraph] Resources/VFX altında .vfx bulunamadı; CPU parçacık yolu kullanılacak (bkz. Docs/VFX_GRAPH_SPEC.md).");
                }
                return false;
            }
            _pools[effect] = new Pool { Asset = asset };
            return true;
        }

        public bool TryPlay(GpuVfxEffect effect, Vector3 position, Vector3 direction, float scale, int surface, float duration)
        {
            int max = GpuVfxBudget.MaxInstances(effect, _tier);
            if (max <= 0 || !Has(effect)) return false;
            var pool = _pools[effect];
            if (pool.Items == null || pool.Items.Length != max)
            {
                if (pool.Items != null) foreach (var o in pool.Items) if (o != null) Object.Destroy(o.gameObject);
                pool.Items = new VisualEffect[max];
                pool.Next = 0;
            }

            var ve = pool.Items[pool.Next];
            pool.Next = (pool.Next + 1) % max;
            if (ve == null)
            {
                var go = new GameObject("VFX_" + effect);
                go.transform.SetParent(Root(), false);
                ve = go.AddComponent<VisualEffect>();
                ve.visualEffectAsset = pool.Asset;
                pool.Items[(pool.Next + max - 1) % max] = ve;
            }

            var t = ve.transform;
            t.position = position;
            t.rotation = direction.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(direction.normalized) : Quaternion.identity;
            ve.Stop();
            Set(ve, "Scale", scale);
            Set(ve, "Direction", direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.up);
            Set(ve, "Surface", surface);
            Set(ve, "Tier", _tier);
            Set(ve, "Capacity", GpuVfxBudget.Capacity(effect, _tier));
            if (duration > 0f) Set(ve, "Duration", duration);
            if (effect == GpuVfxEffect.Explosion || effect == GpuVfxEffect.SmokeGrenade || effect == GpuVfxEffect.RotorDust)
                Set(ve, "Radius", scale);
            ve.Reinit();
            ve.SendEvent("OnPlay");
            return true;
        }

        public void SetWeather(GpuVfxEffect effect, Transform follow, float intensity)
        {
            if (effect != GpuVfxEffect.Rain && effect != GpuVfxEffect.Snow) return;
            _weather.TryGetValue(effect, out var ve);
            if (intensity <= 0.001f || GpuVfxBudget.MaxInstances(effect, _tier) <= 0 || !Has(effect))
            {
                if (ve != null) ve.Stop();
                return;
            }
            if (ve == null)
            {
                var go = new GameObject("VFX_" + effect);
                go.transform.SetParent(Root(), false);
                ve = go.AddComponent<VisualEffect>();
                ve.visualEffectAsset = _pools[effect].Asset;
                _weather[effect] = ve;
            }
            if (follow != null) ve.transform.SetPositionAndRotation(follow.position, Quaternion.identity);
            Set(ve, "Intensity", Mathf.Clamp01(intensity));
            Set(ve, "Capacity", GpuVfxBudget.Capacity(effect, _tier));
            if (!ve.culled) ve.Play();
        }

        private Transform Root()
        {
            if (_root == null)
            {
                var go = new GameObject("[VfxGraph]");
                Object.DontDestroyOnLoad(go);
                _root = go.transform;
            }
            return _root;
        }

        private static void Set(VisualEffect ve, string name, float v) { if (ve.HasFloat(name)) ve.SetFloat(name, v); }
        private static void Set(VisualEffect ve, string name, int v) { if (ve.HasInt(name)) ve.SetInt(name, v); }
        private static void Set(VisualEffect ve, string name, Vector3 v) { if (ve.HasVector3(name)) ve.SetVector3(name, v); }
    }
#endif

    /// <summary>Oynatıcıyı GpuVfx.Backend'e kaydeder. HAREKAT_VFXGRAPH yoksa hiçbir şey yapmaz.</summary>
    public static class VfxGraphBootstrap
    {
#if HAREKAT_VFXGRAPH
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (GpuVfx.Backend != null) return;
            var player = new VfxGraphPlayer();
            player.SetTier(GpuVfx.Tier);
            GpuVfx.Backend = player;
        }
#endif
    }
}
