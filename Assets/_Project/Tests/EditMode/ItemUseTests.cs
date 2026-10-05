using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    /// <summary>ItemUseService (süreli tıbbi/boost kullanımı) ve BoostService testleri.</summary>
    public sealed class ItemUseTests
    {
        private sealed class RecordingBus : IEventBus
        {
            public readonly List<ItemUsedEvent> ItemEvents = new();

            public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
            {
                if (gameEvent is ItemUsedEvent used)
                    ItemEvents.Add(used);
            }

            public void Subscribe<TEvent>(System.Action<TEvent> handler) where TEvent : IGameEvent { }
            public void Unsubscribe<TEvent>(System.Action<TEvent> handler) where TEvent : IGameEvent { }
        }

        private sealed class HealTarget : IHealable
        {
            public float Total;
            public int Calls;

            public void Heal(float amount)
            {
                Total += amount;
                Calls++;
            }
        }

        private static readonly PlayerId Owner = new(3);

        private RecordingBus _bus;
        private InventoryService _inventory;
        private HealthService _health;
        private BoostService _boost;
        private ItemUseService _use;

        [SetUp]
        public void SetUp()
        {
            _bus = new RecordingBus();
            _inventory = new InventoryService(_bus, Owner);
            _health = new HealthService(Owner, 100f, _bus);
            _boost = new BoostService();
            _use = new ItemUseService(Owner, _inventory, _health, _boost, _bus);
        }

        private void SetHealth(float value)
        {
            var damage = _health.Current - value;
            if (damage > 0f)
                _health.ApplyDamage(new DamageInfo(damage, PlayerId.Invalid, "test"));
        }

        private void TickFor(float seconds, float step = 0.1f)
        {
            var elapsed = 0f;
            while (elapsed < seconds - 1e-4f)
            {
                var dt = seconds - elapsed < step ? seconds - elapsed : step;
                _use.Tick(dt);
                elapsed += dt;
            }
        }

        // ------------------------------------------------------------------ ItemUseService

        [Test]
        public void Bandage_HealsTenAfterFourSeconds()
        {
            _inventory.GiveItem(ItemIds.Bandage, 2);
            SetHealth(50f);

            Assert.IsTrue(_use.TryBegin(ItemIds.Bandage));
            Assert.IsTrue(_use.IsUsing);
            Assert.AreEqual(ItemIds.Bandage, _use.CurrentItemId);
            Assert.AreEqual(0.5f, _use.MovementSpeedMultiplier, 1e-5f);
            Assert.AreEqual(4f, _use.RemainingSeconds, 1e-4f);

            TickFor(2f);
            Assert.AreEqual(0.5f, _use.Progress, 1e-3f);
            Assert.AreEqual(2f, _use.RemainingSeconds, 1e-3f);
            Assert.AreEqual(50f, _health.Current, 1e-4f, "süre bitmeden iyileşme yok");
            Assert.AreEqual(2, _inventory.GetCount(ItemIds.Bandage), "süre bitmeden tüketim yok");

            TickFor(2.05f);
            Assert.IsFalse(_use.IsUsing);
            Assert.AreEqual(60f, _health.Current, 1e-4f);
            Assert.AreEqual(1, _inventory.GetCount(ItemIds.Bandage));
            Assert.AreEqual(1f, _use.MovementSpeedMultiplier, 1e-5f);
            Assert.AreEqual(0f, _use.Progress, 1e-5f);
        }

        [Test]
        public void Bandage_IsCappedAt75()
        {
            _inventory.GiveItem(ItemIds.Bandage, 5);
            SetHealth(70f);

            Assert.IsTrue(_use.TryBegin(ItemIds.Bandage));
            TickFor(4.1f);
            Assert.AreEqual(75f, _health.Current, 1e-4f);

            Assert.IsFalse(_use.CanUse(ItemIds.Bandage), "75'te sargı bezi işe yaramaz");
            Assert.IsFalse(_use.TryBegin(ItemIds.Bandage));
        }

        [Test]
        public void FirstAid_HealsTo75_MedKitHealsToFull()
        {
            _inventory.GiveItem(ItemIds.FirstAid, 1);
            _inventory.GiveItem(ItemIds.MedKit, 1);
            SetHealth(20f);

            Assert.IsTrue(_use.TryBegin(ItemIds.FirstAid));
            TickFor(5.9f);
            Assert.IsTrue(_use.IsUsing);
            TickFor(0.2f);
            Assert.AreEqual(75f, _health.Current, 1e-4f);

            Assert.IsFalse(_use.CanUse(ItemIds.FirstAid), "elde kalmadı");
            Assert.IsTrue(_use.TryBegin(ItemIds.MedKit));
            TickFor(8.1f);
            Assert.AreEqual(100f, _health.Current, 1e-4f);
            Assert.AreEqual(0, _inventory.GetCount(ItemIds.MedKit));
        }

        [Test]
        public void CannotUse_WithoutItem_OrAtFullHealth_OrUnknown()
        {
            Assert.IsFalse(_use.CanUse(ItemIds.Bandage));
            Assert.IsFalse(_use.TryBegin(ItemIds.Bandage));

            _inventory.GiveItem(ItemIds.MedKit, 1);
            Assert.IsFalse(_use.CanUse(ItemIds.MedKit), "can dolu");

            _inventory.GiveItem(ItemIds.FragGrenade, 1);
            SetHealth(50f);
            Assert.IsFalse(_use.CanUse(ItemIds.FragGrenade), "bomba kullanılamaz");
            Assert.IsFalse(_use.CanUse("unknown"));
            Assert.IsFalse(_use.CanUse(null));
            Assert.AreEqual(0, _bus.ItemEvents.Count);
        }

        [Test]
        public void Events_StartedAndCompleted()
        {
            _inventory.GiveItem(ItemIds.Bandage, 1);
            SetHealth(40f);
            var completed = 0;
            _use.Completed += _ => completed++;

            _use.TryBegin(ItemIds.Bandage);
            TickFor(4.1f);

            Assert.AreEqual(2, _bus.ItemEvents.Count);
            Assert.IsTrue(_bus.ItemEvents[0].Started);
            Assert.IsFalse(_bus.ItemEvents[0].Completed);
            Assert.AreEqual(Owner, _bus.ItemEvents[0].UserId);
            Assert.AreEqual(ItemIds.Bandage, _bus.ItemEvents[0].ItemId);
            Assert.IsFalse(_bus.ItemEvents[1].Started);
            Assert.IsTrue(_bus.ItemEvents[1].Completed);
            Assert.AreEqual(1, completed);
        }

        [Test]
        public void Cancel_DoesNotConsume_AndPublishesCancel()
        {
            _inventory.GiveItem(ItemIds.FirstAid, 1);
            SetHealth(40f);

            _use.TryBegin(ItemIds.FirstAid);
            TickFor(3f);
            _use.Cancel();

            Assert.IsFalse(_use.IsUsing);
            Assert.AreEqual(1, _inventory.GetCount(ItemIds.FirstAid));
            Assert.AreEqual(40f, _health.Current, 1e-4f);
            Assert.AreEqual(2, _bus.ItemEvents.Count);
            Assert.IsFalse(_bus.ItemEvents[1].Started);
            Assert.IsFalse(_bus.ItemEvents[1].Completed);

            _use.Cancel();
            Assert.AreEqual(2, _bus.ItemEvents.Count, "kullanım yokken iptal olay üretmez");

            TickFor(10f);
            Assert.AreEqual(40f, _health.Current, 1e-4f);
        }

        [Test]
        public void SameItemTwice_DoesNotRestart_DifferentItemSwitches()
        {
            _inventory.GiveItem(ItemIds.Bandage, 3);
            _inventory.GiveItem(ItemIds.FirstAid, 1);
            SetHealth(30f);

            Assert.IsTrue(_use.TryBegin(ItemIds.Bandage));
            TickFor(2f);
            Assert.IsFalse(_use.TryBegin(ItemIds.Bandage));
            Assert.AreEqual(0.5f, _use.Progress, 1e-3f);

            Assert.IsTrue(_use.TryBegin(ItemIds.FirstAid));
            Assert.AreEqual(ItemIds.FirstAid, _use.CurrentItemId);
            Assert.AreEqual(0f, _use.Progress, 1e-5f);
        }

        [Test]
        public void Death_CancelsUse()
        {
            _inventory.GiveItem(ItemIds.MedKit, 1);
            SetHealth(10f);
            _use.TryBegin(ItemIds.MedKit);
            TickFor(1f);

            _health.ApplyDamage(new DamageInfo(50f, PlayerId.Invalid, "test"));
            _use.Tick(0.1f);

            Assert.IsFalse(_use.IsUsing);
            Assert.AreEqual(1, _inventory.GetCount(ItemIds.MedKit));
            Assert.IsFalse(_use.TryBeginBestHeal());
        }

        [Test]
        public void ItemLostMidUse_Cancels()
        {
            _inventory.GiveItem(ItemIds.Bandage, 1);
            SetHealth(50f);
            _use.TryBegin(ItemIds.Bandage);
            TickFor(1f);

            Assert.IsTrue(_inventory.TryDropStack(ItemIds.Bandage, 1, out _));
            TickFor(4f);

            Assert.IsFalse(_use.IsUsing);
            Assert.AreEqual(50f, _health.Current, 1e-4f);
        }

        [Test]
        public void ReachingCapMidUse_CancelsWithoutConsuming()
        {
            _inventory.GiveItem(ItemIds.Bandage, 1);
            SetHealth(70f);
            Assert.IsTrue(_use.TryBegin(ItemIds.Bandage));
            TickFor(1f);

            // Boost iyileştirmesi canı sargı bezi sınırına (75) çıkardı: eşya boşa harcanmamalı.
            _health.Heal(5f);
            _use.Tick(0.1f);

            Assert.IsFalse(_use.IsUsing);
            Assert.AreEqual(1, _inventory.GetCount(ItemIds.Bandage));
            Assert.AreEqual(75f, _health.Current, 1e-4f);
            Assert.IsFalse(_bus.ItemEvents[_bus.ItemEvents.Count - 1].Completed);
        }

        [Test]
        public void ZeroDeltaTick_DoesNotProgress()
        {
            _inventory.GiveItem(ItemIds.FirstAid, 1);
            SetHealth(30f);
            _use.TryBegin(ItemIds.FirstAid);
            _use.Tick(0f);
            _use.Tick(-1f);

            Assert.IsTrue(_use.IsUsing);
            Assert.AreEqual(0f, _use.Progress, 1e-5f);
            Assert.AreEqual(6f, _use.Duration, 1e-5f);
            Assert.AreEqual("İlk Yardım Çantası", _use.CurrentItemName);
        }

        [Test]
        public void Boosts_AddToBoostBar()
        {
            _inventory.GiveItem(ItemIds.EnergyDrink, 1);
            _inventory.GiveItem(ItemIds.Painkiller, 1);

            Assert.IsTrue(_use.TryBegin(ItemIds.EnergyDrink));
            TickFor(4.05f);
            Assert.AreEqual(40f, _boost.Value, 1e-4f);

            Assert.IsTrue(_use.TryBegin(ItemIds.Painkiller));
            TickFor(6.05f);
            Assert.AreEqual(100f, _boost.Value, 1e-4f);
            Assert.IsFalse(_use.CanUse(ItemIds.Painkiller));
        }

        [Test]
        public void BestHeal_And_BestBoost()
        {
            Assert.IsFalse(_use.TryBeginBestHeal());
            Assert.IsFalse(_use.TryBeginBestBoost());

            _inventory.GiveItem(ItemIds.Bandage, 2);
            _inventory.GiveItem(ItemIds.FirstAid, 1);
            _inventory.GiveItem(ItemIds.EnergyDrink, 1);

            SetHealth(60f);
            Assert.IsTrue(_use.TryBeginBestHeal());
            Assert.AreEqual(ItemIds.Bandage, _use.CurrentItemId);
            _use.Cancel();

            SetHealth(25f);
            Assert.IsTrue(_use.TryBeginBestHeal());
            Assert.AreEqual(ItemIds.FirstAid, _use.CurrentItemId);
            _use.Cancel();

            Assert.IsTrue(_use.TryBeginBestBoost());
            Assert.AreEqual(ItemIds.EnergyDrink, _use.CurrentItemId);
        }

        [Test]
        public void NullDependencies_DoNotThrow()
        {
            var use = new ItemUseService(Owner, null, null, null, null);
            Assert.DoesNotThrow(() =>
            {
                Assert.IsFalse(use.CanUse(ItemIds.Bandage));
                Assert.IsFalse(use.TryBegin(ItemIds.Bandage));
                Assert.IsFalse(use.TryBeginBestHeal());
                Assert.IsFalse(use.TryBeginBestBoost());
                use.Tick(1f);
                use.Cancel();
            });
            Assert.AreEqual(1f, use.MovementSpeedMultiplier, 1e-5f);
        }

        // ------------------------------------------------------------------ BoostService

        [Test]
        public void Boost_AddClampsAndResets()
        {
            var boost = new BoostService();
            Assert.AreEqual(0f, boost.Value, 1e-5f);

            boost.Add(70f);
            boost.Add(70f);
            Assert.AreEqual(BoostService.Max, boost.Value, 1e-5f);
            Assert.AreEqual(1f, boost.Normalized, 1e-5f);

            boost.Add(-20f);
            boost.Add(float.NaN);
            Assert.AreEqual(BoostService.Max, boost.Value, 1e-5f);

            boost.Reset();
            Assert.AreEqual(0f, boost.Value, 1e-5f);
        }

        [Test]
        public void Boost_DecaysAtFixedRate()
        {
            var boost = new BoostService();
            boost.Add(40f);
            boost.Tick(10f, null, false);
            Assert.AreEqual(40f - 10f * BoostService.DecayPerSecond, boost.Value, 1e-3f);

            boost.Tick(1000f, null, false);
            Assert.AreEqual(0f, boost.Value, 1e-5f);
        }

        [Test]
        public void Boost_SpeedBonusAboveSixty()
        {
            var boost = new BoostService();
            boost.Add(60f);
            Assert.AreEqual(1f, boost.SpeedMultiplier, 1e-5f);
            boost.Add(1f);
            Assert.AreEqual(1.04f, boost.SpeedMultiplier, 1e-5f);
        }

        [Test]
        public void Boost_HealRateGrowsWithValue()
        {
            Assert.AreEqual(0f, BoostService.HealRateFor(0f), 1e-5f);
            Assert.Greater(BoostService.HealRateFor(10f), 0f);
            Assert.Greater(BoostService.HealRateFor(50f), BoostService.HealRateFor(10f));
            Assert.Greater(BoostService.HealRateFor(90f), BoostService.HealRateFor(50f));
        }

        [Test]
        public void Boost_HealsLivingTargetInChunks()
        {
            var boost = new BoostService();
            var target = new HealTarget();
            boost.Add(30f);

            for (var i = 0; i < 100; i++)
                boost.Tick(0.1f, target, true);

            // 10 sn * 0.5 can/sn = 5 can, en az 1'lik parçalarla.
            Assert.AreEqual(10f * BoostService.HealTier1Rate, target.Total, 0.51f);
            Assert.LessOrEqual(target.Calls, 6);
            Assert.AreEqual(30f - 10f * BoostService.DecayPerSecond, boost.Value, 1e-2f);
        }

        [Test]
        public void Boost_FullBarHealsExpectedTotal()
        {
            var boost = new BoostService();
            var target = new HealTarget();
            boost.Add(100f);

            for (var i = 0; i < 4000 && boost.Value > 0f; i++)
                boost.Tick(0.05f, target, true);

            Assert.AreEqual(0f, boost.Value, 1e-5f);
            var expected = (20f * BoostService.HealTier3Rate + 40f * BoostService.HealTier2Rate + 40f * BoostService.HealTier1Rate)
                / BoostService.DecayPerSecond;
            Assert.AreEqual(expected, target.Total, 1.5f);
        }

        [Test]
        public void Boost_DoesNotHealDeadTarget()
        {
            var boost = new BoostService();
            var target = new HealTarget();
            boost.Add(100f);
            boost.Tick(5f, target, false);

            Assert.AreEqual(0, target.Calls);
            Assert.Less(boost.Value, 100f);
        }

        [Test]
        public void Boost_IntegratesWithHealthService()
        {
            SetHealth(50f);
            _boost.Add(100f);
            for (var i = 0; i < 100; i++)
                _boost.Tick(0.1f, _health, _health.IsAlive);

            Assert.Greater(_health.Current, 50f);
            Assert.LessOrEqual(_health.Current, 100f);
        }
    }
}
