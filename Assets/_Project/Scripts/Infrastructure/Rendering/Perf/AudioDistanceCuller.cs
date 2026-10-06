using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Rendering.Perf
{
    /// <summary>Dinleyiciden uzak 3B ses kaynaklarını devre dışı bırakır, yaklaşınca açar (histerezisli).</summary>
    public sealed class AudioDistanceCuller : MonoBehaviour
    {
        public float Interval = 0.5f;
        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private readonly HashSet<AudioSource> _off = new HashSet<AudioSource>();
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
                _sources.Clear();
                _sources.AddRange(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None));
            }
            int tier = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, PipelineTiers.Count - 1);
            Vector3 p = cam.transform.position;
            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                var s = _sources[i];
                if (s == null) { _sources.RemoveAt(i); _off.Remove(s); continue; }
                if (s.spatialBlend < 0.5f) continue; // 2B (müzik/UI) asla kısılmaz
                bool disabled = _off.Contains(s);
                float dist = Vector3.Distance(p, s.transform.position);
                bool want = PerfMath.ShouldDisableAudio(dist, PerfMath.AudioCullDistance(tier, s.maxDistance), disabled);
                if (want && !disabled && s.enabled) { s.enabled = false; _off.Add(s); }
                else if (!want && disabled) { s.enabled = true; _off.Remove(s); }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (UnityEngine.Application.isBatchMode || FindAnyObjectByType<AudioDistanceCuller>() != null) return;
            var go = new GameObject("[AudioDistanceCuller]");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioDistanceCuller>();
        }
    }
}
