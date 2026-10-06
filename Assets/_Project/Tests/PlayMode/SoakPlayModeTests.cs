using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.AI;
using Project.Infrastructure.Diagnostics;
using Project.Presentation.Bootstrap;
using Project.Presentation.Spectator;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.PlayMode
{
    /// <summary>
    /// Uzun dayanıklılık (soak) koşuları — varsayılan PlayMode paketinden hariç (kategori Soak).
    /// <c>zsh Tools/UnityVerify/run_soak.sh</c> ile çalıştır.
    /// </summary>
    [Category("Soak")]
    public sealed class SoakPlayModeTests
    {
        [UnityTest]
        [Timeout(1_200_000)]
        public IEnumerator KuzgunVadisi_BotMatch_Spectator_UntilEnd()
        {
            yield return RunBotMatchSoak(SceneNames.Operation, "kuzgun", MaxRealtime("HAREKAT_SOAK_SECONDS", 1200f));
        }

        [UnityTest]
        [Timeout(600_000)]
        public IEnumerator AyazGecidi_Soak_FiveMinutes()
        {
            yield return RunTimedMapSoak(SceneNames.AyazGecidi, "ayaz", MaxRealtime("HAREKAT_SOAK_SHORT_SECONDS", 300f));
        }

        [UnityTest]
        [Timeout(600_000)]
        public IEnumerator MaviLiman_Soak_FiveMinutes()
        {
            yield return RunTimedMapSoak(SceneNames.MaviLiman, "liman", MaxRealtime("HAREKAT_SOAK_SHORT_SECONDS", 300f));
        }

        [UnityTest]
        [Timeout(600_000)]
        public IEnumerator TrainingRange_Soak_FiveMinutes()
        {
            yield return RunTrainingSoak(MaxRealtime("HAREKAT_SOAK_SHORT_SECONDS", 300f));
        }

        [UnityTest]
        [Timeout(600_000)]
        public IEnumerator Skirmish_Soak_FiveMinutes()
        {
            // MatchBootstrap, Mode=Skirmish görünce SkirmishBootstrap'a devreder.
            GameSession.Mode = GameMode.Skirmish;
            yield return RunTimedMapSoak(SceneNames.Skirmish, "skirmish", MaxRealtime("HAREKAT_SOAK_SHORT_SECONDS", 300f));
        }

        private static IEnumerator RunBotMatchSoak(string scene, string tag, float maxRealtime)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dir = Path.Combine(PlayModeHelpers.ScreensDirectory, "..", "soak", stamp + "_" + tag);
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);

            using var log = new SoakLogCapture();
            log.Start();

            var cfg = GameSession.AcquireMatchConfig();
            cfg.WithTeams(4, 10);
            cfg.MatchDurationSeconds = Mathf.Min(cfg.MatchDurationSeconds, 1500f);

            yield return PlayModeHelpers.LoadScene(scene, 120f);
            yield return PlayModeHelpers.WaitUntil(
                () => PlayModeHelpers.FindMatchBootstrap() != null &&
                      PlayModeHelpers.FindMatchBootstrap().IsReady &&
                      PlayModeHelpers.CountAliveBots() >= 30,
                120f,
                "Soak: maç/bot hazır olmadı");

            if (PlayModeHelpers.TryGetMatchService(out var match) && match.CurrentPhase != MatchPhase.InMatch)
                yield return PlayModeHelpers.AccelerateToInMatch(40f);

            // Oyuncuyu yaralı→ölü yap → izleyici; botlar maçı oynasın.
            var player = PlayModeHelpers.FindLocalPlayer();
            if (player?.Combatant != null && player.Combatant.IsAlive)
            {
                player.Combatant.ApplyDamage(new DamageInfo(10_000f, PlayerId.Invalid, "soak"));
                if (player.Combatant.IsAlive)
                    player.Combatant.ApplyDamage(new DamageInfo(10_000f, PlayerId.Invalid, "soak"));
            }

            yield return PlayModeHelpers.WaitUntil(
                () => UnityEngine.Object.FindAnyObjectByType<SpectatorController>() != null ||
                      (player != null && player.Combatant != null && !player.Combatant.IsAlive),
                15f,
                "Soak: izleyici / ölüm beklenirken zaman aşımı");

            var previousScale = Time.timeScale;
            Time.timeScale = 2f;
            var sampler = new PerfSampler(600);
            var botSeries = new List<string>(64);
            var started = Time.realtimeSinceStartup;
            var nextShot = started;
            var nextSample = started;

            try
            {
                while (Time.realtimeSinceStartup - started < maxRealtime)
                {
                    sampler.SampleFrame();

                    if (Time.realtimeSinceStartup >= nextSample)
                    {
                        nextSample = Time.realtimeSinceStartup + 5f;
                        var bots = PlayModeHelpers.CountAliveBots();
                        PlayModeHelpers.TryGetMatchService(out var m);
                        botSeries.Add($"{Time.realtimeSinceStartup - started:F0}s phase={m?.CurrentPhase} bots={bots} fps~{sampler.Fps:F0}");
                    }

                    if (Time.realtimeSinceStartup >= nextShot)
                    {
                        nextShot = Time.realtimeSinceStartup + 30f;
                        var name = $"soak_{tag}_{(int)(Time.realtimeSinceStartup - started)}s";
                        yield return CaptureTo(dir, name);
                    }

                    if (PlayModeHelpers.TryGetMatchService(out var ended) &&
                        ended.CurrentPhase == MatchPhase.Ending)
                        break;

                    yield return null;
                }
            }
            finally
            {
                Time.timeScale = previousScale > 0f ? previousScale : 1f;
                sampler.Dispose();
            }

            WriteReport(dir, tag, log, sampler, botSeries, started);
            log.AssertNoExceptions("Soak " + tag);
        }

        private static IEnumerator RunTimedMapSoak(string scene, string tag, float maxRealtime)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dir = Path.GetFullPath(Path.Combine(PlayModeHelpers.ScreensDirectory, "..", "soak", stamp + "_" + tag));
            Directory.CreateDirectory(dir);

            using var log = new SoakLogCapture();
            log.Start();

            yield return PlayModeHelpers.LoadScene(scene, 120f);
            yield return PlayModeHelpers.WaitUntil(
                () => PlayModeHelpers.FindMatchBootstrap() != null ||
                      PlayModeHelpers.FindTrainingBootstrap() != null ||
                      UnityEngine.Object.FindAnyObjectByType<SkirmishBootstrap>() != null,
                90f,
                "Soak map bootstrap yok: " + tag);

            var previousScale = Time.timeScale;
            Time.timeScale = 2f;
            var sampler = new PerfSampler(300);
            var botSeries = new List<string>(32);
            var started = Time.realtimeSinceStartup;
            var nextShot = started;
            var nextSample = started;

            try
            {
                while (Time.realtimeSinceStartup - started < maxRealtime)
                {
                    sampler.SampleFrame();
                    if (Time.realtimeSinceStartup >= nextSample)
                    {
                        nextSample = Time.realtimeSinceStartup + 5f;
                        botSeries.Add($"{Time.realtimeSinceStartup - started:F0}s bots={PlayModeHelpers.CountAliveBots()} fps~{sampler.Fps:F0}");
                    }

                    if (Time.realtimeSinceStartup >= nextShot)
                    {
                        nextShot = Time.realtimeSinceStartup + 30f;
                        yield return CaptureTo(dir, $"soak_{tag}_{(int)(Time.realtimeSinceStartup - started)}s");
                    }

                    yield return null;
                }
            }
            finally
            {
                Time.timeScale = previousScale > 0f ? previousScale : 1f;
                sampler.Dispose();
            }

            WriteReport(dir, tag, log, sampler, botSeries, started);
            log.AssertNoExceptions("Soak " + tag);
        }

        private static IEnumerator RunTrainingSoak(float maxRealtime)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dir = Path.GetFullPath(Path.Combine(PlayModeHelpers.ScreensDirectory, "..", "soak", stamp + "_training"));
            Directory.CreateDirectory(dir);

            using var log = new SoakLogCapture();
            log.Start();

            yield return PlayModeHelpers.LoadScene(SceneNames.Training, 90f);
            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var boot = PlayModeHelpers.FindTrainingBootstrap();
                    return boot != null && boot.IsReady && boot.Player != null;
                },
                60f,
                "Soak: poligon hazır değil");

            var previousScale = Time.timeScale;
            Time.timeScale = 2f;
            var sampler = new PerfSampler(300);
            var series = new List<string>(32);
            var started = Time.realtimeSinceStartup;
            var nextShot = started;

            try
            {
                while (Time.realtimeSinceStartup - started < maxRealtime)
                {
                    sampler.SampleFrame();
                    if (Time.realtimeSinceStartup >= nextShot)
                    {
                        nextShot = Time.realtimeSinceStartup + 30f;
                        series.Add($"{Time.realtimeSinceStartup - started:F0}s fps~{sampler.Fps:F0}");
                        yield return CaptureTo(dir, $"soak_training_{(int)(Time.realtimeSinceStartup - started)}s");
                    }

                    yield return null;
                }
            }
            finally
            {
                Time.timeScale = previousScale > 0f ? previousScale : 1f;
                sampler.Dispose();
            }

            WriteReport(dir, "training", log, sampler, series, started);
            log.AssertNoExceptions("Soak training");
        }

        private static IEnumerator CaptureTo(string dir, string fileName)
        {
            // Batchmode'da Game View yok → CaptureScreenshotAsTexture Error log + test fail.
            if (UnityEngine.Application.isBatchMode)
                yield break;
            yield return new WaitForEndOfFrame();
            var safe = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".png";
            var path = Path.Combine(dir, safe);
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null)
                yield break;
            try
            {
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }

        private static float MaxRealtime(string envKey, float fallback)
        {
            var env = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrEmpty(env) && float.TryParse(env, out var seconds) && seconds > 5f)
                return seconds;
            return fallback;
        }

        private static void WriteReport(
            string dir,
            string tag,
            SoakLogCapture log,
            PerfSampler sampler,
            List<string> series,
            float startedRealtime)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Soak raporu — " + tag);
            sb.AppendLine();
            sb.AppendLine("- Tarih: " + DateTime.Now.ToString("O"));
            sb.AppendLine("- Süre (gerçek sn): " + (Time.realtimeSinceStartup - startedRealtime).ToString("F1"));
            sb.AppendLine("- Ort. kare ms: " + sampler.AverageFrameMs().ToString("F2"));
            sb.AppendLine("- %1 low ms: " + sampler.OnePercentLowMs().ToString("F2"));
            sb.AppendLine("- Son FPS: " + sampler.Fps.ToString("F1"));
            sb.AppendLine("- Exception: " + log.Exceptions.Count);
            sb.AppendLine("- Error: " + log.Errors.Count);
            sb.AppendLine();
            sb.AppendLine("## Bot / faz zaman serisi");
            for (var i = 0; i < series.Count; i++)
                sb.AppendLine("- " + series[i]);
            sb.AppendLine();
            sb.AppendLine("## Exception'lar");
            if (log.Exceptions.Count == 0)
                sb.AppendLine("_yok_");
            for (var i = 0; i < log.Exceptions.Count; i++)
                sb.AppendLine("```\n" + log.Exceptions[i] + "\n```");
            sb.AppendLine();
            sb.AppendLine("## Error logları");
            if (log.Errors.Count == 0)
                sb.AppendLine("_yok_");
            for (var i = 0; i < log.Errors.Count; i++)
                sb.AppendLine("```\n" + log.Errors[i] + "\n```");

            File.WriteAllText(Path.Combine(dir, "RAPOR.md"), sb.ToString());

            // Merkez özet
            var rootReport = Path.GetFullPath(Path.Combine(PlayModeHelpers.ScreensDirectory, "..", "soak", "RAPOR.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(rootReport) ?? ".");
            File.AppendAllText(rootReport,
                $"\n## {DateTime.Now:HH:mm} {tag}\n- dir: `{dir}`\n- exceptions: {log.Exceptions.Count}, errors: {log.Errors.Count}, avgMs: {sampler.AverageFrameMs():F2}\n");
        }
    }

    /// <summary>Soak için Exception + Error yakalama (AssertionException üretmez ta ki AssertNoExceptions çağrılana kadar).</summary>
    internal sealed class SoakLogCapture : IDisposable
    {
        private readonly List<string> _exceptions = new(16);
        private readonly List<string> _errors = new(32);
        private bool _active;

        public IReadOnlyList<string> Exceptions => _exceptions;
        public IReadOnlyList<string> Errors => _errors;

        public void Start()
        {
            if (_active) return;
            _active = true;
            UnityEngine.Application.logMessageReceivedThreaded += OnLog;
        }

        public void AssertNoExceptions(string context)
        {
            if (_exceptions.Count == 0)
                return;
            Assert.Fail(context + " — exception:\n" + string.Join("\n---\n", _exceptions));
        }

        public void Dispose()
        {
            if (!_active) return;
            _active = false;
            UnityEngine.Application.logMessageReceivedThreaded -= OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert)
                _exceptions.Add(type + ": " + condition + "\n" + stackTrace);
            else if (type == LogType.Error)
                _errors.Add(condition + "\n" + stackTrace);
        }
    }
}
