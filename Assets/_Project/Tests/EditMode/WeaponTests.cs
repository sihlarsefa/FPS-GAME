using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    /// <summary>WeaponCatalog ve WeaponRuntimeService: denge değerleri, ateş modları, şarjör, sekme, sapma.</summary>
    [TestFixture]
    public sealed class WeaponTests
    {
        private const float Eps = 1e-3f;

        private sealed class RecordingBus : IEventBus
        {
            public readonly List<object> Events = new();

            public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent => Events.Add(gameEvent);
            public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent { }
            public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent { }

            public int Count<T>()
            {
                var n = 0;
                foreach (var e in Events)
                {
                    if (e is T)
                        n++;
                }

                return n;
            }

            public T Last<T>()
            {
                for (var i = Events.Count - 1; i >= 0; i--)
                {
                    if (Events[i] is T t)
                        return t;
                }

                throw new InvalidOperationException("Olay yok: " + typeof(T).Name);
            }
        }

        private sealed class FakeAmmo : IAmmoSource
        {
            public readonly Dictionary<AmmoType, int> Stock = new();

            public int GetAmmo(AmmoType type) => Stock.TryGetValue(type, out var n) ? n : 0;

            public int TakeAmmo(AmmoType type, int maxAmount)
            {
                var have = GetAmmo(type);
                var take = Math.Min(have, Math.Max(0, maxAmount));
                Stock[type] = have - take;
                return take;
            }
        }

        private RecordingBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new RecordingBus();
        }

        private WeaponRuntimeService Create(string id, int loaded = -1)
        {
            return new WeaponRuntimeService(WeaponCatalog.Get(id), _bus, loaded) { OwnerId = new PlayerId(7) };
        }

        /// <summary>Hazır olana kadar küçük adımlarla ilerletir; geçen süreyi döndürür.</summary>
        private static float TickUntilReady(WeaponRuntimeService weapon, float step = 0.01f, float limit = 10f)
        {
            var elapsed = 0f;
            while (weapon.TimeUntilReady > 0f && elapsed < limit)
            {
                weapon.Tick(step);
                elapsed += step;
            }

            return elapsed;
        }

        // ---------------------------------------------------------------- Catalog

        [Test]
        public void Catalog_ContainsAllTenTurkishWeapons()
        {
            var ids = new[]
            {
                WeaponIds.Sar9, WeaponIds.Tp9, WeaponIds.Sar109, WeaponIds.Mpt55, WeaponIds.Mpt76,
                WeaponIds.G3, WeaponIds.Knt76, WeaponIds.Jng90, WeaponIds.Pmt76, WeaponIds.Escort
            };

            Assert.GreaterOrEqual(WeaponCatalog.All.Count, 10);
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                Assert.IsTrue(WeaponCatalog.TryGet(id, out var def), id);
                Assert.IsNotNull(def);
                Assert.AreEqual(id, def.WeaponId);
                Assert.AreSame(def, WeaponCatalog.Get(id));
                Assert.IsTrue(seen.Add(id));
                Assert.AreNotEqual(AmmoType.None, def.AmmoType, id);
                Assert.IsTrue(def.FireModes != null && def.FireModes.Length > 0, id);
                Assert.Greater(def.MuzzleVelocity, 100f, id);
                Assert.Greater(def.HipSpread, def.AdsSpread, id);
                Assert.Greater(def.FalloffEnd, def.FalloffStart, id);
                Assert.Greater(def.EquipSeconds, 0f, id);
                Assert.Greater(def.ReloadDurationSeconds, 0f, id);
                Assert.IsFalse(string.IsNullOrEmpty(def.DisplayName), id);
                Assert.AreNotEqual(id, def.DisplayName, id);
            }
        }

        [TestCase(WeaponIds.Sar9, 28f, 15)]
        [TestCase(WeaponIds.Tp9, 26f, 18)]
        [TestCase(WeaponIds.Sar109, 22f, 30)]
        [TestCase(WeaponIds.Mpt55, 26f, 30)]
        [TestCase(WeaponIds.Mpt76, 36f, 20)]
        [TestCase(WeaponIds.G3, 38f, 20)]
        [TestCase(WeaponIds.Knt76, 52f, 10)]
        [TestCase(WeaponIds.Jng90, 90f, 5)]
        [TestCase(WeaponIds.Pmt76, 34f, 100)]
        [TestCase(WeaponIds.Escort, 20f, 7)]
        public void Catalog_DamageAndMagazineBalance(string id, float damage, int magazine)
        {
            var def = WeaponCatalog.Get(id);
            Assert.AreEqual(damage, def.Damage, Eps);
            Assert.AreEqual(magazine, def.MagazineSize);
        }

        [TestCase(WeaponIds.Sar109, 800f)]
        [TestCase(WeaponIds.Mpt55, 750f)]
        [TestCase(WeaponIds.Mpt76, 600f)]
        [TestCase(WeaponIds.G3, 550f)]
        [TestCase(WeaponIds.Pmt76, 650f)]
        public void Catalog_RoundsPerMinute(string id, float rpm)
        {
            Assert.AreEqual(rpm, WeaponCatalog.RoundsPerMinute(WeaponCatalog.Get(id)), 1f);
        }

        [Test]
        public void Catalog_SpecialWeaponTraits()
        {
            var jng = WeaponCatalog.Get(WeaponIds.Jng90);
            Assert.IsTrue(jng.IsBoltAction);
            Assert.IsTrue(jng.HasScope);
            Assert.AreEqual(6f, jng.AdsZoom, Eps);
            Assert.AreEqual(2.5f, jng.HeadshotMultiplier, Eps);
            Assert.AreEqual(1.4f, jng.FireIntervalSeconds, 0.05f);
            Assert.AreEqual(AmmoType.Mm762, jng.AmmoType);

            var knt = WeaponCatalog.Get(WeaponIds.Knt76);
            Assert.IsTrue(knt.HasScope);
            Assert.AreEqual(3f, knt.AdsZoom, Eps);
            Assert.IsFalse(knt.SupportsFireMode(FireMode.Auto));

            var escort = WeaponCatalog.Get(WeaponIds.Escort);
            Assert.AreEqual(9, escort.PelletCount);
            Assert.AreEqual(AmmoType.Gauge12, escort.AmmoType);
            Assert.Greater(escort.PelletSpread, 0f);

            var pmt = WeaponCatalog.Get(WeaponIds.Pmt76);
            Assert.AreEqual(6f, pmt.ReloadDurationSeconds, Eps);
            Assert.IsTrue(pmt.SupportsFireMode(FireMode.Auto));

            Assert.AreEqual(AmmoType.Mm556, WeaponCatalog.Get(WeaponIds.Mpt55).AmmoType);
            Assert.AreEqual(AmmoType.Mm9, WeaponCatalog.Get(WeaponIds.Sar109).AmmoType);
            Assert.IsTrue(WeaponCatalog.Get(WeaponIds.Sar9).IsSidearm);
            Assert.IsFalse(WeaponCatalog.Get(WeaponIds.Sar9).SupportsFireMode(FireMode.Auto));
        }

        [Test]
        public void Catalog_DisplayNames()
        {
            Assert.AreEqual("MPT-76", WeaponCatalog.GetDisplayName(WeaponIds.Mpt76));
            Assert.AreEqual("Harekât Sınırı", WeaponCatalog.GetDisplayName(DamageSourceIds.Zone));
            Assert.AreEqual("Düşme", WeaponCatalog.GetDisplayName(DamageSourceIds.Fall));
            Assert.AreEqual("El Bombası", WeaponCatalog.GetDisplayName(DamageSourceIds.FragGrenade));
            Assert.AreEqual("Yumruk", WeaponCatalog.GetDisplayName(DamageSourceIds.Fists));
            Assert.AreEqual("Topçu Ateşi", WeaponCatalog.GetDisplayName(DamageSourceIds.Artillery));
            Assert.AreEqual("Araç", WeaponCatalog.GetDisplayName(DamageSourceIds.Vehicle));
            Assert.AreEqual("bilinmeyen_kaynak", WeaponCatalog.GetDisplayName("bilinmeyen_kaynak"));
            Assert.AreEqual(WeaponCatalog.UnknownSourceName, WeaponCatalog.GetDisplayName(null));
            Assert.AreEqual(WeaponCatalog.UnknownSourceName, WeaponCatalog.GetDisplayName(string.Empty));
        }

        [Test]
        public void Catalog_UnknownIdIsSafe()
        {
            Assert.IsNull(WeaponCatalog.Get("yok"));
            Assert.IsNull(WeaponCatalog.Get(null));
            Assert.IsFalse(WeaponCatalog.TryGet("yok", out var def));
            Assert.IsNull(def);
            Assert.IsFalse(WeaponCatalog.TryGet(null, out _));
            Assert.IsFalse(WeaponCatalog.Contains(null));
            Assert.IsTrue(WeaponCatalog.Contains(WeaponIds.G3));
        }

        // ---------------------------------------------------------------- Construction

        [Test]
        public void Runtime_ConstructorAmmoAndDefaults()
        {
            Assert.AreEqual(20, Create(WeaponIds.Mpt76).CurrentAmmo);
            Assert.AreEqual(5, Create(WeaponIds.Mpt76, 5).CurrentAmmo);
            Assert.AreEqual(0, Create(WeaponIds.Mpt76, 0).CurrentAmmo);
            Assert.AreEqual(20, Create(WeaponIds.Mpt76, 999).CurrentAmmo);
            Assert.AreEqual(FireMode.Auto, Create(WeaponIds.Mpt55).CurrentFireMode);
            Assert.AreEqual(FireMode.Single, Create(WeaponIds.Sar9).CurrentFireMode);
            Assert.AreEqual(0, Create(WeaponIds.Mpt76).ReserveAmmo, "IWeaponRuntime: AmmoSource yoksa 0");
            Assert.IsTrue(Create(WeaponIds.Mpt76).HasInfiniteReserve);
            Assert.IsTrue(Create(WeaponIds.Mpt76).CanFire);
            Assert.Throws<ArgumentNullException>(() => new WeaponRuntimeService(null, _bus));
        }

        [Test]
        public void Runtime_SetLoadedAmmoClamps()
        {
            var w = Create(WeaponIds.Mpt76);
            w.SetLoadedAmmo(-5);
            Assert.AreEqual(0, w.CurrentAmmo);
            Assert.IsFalse(w.CanFire);
            w.SetLoadedAmmo(500);
            Assert.AreEqual(20, w.CurrentAmmo);
            w.SetLoadedAmmo(7);
            Assert.AreEqual(7, w.CurrentAmmo);
        }

        // ---------------------------------------------------------------- Fire modes

        [Test]
        public void Single_RequiresPressedEdge()
        {
            var w = Create(WeaponIds.Sar9);
            Assert.IsTrue(w.TryTrigger(true, true));
            Assert.AreEqual(14, w.CurrentAmmo);

            TickUntilReady(w);
            Assert.IsFalse(w.TryTrigger(true, false), "Basılı tutmak tek atışta yeni atış üretmemeli");
            Assert.AreEqual(14, w.CurrentAmmo);

            Assert.IsTrue(w.TryTrigger(true, true));
            Assert.AreEqual(13, w.CurrentAmmo);
        }

        [Test]
        public void Single_RespectsCooldownAndBuffersEarlyPress()
        {
            var w = Create(WeaponIds.Sar9);
            Assert.IsTrue(w.TryTrigger(true, true));
            w.Tick(0.1f);
            Assert.IsFalse(w.TryTrigger(true, true), "Soğuma bitmeden atış olmaz");
            Assert.AreEqual(14, w.CurrentAmmo);

            // 0.16 s aralık: 0.07 s sonra hazır; tampon (0.12 s) atışı tetik bırakılmış olsa da üretir.
            w.Tick(0.07f);
            Assert.IsTrue(w.TryTrigger(false, false));
            Assert.AreEqual(13, w.CurrentAmmo);

            // Tampon tüketildi: ikinci kez atış yok.
            TickUntilReady(w);
            Assert.IsFalse(w.TryTrigger(false, false));
        }

        [Test]
        public void Single_BufferExpires()
        {
            var w = Create(WeaponIds.Jng90);
            Assert.IsTrue(w.TryTrigger(true, true));
            w.Tick(0.2f);
            Assert.IsFalse(w.TryTrigger(true, true));
            TickUntilReady(w);
            Assert.IsFalse(w.TryTrigger(false, false), "Süresi dolan tampon atış üretmemeli");
            Assert.AreEqual(4, w.CurrentAmmo);
        }

        [Test]
        public void Auto_FiresWhileHeldAtRatedRpm()
        {
            var w = Create(WeaponIds.Mpt55); // 750 rpm, 30'luk şarjör
            Assert.AreEqual(FireMode.Auto, w.CurrentFireMode);

            var shots = 0;
            const float dt = 1f / 60f;
            for (var frame = 0; frame < 120; frame++) // 2 saniye @60fps
            {
                if (w.TryTrigger(true, frame == 0))
                    shots++;
                w.Tick(dt);
            }

            // 2 s'de 25 atış (750 rpm) alınır; şarjör sınırı 30.
            Assert.AreEqual(25, shots, 1f);
        }

        [Test]
        public void Auto_RateIndependentOfFrameRate()
        {
            var w = Create(WeaponIds.Pmt76); // 650 rpm, 100'lük şarjör
            var shots = 0;
            const float dt = 1f / 30f;
            for (var frame = 0; frame < 150; frame++) // 5 s @30fps
            {
                if (w.TryTrigger(true, false))
                    shots++;
                w.Tick(dt);
            }

            Assert.AreEqual(650f / 60f * 5f, shots, 3f);
        }

        [Test]
        public void Auto_StopsWhenReleasedAndRespectsInterval()
        {
            var w = Create(WeaponIds.Mpt76); // 0.1 s
            Assert.IsTrue(w.TryTrigger(true, true));
            w.Tick(0.05f);
            Assert.IsFalse(w.TryTrigger(true, false));
            w.Tick(0.05f);
            Assert.IsTrue(w.TryTrigger(true, false));
            TickUntilReady(w);
            Assert.IsFalse(w.TryTrigger(false, false));
            Assert.AreEqual(18, w.CurrentAmmo);
        }

        [Test]
        public void Burst_FiresBurstCountShotsFromSinglePress()
        {
            var w = Create(WeaponIds.Mpt55);
            Assert.IsTrue(w.TrySetFireMode(FireMode.Burst));
            var interval = w.FireIntervalSeconds;

            Assert.IsTrue(w.TryTrigger(true, true));
            Assert.IsTrue(w.IsBurstActive);
            Assert.IsFalse(w.TryTrigger(false, false), "Burst aralığı beklenmeli");

            w.Tick(interval);
            Assert.IsTrue(w.TryTrigger(false, false), "Burst tetik bırakılsa da devam eder");
            w.Tick(interval);
            Assert.IsTrue(w.TryTrigger(true, false));
            Assert.IsFalse(w.IsBurstActive);
            Assert.AreEqual(27, w.CurrentAmmo);

            // Tetik basılı kalsa da yeni burst için yeni basış gerekir.
            for (var i = 0; i < 100; i++)
            {
                w.Tick(0.01f);
                Assert.IsFalse(w.TryTrigger(true, false));
            }

            Assert.IsTrue(w.TryTrigger(true, true));
            Assert.AreEqual(26, w.CurrentAmmo);
        }

        [Test]
        public void Burst_StopsWhenMagazineEmpties()
        {
            var w = Create(WeaponIds.Mpt55, 2);
            w.TrySetFireMode(FireMode.Burst);
            Assert.IsTrue(w.TryTrigger(true, true));
            w.Tick(w.FireIntervalSeconds);
            Assert.IsTrue(w.TryTrigger(true, false));
            Assert.AreEqual(0, w.CurrentAmmo);
            Assert.IsFalse(w.IsBurstActive);
            w.Tick(1f);
            Assert.IsFalse(w.TryTrigger(true, false));
        }

        [Test]
        public void CycleFireMode_CyclesAndWraps()
        {
            var w = Create(WeaponIds.Mpt55); // Single, Burst, Auto → varsayılan Auto
            Assert.AreEqual(FireMode.Auto, w.CurrentFireMode);
            w.CycleFireMode();
            Assert.AreEqual(FireMode.Single, w.CurrentFireMode);
            w.CycleFireMode();
            Assert.AreEqual(FireMode.Burst, w.CurrentFireMode);
            w.CycleFireMode();
            Assert.AreEqual(FireMode.Auto, w.CurrentFireMode);

            var pistol = Create(WeaponIds.Tp9);
            pistol.CycleFireMode();
            Assert.AreEqual(FireMode.Single, pistol.CurrentFireMode);
            Assert.IsFalse(pistol.TrySetFireMode(FireMode.Auto));
        }

        [Test]
        public void BoltAction_CyclesBetweenShots()
        {
            var w = Create(WeaponIds.Jng90);
            Assert.IsTrue(w.TryTrigger(true, true));
            Assert.IsTrue(w.IsCyclingBolt);
            Assert.IsFalse(w.CanFire);

            w.Tick(1.0f);
            Assert.IsFalse(w.TryTrigger(true, true));
            Assert.IsTrue(w.IsCyclingBolt);

            w.Tick(0.45f);
            Assert.IsFalse(w.IsCyclingBolt);
            Assert.IsTrue(w.TryTrigger(true, true));
            Assert.AreEqual(3, w.CurrentAmmo);
        }

        [Test]
        public void EmptyMagazine_DoesNotFireAndRaisesDryFire()
        {
            var w = Create(WeaponIds.Sar9, 1);
            var dry = 0;
            w.DryFired += _ => dry++;
            Assert.IsTrue(w.TryTrigger(true, true));
            TickUntilReady(w);
            Assert.IsFalse(w.TryTrigger(true, true));
            Assert.IsFalse(w.CanFire);
            Assert.AreEqual(0, w.CurrentAmmo);
            Assert.AreEqual(1, dry);
            Assert.IsFalse(w.TryFire(out _));
        }

        [Test]
        public void TryFire_ProducesDamageInfoForOwner()
        {
            var w = Create(WeaponIds.G3);
            Assert.IsTrue(w.TryFire(out var info));
            Assert.AreEqual(38f, info.Amount, Eps);
            Assert.AreEqual(new PlayerId(7), info.AttackerId);
            Assert.AreEqual(WeaponIds.G3, info.SourceWeaponId);
            Assert.AreEqual(19, w.CurrentAmmo);
            Assert.IsFalse(w.TryFire(out _), "Soğuma sırasında TryFire başarısız olmalı");
            Assert.AreEqual(1, w.ShotsFired);
        }

        [Test]
        public void BeginEquip_BlocksFireForEquipTime()
        {
            var w = Create(WeaponIds.Pmt76);
            w.BeginEquip();
            Assert.IsTrue(w.IsEquipping);
            Assert.IsFalse(w.TryTrigger(true, true));
            w.Tick(w.Definition.EquipSeconds * 0.5f);
            Assert.IsFalse(w.TryTrigger(true, false));
            w.Tick(w.Definition.EquipSeconds * 0.5f + 0.01f);
            Assert.IsFalse(w.IsEquipping);
            Assert.IsTrue(w.TryTrigger(true, false));
        }

        // ---------------------------------------------------------------- Reload

        [Test]
        public void Reload_TakesFromReserveAndPublishesEvents()
        {
            var ammo = new FakeAmmo();
            ammo.Stock[AmmoType.Mm762] = 50;
            var w = Create(WeaponIds.Mpt76, 4);
            w.AmmoSource = ammo;

            Assert.AreEqual(50, w.ReserveAmmo);
            Assert.IsTrue(w.TryBeginReload());
            Assert.IsTrue(w.IsReloading);
            Assert.IsFalse(w.TryBeginReload(), "Zaten değiştiriliyor");
            Assert.AreEqual(1, _bus.Count<WeaponReloadStartedEvent>());
            var started = _bus.Last<WeaponReloadStartedEvent>();
            Assert.AreEqual(new PlayerId(7), started.ShooterId);
            Assert.AreEqual(WeaponIds.Mpt76, started.WeaponId);
            Assert.AreEqual(2.5f, started.DurationSeconds, Eps);

            w.Tick(1.25f);
            Assert.AreEqual(0.5f, w.ReloadProgress, 0.01f);
            Assert.AreEqual(4, w.CurrentAmmo, "Süre dolmadan mermi eklenmez");
            Assert.AreEqual(0, _bus.Count<WeaponReloadedEvent>());

            w.Tick(1.3f);
            Assert.IsFalse(w.IsReloading);
            Assert.AreEqual(20, w.CurrentAmmo);
            Assert.AreEqual(34, w.ReserveAmmo);
            Assert.AreEqual(1, _bus.Count<WeaponReloadedEvent>());
            var reloaded = _bus.Last<WeaponReloadedEvent>();
            Assert.AreEqual(new PlayerId(7), reloaded.ShooterId);
            Assert.AreEqual(20, reloaded.AmmoAfterReload);
        }

        [Test]
        public void Reload_PartialReserve()
        {
            var ammo = new FakeAmmo();
            ammo.Stock[AmmoType.Mm556] = 5;
            var w = Create(WeaponIds.Mpt55, 0);
            w.AmmoSource = ammo;
            Assert.IsTrue(w.TryBeginReload());
            w.Tick(10f);
            Assert.AreEqual(5, w.CurrentAmmo);
            Assert.AreEqual(0, w.ReserveAmmo);
            Assert.IsFalse(w.TryBeginReload(), "Yedek bitti");
        }

        [Test]
        public void Reload_RequiresReserveOrInfiniteSource()
        {
            var w = Create(WeaponIds.Mpt76, 0);
            w.AmmoSource = new FakeAmmo();
            Assert.AreEqual(0, w.ReserveAmmo);
            Assert.IsFalse(w.HasInfiniteReserve);
            Assert.IsFalse(w.TryBeginReload());
            Assert.IsFalse(w.CanReload);
            Assert.AreEqual(0, _bus.Count<WeaponReloadStartedEvent>());

            w.AmmoSource = null; // sınırsız
            Assert.AreEqual(0, w.ReserveAmmo);
            Assert.IsTrue(w.HasInfiniteReserve);
            Assert.IsTrue(w.CanReload);
            Assert.IsTrue(w.TryBeginReload());
            w.Tick(5f);
            Assert.AreEqual(20, w.CurrentAmmo);
        }

        [Test]
        public void Reload_FullMagazineRejected()
        {
            var w = Create(WeaponIds.Mpt76);
            Assert.IsFalse(w.TryBeginReload());
            Assert.IsFalse(w.IsReloading);
        }

        [Test]
        public void Reload_WrongAmmoTypeNotUsed()
        {
            var ammo = new FakeAmmo();
            ammo.Stock[AmmoType.Mm9] = 100;
            var w = Create(WeaponIds.Mpt76, 0);
            w.AmmoSource = ammo;
            Assert.IsFalse(w.TryBeginReload());
            Assert.AreEqual(100, ammo.Stock[AmmoType.Mm9]);
        }

        [Test]
        public void CancelReload_KeepsAmmo()
        {
            var w = Create(WeaponIds.Mpt76, 3);
            Assert.IsTrue(w.TryBeginReload());
            w.Tick(1f);
            w.CancelReload();
            Assert.IsFalse(w.IsReloading);
            Assert.AreEqual(0f, w.ReloadProgress, Eps);
            w.Tick(5f);
            Assert.AreEqual(3, w.CurrentAmmo);
            Assert.AreEqual(0, _bus.Count<WeaponReloadedEvent>());
        }

        [Test]
        public void Trigger_DuringReloadWithAmmoInterrupts_EmptyDoesNot()
        {
            var w = Create(WeaponIds.Mpt76, 3);
            Assert.IsTrue(w.TryBeginReload());
            Assert.IsFalse(w.TryTrigger(true, false), "Basılı tutmak değiştirmeyi kesmez");
            Assert.IsTrue(w.IsReloading);
            Assert.IsTrue(w.TryTrigger(true, true));
            Assert.IsFalse(w.IsReloading);
            Assert.AreEqual(2, w.CurrentAmmo);

            var empty = Create(WeaponIds.Mpt76, 0);
            Assert.IsTrue(empty.TryBeginReload());
            Assert.IsFalse(empty.TryTrigger(true, true));
            Assert.IsTrue(empty.IsReloading);
        }

        [Test]
        public void Trigger_DoesNotInterruptReloadWhenDisabled()
        {
            var w = Create(WeaponIds.Mpt76, 3);
            w.TriggerInterruptsReload = false;
            Assert.IsTrue(w.TryBeginReload());
            Assert.IsFalse(w.TryTrigger(true, true));
            Assert.IsTrue(w.IsReloading);
            w.Tick(3f);
            Assert.AreEqual(20, w.CurrentAmmo);
        }

        [Test]
        public void InstantReload_FillsFromSource()
        {
            var ammo = new FakeAmmo();
            ammo.Stock[AmmoType.Gauge12] = 3;
            var w = Create(WeaponIds.Escort, 1);
            w.AmmoSource = ammo;
            w.Reload();
            Assert.AreEqual(4, w.CurrentAmmo);
            Assert.AreEqual(0, ammo.Stock[AmmoType.Gauge12]);
            Assert.AreEqual(1, _bus.Count<WeaponReloadedEvent>());
        }

        // ---------------------------------------------------------------- Spread & bloom

        [Test]
        public void Spread_ModifiersApply()
        {
            var w = Create(WeaponIds.Mpt76);
            var def = w.Definition;

            Assert.AreEqual(def.HipSpread, w.GetSpreadAngle(false, 0f, true, Stance.Standing), Eps);
            Assert.AreEqual(def.AdsSpread, w.GetSpreadAngle(true, 0f, true, Stance.Standing), Eps);
            Assert.AreEqual(def.HipSpread * 0.8f, w.GetSpreadAngle(false, 0f, true, Stance.Crouching), Eps);
            Assert.AreEqual(def.HipSpread * 0.6f, w.GetSpreadAngle(false, 0f, true, Stance.Prone), Eps);
            Assert.AreEqual(def.HipSpread * 2.5f, w.GetSpreadAngle(false, 1f, true, Stance.Standing), Eps);
            Assert.AreEqual(def.HipSpread * 1.75f, w.GetSpreadAngle(false, 0.5f, true, Stance.Standing), Eps);
            Assert.AreEqual(def.HipSpread * 2.5f, w.GetSpreadAngle(false, 0f, false, Stance.Standing), Eps);
            Assert.AreEqual(def.HipSpread * 2.5f, w.GetSpreadAngle(false, 5f, true, Stance.Standing), Eps, "Hız 1'e kırpılır");
            Assert.Less(w.GetSpreadAngle(true, 0f, true, Stance.Prone), w.GetSpreadAngle(false, 1f, false, Stance.Standing));
        }

        [Test]
        public void Bloom_GrowsCapsAndAimHalves()
        {
            var w = Create(WeaponIds.Pmt76);
            var def = w.Definition;

            Assert.IsTrue(w.TryTrigger(true, true));
            Assert.AreEqual(def.BloomPerShot, w.CurrentBloom, Eps);
            Assert.AreEqual(def.HipSpread + def.BloomPerShot, w.GetSpreadAngle(false, 0f, true, Stance.Standing), Eps);
            Assert.AreEqual(def.AdsSpread + def.BloomPerShot * 0.5f, w.GetSpreadAngle(true, 0f, true, Stance.Standing), Eps);

            for (var i = 0; i < 60; i++)
            {
                w.Tick(w.FireIntervalSeconds);
                w.TryTrigger(true, false);
            }

            Assert.AreEqual(def.MaxBloom, w.CurrentBloom, Eps);
        }

        [Test]
        public void Bloom_DecaysAboutFourDegreesPerSecond()
        {
            var w = Create(WeaponIds.Pmt76);
            for (var i = 0; i < 40; i++)
            {
                w.TryTrigger(true, false);
                w.Tick(w.FireIntervalSeconds);
            }

            var peak = w.CurrentBloom;
            Assert.Greater(peak, 2f);

            // 0.5 s: en fazla 0.35 s tutma, kalan süre boyunca 4°/s sönme.
            w.Tick(0.5f);
            Assert.LessOrEqual(w.CurrentBloom, peak - 4f * 0.15f + Eps);
            Assert.GreaterOrEqual(w.CurrentBloom, peak - 4f * 0.5f - Eps);

            w.Tick(2f);
            Assert.AreEqual(0f, w.CurrentBloom, Eps);
        }

        [Test]
        public void Recoil_KickScalesWithAimAndStance()
        {
            var w = Create(WeaponIds.G3);
            w.GetRecoilKick(false, Stance.Standing, 1f, out var pitch, out var yaw);
            Assert.AreEqual(w.Definition.RecoilVertical, pitch, Eps);
            Assert.AreEqual(w.Definition.RecoilHorizontal, yaw, Eps);

            w.GetRecoilKick(true, Stance.Prone, 0f, out var pitchProne, out var yawProne);
            Assert.Less(pitchProne, pitch);
            Assert.Less(yawProne, 0f);
        }

        [Test]
        public void Tick_IgnoresInvalidDelta()
        {
            var w = Create(WeaponIds.Mpt76, 0);
            w.TryBeginReload();
            Assert.DoesNotThrow(() =>
            {
                w.Tick(-1f);
                w.Tick(0f);
                w.Tick(float.NaN);
            });
            Assert.IsTrue(w.IsReloading);
            Assert.AreEqual(0f, w.ReloadProgress, Eps);
        }
    }
}
