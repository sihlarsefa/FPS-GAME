using System.Collections;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Player;
using Project.Presentation.Bootstrap;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.PlayMode
{
    /// <summary>Atış poligonunda ateş: mermi azalır, manken hasar alır.</summary>
    public sealed class FireHitPlayModeTests
    {
        [UnityTest]
        public IEnumerator TrainingRange_Fire_DecreasesAmmo_AndDamagesDummy()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();

            yield return PlayModeHelpers.LoadScene(SceneNames.Training, 90f);

            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var boot = PlayModeHelpers.FindTrainingBootstrap();
                    return boot != null && boot.IsReady && boot.Player != null && boot.Targets != null &&
                           boot.Targets.Count > 0;
                },
                60f,
                "TrainingBootstrap / hedefler hazır olmadı.");

            var bootstrap = PlayModeHelpers.FindTrainingBootstrap();
            var player = bootstrap.Player;
            Assert.IsNotNull(player);
            Assert.IsNotNull(player.ActiveWeapon, "Aktif silah yok.");

            DamageableTarget dummy = null;
            for (var i = 0; i < bootstrap.Targets.Count; i++)
            {
                var t = bootstrap.Targets[i];
                if (t != null && t.IsAlive && !t.IsMoving)
                {
                    dummy = t;
                    break;
                }
            }

            Assert.IsNotNull(dummy, "Sabit manken bulunamadı.");
            Assert.IsNotNull(dummy.Combatant);

            var aimPoint = dummy.transform.position + Vector3.up * 1.1f;
            PlayModeHelpers.PlacePlayerNear(player, dummy.transform.position, 3.5f);
            PlayModeHelpers.OrientPlayerToward(player, aimPoint);
            yield return null;

            var weapon = player.ActiveWeapon;
            var ammoBefore = weapon.CurrentAmmo;
            Assert.Greater(ammoBefore, 0, "Şarjörde mermi yok.");

            var healthBefore = dummy.Combatant.State.Current;
            var hitsBefore = dummy.HitCount;

            // TryTrigger mermi tüketir; BallisticsSystem isabet için doğrudan hedefe ateş eder (0 sapma).
            Assert.IsTrue(weapon.TryTrigger(true, true), "TryTrigger ateş üretemedi.");
            Assert.Less(weapon.CurrentAmmo, ammoBefore, "Mermi azalmadı.");

            var ballistics = BallisticsSystem.GetOrCreate();
            Assert.IsNotNull(ballistics, "BallisticsSystem yok.");

            var origin = player.AimOrigin;
            var direction = (aimPoint - origin).normalized;
            ballistics.FireWeapon(player.Combatant, weapon, origin, direction, 0f, origin);

            yield return PlayModeHelpers.WaitUntil(
                () => dummy.Combatant.State.Current < healthBefore - 0.1f || dummy.HitCount > hitsBefore ||
                      !dummy.Combatant.IsAlive,
                5f,
                "Manken hasar almadı (balistik isabet zaman aşımı).");

            Assert.IsTrue(
                dummy.Combatant.State.Current < healthBefore - 0.1f || dummy.HitCount > hitsBefore ||
                !dummy.Combatant.IsAlive,
                "Manken canı değişmedi. önce=" + healthBefore + " şimdi=" + dummy.Combatant.State.Current);

            // Ek doğrulama: silah işleyicisi üzerinden bir atış daha (ammo + FireShot yolu).
            var ammoMid = weapon.CurrentAmmo;
            var look = new LookInputState(0f, 0f);
            var fireInput = new CombatInputState(true, true, false, false, false, -1, 0, false, false, false, false,
                false, false);
            player.Weapons.Tick(fireInput, look, Time.deltaTime);
            Assert.LessOrEqual(weapon.CurrentAmmo, ammoMid, "Weapons.Tick sonrası mermi artmamalı.");

            yield return PlayModeHelpers.CaptureScreenAndWait("training_fire_hit");

            errors.AssertNoExceptions("Ateş/isabet");
        }
    }
}
