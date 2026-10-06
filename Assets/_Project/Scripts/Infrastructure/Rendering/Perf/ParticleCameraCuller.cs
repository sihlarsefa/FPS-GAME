using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Rendering.Perf
{
    /// <summary>Kameranın arkasındaki particle sistemlerini durdurur, öne gelince sürdürür (yarım saniyede bir; kare başına maliyet yok).</summary>
    public sealed class ParticleCameraCuller : MonoBehaviour
    {
        public float SafeRadius = 6f;
        public float Interval = 0.5f;
        private readonly List<ParticleSystem> _systems = new List<ParticleSystem>();
        private readonly HashSet<ParticleSystem> _paused = new HashSet<ParticleSystem>();
        private float _next, _nextRefresh;

        private void Update()
        {
            float t = Time.unscaledTime;
            if (t < _next) return;
            _next = t + Interval;
            var cam = Camera.main;
            if (cam == null) return;
            if (t >= _nextRefresh)
            {
                _nextRefresh = t + 5f;
                _systems.Clear();
                _systems.AddRange(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None));
            }
            var ct = cam.transform;
            for (int i = _systems.Count - 1; i >= 0; i--)
            {
                var ps = _systems[i];
                if (ps == null) { _systems.RemoveAt(i); continue; }
                bool behind = PerfMath.ShouldPauseParticle(ct.position, ct.forward, ps.transform.position, SafeRadius);
                if (behind && ps.isPlaying && !_paused.Contains(ps)) { ps.Pause(true); _paused.Add(ps); }
                else if (!behind && _paused.Remove(ps)) ps.Play(true);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (UnityEngine.Application.isBatchMode || FindAnyObjectByType<ParticleCameraCuller>() != null) return;
            var go = new GameObject("[ParticleCameraCuller]");
            DontDestroyOnLoad(go);
            go.AddComponent<ParticleCameraCuller>();
        }
    }
}
