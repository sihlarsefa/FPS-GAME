using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.AI;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.PlayMode
{
    /// <summary>
    /// İki düşman bot 40 m arayla doğurulur, 30 sn çarpıştırılır: ateş, isabet, istisna ve kare süresi denetlenir.
    /// Sahne/sistem yoksa Assert.Ignore ile atlanır.
    /// </summary>
    public sealed class BotClashSmokeTests
    {
        private const float SeparationMeters = 40f;
        private const float RunSeconds = 30f;
        private const int TeamA = 41;
        private const int TeamB = 42;

        [UnityTest]
        public IEnumerator TwoHostileBots_Clash30s_FireHitNoExceptions()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();

            yield return PlayModeHelpers.LoadScene(SceneNames.Training, 60f);
            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var boot = PlayModeHelpers.FindTrainingBootstrap();
                    return boot != null && boot.IsReady && boot.Player != null;
                },
                40f,
                "TrainingBootstrap hazır olmadı.");

            if (!GameContext.TryGet<IEventBus>(out var bus) || bus == null)
            {
                SmokeSupport.Skip("EventBus yok; bot çatışma testi atlandı.");
                yield break;
            }

            var player = PlayModeHelpers.FindLocalPlayer();
            var center = player != null ? player.transform.position + player.transform.forward * 30f : Vector3.zero;
            var right = player != null ? player.transform.right : Vector3.right;
            right.y = 0f;
            right = right.sqrMagnitude < 1e-4f ? Vector3.right : right.normalized;

            var posA = Ground(center - right * (SeparationMeters * 0.5f));
            var posB = Ground(center + right * (SeparationMeters * 0.5f));

            var idA = new PlayerId(95001);
            var idB = new PlayerId(95002);
            var botA = SpawnBot(idA, "ClashA", TeamA, posA, right);
            var botB = SpawnBot(idB, "ClashB", TeamB, posB, -right);

            if (botA == null || botB == null || botA.Combatant == null || botB.Combatant == null)
            {
                SmokeSupport.Skip("Bot doğurulamadı (bot API/sahne eksik).");
                yield break;
            }

            var shotsA = 0;
            var shotsB = 0;
            var hits = 0;
            using var firedProbe = new CountingSubscription<WeaponFiredEvent>(bus, e =>
            {
                if (e.ShooterId.Value == idA.Value) shotsA++;
                else if (e.ShooterId.Value == idB.Value) shotsB++;
            });
            using var hitProbe = new CountingSubscription<HitConfirmedEvent>(bus, e =>
            {
                if (e.AttackerId.Value == idA.Value || e.AttackerId.Value == idB.Value) hits++;
            });

            yield return null;
            Assert.IsTrue(botA.isActiveAndEnabled && botB.isActiveAndEnabled, "Botlar etkin değil.");

            var start = Time.realtimeSinceStartup;
            var frames = 0;
            var worstFrame = 0f;
            var sumFrame = 0f;
            while (Time.realtimeSinceStartup - start < RunSeconds)
            {
                var dt = Time.unscaledDeltaTime;
                frames++;
                sumFrame += dt;
                if (dt > worstFrame) worstFrame = dt;
                yield return null;
            }

            Debug.Log($"[BotClash] atışA={shotsA} atışB={shotsB} isabet={hits} kare={frames} " +
                      $"ortalama={(frames > 0 ? sumFrame / frames * 1000f : 0f):F1}ms en kötü={worstFrame * 1000f:F0}ms " +
                      $"durumA={botA?.StateName} durumB={botB?.StateName}");

            errors.AssertNoExceptions("Bot çatışması");

            Assert.GreaterOrEqual(shotsA, 1, "Bot A hiç ateş etmedi.");
            Assert.GreaterOrEqual(shotsB, 1, "Bot B hiç ateş etmedi.");
            Assert.GreaterOrEqual(hits, 1, "Hiç isabet kaydedilmedi.");

            // Siper kullanımı: BotController'da herkese açık bir siper durumu yok (_inCoverLatched özel).
            // ENTEGRASYON: BotController.IsInCover / CoverEntries açılırsa burada assert edilmeli. Şimdilik atlandı.
            Debug.Log("[BotClash] NOT: siper assert'i atlandı (BotController siper durumunu dışa açmıyor).");

            if (frames >= 10)
            {
                var avg = sumFrame / frames;
                Assert.Less(avg, 0.25f, "Ortalama kare süresi çok yüksek: " + avg * 1000f + " ms");
            }

            DestroyBot(botA);
            DestroyBot(botB);
        }

        private static BotController SpawnBot(PlayerId id, string name, int team, Vector3 pos, Vector3 facing)
        {
            try
            {
                return BotController.Create(new BotSpawnArgs
                {
                    Id = id,
                    Name = name,
                    Team = team,
                    Role = TeamRole.Rifleman,
                    Difficulty = BotDifficulty.Normal,
                    SpawnOnGround = true,
                    GroundPosition = pos,
                    GroundYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg,
                    Slot = 1,
                    Seed = id.Value * 13 + 5,
                    RegisterWithMatch = false,
                    GiveLoadout = true,
                    DropLootOnDeath = false
                });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BotClash] Bot doğuşu başarısız: " + e.Message);
                return null;
            }
        }

        private static Vector3 Ground(Vector3 p)
        {
            var from = new Vector3(p.x, p.y + 100f, p.z);
            if (Physics.Raycast(from, Vector3.down, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return p;
        }

        private static void DestroyBot(BotController bot)
        {
            if (bot != null)
                Object.Destroy(bot.gameObject);
        }

        private sealed class CountingSubscription<T> : System.IDisposable where T : IGameEvent
        {
            private readonly IEventBus _bus;
            private readonly System.Action<T> _handler;

            public CountingSubscription(IEventBus bus, System.Action<T> handler)
            {
                _bus = bus;
                _handler = handler;
                _bus.Subscribe(_handler);
            }

            public void Dispose() => _bus.Unsubscribe(_handler);
        }
    }
}
