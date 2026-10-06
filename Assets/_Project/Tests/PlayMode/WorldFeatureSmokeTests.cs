using System.Collections;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.AI;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.PlayMode
{
    /// <summary>KuzgunVadisi görsel/dünya sistemleri duman testleri; özellik yoksa atlanır.</summary>
    public sealed class WorldFeatureSmokeTests
    {
        [UnityTest]
        public IEnumerator Ragdoll_SpawnsOnBotDeath_ThenSettles()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();
            var previousScale = Time.timeScale;

            yield return SmokeSupport.LoadOperationReady();
            yield return PlayModeHelpers.WaitUntil(() => PlayModeHelpers.CountAliveBots() >= 1, 10f, "Bot oluşmadı.");
            BotController victim = null;
            var all = BotController.All;
            for (var i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b != null && b.isActiveAndEnabled && b.Model != null && b.Combatant != null && b.Combatant.IsAlive &&
                    !b.IsSeated)
                {
                    victim = b;
                    break;
                }
            }

            if (victim == null)
                SmokeSupport.Skip("Araçta olmayan, modeli olan bot yok.");
            if (!victim.Model.RagdollEnabled)
                SmokeSupport.Skip("Ragdoll kapalı.");

            var model = victim.Model;
            victim.Combatant.ApplyDamage(new DamageInfo(9999f, PlayerId.Invalid, "test_kill", false));
            Assert.IsFalse(victim.Combatant.IsAlive, "Bot öldürülemedi.");

            try
            {
                yield return PlayModeHelpers.WaitUntil(() => model == null || model.IsRagdoll, 5f, "Ölümde ragdoll başlamadı.");
                if (model == null)
                    SmokeSupport.Skip("Model ölümde yok edildi.");

                var rig = model.GetComponent<RagdollRig>();
                if (rig == null)
                    SmokeSupport.Skip("RagdollRig yok (prosedürel düşüş kullanılmış).");

                Assert.IsTrue(rig.Active, "Ragdoll fiziği etkin değil.");
                Assert.GreaterOrEqual(RagdollRig.ActiveCount, 1);
                var hips = rig.Body(RagdollPart.Hips);
                Assert.IsNotNull(hips, "Kalça rigidbody yok.");
                Assert.IsFalse(hips.isKinematic, "Kalça kinematik; fizik etkin olmalı.");

                Time.timeScale = 4f;
                yield return PlayModeHelpers.WaitUntil(
                    () => rig == null || !rig.Active || hips == null ||
                          (hips.linearVelocity.sqrMagnitude < 0.01f && hips.IsSleeping()),
                    20f,
                    "Ragdoll 20 sn içinde durulmadı.");
            }
            finally
            {
                Time.timeScale = previousScale > 0f ? previousScale : 1f;
            }

            errors.AssertNoExceptions("Ragdoll duman");
        }

        [UnityTest]
        public IEnumerator Grass_InstallsOnTerrain_WithoutExceptions()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();
            yield return SmokeSupport.LoadOperationReady();

            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                SmokeSupport.Skip("Sahnede Terrain yok.");
            var cam = SmokeSupport.FindCamera();
            if (cam == null)
                SmokeSupport.Skip("Kamera yok.");

            var grass = GrassSystem.Install(cam, terrain, 2);
            Assert.IsNotNull(grass, "GrassSystem kurulamadı.");
            yield return null;
            yield return PlayModeHelpers.WaitRealtime(1f);

            Assert.IsNotNull(GrassSystem.Instance);
            Assert.GreaterOrEqual(grass.CellCount, 0);
            Debug.Log("[PlayMode] Çim hücre=" + grass.CellCount + " çizilen=" + grass.LastDrawnInstances);

            errors.AssertNoExceptions("Çim kurulumu duman");
        }

        [UnityTest]
        public IEnumerator KuzgunVadisi_HasWaterAndSkyObjects()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();
            yield return SmokeSupport.LoadOperationReady();
            yield return PlayModeHelpers.WaitRealtime(0.5f);

            var water = GameObject.Find(WorldWater.RootName);
            Assert.IsNotNull(water, "Su kök nesnesi ('" + WorldWater.RootName + "') yok.");
            Assert.Greater(water.transform.childCount, 0, "Su yüzeyi çocuğu yok.");

            var sky = Object.FindAnyObjectByType<SkyEnvironment>();
            Assert.IsNotNull(sky, "SkyEnvironment yok.");

            errors.AssertNoExceptions("Su/gökyüzü duman");
        }

        [UnityTest]
        public IEnumerator QualityTier_SwitchesZeroToThreeToZero_WithoutExceptions()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();
            yield return SmokeSupport.LoadOperationReady();

            var cam = SmokeSupport.FindCamera();
            if (cam == null)
                SmokeSupport.Skip("Kamera yok.");

            var tiers = new[] { 0, 3, 0 };
            for (var i = 0; i < tiers.Length; i++)
            {
                QualityTierApplier.Apply(tiers[i], cam);
                yield return null;
                yield return PlayModeHelpers.WaitRealtime(0.3f);
                Assert.AreEqual(tiers[i], QualityTierApplier.LastTier, "Kademe uygulanmadı: " + tiers[i]);
            }

            errors.AssertNoExceptions("Kalite kademesi geçişi");
        }
    }
}
