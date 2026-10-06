using static Project.Infrastructure.Audio.SynthDsp;

namespace Project.Infrastructure.Audio.HdrMix
{
    /// <summary>Miks katmanının prosedürel sesleri (kulak çınlaması). Saf C#.</summary>
    public static class MixSynth
    {
        public const float RingLoopSeconds = 1f;

        /// <summary>
        /// Kulak çınlaması döngüsü: ~3.8 kHz ana ton + ikinci ton + hafif titreşim. Frekanslar döngüde tam sayı
        /// çevrim olduğundan kesintisizdir.
        /// </summary>
        public static float[] RenderTinnitusRing()
        {
            var b = Buffer(RingLoopSeconds);
            for (var i = 0; i < b.Length; i++)
            {
                var t = (double)i / SampleRate;
                var wobble = 1f + 0.12f * Sin(t * 5.0);
                b[i] = (Sin(t * 3800.0) * 0.6f + Sin(t * 6100.0) * 0.25f + Sin(t * 2450.0) * 0.15f) * wobble;
            }

            Normalize(b, 0.7f);
            return b;
        }
    }
}
