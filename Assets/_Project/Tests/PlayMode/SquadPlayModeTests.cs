using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.AI;
using Project.Infrastructure.Combat;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.PlayMode
{
    /// <summary>F1 takip emri: tim botları komutana yaklaşır. Komuta devri: komutan ölünce olay yayınlanır.</summary>
    public sealed class SquadPlayModeTests
    {
        [UnityTest]
        public IEnumerator Squad_F1_Follow_BotsApproachCommander()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();

            yield return PlayModeHelpers.LoadScene(SceneNames.Operation, 120f);

            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var boot = PlayModeHelpers.FindMatchBootstrap();
                    return boot != null && boot.IsReady && boot.Player != null &&
                           PlayModeHelpers.CountAliveBots() >= 9;
                },
                90f,
                "Maç / botlar hazır olmadı.");

            if (PlayModeHelpers.TryGetMatchService(out var match) && match.CurrentPhase != MatchPhase.InMatch)
                yield return PlayModeHelpers.AccelerateToInMatch(30f);

            yield return PlayModeHelpers.EnsureBotsLanded(15, 30f);

            var player = PlayModeHelpers.FindLocalPlayer();
            Assert.IsNotNull(player);
            Assert.IsNotNull(player.Combatant);

            var team = player.Combatant.Team;
            Assert.IsTrue(PlayModeHelpers.TryGetOrders(out var orders), "SquadOrderService yok.");

            // Tim botlarını komutan yakınına koy (intikal sonrası dağınık olabilirler).
            var squad = PlayModeHelpers.CollectTeamBots(team, requireLanded: true);
            Assert.GreaterOrEqual(squad.Count, 3, "Timde yeterli inmiş bot yok.");

            var commanderPos = player.transform.position;
            var followers = new List<BotController>(8);
            for (var i = 0; i < squad.Count; i++)
            {
                var bot = squad[i];
                if (bot.Combatant != null && bot.Combatant.Id == player.Combatant.Id)
                    continue;
                if (bot.IsCommander && bot.Combatant != null &&
                    bot.Combatant.Id == player.Combatant.Id)
                    continue;

                var angle = 40f + followers.Count * 35f;
                PlayModeHelpers.WarpBotNear(bot, commanderPos, 18f, angle);
                followers.Add(bot);
                if (followers.Count >= 5)
                    break;
            }

            Assert.GreaterOrEqual(followers.Count, 3, "Takipçi bot yok.");
            yield return null;

            var avgBefore = AverageDistance(followers, player.transform.position);
            Assert.Greater(avgBefore, 8f, "Başlangıç mesafesi beklenenden küçük.");

            // F1 = Beni takip et
            orders.Issue(team, SquadOrder.Follow, new Float3(commanderPos.x, commanderPos.y, commanderPos.z));

            Assert.IsTrue(orders.TryGetOrder(team, out var issued, out _), "Emir kaydı yok.");
            Assert.AreEqual(SquadOrder.Follow, issued);

            var previousScale = Time.timeScale;
            Time.timeScale = 8f;
            try
            {
                yield return PlayModeHelpers.WaitUntil(
                    () => AverageDistance(followers, player.transform.position) < avgBefore - 2.5f,
                    20f,
                    "Botlar komutana yaklaşmadı. önce=" + avgBefore +
                    " şimdi=" + AverageDistance(followers, player.transform.position));
            }
            finally
            {
                Time.timeScale = previousScale > 0f ? previousScale : 1f;
            }

            var avgAfter = AverageDistance(followers, player.transform.position);
            Assert.Less(avgAfter, avgBefore - 2f,
                "Takip sonrası mesafe düşmedi. önce=" + avgBefore + " sonra=" + avgAfter);

            yield return PlayModeHelpers.CaptureScreenAndWait("squad_f1_follow");

            errors.AssertNoExceptions("Tim F1 takip");
        }

        [UnityTest]
        public IEnumerator CommandTransfer_WhenCommanderDies_PublishesEvent()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();

            yield return PlayModeHelpers.LoadScene(SceneNames.Operation, 120f);

            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var boot = PlayModeHelpers.FindMatchBootstrap();
                    return boot != null && boot.IsReady && PlayModeHelpers.CountAliveBots() >= 20 &&
                           PlayModeHelpers.TryGetChain(out _);
                },
                90f,
                "Komuta zinciri / botlar hazır olmadı.");

            Assert.IsTrue(PlayModeHelpers.TryGetChain(out var chain));
            Assert.IsTrue(GameContext.TryGet<IEventBus>(out var bus) && bus != null, "EventBus yok.");

            // Yerel oyuncuyu öldürmek maç sonunu tetikleyebilir; başka bir timin komutanını öldür.
            const int targetTeam = 1;
            var commanderId = chain.GetCommander(targetTeam);
            Assert.IsTrue(commanderId.IsValid, "Tim 1 komutanı yok.");

            var deputyId = chain.GetDeputy(targetTeam);
            Assert.IsTrue(deputyId.IsValid, "Tim 1'de yedek komutan (deputy) yok — devir test edilemez.");

            var commanderCombatant = FindCombatant(commanderId);
            Assert.IsNotNull(commanderCombatant, "Komutan Combatant bulunamadı: " + commanderId.Value);

            using var probe = new EventProbe<CommandTransferredEvent>(bus);

            commanderCombatant.ApplyDamage(new DamageInfo(10_000f, PlayerId.Invalid, "playmode_test"));

            yield return PlayModeHelpers.WaitUntil(
                () => probe.Received || !commanderCombatant.IsAlive,
                5f,
                "Komutan ölümü / CommandTransferredEvent beklenirken zaman aşımı.");

            // Ölüm olayı zincire işlenmemişse MarkDead ile aynı yolu doğrula.
            if (!probe.Received && !commanderCombatant.IsAlive)
                chain.MarkDead(commanderId);

            yield return PlayModeHelpers.WaitUntil(() => probe.Received, 2f,
                "CommandTransferredEvent yayınlanmadı.");

            Assert.IsTrue(probe.Received, "CommandTransferredEvent gelmedi.");
            Assert.AreEqual(targetTeam, probe.Last.Team);
            Assert.AreEqual(commanderId, probe.Last.PreviousCommanderId);
            Assert.AreEqual(deputyId, probe.Last.NewCommanderId);
            Assert.AreEqual(deputyId, chain.GetCommander(targetTeam));

            yield return PlayModeHelpers.CaptureScreenAndWait("command_transfer");

            errors.AssertNoExceptions("Komuta devri");
        }

        private static float AverageDistance(List<BotController> bots, Vector3 target)
        {
            if (bots == null || bots.Count == 0)
                return 0f;

            var sum = 0f;
            var n = 0;
            for (var i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (bot == null)
                    continue;
                sum += Vector3.Distance(bot.transform.position, target);
                n++;
            }

            return n > 0 ? sum / n : 0f;
        }

        private static Combatant FindCombatant(PlayerId id)
        {
            if (CombatantRegistry.TryGet(id, out var combatant) && combatant != null)
                return combatant;

            var bots = BotController.All;
            for (var i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (bot?.Combatant != null && bot.Combatant.Id == id)
                    return bot.Combatant;
            }

            var player = PlayModeHelpers.FindLocalPlayer();
            if (player?.Combatant != null && player.Combatant.Id == id)
                return player.Combatant;

            return null;
        }
    }
}
