using System.Collections;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.AI;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.PlayMode
{
    /// <summary>
    /// KuzgunVadisi duman: oyuncu + ≥30 bot, intikal başlar, exception yok,
    /// hızlandırılmış zamanla Insertion → InMatch.
    /// </summary>
    public sealed class KuzgunVadisiSmokeTests
    {
        [UnityTest]
        public IEnumerator KuzgunVadisi_Smoke_SpawnsPlayerAndBots_ThenReachesInMatch()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();

            yield return PlayModeHelpers.LoadScene(SceneNames.Operation, 120f);

            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var boot = PlayModeHelpers.FindMatchBootstrap();
                    return boot != null && boot.IsReady && boot.Player != null;
                },
                90f,
                "MatchBootstrap hazır olmadı (dünya/NavMesh üretimi uzun sürebilir).");

            var bootstrap = PlayModeHelpers.FindMatchBootstrap();
            Assert.IsNotNull(bootstrap.Player, "Yerel oyuncu yok.");

            yield return PlayModeHelpers.WaitUntil(
                () => PlayModeHelpers.CountAliveBots() >= 30,
                20f,
                "20 sn içinde en az 30 bot oluşmalı; mevcut=" + PlayModeHelpers.CountAliveBots());

            Assert.GreaterOrEqual(PlayModeHelpers.CountAliveBots(), 30,
                "Bot sayısı yetersiz. All=" + BotController.All.Count);

            yield return PlayModeHelpers.WaitUntil(
                () => PlayModeHelpers.TryGetMatchService(out var match) &&
                      (match.CurrentPhase == MatchPhase.Insertion ||
                       match.CurrentPhase == MatchPhase.InMatch ||
                       match.CurrentPhase == MatchPhase.PreMatch),
                15f,
                "MatchService fazı başlamadı.");

            Assert.IsTrue(PlayModeHelpers.TryGetMatchService(out var matchService));
            Debug.Log("[PlayMode] KuzgunVadisi faz (erken): " + matchService.CurrentPhase +
                      ", bot=" + PlayModeHelpers.CountAliveBots());

            // Intikalin başlamasını bekle (PreMatch kısa); ardından hızlandırılmış InMatch.
            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    PlayModeHelpers.TryGetMatchService(out var m);
                    return m != null &&
                           (m.CurrentPhase == MatchPhase.Insertion ||
                            m.CurrentPhase == MatchPhase.InMatch);
                },
                20f,
                "Intikal (Insertion) başlamadı; faz=" + matchService.CurrentPhase);

            Assert.IsTrue(PlayModeHelpers.TryGetMatchService(out matchService));
            Assert.IsTrue(
                matchService.CurrentPhase == MatchPhase.Insertion ||
                matchService.CurrentPhase == MatchPhase.InMatch,
                "Intikal bekleniyordu; faz=" + matchService.CurrentPhase);

            yield return PlayModeHelpers.CaptureScreenAndWait("kuzgunvadisi_insertion");

            if (matchService.CurrentPhase != MatchPhase.InMatch)
                yield return PlayModeHelpers.AccelerateToInMatch(30f);

            Assert.AreEqual(MatchPhase.InMatch, matchService.CurrentPhase);
            Assert.IsNotNull(bootstrap.Player);
            Assert.GreaterOrEqual(PlayModeHelpers.CountAliveBots(), 30);

            yield return PlayModeHelpers.CaptureScreenAndWait("kuzgunvadisi_inmatch");

            errors.AssertNoExceptions("KuzgunVadisi duman");
        }
    }
}
