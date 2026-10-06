using System.Collections;
using NUnit.Framework;
using Project.Presentation.Bootstrap;
using UnityEngine;

namespace Project.Tests.PlayMode
{
    /// <summary>Yeni duman testleri için kısa, sınırlı bekleme yardımcıları (her test &lt; 60 sn).</summary>
    internal static class SmokeSupport
    {
        /// <summary>Harekât sahnesini yükler ve MatchBootstrap hazır olana dek bekler (toplam ≤ ~50 sn).</summary>
        public static IEnumerator LoadOperationReady()
        {
            yield return PlayModeHelpers.LoadScene(SceneNames.Operation, 25f);
            yield return PlayModeHelpers.WaitUntil(
                () =>
                {
                    var boot = PlayModeHelpers.FindMatchBootstrap();
                    return boot != null && boot.IsReady && boot.Player != null;
                },
                25f,
                "MatchBootstrap hazır olmadı.");
        }

        /// <summary>Dünya kamerası: oyuncu kamerası ya da Camera.main (yoksa null).</summary>
        public static Camera FindCamera()
        {
            var player = PlayModeHelpers.FindLocalPlayer();
            if (player != null && player.CameraRig != null && player.CameraRig.WorldCamera != null)
                return player.CameraRig.WorldCamera;
            return Camera.main;
        }

        public static void Skip(string message)
        {
            Debug.Log("[PlayMode] ATLANDI: " + message);
            Assert.Ignore(message);
        }
    }
}
