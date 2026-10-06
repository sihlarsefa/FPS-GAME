using Project.Infrastructure.World.Lobby;
using UnityEngine;

namespace Project.Presentation.Lobby
{
    /// <summary>
    /// Yavaş kamera parallax'ı: imleç konumu + çok yavaş salınım. Kamerayı kendisi sürmez; <see cref="Offset"/> değerini
    /// kamera rig'i yerel konumuna ekler. Uzak katmanlar (dağ silueti) için <see cref="LayerFactor"/> kullanılır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyParallax : MonoBehaviour
    {
        public float MaxMeters = 0.18f;
        public float Rate = 1.6f;
        public Vector3 Offset { get; private set; }

        public static float LayerFactor(float depthMeters) => LobbyCinematicMath.LayerFactor(depthMeters);

        private void Update()
        {
            var pointer = Vector2.zero;
            try
            {
                if (UnityEngine.Screen.width > 0 && UnityEngine.Screen.height > 0)
                {
                    var mp = UnityEngine.Input.mousePosition;
                    pointer = new Vector2(mp.x / UnityEngine.Screen.width * 2f - 1f, mp.y / UnityEngine.Screen.height * 2f - 1f);
                }
            }
            catch (System.Exception) { /* Yeni Input System: imleç yok, yalnız salınım. */ }

            var target = LobbyCinematicMath.ParallaxOffset(pointer, Time.unscaledTime, MaxMeters);
            Offset = LobbyCinematicMath.Damp(Offset, target, Mathf.Min(Time.unscaledDeltaTime, 0.1f), Rate);
        }
    }
}
