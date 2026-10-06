using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests
{
    public sealed class InteriorAnchorsTests
    {
        [Test]
        public void RegisterSnapshotClear_RoundTrip()
        {
            InteriorAnchors.Clear();
            InteriorAnchors.Register(new Vector3(1f, 0f, 2f), InteriorAnchorKind.Crate, RoomKind.Storage, InteriorAnchors.TierFor(InteriorAnchorKind.Crate));
            var list = new List<InteriorAnchor>();
            InteriorAnchors.Snapshot(list);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(2, list[0].TierHint);
            InteriorAnchors.Clear();
            Assert.AreEqual(0, InteriorAnchors.Count);
        }
    }
}
