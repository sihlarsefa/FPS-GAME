using System.Collections;
using NUnit.Framework;
using Project.Presentation.Bootstrap;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.PlayMode
{
    /// <summary>MainMenu sahnesi yüklenir; hata logu olmaz; menü Canvas'ı vardır.</summary>
    public sealed class MainMenuLoadTests
    {
        [UnityTest]
        public IEnumerator MainMenu_Loads_WithoutErrors_AndHasCanvas()
        {
            using var errors = PlayModeHelpers.BeginExceptionCapture();

            yield return PlayModeHelpers.LoadScene(SceneNames.MainMenu);

            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var boot = PlayModeHelpers.FindMainMenuBootstrap();
                    return boot != null && boot.Menu != null && boot.Menu.Canvas != null;
                },
                30f,
                "MainMenuBootstrap / Canvas hazır olmadı.");

            var bootstrap = PlayModeHelpers.FindMainMenuBootstrap();
            Assert.IsNotNull(bootstrap, "MainMenuBootstrap yok.");
            Assert.IsNotNull(bootstrap.Menu, "MainMenuController yok.");
            Assert.IsNotNull(bootstrap.Menu.Canvas, "Menü Canvas yok.");

            var anyCanvas = Object.FindAnyObjectByType<Canvas>();
            Assert.IsNotNull(anyCanvas, "Sahnede Canvas bulunamadı.");

            yield return PlayModeHelpers.CaptureScreenAndWait("mainmenu");

            errors.AssertNoExceptions("MainMenu yükleme");
        }
    }
}
