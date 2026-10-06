using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.Characters;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Project.Tests.PlayMode
{
    /// <summary>
    /// Ana menü duman testi: 6 asker modeli (renderer sayısı, kök etrafı hacim), kamera, UI kökleri,
    /// 10 sn exception yok, TİM/DONANIM/SEZON sayfa geçişleri hatasız. Sistem yoksa güvenli skip.
    /// </summary>
    public sealed class MainMenuSmokeTests
    {
        private const int ExpectedModels = 6;
        private const int MinRenderers = 60;
        private const float MaxHorizontal = 1f;
        private const float MinY = -0.3f;
        private const float MaxY = 2.3f;

        [UnityTest]
        public IEnumerator MainMenu_Soldiers_Camera_Ui_AndPageSwitches_AreHealthy()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();

            yield return PlayModeHelpers.LoadScene(SceneNames.MainMenu, 30f);

            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var b = PlayModeHelpers.FindMainMenuBootstrap();
                    return b != null && b.Menu != null && b.Menu.Canvas != null;
                },
                25f,
                "MainMenuBootstrap / Canvas hazır olmadı.");

            var boot = PlayModeHelpers.FindMainMenuBootstrap();
            if (boot == null || boot.Menu == null)
            {
                SmokeSupport.Skip("Ana menü sistemi yok.");
                yield break;
            }

            // Modellerin kurulması için kısa bekleme.
            yield return PlayModeHelpers.WaitRealtime(1.5f);

            var models = Object.FindObjectsByType<SoldierModel>(FindObjectsSortMode.None);
            if (models.Length == 0)
            {
                SmokeSupport.Skip("Menüde SoldierModel yok (dekor kurulmamış).");
                yield break;
            }

            Assert.AreEqual(ExpectedModels, models.Length, "Menüde 6 SoldierModel beklenir.");

            foreach (var model in models)
            {
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                Assert.Greater(renderers.Length, MinRenderers, model.name + ": renderer sayısı yetersiz.");

                var root = model.transform;
                var socket = model.WeaponSocket;
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (socket != null && r.transform.IsChildOf(socket)) continue; // silah gövde parçası değil
                    var local = root.InverseTransformPoint(r.bounds.center);
                    var horizontal = new Vector2(local.x, local.z).magnitude;
                    Assert.LessOrEqual(horizontal, MaxHorizontal,
                        model.name + "/" + r.name + " kökten " + horizontal.ToString("F2") + " m yatay uzakta.");
                    Assert.GreaterOrEqual(local.y, MinY, model.name + "/" + r.name + " çok aşağıda: " + local.y.ToString("F2"));
                    Assert.LessOrEqual(local.y, MaxY, model.name + "/" + r.name + " çok yukarıda: " + local.y.ToString("F2"));
                }
            }

            Assert.Greater(Camera.allCamerasCount, 0, "Menüde kamera yok.");
            var activeCam = false;
            foreach (var c in Camera.allCameras)
                if (c != null && c.isActiveAndEnabled) activeCam = true;
            Assert.IsTrue(activeCam, "Etkin kamera yok.");

            Assert.IsTrue(boot.Menu.Canvas.gameObject.activeInHierarchy, "Menü Canvas aktif değil.");
            Assert.IsTrue(boot.Menu.isActiveAndEnabled, "MainMenuController aktif değil.");

            // Sayfa geçişleri: TİM / DONANIM / SEZON aç-kapa.
            foreach (var label in new[] { "TİM", "DONANIM", "SEZON" })
            {
                var button = FindButton(boot.Menu.Canvas, label);
                if (button == null)
                {
                    Debug.Log("[PlayMode] Menü düğmesi bulunamadı, atlandı: " + label);
                    continue;
                }

                button.onClick.Invoke();
                yield return PlayModeHelpers.WaitRealtime(0.6f);
                if (boot.Menu.IsPanelOpen)
                {
                    boot.Menu.Back();
                    yield return PlayModeHelpers.WaitRealtime(0.4f);
                }

                boot.Menu.Back();
                yield return PlayModeHelpers.WaitRealtime(0.4f);
            }

            // Toplam ~10 sn exception gözlemi.
            yield return PlayModeHelpers.WaitRealtime(5f);

            Assert.IsNotNull(PlayModeHelpers.FindMainMenuBootstrap(), "Menü bootstrap kayboldu.");
            errors.AssertNoExceptions("MainMenu duman testi");
        }

        private static Button FindButton(Canvas canvas, string label)
        {
            var buttons = canvas.GetComponentsInChildren<Button>(false);
            var found = new List<Button>(buttons);
            foreach (var b in found)
            {
                var texts = b.GetComponentsInChildren<Text>(true);
                foreach (var t in texts)
                    if (t != null && t.text != null && t.text.Trim() == label)
                        return b;
            }

            return null;
        }
    }
}
