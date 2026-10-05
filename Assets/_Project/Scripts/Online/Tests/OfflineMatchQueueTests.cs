using System;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Online.Profile;
using UnityEngine;

namespace Project.Online.Tests
{
    [TestFixture]
    public sealed class OfflineMatchQueueTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(OfflineMatchQueue.PrefsKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(OfflineMatchQueue.PrefsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void StateMachine_IdlePendingSyncingFailedIdle()
        {
            var queue = new OfflineMatchQueue();
            queue.Load();
            Assert.AreEqual(OfflineQueueState.Idle, queue.State);
            Assert.AreEqual(0, queue.Count);

            var result = new MatchResult(false, 12, 60, 3, 1, 450f, 120f, 0.4f, null, 4, 20, "Tim", 8);
            queue.Enqueue(result);
            Assert.AreEqual(OfflineQueueState.Pending, queue.State);
            Assert.AreEqual(1, queue.Count);
            Assert.IsTrue(queue.TryPeek(out var peek));
            Assert.AreEqual(3, peek.Kills);
            Assert.AreEqual(12, peek.Placement);

            queue.MarkSyncing();
            Assert.AreEqual(OfflineQueueState.Syncing, queue.State);

            queue.MarkFailed("ağ hatası");
            Assert.AreEqual(OfflineQueueState.Failed, queue.State);
            Assert.AreEqual("ağ hatası", queue.LastError);
            Assert.AreEqual(1, queue.Count);

            queue.Dequeue();
            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(OfflineQueueState.Idle, queue.State);
        }

        [Test]
        public void PersistAndReload_KeepsEntries()
        {
            var queue = new OfflineMatchQueue();
            queue.Load();
            queue.Enqueue(new MatchResult(true, 1, 40, 7, 2, 900f, 600f, 0.55f, null));
            queue.Enqueue(new MatchResult(false, 20, 40, 1, 0, 50f, 90f, 0.2f, "Rakip"));

            var reloaded = new OfflineMatchQueue();
            reloaded.Load();
            Assert.AreEqual(2, reloaded.Count);
            Assert.AreEqual(OfflineQueueState.Pending, reloaded.State);
            Assert.IsTrue(reloaded.TryPeek(out var first));
            Assert.IsTrue(first.IsWinner);
            Assert.AreEqual(7, first.Kills);

            reloaded.Dequeue();
            Assert.IsTrue(reloaded.TryPeek(out var second));
            Assert.AreEqual(20, second.Placement);
            Assert.AreEqual(1, second.Kills);

            reloaded.Clear();
            Assert.AreEqual(OfflineQueueState.Idle, reloaded.State);
            Assert.AreEqual(0, reloaded.Count);
            Assert.IsFalse(PlayerPrefs.HasKey(OfflineMatchQueue.PrefsKey));
        }

        [Test]
        public void QueuedMatchEntry_SerializeParse_RoundTrip()
        {
            var when = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            var entry = QueuedMatchEntry.FromResult(
                new MatchResult(true, 2, 50, 4, 1, 321.5f, 200.25f, 0.33f, null, 1, 10, "A", 9),
                when);

            var line = entry.Serialize();
            Assert.IsTrue(QueuedMatchEntry.TryParse(line, out var parsed));
            Assert.IsTrue(parsed.IsWinner);
            Assert.AreEqual(2, parsed.Placement);
            Assert.AreEqual(50, parsed.TotalPlayers);
            Assert.AreEqual(4, parsed.Kills);
            Assert.AreEqual(1, parsed.Headshots);
            Assert.AreEqual(321.5f, parsed.DamageDealt, 1e-4f);
            Assert.AreEqual(200.25f, parsed.SurvivalSeconds, 1e-4f);
            Assert.AreEqual(0.33f, parsed.Accuracy, 1e-4f);
            Assert.AreEqual(1, parsed.TeamPlacement);
            Assert.AreEqual(10, parsed.TeamCount);
            Assert.AreEqual(when.ToUnixTimeSeconds(), parsed.EnqueuedUnix);

            var match = parsed.ToMatchResult();
            Assert.IsTrue(match.IsWinner);
            Assert.AreEqual(4, match.Kills);
        }

        [Test]
        public void TryParse_RejectsGarbage()
        {
            Assert.IsFalse(QueuedMatchEntry.TryParse(null, out _));
            Assert.IsFalse(QueuedMatchEntry.TryParse("", out _));
            Assert.IsFalse(QueuedMatchEntry.TryParse("1|2|3", out _));
        }

        [Test]
        public void MaxEntries_DropsOldest()
        {
            var queue = new OfflineMatchQueue();
            queue.Load();
            for (var i = 0; i < OfflineMatchQueue.MaxEntries + 5; i++)
            {
                queue.Enqueue(new MatchResult(false, i + 1, 60, i, 0, 1f, 1f, 0f, null));
            }

            Assert.AreEqual(OfflineMatchQueue.MaxEntries, queue.Count);
            Assert.IsTrue(queue.TryPeek(out var oldestKept));
            Assert.AreEqual(6, oldestKept.Placement);
        }
    }
}
