using UnityEngine;

namespace Project.Presentation.Lobby.CameraWork
{
    /// <summary>
    /// Hafif el kamerası titreşimi (saf, deterministik). Üç oktav yumuşak değer gürültüsü: 0,35 / 1,1 / 3,3 Hz (ağırlık 1 / 0,5 / 0,15).
    /// Operatör konumdan çok DÖNÜŞTE titrer: dönüş ~0,10 derece, konum ~2 mm. Dar FOV'da (tele) açısal etki büyür; bu yüzden genlik
    /// FOV / 40 ile ölçeklenir. Açılışta 1,2 sn'de yumuşak devreye girer, geçişte ~%40 güçlenir (kamera taşınırken omuz oynar).
    /// </summary>
    public struct LobbyHandheldSample
    {
        public float YawDegrees;
        public float PitchDegrees;
        public float RollDegrees;
        public Vector3 PositionOffset;
    }

    public static class LobbyHandheld
    {
        public const float BaseRotationDegrees = 0.10f;
        public const float BasePositionMeters = 0.002f;
        public const float RampSeconds = 1.2f;
        public const float TransitBoost = 0.4f;
        public const float ReferenceFov = 40f;

        public static readonly float[] Frequencies = { 0.35f, 1.1f, 3.3f };
        public static readonly float[] Weights = { 1f, 0.5f, 0.15f };

        private static float Hash(int n)
        {
            unchecked
            {
                var x = (uint)n;
                x ^= x >> 16; x *= 0x7feb352dU;
                x ^= x >> 15; x *= 0x846ca68bU;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
        }

        /// <summary>Tek boyutlu yumuşak gürültü, aralık [-1, 1]; tamsayı düğümler arası smoothstep.</summary>
        public static float Noise(float x, int seed)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) return 0f;
            var i = Mathf.FloorToInt(x);
            var f = x - i;
            var u = f * f * (3f - 2f * f);
            var a = Hash(i * 7919 + seed * 104729);
            var b = Hash((i + 1) * 7919 + seed * 104729);
            return a + (b - a) * u;
        }

        /// <summary>Çok oktavlı gürültü, ağırlık toplamıyla normalize ([-1, 1]).</summary>
        public static float Fbm(float time, int seed)
        {
            var sum = 0f;
            var wsum = 0f;
            for (var o = 0; o < Frequencies.Length; o++)
            {
                sum += Noise(time * Frequencies[o] + o * 13.7f, seed + o * 31) * Weights[o];
                wsum += Weights[o];
            }

            return sum / wsum;
        }

        /// <summary>
        /// Genlik çarpanı: FOV ölçeği (0,6..1,6), açılış rampası, çekim ölçeği ve geçiş güçlenmesi.
        /// </summary>
        public static float Amplitude(float fov, float timeSinceStart, float shotScale, float transit01)
        {
            var fovScale = Mathf.Clamp(fov / ReferenceFov, 0.6f, 1.6f);
            var ramp = LobbyEasing.Smoothstep(timeSinceStart / RampSeconds);
            var transit = 1f + TransitBoost * LobbyEasing.Clamp01(transit01);
            return fovScale * ramp * Mathf.Max(0f, shotScale) * transit;
        }

        public static LobbyHandheldSample Sample(float time, float fov, float timeSinceStart, float shotScale, float transit01)
        {
            var amp = Amplitude(fov, timeSinceStart, shotScale, transit01);
            return new LobbyHandheldSample
            {
                YawDegrees = Fbm(time, 11) * BaseRotationDegrees * amp,
                PitchDegrees = Fbm(time, 23) * BaseRotationDegrees * 0.8f * amp,
                RollDegrees = Fbm(time, 37) * BaseRotationDegrees * 0.5f * amp,
                PositionOffset = new Vector3(Fbm(time, 41), Fbm(time, 53), Fbm(time, 67)) * (BasePositionMeters * amp)
            };
        }
    }
}
