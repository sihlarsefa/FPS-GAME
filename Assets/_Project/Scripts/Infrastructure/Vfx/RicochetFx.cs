using Project.Infrastructure.Audio;
using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Sekme görsel + işitsel efekti: isabet noktasından dağılan kıvılcım çizgileri (kısa parlak izler) ve sapmış
    /// mermi izi, ıslık sesi. Tüm rastgelelik atış tohumundan gelir (deterministik).
    /// </summary>
    public static class RicochetFx
    {
        private const int SparkCount = 5;
        private const float WhineVolume = 0.55f;

        /// <summary>
        /// <paramref name="deflected"/> = sekme sonrası yön. Dinleyici yoksa ses atlanır; görsel yine çizilir.
        /// </summary>
        public static void Play(Vector3 point, Vector3 normal, Vector3 deflected, uint seed, bool hasListener,
            Vector3 listener, float audioDistance)
        {
            try
            {
                var n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
                for (var i = 0; i < SparkCount; i++)
                {
                    var spread = new Vector3(RicochetRules.Roll01(seed, 40 + i * 3) - 0.5f,
                        RicochetRules.Roll01(seed, 41 + i * 3) - 0.5f, RicochetRules.Roll01(seed, 42 + i * 3) - 0.5f);
                    var dir = (deflected * 0.8f + n * 0.35f + spread * 0.9f).normalized;
                    var len = 0.25f + 0.55f * RicochetRules.Roll01(seed, 60 + i);
                    GameVfx.Tracer(point + n * 0.02f, point + n * 0.02f + dir * len, 0.07f, 0.012f);
                }

                // Sapmış mermi izi.
                GameVfx.Tracer(point + n * 0.03f, point + n * 0.03f + deflected * 14f, 0.06f, 0.02f);

                if (hasListener)
                    ImpactAudio.PlayRicochet(point, listener, seed, WhineVolume, audioDistance);
            }
            catch (System.Exception)
            {
                // Efekt isteğe bağlıdır.
            }
        }
    }
}
