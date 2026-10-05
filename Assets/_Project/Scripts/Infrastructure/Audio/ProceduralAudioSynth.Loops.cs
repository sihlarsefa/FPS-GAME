using System;
using static Project.Infrastructure.Audio.SynthDsp;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Kesintisiz döngü sesleri. Kural: her periyodik bileşenin frekansı döngü süresine tam sayı periyot sığacak
    /// şekilde seçilir, gürültü katmanları dairesel (iki geçişli) filtrelenir, zaman içinde yerleştirilen olaylar
    /// "wrap" ile tampon başına sarılır. Böylece döngü noktasında tık/kesinti olmaz. Döngülere kenar sönümü uygulanmaz.
    /// </summary>
    public static partial class ProceduralAudioSynth
    {
        /// <summary>Döngü süreleri (saniye) — periyodik bileşenler bunlara göre seçilmiştir.</summary>
        public const float RotorLoopSeconds = 2f;
        public const float EngineLoopSeconds = 2f;
        public const float WindLoopSeconds = 8f;
        public const float AmbienceLoopSeconds = 12f;
        public const float DistantBattleLoopSeconds = 12f;
        public const float MenuMusicLoopSeconds = 16f;
        public const float HeartbeatLoopSeconds = 0.8f;

        private const int ControlStep = 32;
        private const int WarmupSamples = SynthDsp.CircularWarmupSamples;
        private const double TwoPi = Math.PI * 2.0;

        // =====================================================================================
        //  HELİKOPTER ROTORU (T-70) — ~5 Hz pal darbeleri + türbin vınlaması
        // =====================================================================================

        private static float[] HelicopterRotorLoop(ref SynthRng rng)
        {
            const float length = RotorLoopSeconds;
            const int blades = 10; // 5 Hz × 2 s
            const float interval = length / blades;
            var b = Buffer(length);
            var n = b.Length;

            // 1) Pal darbeleri ("vop vop"): alçak gövde + aşağı kayan bas + kısa pal şaklaması.
            for (var k = 0; k < blades; k++)
            {
                var t = k * interval;
                var v = ((k & 1) == 0 ? 1f : 0.86f) * rng.Range(0.9f, 1.05f);
                NoiseBurst(b, ref rng, t, 0.006f, 0.05f, 0.95f * v, FilterKind.Lowpass, 240f, 0.9f, true);
                Tone(b, t, 98f, 50f, 0.02f, 0.004f, 0.055f, 0.85f * v, 0, true);
                NoiseBurst(b, ref rng, t + 0.003f, 0.0015f, 0.014f, 0.32f * v, FilterKind.Bandpass, 650f, 1.1f, true);
            }

            // 2) Rotor yıkaması: alçak geçirilmiş gürültü, pal frekansıyla genlik modülasyonu.
            var wash = WhiteNoise(n, ref rng);
            FilterCircular(wash, FilterKind.Lowpass, 420f, 0.7f);
            var washGain = BandwidthGain(FilterKind.Lowpass, 420f, 0.7f) * 0.32f;

            // 3) Türbin hışırtısı: yüksek geçirilmiş gürültü.
            var roar = WhiteNoise(n, ref rng);
            FilterCircular(roar, FilterKind.Bandpass, 3800f, 0.8f);
            var roarGain = BandwidthGain(FilterKind.Bandpass, 3800f, 0.8f) * 0.07f;

            for (var i = 0; i < n; i++)
            {
                var t = (double)i / SampleRate;
                var bladePhase = t * (blades / length); // 5 Hz
                var am = 0.55f + 0.45f * (0.5f + 0.5f * Sin(bladePhase));

                // Tüm frekanslar 0.5 Hz katı → 2 s'ye tam sayı periyot sığar.
                var whine = Sin(t * 1650.0) * 0.05f + Sin(t * 2475.0) * 0.022f + Sin(t * 3300.0) * 0.012f;
                whine *= 0.85f + 0.15f * Sin(bladePhase + 0.25);
                var gear = Saw(t * 330.0) * 0.035f + Sin(t * 82.5) * 0.06f;

                b[i] += wash[i] * washGain * am + roar[i] * roarGain + whine + gear;
            }

            return FinishLoop(b, 0.85f, 1.3f);
        }

        // =====================================================================================
        //  KİRPİ DİZEL MOTORU — ateşleme darbeleri + takırtı + gövde uğultusu
        // =====================================================================================

        private static float[] VehicleEngineLoop(ref SynthRng rng)
        {
            const float length = EngineLoopSeconds;
            const int firings = 72; // 36 Hz × 2 s
            var b = Buffer(length);
            var n = b.Length;

            var jitter = new float[firings];
            for (var k = 0; k < firings; k++)
                jitter[k] = rng.Range(0.72f, 1f);

            var knock = WhiteNoise(n, ref rng);
            FilterCircular(knock, FilterKind.Bandpass, 1250f, 1.1f);
            var knockGain = BandwidthGain(FilterKind.Bandpass, 1250f, 1.1f) * 0.32f;

            var rumble = WhiteNoise(n, ref rng);
            FilterCircular(rumble, FilterKind.Lowpass, 140f, 0.8f);
            var rumbleGain = BandwidthGain(FilterKind.Lowpass, 140f, 0.8f) * 0.35f;

            var fireRate = firings / (double)length;
            for (var i = 0; i < n; i++)
            {
                var t = (double)i / SampleRate;
                var phase = t * fireRate;
                var cycle = (int)phase;
                var frac = (float)(phase - cycle);
                var amp = jitter[cycle % firings];
                var pulse = (float)Math.Exp(-frac * 5.5f) * amp;
                var sharp = pulse * pulse * pulse;

                var f = t * (fireRate);
                var harmonics = Sin(f) * 0.55f + Sin(f * 2.0) * 0.45f + Sin(f * 3.0) * 0.28f + Sin(f * 4.0) * 0.14f
                                + Saw(f * 0.5) * 0.12f;
                var wobble = 0.9f + 0.1f * Sin(t * 1.0);

                b[i] = harmonics * (0.45f + 0.55f * pulse) * wobble * 0.55f
                       + knock[i] * knockGain * sharp
                       + rumble[i] * rumbleGain * (0.7f + 0.3f * pulse)
                       + Sin(t * 2200.0) * 0.012f;
            }

            FilterCircular(b, FilterKind.Lowpass, 2600f, 0.7f);
            return FinishLoop(b, 0.85f, 1.6f);
        }

        // =====================================================================================
        //  RÜZGÂR — esintili bant gürültüsü + ıslık katmanı
        // =====================================================================================

        private static float[] WindLoop(ref SynthRng rng)
        {
            var b = WindLayer(ref rng, WindLoopSeconds, 1f, 280f, 650f, 650f, 1350f, 0.42f);
            return FinishLoop(b, 0.8f, 0f);
        }

        /// <summary>
        /// Rüzgâr katmanı: düşük gürleme (değişken kesimli alçak geçiren) + ıslık (değişken merkezli dar bant).
        /// Esinti eğrisi döngü süresinin tam katı frekanslı sinüslerden oluşur.
        /// </summary>
        private static float[] WindLayer(ref SynthRng rng, float length, float intensity, float lowMin, float lowMax,
            float whistleMin, float whistleMax, float whistleAmp)
        {
            var b = Buffer(length);
            var n = b.Length;
            var controls = n / ControlStep + 1;

            var p1 = rng.Unit();
            var p2 = rng.Unit();
            var p3 = rng.Unit();
            var p4 = rng.Unit();

            var gust = new float[controls];
            var lowHz = new float[controls];
            var whistleHz = new float[controls];
            for (var c = 0; c < controls; c++)
            {
                var u = (double)c * ControlStep / n; // 0..1 döngü
                var g = 0.55f + 0.22f * Sin(u * 1.0 + p1) + 0.14f * Sin(u * 2.0 + p2) + 0.07f * Sin(u * 5.0 + p3)
                        + 0.04f * Sin(u * 11.0 + p4);
                g = Math.Max(0.12f, g);
                gust[c] = g;
                lowHz[c] = lowMin + (lowMax - lowMin) * Clamp01((g - 0.25f) / 0.7f);
                whistleHz[c] = whistleMin + (whistleMax - whistleMin) * Clamp01(0.5f + 0.5f * Sin(u * 3.0 + p2 + 0.3));
            }

            var noise = WhiteNoise(n, ref rng);
            var low = new float[n];
            var whistle = new float[n];
            SweptFilterCircular(noise, low, FilterKind.Lowpass, lowHz, 0.6f);
            SweptFilterCircular(noise, whistle, FilterKind.Bandpass, whistleHz, 7f);

            var lowGain = BandwidthGain(FilterKind.Lowpass, (lowMin + lowMax) * 0.5f, 0.6f) * 0.5f * intensity;
            var whistleGain = BandwidthGain(FilterKind.Bandpass, (whistleMin + whistleMax) * 0.5f, 7f) * whistleAmp * intensity;
            for (var i = 0; i < n; i++)
            {
                var g = ControlAt(gust, i);
                b[i] = low[i] * lowGain * g + whistle[i] * whistleGain * g * g;
            }

            FilterCircular(b, FilterKind.Highpass, 55f, 0.7f);
            return b;
        }

        // =====================================================================================
        //  ORTAM — hafif rüzgâr + uzak kuş sesleri + yaprak hışırtısı (Kuzgun Vadisi)
        // =====================================================================================

        private static float[] AmbienceLoop(ref SynthRng rng)
        {
            const float length = AmbienceLoopSeconds;
            var b = WindLayer(ref rng, length, 0.75f, 200f, 480f, 520f, 950f, 0.22f);
            var n = b.Length;

            // Yaprak hışırtısı: yüksek bant gürültü, yavaş genlik dalgalanması.
            var leaves = WhiteNoise(n, ref rng);
            FilterCircular(leaves, FilterKind.Bandpass, 4200f, 0.9f);
            var leavesGain = BandwidthGain(FilterKind.Bandpass, 4200f, 0.9f) * 0.018f;
            var ph = rng.Unit();
            for (var i = 0; i < n; i++)
            {
                var u = (double)i / n;
                var m = Clamp01(0.4f + 0.6f * Sin(u * 3.0 + ph) * Sin(u * 7.0 + ph * 2f));
                b[i] += leaves[i] * leavesGain * m;
            }

            // Kuşlar: döngü boyunca dağılmış kısa ötüş dizileri (uzakta, yumuşak).
            var callCount = 9;
            for (var c = 0; c < callCount; c++)
            {
                var start = (c + rng.Range(0.05f, 0.85f)) * (length / callCount);
                var species = rng.RangeInt(0, 3);
                var amp = rng.Range(0.05f, 0.13f);
                BirdCall(b, ref rng, start, species, amp);
            }

            // Çok uzak, belli belirsiz bir gürleme (bölgede tatbikat).
            NoiseBurst(b, ref rng, rng.Range(2f, 9f), 0.08f, 0.9f, 0.12f, FilterKind.Lowpass, 90f, 0.7f, true);

            return FinishLoop(b, 0.75f, 0f);
        }

        private static void BirdCall(float[] b, ref SynthRng rng, float start, int species, float amp)
        {
            var t = start;
            switch (species)
            {
                case 0:
                {
                    // Tekrarlı aşağı kayan cıvıltı: "tsii-tsii-tsii"
                    var count = rng.RangeInt(3, 6);
                    var f = rng.Range(3800f, 5200f);
                    for (var k = 0; k < count; k++)
                    {
                        var d = rng.Range(0.05f, 0.08f);
                        Chirp(b, t, d, f, f * 0.72f, amp * rng.Range(0.8f, 1f), 0f, 0f);
                        t += d + rng.Range(0.06f, 0.1f);
                    }

                    break;
                }
                case 1:
                {
                    // Titreşimli (tril) çağrı
                    var f = rng.Range(2800f, 3600f);
                    var d = rng.Range(0.35f, 0.6f);
                    Chirp(b, t, d, f, f * rng.Range(1.05f, 1.2f), amp * 0.8f, rng.Range(24f, 34f), 0.06f);
                    break;
                }
                default:
                {
                    // İki notalı ıslık: "fii-yuu"
                    var f = rng.Range(2200f, 3000f);
                    Chirp(b, t, 0.16f, f, f * 1.18f, amp, 5f, 0.01f);
                    Chirp(b, t + 0.2f, 0.22f, f * 1.12f, f * 0.84f, amp * 0.85f, 5f, 0.01f);
                    if (rng.Chance(0.5f))
                        Chirp(b, t + 0.5f, 0.16f, f, f * 1.18f, amp * 0.7f, 5f, 0.01f);
                    break;
                }
            }
        }

        /// <summary>Kuş ötüşü: sin² zarflı, üstel frekans kaymalı, isteğe bağlı trilli ton (dairesel yazılır).</summary>
        private static void Chirp(float[] b, float start, float duration, float f0, float f1, float amp,
            float trillHz, float trillDepth)
        {
            var s0 = (int)(start * SampleRate);
            var len = Samples(duration);
            var phase = 0.0;
            var trill = 0.0;
            var step = (float)Math.Pow(f1 / f0, 1.0 / len);
            var f = f0;
            var idx = Index(s0, b.Length, true);
            var bl = b.Length;
            for (var j = 0; j < len; j++)
            {
                var u = (float)j / len;
                var sh = Sin(u * 0.5);
                var env = sh * sh;
                f *= step;
                var fi = f;
                if (trillDepth > 0f)
                {
                    trill += trillHz * Dt;
                    fi *= 1f + trillDepth * Sin(trill);
                }

                phase += fi * Dt;
                b[idx] += (Sin(phase) + Sin(phase * 2.0) * 0.12f) * env * amp;
                if (++idx == bl)
                    idx = 0;
            }
        }

        // =====================================================================================
        //  UZAK ÇATIŞMA — uzak patlamalar, makineli/tüfek atış dizileri, vadi yankısı
        // =====================================================================================

        private static float[] DistantBattleLoop(ref SynthRng rng)
        {
            const float length = DistantBattleLoopSeconds;
            var b = Buffer(length);
            var n = b.Length;

            // Önceden (dairesel) filtrelenmiş ortak gürültü kaynakları: her olay rastgele bir ofsetten zarflı kopya alır.
            // Olay başına ayrı biquad çalıştırmaktan çok daha ucuzdur; ofsetler sayesinde olaylar birbirine benzemez.
            var boomDark = FilteredNoise(Samples(6f), ref rng, FilterKind.Lowpass, 120f, 0.8f);
            var boomBright = FilteredNoise(Samples(6f), ref rng, FilterKind.Lowpass, 220f, 0.8f);
            var pops = FilteredNoise(Samples(1.5f), ref rng, FilterKind.Bandpass, 560f, 0.8f);
            var tails = FilteredNoise(Samples(3f), ref rng, FilterKind.Lowpass, 320f, 0.7f);

            // Uzak patlamalar (topçu/el bombası) + yankıları.
            var booms = 6;
            for (var e = 0; e < booms; e++)
            {
                var t = (e + rng.Range(0.1f, 0.9f)) * (length / booms);
                var a = rng.Range(0.45f, 1f);
                var tau = rng.Range(0.45f, 0.9f);
                var src = rng.Chance(0.5f) ? boomDark : boomBright;
                EnvelopeBurst(b, src, rng.RangeInt(0, src.Length), t, rng.Range(0.012f, 0.04f), tau, a, true);
                Tone(b, t, 55f, 30f, 0.15f, 0.01f, tau * 0.5f, a * 0.5f, 0, true, tau * 2.5f);
                EnvelopeBurst(b, boomDark, rng.RangeInt(0, boomDark.Length), t + rng.Range(0.35f, 0.75f), 0.05f, tau * 1.2f, a * 0.3f,
                    true);
            }

            // Atış dizileri: makineli (uzun, sık), tüfek (2-4 atım), tek atım keskin nişancı.
            var bursts = 14;
            for (var k = 0; k < bursts; k++)
            {
                var t = (k + rng.Range(0f, 0.9f)) * (length / bursts);
                var kind = rng.RangeInt(0, 3);
                int count;
                float spacing;
                switch (kind)
                {
                    case 0:
                        count = rng.RangeInt(5, 11);
                        spacing = rng.Range(0.075f, 0.095f);
                        break;
                    case 1:
                        count = rng.RangeInt(2, 5);
                        spacing = rng.Range(0.11f, 0.22f);
                        break;
                    default:
                        count = 1;
                        spacing = 0f;
                        break;
                }

                var a = rng.Range(0.12f, 0.38f) * (kind == 2 ? 1.5f : 1f);
                for (var s = 0; s < count; s++)
                {
                    var ts = t + s * spacing + rng.Range(-0.006f, 0.006f);
                    var av = a * rng.Range(0.75f, 1f);
                    EnvelopeBurst(b, pops, rng.RangeInt(0, pops.Length), ts, 0.0015f, rng.Range(0.018f, 0.032f), av, true);
                    EnvelopeBurst(b, tails, rng.RangeInt(0, tails.Length), ts + 0.004f, 0.01f, kind == 2 ? 0.45f : 0.14f, av * 0.35f,
                        true);
                }
            }

            // Yankı: tüm sahnenin gecikmeli, daha karanlık kopyası (dairesel).
            var dry = (float[])b.Clone();
            EchoCircular(b, dry, 0.42f, 0.28f, 700f);
            EchoCircular(b, dry, 0.95f, 0.14f, 450f);

            // Sürekli çok uzak uğultu.
            var bed = WhiteNoise(n, ref rng);
            FilterCircular(bed, FilterKind.Lowpass, 75f, 0.7f);
            Mix(b, bed, BandwidthGain(FilterKind.Lowpass, 75f, 0.7f) * 0.06f);

            FilterCircular(b, FilterKind.Lowpass, 1400f, 0.7f);
            return FinishLoop(b, 0.8f, 1.4f);
        }

        // =====================================================================================
        //  MENÜ MÜZİĞİ — karanlık askerî pad + davul, 16 s (60 BPM, 4 ölçü), kesintisiz
        // =====================================================================================

        private static float[] MenuMusicLoop(ref SynthRng rng)
        {
            const float length = MenuMusicLoopSeconds;
            const float bar = 4f;
            const float beat = 1f;
            var b = Buffer(length);
            var n = b.Length;

            // ---- Pad: Re minör — Si♭ — Sol minör — La (i – VI – iv – V), dedektörlü testere dişleri.
            var pad = new float[n];
            var chords = new[]
            {
                new[] { 73.42f, 110.00f, 146.83f, 174.61f },  // Dm  (D2 A2 D3 F3)
                new[] { 58.27f, 87.31f, 116.54f, 146.83f },   // Bb  (Bb1 F2 Bb2 D3)
                new[] { 49.00f, 98.00f, 116.54f, 146.83f },   // Gm  (G1 G2 Bb2 D3)
                new[] { 55.00f, 82.41f, 110.00f, 138.59f }    // A   (A1 E2 A2 C#3)
            };
            for (var c = 0; c < chords.Length; c++)
                PadChord(pad, c * bar, bar, 1.1f, 1.8f, chords[c], 0.0045f);

            var padHz = new float[n / ControlStep + 1];
            for (var i = 0; i < padHz.Length; i++)
            {
                var u = (double)i * ControlStep / n;
                padHz[i] = 520f + 380f * (0.5f + 0.5f * Sin(u * 2.0 - 0.25));
            }

            var padFiltered = new float[n];
            SweptFilterCircular(pad, padFiltered, FilterKind.Lowpass, padHz, 0.9f);
            Mix(b, padFiltered, 0.42f);

            // ---- Dron: Re1 + Re2 (döngüye tam oturan frekanslar: 36.75 / 73.5 Hz) yavaş nefes alır.
            for (var i = 0; i < n; i++)
            {
                var t = (double)i / SampleRate;
                var breathe = 0.75f + 0.25f * Sin(t * (2.0 / length) - 0.25);
                b[i] += (Sin(t * 36.75) * 0.22f + Sin(t * 73.5) * 0.1f + Sin(t * 110.25) * 0.03f) * breathe;
            }

            // ---- Boru (kornet/korno) motifi: düşük, yumuşak bakır.
            var horn = new float[n];
            HornNote(horn, 4.0f, 1.45f, 146.83f);
            HornNote(horn, 5.5f, 0.45f, 174.61f);
            HornNote(horn, 6.0f, 1.9f, 146.83f);
            HornNote(horn, 8.0f, 1.9f, 116.54f);
            HornNote(horn, 10.0f, 1.9f, 110.00f);
            HornNote(horn, 12.0f, 0.95f, 110.00f);
            HornNote(horn, 13.0f, 0.95f, 138.59f);
            HornNote(horn, 14.0f, 1.85f, 164.81f);
            FilterCircular(horn, FilterKind.Lowpass, 950f, 0.8f);
            Mix(b, horn, 0.3f);

            // ---- Savaş davulu ve trampet: vuruşlar bir kez şablon olarak üretilir, ölçeklenmiş kopyalar karıştırılır.
            var drum = WarDrumTemplate(ref rng);
            var snares = new[] { SnareTemplate(ref rng), SnareTemplate(ref rng), SnareTemplate(ref rng) };
            var snareIndex = 0;

            // Ağır vuruşlar, 4. ölçüde dolgu.
            for (var m = 0; m < 4; m++)
            {
                var t0 = m * bar;
                MixAt(b, drum, t0, 1f);
                MixAt(b, drum, t0 + 2f * beat, 0.72f);
                if (m < 3)
                {
                    MixAt(b, drum, t0 + 3.5f * beat, 0.42f);
                }
                else
                {
                    MixAt(b, drum, t0 + 2.5f * beat, 0.4f);
                    MixAt(b, drum, t0 + 3f * beat, 0.55f);
                    MixAt(b, drum, t0 + 3.5f * beat, 0.62f);
                    MixAt(b, drum, t0 + 3.75f * beat, 0.7f);
                }

                // Askerî trampet: hafif ikinci/dördüncü vuruş + hayalet notalar.
                MixAt(b, snares[snareIndex++ % snares.Length], t0 + 1f * beat, 0.2f);
                MixAt(b, snares[snareIndex++ % snares.Length], t0 + 3f * beat, 0.22f);
                if ((m & 1) == 1)
                {
                    MixAt(b, snares[snareIndex++ % snares.Length], t0 + 1.75f * beat, 0.07f);
                    MixAt(b, snares[snareIndex++ % snares.Length], t0 + 2.75f * beat, 0.08f);
                }
            }

            // Son ölçünün sonunda kreşendo trampet ruloso → döngü başına bağlanır.
            const int rollStrokes = 16;
            for (var s = 0; s < rollStrokes; s++)
            {
                var u = (float)s / rollStrokes;
                MixAt(b, snares[snareIndex++ % snares.Length], length - beat + s * (beat / rollStrokes),
                    0.04f + 0.16f * u * u);
            }

            // ---- Hafif hava/gürültü yatağı.
            var air = WhiteNoise(n, ref rng);
            FilterCircular(air, FilterKind.Bandpass, 700f, 0.6f);
            Mix(b, air, BandwidthGain(FilterKind.Bandpass, 700f, 0.6f) * 0.012f);

            return FinishLoop(b, 0.8f, 1.25f);
        }

        /// <summary>Akor: her nota için iki hafif kaydırılmış testere dişi (hızlı faz biriktirici), yumuşak atak/salınım.</summary>
        private static void PadChord(float[] b, float start, float duration, float attack, float release, float[] notes,
            float detune)
        {
            var s0 = (int)(start * SampleRate);
            var len = Samples(duration + release);
            var hold = Samples(duration);
            var att = Samples(attack);
            var rel = Samples(release);
            var voices = notes.Length * 2;
            var phases = new float[voices];
            var incs = new float[voices];
            for (var v = 0; v < notes.Length; v++)
            {
                incs[v * 2] = notes[v] * (1f - detune) / SampleRate;
                incs[v * 2 + 1] = notes[v] * (1f + detune) / SampleRate;
                phases[v * 2] = (v * 0.13f) % 1f;
                phases[v * 2 + 1] = (v * 0.29f + 0.5f) % 1f;
            }

            var norm = 1f / voices;
            var idx = Index(s0, b.Length, true);
            var bl = b.Length;
            for (var j = 0; j < len; j++)
            {
                float e;
                if (j < att)
                    e = (float)j / att;
                else if (j < hold)
                    e = 1f;
                else
                    e = Math.Max(0f, 1f - (float)(j - hold) / rel);
                e *= e * (3f - 2f * e);

                var sum = 0f;
                for (var v = 0; v < voices; v++)
                {
                    var p = phases[v] + incs[v];
                    if (p >= 1f)
                        p -= 1f;
                    phases[v] = p;
                    sum += SawUnit(p);
                }

                b[idx] += sum * norm * e;
                if (++idx == bl)
                    idx = 0;
            }
        }

        private static void HornNote(float[] b, float start, float duration, float frequency)
        {
            Note(b, start, duration, frequency, 0.14f, 0.4f, 0.5f, 1, 0.003f, 4.6f, 0.0035f, true);
        }

        /// <summary>Savaş davulu (taiko benzeri) tek vuruş şablonu.</summary>
        private static float[] WarDrumTemplate(ref SynthRng rng)
        {
            var d = Buffer(1.6f);
            Tone(d, 0f, 96f, 47f, 0.045f, 0.002f, 0.28f, 0.95f, 0, false);
            Tone(d, 0f, 150f, 92f, 0.03f, 0.002f, 0.09f, 0.25f, 0, false);
            NoiseBurst(d, ref rng, 0f, 0.001f, 0.025f, 0.45f, FilterKind.Lowpass, 700f, 0.8f);
            NoiseBurst(d, ref rng, 0f, 0.004f, 0.14f, 0.3f, FilterKind.Bandpass, 180f, 1.2f);
            FadeEdges(d, 0f, 0.2f);
            return d;
        }

        /// <summary>Askerî trampet tek vuruş şablonu.</summary>
        private static float[] SnareTemplate(ref SynthRng rng)
        {
            var d = Buffer(0.3f);
            NoiseBurst(d, ref rng, 0f, 0.0008f, 0.055f, 1f, FilterKind.Bandpass, 2600f, 0.7f);
            NoiseBurst(d, ref rng, 0f, 0.0005f, 0.012f, 0.6f, FilterKind.Highpass, 5000f, 0.7f);
            Tone(d, 0f, 230f, 185f, 0.01f, 0.0008f, 0.035f, 0.55f, 0, false);
            FadeEdges(d, 0f, 0.04f);
            return d;
        }

        /// <summary>Şablonu verilen zamana ölçekleyerek ekler (dairesel).</summary>
        private static void MixAt(float[] b, float[] src, float start, float gain)
        {
            var idx = Index((int)(start * SampleRate), b.Length, true);
            var n = Math.Min(src.Length, b.Length);
            var bl = b.Length;
            for (var j = 0; j < n; j++)
            {
                b[idx] += src[j] * gain;
                if (++idx == bl)
                    idx = 0;
            }
        }

        /// <summary>Dairesel filtrelenmiş, bant genişliğine göre seviyelenmiş beyaz gürültü.</summary>
        private static float[] FilteredNoise(int n, ref SynthRng rng, FilterKind kind, float frequency, float q)
        {
            var w = WhiteNoise(n, ref rng);
            FilterCircular(w, kind, frequency, q);
            var g = BandwidthGain(kind, frequency, q);
            for (var i = 0; i < n; i++)
                w[i] *= g;
            return w;
        }

        /// <summary>
        /// Önceden filtrelenmiş kaynaktan zarflı (doğrusal atak + üstel sönüm) kopya ekler. Kaynak dairesel okunur.
        /// </summary>
        private static void EnvelopeBurst(float[] b, float[] source, int sourceOffset, float start, float attack, float tau,
            float amp, bool wrap)
        {
            if (source == null || source.Length == 0 || amp == 0f || tau <= 0f)
                return;

            var attackSamples = Math.Max(1, (int)(attack * SampleRate));
            if (!PrepareSpan(b.Length, (int)(start * SampleRate), attackSamples + (int)(tau * 6f * SampleRate), wrap,
                    out var idx, out var total))
                return;

            var srcLen = source.Length;
            var si = sourceOffset % srcLen;
            if (si < 0)
                si += srcLen;

            var k = DecayFactor(tau);
            var env = 1f;
            var invAttack = 1f / attackSamples;
            var bl = b.Length;
            for (var j = 0; j < total; j++)
            {
                float e;
                if (j < attackSamples)
                {
                    e = j * invAttack;
                }
                else
                {
                    env *= k;
                    e = env;
                }

                b[idx] += source[si] * e * amp;
                if (++idx == bl)
                    idx = 0;
                if (++si == srcLen)
                    si = 0;
            }
        }

        // =====================================================================================
        //  KALP ATIŞI — 75 BPM "lub-dub" (düşük sağlıkta; hız pitch ile ayarlanabilir)
        // =====================================================================================

        private static float[] HeartbeatLoop(ref SynthRng rng)
        {
            var b = Buffer(HeartbeatLoopSeconds);

            // Lub
            Tone(b, 0f, 72f, 44f, 0.03f, 0.008f, 0.065f, 1f, 0, true);
            NoiseBurst(b, ref rng, 0f, 0.006f, 0.05f, 0.55f, FilterKind.Lowpass, 110f, 0.8f, true);
            // Dub
            Tone(b, 0.27f, 88f, 54f, 0.025f, 0.006f, 0.05f, 0.72f, 0, true);
            NoiseBurst(b, ref rng, 0.27f, 0.005f, 0.04f, 0.4f, FilterKind.Lowpass, 130f, 0.8f, true);

            FilterCircular(b, FilterKind.Lowpass, 220f, 0.7f);
            return FinishLoop(b, 0.9f, 2.4f);
        }

        // =====================================================================================
        //  DÖNGÜ YARDIMCILARI
        // =====================================================================================

        /// <summary>Döngü sonu: dairesel DC temizliği, normalize, isteğe bağlı doyum (durumsuz → döngü güvenli).</summary>
        private static float[] FinishLoop(float[] b, float peak, float drive)
        {
            RemoveDc(b, true);
            Normalize(b, 1f);
            if (drive > 0f)
                SoftClip(b, drive);
            Normalize(b, peak);
            return b;
        }

        /// <summary>
        /// Zamanla değişen dairesel filtre: katsayılar her <see cref="ControlStep"/> örnekte kontrol eğrisinden
        /// güncellenir. İlk geçiş durumu ısıtır, ikinci geçiş yazar; eğri periyodik olduğu için döngü kesintisizdir.
        /// </summary>
        private static void SweptFilterCircular(float[] src, float[] dst, FilterKind kind, float[] controlHz, float q)
        {
            var n = Math.Min(src.Length, dst.Length);
            if (n == 0)
                return;

            // Isınma: yalnızca son WarmupSamples örnek (filtre durumu milisaniyeler içinde oturur).
            var warmStart = Math.Max(0, n - WarmupSamples) & ~(ControlStep - 1);
            var f = Biquad.Create(kind, controlHz[Math.Min(controlHz.Length - 1, warmStart / ControlStep)], q);
            for (var i = warmStart; i < n; i++)
            {
                if ((i & (ControlStep - 1)) == 0)
                    f.Set(kind, controlHz[Math.Min(controlHz.Length - 1, i / ControlStep)], q);
                f.Process(src[i]);
            }

            for (var i = 0; i < n; i++)
            {
                if ((i & (ControlStep - 1)) == 0)
                    f.Set(kind, controlHz[Math.Min(controlHz.Length - 1, i / ControlStep)], q);
                dst[i] = f.Process(src[i]);
            }
        }

        /// <summary>Kontrol eğrisinden doğrusal aradeğer.</summary>
        private static float ControlAt(float[] curve, int sample)
        {
            var c = sample / ControlStep;
            if (c >= curve.Length - 1)
                return curve[curve.Length - 1];
            var frac = (float)(sample - c * ControlStep) / ControlStep;
            return curve[c] + (curve[c + 1] - curve[c]) * frac;
        }

        /// <summary>Dairesel yankı: gecikmeli, alçak geçirilmiş kopya tampon sonundan başa sarılır.</summary>
        private static void EchoCircular(float[] buf, float[] dry, float delay, float amp, float lowpassHz)
        {
            var d = (int)(delay * SampleRate);
            var n = Math.Min(buf.Length, dry.Length);
            if (n == 0)
                return;

            var lp1 = new OnePole(lowpassHz);
            var lp2 = new OnePole(lowpassHz * 1.3f);
            // Isınma geçişi (dairesel durum) — tek kutuplu filtreler hızla oturur.
            for (var i = Math.Max(0, n - WarmupSamples); i < n; i++)
                lp2.Lowpass(lp1.Lowpass(dry[i]));
            var w = d % n;
            for (var i = 0; i < n; i++)
            {
                buf[w] += lp2.Lowpass(lp1.Lowpass(dry[i])) * amp;
                if (++w == n)
                    w = 0;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
