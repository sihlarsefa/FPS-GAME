using System.Collections;
using NUnit.Framework;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.PlayMode
{
    /// <summary>TrainingRange sahnesi yüklenir; bootstrap hazır olur; hata logu olmaz.</summary>
    public sealed class TrainingRangeLoadTests
    {
        [UnityTest]
        public IEnumerator TrainingRange_Loads_WithoutErrors()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();

            yield return PlayModeHelpers.LoadScene(SceneNames.Training, 90f);

            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var boot = PlayModeHelpers.FindTrainingBootstrap();
                    return boot != null && boot.IsReady && boot.Player != null;
                },
                60f,
                "TrainingBootstrap hazır olmadı.");

            var bootstrap = PlayModeHelpers.FindTrainingBootstrap();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap.IsReady);
            Assert.IsNotNull(bootstrap.Player, "Poligon oyuncusu yok.");
            Assert.IsNotNull(bootstrap.Targets, "Hedef listesi null.");
            Assert.Greater(bootstrap.Targets.Count, 0, "Poligonda hedef yok.");

            yield return PlayModeHelpers.CaptureScreenAndWait("trainingrange");

            errors.AssertNoExceptions("TrainingRange yükleme");
        }
    }
}
