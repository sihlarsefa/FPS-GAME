using NUnit.Framework;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class MenuSquadLayoutTests
    {
        private static readonly Vector3 Cmd = new Vector3(3.95f, 0f, 2.55f);
        private static readonly Vector3 Veh = new Vector3(7.5f, 0f, 2.9f);

        [Test]
        public void SixSoldiers_OneCommanderWithBeret()
        {
            var s = MenuSquadLayout.Build(Cmd, Veh, 206f);
            Assert.AreEqual(6, s.Length);
            var berets = 0;
            for (var i = 0; i < s.Length; i++)
                if (s[i].Beret) berets++;
            Assert.AreEqual(1, berets);
            Assert.IsTrue(s[0].Pose == SquadPose.Commander);
            Assert.AreEqual(MenuSquadLayout.FacingCameraYaw, s[0].Yaw, 0.01f);
        }

        [Test]
        public void EveryoneIsBattleWorn()
        {
            var s = MenuSquadLayout.Build(Cmd, Veh, 206f);
            for (var i = 0; i < s.Length; i++)
                Assert.IsTrue(s[i].Wear >= MenuSquadLayout.MinSquadWear);
        }

        [Test]
        public void SoldiersDoNotOverlap()
        {
            var s = MenuSquadLayout.Build(Cmd, Veh, 206f);
            for (var i = 0; i < s.Length; i++)
                for (var j = i + 1; j < s.Length; j++)
                    Assert.IsTrue((s[i].Position - s[j].Position).magnitude > 0.8f);
        }

        [Test]
        public void LeanerStaysNextToVehicle_OnCommanderSide()
        {
            var s = MenuSquadLayout.LeaningSlot(Cmd, Veh, 206f);
            Assert.IsTrue((s.Position - Veh).magnitude < 2.5f);
            Assert.IsTrue((s.Position - Cmd).magnitude < (Veh - Cmd).magnitude + 1.5f);
        }

        [Test]
        public void PoseFlags_SeatedAndKneelingAreUnique()
        {
            var s = MenuSquadLayout.Build(Cmd, Veh, 206f);
            int seated = 0, crouch = 0;
            for (var i = 0; i < s.Length; i++)
            {
                if (s[i].Seated) seated++;
                if (s[i].Crouching) crouch++;
            }
            Assert.AreEqual(1, seated);
            Assert.AreEqual(1, crouch);
        }
    }
}
