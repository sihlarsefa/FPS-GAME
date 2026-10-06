using System.Collections;
using System.IO;
using NUnit.Framework;
using Project.Presentation.Benchmark;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.PlayMode
{
    /// <summary>AAA_Benchmark duman: sahne açılır, Vitrin en az bir planı tamamlar, ekran görüntüsü yazılır.</summary>
    public sealed class AaaBenchmarkSmokeTests
    {
        [UnityTest]
        public IEnumerator AaaBenchmark_Vitrin_CompletesOneShot_AndSavesScreenshot()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();
            var previousScale = Time.timeScale;

            yield return PlayModeHelpers.LoadScene(SceneNames.AaaBenchmark, 25f);

            var bootstrap = Object.FindAnyObjectByType<AaaBenchmarkBootstrap>();
            if (bootstrap == null)
                SmokeSupport.Skip("AAA_Benchmark sahnesinde AaaBenchmarkBootstrap yok.");

            yield return PlayModeHelpers.WaitUntil(() => bootstrap.IsReady, 25f, "AaaBenchmarkBootstrap hazır olmadı.");

            var vitrin = Object.FindAnyObjectByType<AaaBenchmarkVitrin>();
            if (vitrin == null || vitrin.ShotCount < 2)
                SmokeSupport.Skip("Vitrin yok ya da plan sayısı < 2.");

            vitrin.SetMode(BenchmarkMode.Vitrin);
            vitrin.SeekToShot(0, 0f);
            yield return null;
            yield return null;
            Assert.AreEqual(BenchmarkMode.Vitrin, vitrin.Mode);

            var firstShot = vitrin.CurrentShotName;
            Assert.IsFalse(string.IsNullOrEmpty(firstShot), "Vitrin ilk plan adı boş.");

            try
            {
                Time.timeScale = 6f;
                yield return PlayModeHelpers.WaitUntil(
                    () => vitrin.CurrentShotName != firstShot,
                    20f,
                    "Vitrin 20 sn içinde 1. planı bitirmedi: " + firstShot);
            }
            finally
            {
                Time.timeScale = previousScale > 0f ? previousScale : 1f;
            }

            // Ekran görüntüsü: Vitrin'in kendi yolu (asenkron) + senkron yardımcı.
            var dir = AaaBenchmarkVitrin.ScreenshotDirectory();
            var before = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png").Length : 0;
            vitrin.TakeScreenshot();
            yield return new WaitForEndOfFrame();
            yield return PlayModeHelpers.WaitRealtime(0.5f);

            yield return PlayModeHelpers.CaptureScreenAndWait("aaa_benchmark_vitrin");
            var shot = Path.Combine(PlayModeHelpers.ScreensDirectory, "aaa_benchmark_vitrin.png");
            Assert.IsTrue(File.Exists(shot), "Ekran görüntüsü yazılmadı: " + shot);
            Assert.Greater(new FileInfo(shot).Length, 0L, "Ekran görüntüsü boş.");

            var after = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png").Length : 0;
            Debug.Log("[PlayMode] Vitrin png sayısı " + before + " → " + after);

            errors.AssertNoExceptions("AAA Benchmark Vitrin duman");
        }
    }
}
