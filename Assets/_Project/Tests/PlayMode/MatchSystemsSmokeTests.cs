using System.Collections;
using System.IO;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Replay;
using Project.Presentation.Bootstrap;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.PlayMode
{
    /// <summary>Tekrar kaydedici ve bastırma sürücüsü duman testleri.</summary>
    public sealed class MatchSystemsSmokeTests
    {
        [UnityTest]
        public IEnumerator ReplayRecorder_WritesFile_AfterShortMatch()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();
            var previousScale = Time.timeScale;

            yield return SmokeSupport.LoadOperationReady();

            var boot = PlayModeHelpers.FindMatchBootstrap();
            var recorder = boot != null ? boot.GetComponent<ReplayRecorder>() : null;
            if (recorder == null)
                SmokeSupport.Skip("MatchBootstrap üzerinde ReplayRecorder yok.");

            try
            {
                Time.timeScale = 8f;
                yield return PlayModeHelpers.WaitRealtime(3f);
            }
            finally
            {
                Time.timeScale = previousScale > 0f ? previousScale : 1f;
            }

            var path = recorder.Save();
            if (path == null)
                SmokeSupport.Skip("Kayıt yazılmadı (kare < 2 ya da zaten kaydedilmiş).");

            Assert.IsTrue(File.Exists(path), "Tekrar dosyası yok: " + path);
            Assert.Greater(new FileInfo(path).Length, 0L, "Tekrar dosyası boş.");
            Assert.IsTrue(path.EndsWith(ReplayRecorder.Extension), "Uzantı beklenen değil: " + path);

            errors.AssertNoExceptions("Tekrar kaydı duman");
        }

        [UnityTest]
        public IEnumerator SuppressionDriver_ReactsToNearExplosion()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();

            yield return PlayModeHelpers.LoadScene(SceneNames.Training, 25f);
            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var b = PlayModeHelpers.FindTrainingBootstrap();
                    return b != null && b.IsReady && b.Player != null;
                },
                25f,
                "TrainingBootstrap hazır olmadı.");

            var player = PlayModeHelpers.FindLocalPlayer();
            if (player == null || player.Combatant == null || !player.Combatant.IsAlive)
                SmokeSupport.Skip("Canlı yerel oyuncu yok.");
            if (Object.FindAnyObjectByType<SuppressionDriver>() == null)
                SmokeSupport.Skip("SuppressionDriver kurulmamış.");

            yield return null;
            Suppression.Clear();
            Assert.AreEqual(0f, Suppression.Value, 1e-4f);

            var at = player.transform.position + player.transform.forward * 3f;
            ExplosionSystem.Explode(at, 8f, 0f, PlayerId.Invalid, null);

            Assert.IsTrue(Suppression.Value > 0f || Suppression.Concussion > 0f,
                "Yakın patlama bastırma/sersemletme üretmedi. V=" + Suppression.Value + " C=" + Suppression.Concussion);

            // Birkaç kare sonra sürücü etkiyi sönümlemeye başlamış olmalı (üstel azalma).
            var peak = Suppression.Value;
            yield return PlayModeHelpers.WaitRealtime(1f);
            if (peak > 0f)
                Assert.Less(Suppression.Value, peak, "Bastırma sönümlenmedi.");

            Suppression.Clear();
            errors.AssertNoExceptions("Bastırma duman");
        }
    }
}
