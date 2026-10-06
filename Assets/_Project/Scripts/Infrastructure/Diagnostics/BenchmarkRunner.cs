using System;
using System.Collections.Generic;
using System.IO;
using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Infrastructure.Diagnostics
{
    /// <summary>
    /// <c>-benchmark [saniye]</c>: Kuzgun Köyü/Vadisi üzerinde sabit kamera güzergâhı uçurur,
    /// CSV'yi persistentDataPath/Benchmarks altına yazar ve (-batchmode ya da -benchmarkquit) çıkar.
    /// İsteğe bağlı: -benchmarktier &lt;ad&gt;.
    /// </summary>
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        public const float DefaultDuration = 60f;
        public const string TargetScene = "KuzgunVadisi";
        private const float WarmupSeconds = 3f;

        private static int _bootstrapped;
        private readonly List<float> _frames = new List<float>(8192);
        private float _elapsed;
        private float _duration = DefaultDuration;
        private float _warm;
        private bool _finished;
        private Camera _cam;
        private string _tier = "orta";

        public static bool IsRequested => DiagnosticsCommandLine.HasArg("-benchmark");
        public static string OutputDirectory => Path.Combine(UnityEngine.Application.persistentDataPath, "Benchmarks");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_bootstrapped != 0 || !IsRequested) return;
            _bootstrapped = 1;
            var go = new GameObject("[Benchmark]");
            DontDestroyOnLoad(go);
            go.AddComponent<BenchmarkRunner>();
        }

        private void Awake()
        {
            if (DiagnosticsCommandLine.TryGetFloat("-benchmark", out var d) && d >= 5f)
                _duration = Mathf.Clamp(d, 5f, 1800f);
            if (DiagnosticsCommandLine.TryGetArg("-benchmarktier", out var t) && !string.IsNullOrWhiteSpace(t))
                _tier = t.Trim();

            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !string.Equals(active.name, TargetScene, StringComparison.OrdinalIgnoreCase))
            {
                try { SceneManager.LoadScene(TargetScene, LoadSceneMode.Single); }
                catch (Exception e) { Debug.LogWarning("[Benchmark] Sahne yüklenemedi, mevcut sahnede devam: " + e.Message); }
            }
            Debug.Log($"[Benchmark] Başlıyor: {_duration:0}s, kademe={_tier}");
        }

        private void Update()
        {
            if (_finished) return;
            EnsureCamera();
            _warm += Time.unscaledDeltaTime;
            if (_warm < WarmupSeconds) { Fly(0f); return; }

            _frames.Add(Time.unscaledDeltaTime * 1000f);
            _elapsed += Time.unscaledDeltaTime;
            Fly(_elapsed / _duration);
            if (_elapsed >= _duration) Finish();
        }

        private void EnsureCamera()
        {
            if (_cam != null) return;
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("[BenchmarkCamera]");
                _cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
        }

        private void Fly(float t)
        {
            if (_cam == null) return;
            var meta = WorldMetadata.Instance;
            var center = meta != null ? meta.MapCenter : Vector2.zero;
            var half = meta != null ? meta.MapHalfSize : 512f;
            BenchmarkStats.EvaluatePath(t, center.x, center.y, half, out var px, out var pz, out var lx, out var lz);
            var pos = new Vector3(px, 0f, pz);
            var look = new Vector3(lx, 0f, lz);
            pos.y = (meta != null ? meta.SampleGroundHeight(pos) : 0f) + 35f;
            look.y = (meta != null ? meta.SampleGroundHeight(look) : 0f) + 5f;
            _cam.transform.position = pos;
            _cam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        }

        private void Finish()
        {
            _finished = true;
            var s = BenchmarkStats.Summarize(_frames);
            var res = Screen.width + "x" + Screen.height;
            var csv = BenchmarkStats.ToCsv("kuzgun", _tier, res, _elapsed, s);
            try
            {
                var dir = OutputDirectory;
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "benchmark_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                File.WriteAllText(path, csv);
                Debug.Log($"[Benchmark] Bitti: ort {s.AvgFps:0.0} FPS, %1 low {s.OnePercentLowFps:0.0}, en kötü {s.WorstMs:0.0} ms -> {path}");
            }
            catch (Exception e)
            {
                Debug.LogError("[Benchmark] CSV yazılamadı: " + e.Message);
            }

            if (UnityEngine.Application.isBatchMode || DiagnosticsCommandLine.HasArg("-benchmarkquit"))
                UnityEngine.Application.Quit();
        }
    }
}
