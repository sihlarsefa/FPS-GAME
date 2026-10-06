using System;
using System.Text;
using Project.Infrastructure.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Otomatik kalite: ilk açılışta menüde 5 sn kare ölçümü + SystemInfo ile kademe seçer (bir kez yazar, kullanıcı değiştirebilir);
    /// çalışma anında 5 sn ortalama &gt; 20 ms ise render ölçeğini bir çentik düşürür (bir kez bildirir), boşluk dönünce geri alır;
    /// kare bütçesi bekçisi en çok zorlayan kategorileri günlüğe yazar.
    /// </summary>
    public sealed class AutoQuality : MonoBehaviour
    {
        public const string PrefDone = "harekat.autoquality.done";
        public const string PrefQuality = "harekat.settings.quality";
        public const string PrefAutoScale = "harekat.autoquality.dynscale";

        /// <summary>Kullanıcıya gösterilecek tek seferlik bildirim (UI dinler).</summary>
        public static event Action<string> Notified;

        private PerfSampler _sampler;
        private float _probeTime;
        private float _windowTime;
        private double _windowSum;
        private int _windowCount;
        private bool _probing;
        private int _notch;
        private int _lastTier = -1;
        private float _lastDynNotice = -1f;
        private AutoQualityRules.SlowTracker _slow = AutoQualityRules.SlowTracker.New();
        private float _nextP95Check;
        private static readonly System.Collections.Generic.List<string> Decisions = new System.Collections.Generic.List<string>();

        /// <summary>Benchmark CSV'sine eklenecek karar satırları (başlıksız).</summary>
        public static string[] DecisionLines() => Decisions.ToArray();
        private static void Record(string line) { if (Decisions.Count < 200) Decisions.Add(line); }
        private bool _dynEnabled = true;
        private float _nextWatchdog;

        /// <summary>Otomatik ekran görüntüsü çekimlerinde (-otoekran*) dinamik ölçek kilitli: kareler tam çözünürlükte kalır.</summary>
        public static readonly bool CaptureLock = DetectCapture();

        private static bool DetectCapture()
        {
            try
            {
                foreach (var a in Environment.GetCommandLineArgs())
                {
                    if (a.StartsWith("-otoekran", StringComparison.Ordinal))
                        return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>Lobide performans bildirimi gösterilmez (vitrin ekranı temiz kalır).</summary>
        private static bool InMenu()
        {
            try { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == MemoryJanitorRules.MenuScene; }
            catch (Exception) { return false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<AutoQuality>() != null) return;
            var go = new GameObject("AutoQuality");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoQuality>();
        }

        private void Awake()
        {
            _sampler = new PerfSampler(600);
            try
            {
                _probing = PlayerPrefs.GetInt(PrefDone, 0) == 0;
                _dynEnabled = PlayerPrefs.GetInt(PrefAutoScale, 1) != 0;
            }
            catch (Exception) { _probing = false; }
        }

        private void OnDestroy() => _sampler?.Dispose();

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _sampler.SampleFrame();

            if (_probing)
            {
                _probeTime += dt;
                if (_probeTime > 1f) { _windowSum += dt * 1000f; _windowCount++; } // ilk saniye ısınma
                if (_probeTime >= AutoQualityRules.ProbeSeconds + 1f)
                    FinishProbe();
                return;
            }

            DynamicScaleTick(dt);
            P95Tick();
            WatchdogTick();
        }

        private void FinishProbe()
        {
            _probing = false;
            var avg = _windowCount > 0 ? (float)(_windowSum / _windowCount) : 0f;
            _windowSum = 0; _windowCount = 0;
            int vram = 0, cores = 4;
            string gpu = "";
            try { vram = SystemInfo.graphicsMemorySize; cores = SystemInfo.processorCount; gpu = SystemInfo.graphicsDeviceName; } catch (Exception) { }
            var tier = AutoQualityRules.ChooseTier(gpu, vram, cores, avg);
            var firstWrite = false;
            try
            {
                // Kullanıcı daha önce kendi seçtiyse (anahtar var) dokunma; yoksa bir kez yaz.
                if (!PlayerPrefs.HasKey(PrefQuality))
                {
                    PlayerPrefs.SetInt(PrefQuality, tier);
                    firstWrite = true;
                }
                PlayerPrefs.SetInt(PrefDone, 1);
                PlayerPrefs.Save();
                tier = PlayerPrefs.GetInt(PrefQuality, tier);
            }
            catch (Exception) { }
            Record(AutoQualityRules.DecisionCsvLine("ilk-aciliş", tier, 0, 0f, avg));
            if (firstWrite) Notified?.Invoke(AutoQualityRules.FirstRunMessage(tier));
            Debug.Log("[AutoQuality] GPU=" + gpu + " VRAM=" + vram + "MB çekirdek=" + cores + " ort=" + avg.ToString("F1") + "ms -> kademe " + tier);
            try { PostProcessing.ApplyQuality(tier); } catch (Exception e) { Debug.LogWarning("[AutoQuality] kademe uygulanamadı: " + e.Message); }
        }

        private void DynamicScaleTick(float dt)
        {
            if (!_dynEnabled || CaptureLock) return;
            var tier = QualityTierApplier.LastTier;
            if (tier < 0) return;
            if (tier != _lastTier) { _lastTier = tier; _notch = 0; ResetWindow(); return; }

            // Yükleme/duraklama kareleri ölçümü bozmasın.
            if (dt > 0.25f) return;
            _windowTime += dt; _windowSum += dt * 1000f; _windowCount++;
            if (_windowTime < AutoQualityRules.WindowSeconds) return;

            var avg = (float)(_windowSum / Math.Max(1, _windowCount));
            ResetWindow();
            var next = AutoQualityRules.NextNotch(_notch, avg);
            if (next == _notch) return;
            var old = _notch;
            _notch = next;
            ApplyScale(tier);
            Record(AutoQualityRules.DecisionCsvLine(next > old ? "olcek-dus" : "olcek-geri", tier, next,
                AutoQualityRules.ScaleFor(PipelineTiers.Get(tier).RenderScale, next), avg));
            var now = Time.unscaledTime;
            if (!InMenu() && AutoQualityRules.ShouldNotifyDynScale(old, next, now, _lastDynNotice))
            {
                _lastDynNotice = now;
                Notified?.Invoke("Performans için çözünürlük ölçeği otomatik düşürüldü");
            }
        }

        private void P95Tick()
        {
            var now = Time.unscaledTime;
            if (now < _nextP95Check) return;
            _nextP95Check = now + 5f;
            var tier = QualityTierApplier.LastTier;
            if (tier < 0) return;
            PerfSnapshot snap;
            try { snap = PerfProbe.Snapshot(); } catch (Exception) { return; }
            if (!snap.Valid) return;
            if (_slow.Update(now, snap.FrameCpuMs.P95, snap.Samples, tier))
            {
                Record(AutoQualityRules.DecisionCsvLine("oneri-kademe", tier, _notch, 0f, snap.FrameCpuMs.P95));
                if (!InMenu() && !CaptureLock)
                    Notified?.Invoke(AutoQualityRules.SuggestMessage);
            }
        }

        private void ResetWindow() { _windowTime = 0; _windowSum = 0; _windowCount = 0; }

        private void ApplyScale(int tier)
        {
            var asset = RenderPipelineInfo.UrpAsset;
            if (asset == null) return;
            var scale = AutoQualityRules.ScaleFor(PipelineTiers.Get(tier).RenderScale, _notch);
            try { asset.renderScale = scale; }
            catch (Exception e) { Debug.LogWarning("[AutoQuality] renderScale yazılamadı: " + e.Message); }
        }

        private void WatchdogTick()
        {
            if (Time.unscaledTime < _nextWatchdog || _sampler.SampleCount < 120) return;
            var avg = _sampler.AverageFrameMs();
            if (avg <= 33f) return;
            _nextWatchdog = Time.unscaledTime + 15f;
            var o = AutoQualityRules.RankOffenders(_sampler.DrawCallsLast, _sampler.BatchesLast, _sampler.GcAllocBytesLast, avg);
            var sb = new StringBuilder("[AutoQuality] Kare bütçesi aşıldı ort=").Append(avg.ToString("F1")).Append("ms %1low=")
                .Append(_sampler.OnePercentLowMs().ToString("F1")).Append("ms DC=").Append(_sampler.DrawCallsLast)
                .Append(" batch=").Append(_sampler.BatchesLast).Append(" GC=").Append(_sampler.GcAllocBytesLast).Append("B suçlular:");
            for (var i = 0; i < o.Length; i++) sb.Append(' ').Append(o[i]);
            Debug.LogWarning(sb.ToString());
        }
    }
}
