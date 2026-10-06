#if UNITY_EDITOR // Unity EditMode koşucusunda çalışır (Infrastructure/Presentation referansı gerekir)
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Infrastructure.Events;
using Project.Presentation.Bootstrap;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class AchievementTrackerTests
    {
        private static AchievementService Svc() => new AchievementService(null, new List<AchievementDefinition>
        {
            new AchievementDefinition { id = "a", metric = "orders_any", target = 99 },
            new AchievementDefinition { id = "b", metric = "kills_ar_mpt76", target = 99 },
            new AchievementDefinition { id = "c", metric = "multi_kills_3", target = 99 },
            new AchievementDefinition { id = "d", metric = "medkits_used", target = 99 },
            new AchievementDefinition { id = "e", metric = "orders_attack", target = 99 },
        });

        [Test]
        public void OrdersMedkitsAndWeaponKillsAreCounted()
        {
            var bus = new EventBus();
            var svc = Svc();
            var me = new PlayerId(1);
            using (new AchievementTracker(bus, svc, me, 0, () => 0f))
            {
                bus.Publish(new SquadOrderIssuedEvent(0, SquadOrder.Attack, default));
                bus.Publish(new SquadOrderIssuedEvent(1, SquadOrder.Attack, default));
                bus.Publish(new ItemUsedEvent(me, ItemIds.MedKit, false, true));
                bus.Publish(new PlayerDiedEvent(new PlayerId(5), me, WeaponIds.Mpt76, false));
            }
            Assert.AreEqual(1, svc.GetProgress("orders_any"));
            Assert.AreEqual(1, svc.GetProgress("orders_attack"));
            Assert.AreEqual(1, svc.GetProgress("medkits_used"));
            Assert.AreEqual(1, svc.GetProgress("kills_ar_mpt76"));
        }

        [Test]
        public void ThreeKillsInWindowCountsMultiKillOnce_AndDisposeStops()
        {
            var bus = new EventBus();
            var svc = Svc();
            var me = new PlayerId(1);
            float t = 0f;
            var tr = new AchievementTracker(bus, svc, me, 0, () => t);
            for (var i = 0; i < 4; i++)
            {
                bus.Publish(new PlayerDiedEvent(new PlayerId(10 + i), me, WeaponIds.G3, false));
                t += 2f;
            }
            Assert.AreEqual(1, svc.GetProgress("multi_kills_3"));
            tr.Dispose();
            bus.Publish(new SquadOrderIssuedEvent(0, SquadOrder.Follow, default));
            Assert.AreEqual(0, svc.GetProgress("orders_any"));
        }

        [Test]
        public void WeaponKillMetricMapsPistolsAndUnknown()
        {
            Assert.AreEqual("kills_pistol", AchievementTracker.WeaponKillMetric(WeaponIds.Tp9));
            Assert.IsNull(AchievementTracker.WeaponKillMetric("x"));
        }
    }
}
#endif
