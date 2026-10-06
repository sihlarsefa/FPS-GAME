using System;

namespace Project.Infrastructure.Audio.Ui
{
    /// <summary>
    /// Ek arayüz sesleri (prosedürel, mono 22.05 kHz). Tasarım dili: onay yükselir, iptal alçalır, hata yumuşak ama ayırt edilir,
    /// seçim "tok" ahşap vuruşudur. Hepsi tek aile: sönümlü sinüs + az kısılmış gürültü, sert kenar yok.
    /// </summary>
    public static class UiSfxExtraSynth
    {
        private const int Rate = UiSfxSynth.SampleRate;

        public static bool Handles(UiSfx k) =>
            k == UiSfx.Select || k == UiSfx.Confirm || k == UiSfx.Cancel || k == UiSfx.ToggleOn || k == UiSfx.ToggleOff;

        public static float[] Render(UiSfx k)
        {
            switch (k)
            {
                case UiSfx.Select: return Select();
                case UiSfx.Confirm: return Confirm();
                case UiSfx.Cancel: return Cancel();
                case UiSfx.ToggleOn: return Toggle(true);
                default: return Toggle(false);
            }
        }

        private static float Sin(double hz, double t) => (float)Math.Sin(2.0 * Math.PI * hz * t);

        /// <summary>Seçim "tok": 310 Hz gövde + 620 Hz harmonik, ~60 ms sönüm, hafif kısık gürültü vuruşu (ahşap).</summary>
        private static float[] Select()
        {
            var s = new float[(int)(0.1f * Rate)];
            float lp = 0f;
            uint r = 0x1234567u;
            for (var i = 0; i < s.Length; i++)
            {
                var t = i / (float)Rate;
                r = r * 1664525u + 1013904223u;
                var n = ((r >> 8) * (1f / 8388608f)) - 1f;
                lp += 0.2f * (n - lp);
                var body = (Sin(310.0, t) + 0.4f * Sin(620.0, t)) * UiSfxSynth.Envelope(t, 0.001f, 0.022f);
                s[i] = body + lp * 0.5f * UiSfxSynth.Envelope(t, 0f, 0.004f);
            }
            return s;
        }

        /// <summary>Onay: yükselen beşli (A4 -> E5), ikinci nota hafif geç ve uzun; üst kısım yumuşak.</summary>
        private static float[] Confirm()
        {
            var s = new float[(int)(0.28f * Rate)];
            const float second = 0.055f;
            for (var i = 0; i < s.Length; i++)
            {
                var t = i / (float)Rate;
                var a = (Sin(440.0, t) + 0.25f * Sin(880.0, t)) * UiSfxSynth.Envelope(t, 0.002f, 0.04f);
                var b = t >= second
                    ? (Sin(659.25, t) + 0.2f * Sin(1318.5, t)) * UiSfxSynth.Envelope(t - second, 0.003f, 0.08f)
                    : 0f;
                s[i] = 0.75f * a + b;
            }
            return s;
        }

        /// <summary>İptal: alçalan beşli (E5 -> A4 yerine D4 -> G3), daha kısa ve kısık; onayın aynası.</summary>
        private static float[] Cancel()
        {
            var s = new float[(int)(0.2f * Rate)];
            const float second = 0.045f;
            for (var i = 0; i < s.Length; i++)
            {
                var t = i / (float)Rate;
                var a = Sin(587.33, t) * UiSfxSynth.Envelope(t, 0.002f, 0.03f);
                var b = t >= second ? Sin(392.0, t) * UiSfxSynth.Envelope(t - second, 0.002f, 0.06f) : 0f;
                s[i] = 0.7f * a + b;
            }
            return s;
        }

        /// <summary>Anahtar: açık = yukarı süpürmeli tık (400->640 Hz), kapalı = aşağı (640->400 Hz).</summary>
        private static float[] Toggle(bool on)
        {
            var s = new float[(int)(0.07f * Rate)];
            double ph = 0;
            for (var i = 0; i < s.Length; i++)
            {
                var t = i / (float)Rate;
                var u = Math.Min(1f, t / 0.05f);
                var f = on ? 400.0 + 240.0 * u : 640.0 - 240.0 * u;
                ph += 2.0 * Math.PI * f / Rate;
                s[i] = (float)Math.Sin(ph) * UiSfxSynth.Envelope(t, 0.0015f, 0.02f);
            }
            return s;
        }
    }
}
