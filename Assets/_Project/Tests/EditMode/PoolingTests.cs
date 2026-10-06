using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.Pooling;

namespace Project.Tests.EditMode
{
    public sealed class PoolingTests
    {
        private sealed class Item { public bool On; }

        private static ObjectPool<Item> MakePool(int max = 4)
            => new ObjectPool<Item>(() => new Item(), i => i.On = true, i => i.On = false, null, max);

        [Test]
        public void Pool_ReusesAndCountsMisses()
        {
            var p = MakePool();
            var a = p.Get();
            Assert.IsTrue(a.On);
            p.Release(a);
            Assert.IsFalse(a.On);
            var b = p.Get();
            Assert.IsTrue(ReferenceEquals(a, b));
            Assert.AreEqual(1, p.Stats.Misses);
            Assert.AreEqual(1, p.Stats.Active);
        }

        [Test]
        public void Pool_DoubleReleaseIgnored()
        {
            var p = MakePool();
            var a = p.Get();
            Assert.IsTrue(p.Release(a));
            Assert.IsFalse(p.Release(a));
            Assert.AreEqual(1, p.Stats.DoubleReleases);
            Assert.AreEqual(1, p.CountInactive);
        }

        [Test]
        public void Pool_OverflowDestroysAndPrewarmFillsToMax()
        {
            var destroyed = 0;
            var p = new ObjectPool<Item>(() => new Item(), null, null, i => destroyed++, 2);
            Assert.AreEqual(2, p.Prewarm(10));
            var x = p.Get(); var y = p.Get(); var z = p.Get();
            p.Release(x); p.Release(y);
            p.Release(z);
            Assert.AreEqual(1, destroyed);
            Assert.AreEqual(1, p.Stats.Overflows);
            Assert.AreEqual(2, p.Trim(0));
            Assert.AreEqual(3, destroyed);
        }

        [Test]
        public void Pool_PeakActiveTracked()
        {
            var p = MakePool(8);
            var a = p.Get(); var b = p.Get(); var c = p.Get();
            p.Release(a); p.Release(b); p.Release(c);
            Assert.AreEqual(3, p.Stats.PeakActive);
            Assert.AreEqual(0, p.Stats.Active);
        }

        [Test]
        public void Pool_ForgetAdjustsCounters()
        {
            var p = MakePool();
            p.Get();
            p.Forget();
            Assert.AreEqual(0, p.CountActive);
            Assert.AreEqual(0, p.CountAll);
        }

        [Test]
        public void ListPool_ReturnsClearedAndReuses()
        {
            List<int> first;
            using (ListPool<int>.Get(out var l)) { l.Add(5); first = l; }
            var again = ListPool<int>.Get();
            Assert.IsTrue(ReferenceEquals(first, again));
            Assert.AreEqual(0, again.Count);
            ListPool<int>.Release(again);
        }

        [Test]
        public void DictionaryAndHashSetPools_Clear()
        {
            Dictionary<int, int> d;
            using (DictionaryPool<int, int>.Get(out var x)) { x[1] = 2; d = x; }
            Assert.AreEqual(0, d.Count);
            HashSet<string> h;
            using (HashSetPool<string>.Get(out var s)) { s.Add("a"); h = s; }
            Assert.AreEqual(0, h.Count);
        }

        [Test]
        public void RingBuffer_OverwritesOldest()
        {
            var r = new RingBuffer<int>(3);
            r.Add(1); r.Add(2); r.Add(3);
            var full = r.Add(4, out var evicted);
            Assert.IsTrue(full);
            Assert.AreEqual(1, evicted);
            Assert.AreEqual(2, r[0]);
            Assert.AreEqual(4, r[2]);
            Assert.AreEqual(4, r.Newest);
            var buf = new int[3];
            Assert.AreEqual(3, r.CopyTo(buf));
            Assert.AreEqual(3, buf[1]);
        }

        [Test]
        public void RingBuffer_PartialOrderAndOutOfRange()
        {
            var r = new RingBuffer<int>(5);
            r.Add(7); r.Add(8);
            Assert.AreEqual(2, r.Count);
            Assert.AreEqual(7, r[0]);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => { var _ = r[2]; });
            r.Clear();
            Assert.AreEqual(0, r.Count);
        }

        [Test]
        public void TimedRelease_ReleasesInDueOrder()
        {
            var s = new TimedReleaseScheduler<Item>(4);
            var items = new Item[6];
            for (var i = 0; i < items.Length; i++) items[i] = new Item();
            // Karışık sırada, kapasite büyümesini de tetikler.
            s.Schedule(items[3], 4.0); s.Schedule(items[0], 1.0); s.Schedule(items[5], 6.0);
            s.Schedule(items[1], 2.0); s.Schedule(items[4], 5.0); s.Schedule(items[2], 3.0);
            Assert.AreEqual(1.0, s.NextDue, 0.0001);
            var order = new List<Item>();
            Assert.AreEqual(3, s.Tick(3.5, order.Add));
            Assert.IsTrue(ReferenceEquals(items[0], order[0]));
            Assert.IsTrue(ReferenceEquals(items[1], order[1]));
            Assert.IsTrue(ReferenceEquals(items[2], order[2]));
            Assert.AreEqual(3, s.Count);
            Assert.AreEqual(3, s.Flush(order.Add));
            Assert.AreEqual(0, s.Count);
        }

        [Test]
        public void TimedRelease_MaxPerCallLimitsWork()
        {
            var s = new TimedReleaseScheduler<Item>();
            for (var i = 0; i < 5; i++) s.Schedule(new Item(), i);
            Assert.AreEqual(2, s.Tick(100, null, 2));
            Assert.AreEqual(3, s.Count);
        }

        [Test]
        public void IntStringCache_CachesAndHandlesRange()
        {
            var c = new IntStringCache(0, 99);
            var a = c.Get(42);
            Assert.IsTrue(ReferenceEquals(a, c.Get(42)));
            Assert.AreEqual("42", a);
            Assert.AreEqual("-5", c.Get(-5));
            Assert.AreEqual("1000", c.Get(1000));
            Assert.AreEqual(1, c.CachedCount);
        }

        [Test]
        public void VoiceStealer_PrefersFreeSlotThenLeastImportant()
        {
            var v = new VoiceInfo[3];
            v[0] = new VoiceInfo { Active = true, Priority = 0, Loudness = 1f, Age = 1f };
            v[1] = new VoiceInfo { Active = true, Priority = 5, Loudness = 0.1f, Age = 6f };
            v[2] = new VoiceInfo { Active = true, Priority = 2, Loudness = 0.5f, Age = 3f };
            Assert.AreEqual(1, VoiceStealer.PickVictim(v, 3, 0, 1f));
            v[2].Active = false;
            Assert.AreEqual(2, VoiceStealer.PickVictim(v, 3, 9, 0f));
        }

        [Test]
        public void VoiceStealer_DropsLessImportantNewVoice()
        {
            var v = new VoiceInfo[2];
            v[0] = new VoiceInfo { Active = true, Priority = 0, Loudness = 1f, Age = 0.5f };
            v[1] = new VoiceInfo { Active = true, Priority = 1, Loudness = 0.9f, Age = 0.5f };
            Assert.AreEqual(-1, VoiceStealer.PickVictim(v, 2, 8, 0.1f));
            Assert.AreEqual(-1, VoiceStealer.PickVictim(null, 0, 0, 1f));
        }

        [Test]
        public void VoiceStealer_MaxVoicesByTier()
        {
            Assert.AreEqual(24, VoiceStealer.MaxVoices(-3));
            Assert.AreEqual(48, VoiceStealer.MaxVoices(2));
            Assert.AreEqual(64, VoiceStealer.MaxVoices(9));
        }
    }
}
