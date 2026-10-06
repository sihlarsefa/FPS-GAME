using System;
using Unity.Profiling;
using UnityEngine;

namespace Project.Infrastructure.Rendering.Perf
{
    /// <summary>
    /// Kare süresi + GC ayırma izleme, kare zamanlama önerisi ve toplu Tick sürücüsü. Tek bir DontDestroyOnLoad
    /// nesnesi; kendiliğinden kurulur. Ek yük: kare başı birkaç aritmetik; istatistik saniyede 1 hesaplanır.
    /// Diğer sistemler: PerfMonitor.BudgetScale ile yoğunluklarını çarpar, PerfMonitor.Ticks ile Update'ini devreder.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class PerfMonitor : MonoBehaviour
    {
        private const float EvalInterval = 1f;
        private const float TickBudgetMs = 1.0f;

        private static PerfMonitor _instance;
        private readonly FrameTimeTracker _frames = new FrameTimeTracker(240);
        private readonly AllocationTracker _allocs = new AllocationTracker(300);
        private readonly FramePacingAdvisor _advisor = new FramePacingAdvisor();
        private readonly TickScheduler _ticks = new TickScheduler(ex => Debug.LogException(ex));
        private ProfilerRecorder _gcAlloc;
        private bool _recorderOk;
        private float _evalTimer;
        private float _quietLogTimer;
        private int _lastGcCount = -1;
        private FrameStats _lastFrameStats;
        private AllocationStats _lastAllocStats;

        /// <summary>Hedef FPS; kalite/ayar katmanı değiştirebilir.</summary>
        public static int TargetFps { get; set; } = 60;

        /// <summary>0.4..1 yumuşak yük bütçesi; kalabalık VFX/ses/dekal sayısını bununla çarp.</summary>
        public static float BudgetScale => _instance != null ? _instance._advisor.BudgetScale : 1f;
        public static FrameStats Frame => _instance != null ? _instance._lastFrameStats : default;
        public static AllocationStats Alloc => _instance != null ? _instance._lastAllocStats : default;
        public static TickScheduler Ticks => Ensure()._ticks;

        /// <summary>Yük azaltma/geri verme önerisi değiştiğinde bildirim.</summary>
        public static event Action<PacingAdvice, float> AdviceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { Ensure(); }

        public static PerfMonitor Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[PerfMonitor]") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PerfMonitor>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            try
            {
                _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                _recorderOk = _gcAlloc.Valid;
            }
            catch (Exception) { _recorderOk = false; }
            _lastGcCount = GC.CollectionCount(0);
            GcTuning.Apply(TargetFps, false);
        }

        private void OnDestroy()
        {
            if (_gcAlloc.Valid) _gcAlloc.Dispose();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _frames.Push(dt);

            var gc = GC.CollectionCount(0);
            var gcDelta = _lastGcCount >= 0 ? gc - _lastGcCount : 0;
            _lastGcCount = gc;
            if (_recorderOk) _allocs.PushFrame(_gcAlloc.LastValue, gcDelta, dt);
            else _allocs.PushTotals(GC.GetTotalMemory(false), gc, dt);

            _ticks.Run(Time.unscaledTimeAsDouble, TickBudgetMs);

            _evalTimer += dt;
            if (_evalTimer >= EvalInterval) { _evalTimer = 0f; Evaluate(); }
        }

        private void Evaluate()
        {
            _lastFrameStats = _frames.Compute();
            _lastAllocStats = _allocs.Compute();
            var hot = AllocationTracker.IsHot(_lastAllocStats);
            var advice = _advisor.Evaluate(_lastFrameStats, TargetFps);
            if (advice != PacingAdvice.Hold)
            {
                Debug.Log("[Perf] " + (advice == PacingAdvice.ReduceLoad ? "Yük azaltma önerisi" : "Yük geri verme önerisi")
                    + " | p95 " + _lastFrameStats.P95Ms.ToString("F1") + " ms, bütçe x" + _advisor.BudgetScale.ToString("F2"));
                AdviceChanged?.Invoke(advice, _advisor.BudgetScale);
            }
            GcTuning.Apply(TargetFps, hot);

            // Sıcak ayırma varsa 30 sn'de bir uyar (log çöpünü sınırlı tut).
            _quietLogTimer += EvalInterval;
            if (hot && _quietLogTimer >= 30f)
            {
                _quietLogTimer = 0f;
                Debug.LogWarning("[Perf] Sürekli GC ayırması: ortalama " + _lastAllocStats.AvgBytesPerFrame.ToString("F0")
                    + " B/kare, tepe " + _lastAllocStats.PeakBytesInFrame + " B, toplama " + _lastAllocStats.Collections);
            }
        }
    }
}
