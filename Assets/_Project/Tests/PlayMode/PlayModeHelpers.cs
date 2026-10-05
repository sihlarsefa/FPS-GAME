using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.AI;
using Project.Presentation.Bootstrap;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests.PlayMode
{
    /// <summary>PlayMode duman testleri için ortak yardımcılar: sahne yükleme, hata yakalama, ekran görüntüsü.</summary>
    internal static class PlayModeHelpers
    {
        public static string ScreensDirectory
        {
            get
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                return Path.Combine(projectRoot, "Logs", "screens");
            }
        }

        public static ExceptionLogCapture BeginExceptionCapture()
        {
            var capture = new ExceptionLogCapture();
            capture.Start();
            return capture;
        }

        public static IEnumerator LoadScene(string sceneName, float timeoutSeconds = 90f)
        {
            Assert.IsFalse(string.IsNullOrEmpty(sceneName), "Sahne adı boş.");

            if (SceneManager.GetActiveScene().name == sceneName && SceneManager.GetActiveScene().isLoaded)
            {
                yield return null;
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, "Sahne yüklenemedi: " + sceneName + " (Build Settings'te olmalı; SetupAll çalıştırın).");

            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!op.isDone)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Sahne zaman aşımı: " + sceneName);
                yield return null;
            }

            yield return null;
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, "Aktif sahne beklenen değil.");
        }

        public static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds, string message)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, message);
                yield return null;
            }
        }

        public static IEnumerator WaitRealtime(float seconds)
        {
            var until = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        public static void CaptureScreen(string fileName)
        {
            Directory.CreateDirectory(ScreensDirectory);
            var safe = SanitizeFileName(fileName);
            if (!safe.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                safe += ".png";

            var path = Path.Combine(ScreensDirectory, safe);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[PlayMode] Ekran görüntüsü: " + path);
        }

        public static IEnumerator CaptureScreenAndWait(string fileName, float settleSeconds = 0.15f)
        {
            yield return WaitRealtime(settleSeconds);
            CaptureScreen(fileName);
            // CaptureScreenshot asenkron yazabilir; sonraki assert öncesi bir kare bekleyelim.
            yield return null;
        }

        public static MatchBootstrap FindMatchBootstrap()
        {
            return Object.FindAnyObjectByType<MatchBootstrap>();
        }

        public static TrainingBootstrap FindTrainingBootstrap()
        {
            return Object.FindAnyObjectByType<TrainingBootstrap>();
        }

        public static MainMenuBootstrap FindMainMenuBootstrap()
        {
            return Object.FindAnyObjectByType<MainMenuBootstrap>();
        }

        public static PlayerController FindLocalPlayer()
        {
            var match = FindMatchBootstrap();
            if (match != null && match.Player != null)
                return match.Player;

            var training = FindTrainingBootstrap();
            if (training != null && training.Player != null)
                return training.Player;

            return Object.FindAnyObjectByType<PlayerController>();
        }

        public static int CountAliveBots()
        {
            var bots = BotController.All;
            var count = 0;
            for (var i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (bot == null || !bot.isActiveAndEnabled)
                    continue;
                if (bot.Combatant != null && bot.Combatant.IsAlive)
                    count++;
            }

            return count;
        }

        public static List<BotController> CollectTeamBots(int team, bool requireLanded = false)
        {
            var result = new List<BotController>(16);
            var bots = BotController.All;
            for (var i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (bot == null || !bot.isActiveAndEnabled || bot.Team != team)
                    continue;
                if (bot.Combatant == null || !bot.Combatant.IsAlive)
                    continue;
                if (requireLanded && !bot.HasLanded)
                    continue;
                result.Add(bot);
            }

            return result;
        }

        public static bool TryGetMatchService(out IMatchService match)
        {
            return GameContext.TryGet(out match) && match != null;
        }

        public static bool TryGetChain(out ChainOfCommandService chain)
        {
            return GameContext.TryGet(out chain) && chain != null;
        }

        public static bool TryGetOrders(out SquadOrderService orders)
        {
            return GameContext.TryGet(out orders) && orders != null;
        }

        public static IEnumerator AccelerateToInMatch(float realtimeBudgetSeconds = 25f)
        {
            Assert.IsTrue(TryGetMatchService(out var match), "MatchService yok.");

            var previousScale = Time.timeScale;
            // dropTimeoutSeconds (~90) / 20 ≈ 4.5 sn gerçek zaman; PreMatch (~6) ihmal.
            Time.timeScale = 20f;
            try
            {
                var deadline = Time.realtimeSinceStartup + realtimeBudgetSeconds;
                while (match.CurrentPhase != MatchPhase.InMatch && match.CurrentPhase != MatchPhase.Ending)
                {
                    Assert.Less(Time.realtimeSinceStartup, deadline,
                        "InMatch'e geçilemedi; faz=" + match.CurrentPhase);
                    yield return null;
                }
            }
            finally
            {
                Time.timeScale = previousScale > 0f ? previousScale : 1f;
            }

            Assert.AreEqual(MatchPhase.InMatch, match.CurrentPhase,
                "Beklenen faz InMatch; mevcut=" + match.CurrentPhase);
        }

        /// <summary>İntikal araçlarından yolcu bırakır ve en az minLanded botun inmesini bekler.</summary>
        public static IEnumerator EnsureBotsLanded(int minLanded = 10, float timeoutSeconds = 25f)
        {
            var transports = Object.FindObjectsByType<Project.Infrastructure.Transport.TransportVehicle>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < transports.Length; i++)
            {
                var transport = transports[i];
                if (transport == null)
                    continue;
                try
                {
                    transport.ReleasePassengers();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PlayMode] ReleasePassengers: " + e.Message);
                }
            }

            var previousScale = Time.timeScale;
            Time.timeScale = Mathf.Max(previousScale, 8f);
            try
            {
                yield return WaitUntil(
                    () =>
                    {
                        var bots = BotController.All;
                        var landed = 0;
                        for (var i = 0; i < bots.Count; i++)
                        {
                            var bot = bots[i];
                            if (bot != null && bot.HasLanded && bot.Combatant != null && bot.Combatant.IsAlive)
                                landed++;
                        }

                        return landed >= minLanded;
                    },
                    timeoutSeconds,
                    "Yeterli bot inmedi (HasLanded). min=" + minLanded);
            }
            finally
            {
                Time.timeScale = previousScale > 0f ? previousScale : 1f;
            }
        }

        public static void OrientPlayerToward(PlayerController player, Vector3 worldPoint)
        {
            Assert.IsNotNull(player);
            var origin = player.AimOrigin;
            var to = worldPoint - origin;
            if (to.sqrMagnitude < 1e-6f)
                return;

            var flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude > 1e-6f)
                player.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

            var dir = to.normalized;
            var pitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            player.CameraController?.SetPitch(pitch);
        }

        public static void PlacePlayerNear(PlayerController player, Vector3 nearPoint, float distance = 4f)
        {
            Assert.IsNotNull(player);
            var flat = nearPoint;
            flat.y = nearPoint.y;
            var offset = player.transform.forward;
            offset.y = 0f;
            if (offset.sqrMagnitude < 1e-4f)
                offset = Vector3.back;
            offset = offset.normalized * distance;

            var pos = nearPoint - offset;
            pos.y = nearPoint.y;
            player.transform.position = pos;
            OrientPlayerToward(player, nearPoint + Vector3.up);
        }

        public static void WarpBotNear(BotController bot, Vector3 nearPoint, float distance, float angleDegrees)
        {
            Assert.IsNotNull(bot);
            var rad = angleDegrees * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * distance;
            var pos = nearPoint + offset;
            pos.y = nearPoint.y;
            bot.transform.position = pos;
            if (bot.Agent != null && bot.Agent.enabled)
                bot.Agent.Warp(pos);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "shot";

            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }

    /// <summary>LogType.Exception / Error yakalayıcı (Assert.Fail için birikir).</summary>
    internal sealed class ExceptionLogCapture : IDisposable
    {
        private readonly List<string> _exceptions = new(8);
        private bool _active;

        public IReadOnlyList<string> Exceptions => _exceptions;
        public bool HasExceptions => _exceptions.Count > 0;

        public void Start()
        {
            if (_active)
                return;
            _active = true;
            Application.logMessageReceivedThreaded += OnLog;
        }

        public void AssertNoExceptions(string context)
        {
            if (_exceptions.Count == 0)
                return;

            Assert.Fail(context + " — exception:\n" + string.Join("\n---\n", _exceptions));
        }

        public void Dispose()
        {
            if (!_active)
                return;
            _active = false;
            Application.logMessageReceivedThreaded -= OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Assert)
                return;

            _exceptions.Add(type + ": " + condition + "\n" + stackTrace);
        }
    }

    /// <summary>EventBus üzerinden tek seferlik olay dinleyicisi.</summary>
    internal sealed class EventProbe<TEvent> : IDisposable where TEvent : IGameEvent
    {
        private readonly IEventBus _bus;
        private readonly Action<TEvent> _handler;
        private bool _disposed;

        public bool Received { get; private set; }
        public TEvent Last { get; private set; }
        public int Count { get; private set; }

        public EventProbe(IEventBus bus)
        {
            _bus = bus;
            _handler = OnEvent;
            _bus?.Subscribe(_handler);
        }

        private void OnEvent(TEvent e)
        {
            Received = true;
            Last = e;
            Count++;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _bus?.Unsubscribe(_handler);
        }
    }
}
