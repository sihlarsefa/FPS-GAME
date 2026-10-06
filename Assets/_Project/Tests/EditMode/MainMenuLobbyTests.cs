using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>Ana menü lobisinin saf mantığı: hareket eğrileri, sayfa geçişi, haber bandı, istatistik çubukları, donanım seçimi, kapak görselleri.</summary>
    public sealed class MainMenuLobbyTests
    {
        private sealed class MemStore : ISettingsStore
        {
            private readonly Dictionary<string, int> _i = new Dictionary<string, int>();
            public bool HasKey(string key) => _i.ContainsKey(key);
            public float GetFloat(string key, float fallback) => fallback;
            public int GetInt(string key, int fallback) => _i.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) { }
            public void SetInt(string key, int value) => _i[key] = value;
            public void Save() { }
        }

        [Test]
        public void Easing_EndpointsAndMonotonic()
        {
            Assert.AreEqual(0f, MainMenuMotion.EaseOutCubic(0f), 1e-5f);
            Assert.AreEqual(1f, MainMenuMotion.EaseOutCubic(1f), 1e-5f);
            Assert.AreEqual(0f, MainMenuMotion.EaseInOutCubic(0f), 1e-5f);
            Assert.AreEqual(1f, MainMenuMotion.EaseInOutCubic(1f), 1e-5f);
            Assert.AreEqual(1f, MainMenuMotion.EaseOutBack(1f), 1e-4f);
            var last = -1f;
            for (var i = 0; i <= 20; i++)
            {
                var v = MainMenuMotion.EaseOutCubic(i / 20f);
                Assert.GreaterOrEqual(v, last);
                last = v;
            }
        }

        [Test]
        public void Approach_ConvergesAndIsFrameRateIndependent()
        {
            var a = 0f;
            for (var i = 0; i < 60; i++) a = MainMenuMotion.Approach(a, 1f, 10f, 1f / 60f);
            var b = 0f;
            for (var i = 0; i < 30; i++) b = MainMenuMotion.Approach(b, 1f, 10f, 1f / 30f);
            Assert.AreEqual(a, b, 1e-3f);
            Assert.Greater(a, 0.99f);
            Assert.AreEqual(5f, MainMenuMotion.Approach(5f, 9f, 10f, 0f));
        }

        [Test]
        public void PageTransition_DurationsWithinBudget()
        {
            Assert.GreaterOrEqual(MainMenuMotion.PageOutSeconds, 0.15f);
            Assert.LessOrEqual(MainMenuMotion.PageInSeconds, 0.25f);

            var p = 0f;
            var t = 0f;
            while (p < 1f && t < 2f) { p = MainMenuMotion.AdvancePage(p, true, 1f / 60f); t += 1f / 60f; }
            Assert.AreEqual(MainMenuMotion.PageInSeconds, t, 0.03f);

            t = 0f;
            while (p > 0f && t < 2f) { p = MainMenuMotion.AdvancePage(p, false, 1f / 60f); t += 1f / 60f; }
            Assert.AreEqual(MainMenuMotion.PageOutSeconds, t, 0.03f);
        }

        [Test]
        public void PageOffset_SlidesInFromRightAndOutToLeft()
        {
            Assert.Greater(MainMenuMotion.PageOffset(0f, 1f), 0f);
            Assert.Less(MainMenuMotion.PageOffset(0f, -1f), 0f);
            Assert.AreEqual(0f, MainMenuMotion.PageOffset(1f, 1f), 1e-4f);
            Assert.AreEqual(1f, MainMenuMotion.PageAlpha(1f), 1e-5f);
        }

        [Test]
        public void Wrap_HandlesNegativeAndZero()
        {
            Assert.AreEqual(4, MainMenuMotion.Wrap(-1, 5));
            Assert.AreEqual(0, MainMenuMotion.Wrap(5, 5));
            Assert.AreEqual(0, MainMenuMotion.Wrap(3, 0));
        }

        [Test]
        public void TickerOffset_ScrollsAndWraps()
        {
            var start = MainMenuMotion.TickerOffset(0f, 70f, 1000f, 800f);
            Assert.AreEqual(800f, start, 1e-3f);
            Assert.Less(MainMenuMotion.TickerOffset(2f, 70f, 1000f, 800f), start);
            var wrapped = MainMenuMotion.TickerOffset(1800f / 70f + 0.01f, 70f, 1000f, 800f);
            Assert.Greater(wrapped, 700f);
        }

        [Test]
        public void CountUp_ReachesTargetAndNeverExceeds()
        {
            Assert.AreEqual(0, MainMenuMotion.CountUp(0, 0.5f));
            Assert.AreEqual(0, MainMenuMotion.CountUp(1200, 0f));
            Assert.AreEqual(1200, MainMenuMotion.CountUp(1200, 1f));
            Assert.LessOrEqual(MainMenuMotion.CountUp(1200, 0.6f), 1200);
        }

        [Test]
        public void SweepAndVignette_StayInRange()
        {
            for (var t = 0f; t < 90f; t += 3.7f)
            {
                var s = MainMenuMotion.SweepPosition(t, 38f);
                Assert.That(s, Is.InRange(0f, 1f));
                Assert.That(MainMenuMotion.VignettePulse(t, 0.5f, 0.07f), Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void PreviewYaw_RotatesUnlessDragging()
        {
            Assert.AreEqual(10f, MainMenuMotion.PreviewYaw(10f, 30f, 1f, true), 1e-4f);
            Assert.AreEqual(40f, MainMenuMotion.PreviewYaw(10f, 30f, 1f, false), 1e-4f);
            Assert.AreEqual(20f, MainMenuMotion.PreviewYaw(350f, 30f, 1f, false), 1e-3f);
        }

        [Test]
        public void ModeCatalog_HasFiveDistinctModes()
        {
            var modes = MenuModeCatalog.Modes;
            Assert.AreEqual(5, modes.Count);
            var ids = new HashSet<string>();
            foreach (var m in modes)
            {
                Assert.IsTrue(ids.Add(m.Id));
                Assert.IsFalse(string.IsNullOrEmpty(m.Title));
                Assert.IsFalse(string.IsNullOrEmpty(m.Description));
                Assert.That(m.ArtIndex, Is.InRange(0, MainMenuKeyArt.ModeCount - 1));
            }

            Assert.IsTrue(modes[MenuModeCatalog.IndexOf(MenuModeCatalog.BattleRoyale)].NeedsMap);
            Assert.IsFalse(modes[MenuModeCatalog.IndexOf(MenuModeCatalog.Range)].NeedsMap);
            Assert.AreEqual(-1, MenuModeCatalog.IndexOf("yok"));
        }

        [Test]
        public void KeyArt_ModesAndMapsAreDeterministicAndFilled()
        {
            for (var i = 0; i < MainMenuKeyArt.ModeCount; i++)
            {
                var a = MainMenuKeyArt.Mode(i, 96, 60);
                var b = MainMenuKeyArt.Mode(i, 96, 60);
                Assert.AreEqual(96 * 60, a.Pixels.Length);
                CollectionAssert.AreEqual(a.Pixels, b.Pixels, "Mod " + i + " deterministik değil.");
                Assert.AreEqual(255, a.Pixels[5].a);
            }

            var seen = new HashSet<int>();
            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var art = MainMenuKeyArt.Map(MapCatalog.IdAt(i), 96, 60);
                var sum = 0;
                foreach (var p in art.Pixels) sum += p.r + p.g * 3 + p.b * 7;
                seen.Add(sum);
            }

            Assert.AreEqual(MapCatalog.Count, seen.Count, "Haritaların kapakları birbirinden farklı olmalı.");
        }

        [Test]
        public void ArtCanvas_PrimitivesDrawInsideBounds()
        {
            var c = new ArtCanvas(32, 32);
            c.FillCircle(16, 16, 6, Color.white);
            c.Line(-10, -10, 50, 50, 2f, Color.red);
            c.Polygon(new[] { new Vector2(2, 2), new Vector2(10, 2), new Vector2(6, 12) }, Color.blue);
            Assert.AreEqual(255, c.Pixels[16 * 32 + 16].r);
            Assert.Greater(c.Pixels[6 * 32 + 6].b, 0);
        }

        [Test]
        public void LoadoutStats_AreNormalizedAndAttachmentsShiftThem()
        {
            foreach (var w in WeaponCatalog.All)
            {
                var s = LoadoutStats.Compute(w);
                for (var i = 0; i < 5; i++)
                    Assert.That(LoadoutStats.Value(s, i), Is.InRange(0f, 1f), w.WeaponId + " çubuk " + i);
            }

            var rifle = WeaponCatalog.Get(WeaponIds.Mpt76);
            var plain = LoadoutStats.Compute(rifle);
            var kitted = LoadoutStats.Compute(rifle, new[] { ItemIds.VerticalGrip, ItemIds.Scope4x });
            Assert.Greater(kitted.Control, plain.Control, "Dikey tutamak kontrolü artırmalı.");
            Assert.Less(kitted.Mobility, plain.Mobility, "Ağırlık hareketi düşürmeli.");
            Assert.Greater(kitted.WeightKg, plain.WeightKg);

            var withMag = LoadoutStats.Compute(rifle, new[] { ItemIds.ExtMag });
            Assert.Greater(withMag.MagazineSize, plain.MagazineSize);

            // Uyumsuz eklenti yok sayılır: tabancaya 4x dürbün.
            var pistol = WeaponCatalog.Get(WeaponIds.Sar9);
            Assert.AreEqual(LoadoutStats.Compute(pistol).Control, LoadoutStats.Compute(pistol, new[] { ItemIds.Scope4x }).Control, 1e-5f);
        }

        [Test]
        public void LoadoutSelection_PersistsRoleWeaponsAndAttachments()
        {
            var store = new MemStore();
            var sel = new LoadoutSelection(store);
            Assert.AreEqual(TeamRole.Rifleman, sel.Role);
            sel.Role = TeamRole.Marksman;
            Assert.AreEqual(TeamRole.Marksman, new LoadoutSelection(store).Role);
            Assert.AreEqual(WeaponIds.Jng90, sel.PrimaryId, "Seçim yokken rolün silahı.");

            sel.PrimaryId = WeaponIds.Mpt76;
            Assert.AreEqual(WeaponIds.Mpt76, new LoadoutSelection(store).PrimaryId);
            sel.SecondaryId = WeaponIds.Tp9;
            Assert.AreEqual(WeaponIds.Tp9, sel.SecondaryId);

            sel.SetAttachment(AttachmentSlot.Sight, ItemIds.Scope4x);
            Assert.AreEqual(ItemIds.Scope4x, sel.GetAttachment(AttachmentSlot.Sight));
            Assert.IsTrue(sel.AttachmentIds().Contains(ItemIds.Scope4x));

            // Silah uyumsuz eklentiyle değişirse (tabanca benzeri kategori yok → keskin nişancıda şarjör yok) eski kayıt görünmez.
            sel.PrimaryId = WeaponIds.Jng90;
            sel.SetAttachment(AttachmentSlot.Magazine, ItemIds.ExtMag);
            Assert.IsNull(sel.GetAttachment(AttachmentSlot.Magazine));
        }

        [Test]
        public void LoadoutSelection_CandidatesAndCycle()
        {
            var primary = LoadoutSelection.PrimaryCandidates();
            var secondary = LoadoutSelection.SecondaryCandidates();
            Assert.IsFalse(primary.Contains(WeaponIds.Sar9));
            Assert.IsTrue(primary.Contains(WeaponIds.Mpt76));
            Assert.IsTrue(secondary.Contains(WeaponIds.Sar9));

            Assert.AreEqual(primary[1], LoadoutSelection.Cycle(primary, primary[0], 1));
            Assert.AreEqual(primary[primary.Count - 1], LoadoutSelection.Cycle(primary, primary[0], -1));
            Assert.AreEqual(primary[0], LoadoutSelection.Cycle(primary, "yok", 1));

            var opts = LoadoutSelection.CompatibleAttachments(AttachmentSlot.Sight, WeaponCategory.AssaultRifle);
            Assert.IsNull(opts[0]);
            Assert.Greater(opts.Count, 1);
            Assert.AreNotEqual(0, LoadoutSelection.Hash("a"));
            Assert.AreNotEqual(LoadoutSelection.Hash("a"), LoadoutSelection.Hash("b"));
        }

        [Test]
        public void Ticker_ComposesAllHeadlines()
        {
            var text = MenuTicker.Compose();
            foreach (var h in MenuTicker.Headlines)
                StringAssert.Contains(h, text);
            Assert.Greater(MenuTicker.Speed, 0f);
        }

        [Test]
        public void MapInfo_HasTextForEveryMap()
        {
            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var id = MapCatalog.IdAt(i);
                Assert.IsFalse(string.IsNullOrEmpty(MenuMapInfo.Tagline(id)));
                StringAssert.Contains("m", MenuMapInfo.SizeText(id));
            }
        }
    }
}
