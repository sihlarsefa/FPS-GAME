using System;
using System.Collections.Generic;

namespace Project.Infrastructure.Audio.Music
{
    public enum DrumKind { Dum, Tek, Ka }

    public enum StingKind { MatchStart, Victory, Defeat }

    /// <summary>Tek melodi/pad notası (vuruş cinsinden).</summary>
    public readonly struct ScoreNote
    {
        public readonly float StartBeat;
        public readonly float Beats;
        public readonly int Midi;
        public readonly float Velocity;

        public ScoreNote(float startBeat, float beats, int midi, float velocity = 1f)
        {
            StartBeat = startBeat; Beats = beats; Midi = midi; Velocity = velocity;
        }
    }

    public readonly struct DrumHit
    {
        public readonly float Beat;
        public readonly DrumKind Kind;
        public readonly float Velocity;

        public DrumHit(float beat, DrumKind kind, float velocity)
        {
            Beat = beat; Kind = kind; Velocity = velocity;
        }
    }

    /// <summary>Pad akoru: belirli vuruş aralığında birlikte tutulan MIDI notaları.</summary>
    public readonly struct ChordSpan
    {
        public readonly float StartBeat;
        public readonly float Beats;
        public readonly int[] Midi;

        public ChordSpan(float startBeat, float beats, int[] midi)
        {
            StartBeat = startBeat; Beats = beats; Midi = midi;
        }
    }

    /// <summary>Bir sting'in (maç başı / zafer / yenilgi) notaları. Tempo 60 BPM: 1 vuruş = 1 sn.</summary>
    public sealed class StingScore
    {
        public StingKind Kind;
        public float Seconds;
        public bool Riser;
        public List<ScoreNote> Melody = new List<ScoreNote>();
        public List<ScoreNote> Drone = new List<ScoreNote>();
        public List<ChordSpan> Chords = new List<ChordSpan>();
        public List<DrumHit> Drums = new List<DrumHit>();
    }

    /// <summary>
    /// Menü teması ve sting'ler için saf nota/desen mantığı (Unity'siz, test edilebilir).
    /// Tema: La (A) Frigyen/Hicaz esintili minör, 64 BPM, 20 ölçü = 75 sn kusursuz döngü.
    /// </summary>
    public static class MusicScore
    {
        /// <summary>Skor değişince artar; önbellek dosya adına girer.</summary>
        public const int Version = 2;
        public const float Bpm = 64f;
        public const int BeatsPerBar = 4;
        public const int LoopBars = 20;
        public const float LoopBeats = LoopBars * BeatsPerBar;
        public const int ChordBeats = 8;

        /// <summary>La Frigyen (Bb'li): La'ya göre yarım ton aralıkları.</summary>
        public static readonly int[] ScaleSemitones = { 0, 1, 3, 5, 7, 8, 10 };

        public const int RootMidi = 69; // A4

        public static float BeatsToSeconds(float beats, float bpm = Bpm) => beats * 60f / bpm;

        public static float LoopSeconds => BeatsToSeconds(LoopBeats);

        public static float MidiToHz(int midi) => 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);

        public static bool InScale(int midi)
        {
            var pc = (((midi - RootMidi) % 12) + 12) % 12;
            return Array.IndexOf(ScaleSemitones, pc) >= 0;
        }

        // Am, Am, F, G, Am, Dm, Bb, Am, E(maj), Am — her biri 8 vuruş (2 ölçü).
        private static readonly int[][] ChordTable =
        {
            new[] { 45, 52, 57, 60 },
            new[] { 45, 52, 57, 64 },
            new[] { 41, 48, 53, 57 },
            new[] { 43, 50, 55, 59 },
            new[] { 45, 52, 57, 60 },
            new[] { 38, 45, 50, 53 },
            new[] { 46, 53, 58, 62 },
            new[] { 45, 52, 57, 60 },
            new[] { 40, 47, 52, 56 },
            new[] { 45, 52, 57, 60 },
        };

        public static IReadOnlyList<ChordSpan> BuildChords()
        {
            var list = new List<ChordSpan>(ChordTable.Length);
            for (var i = 0; i < ChordTable.Length; i++)
                list.Add(new ChordSpan(i * ChordBeats, ChordBeats, (int[])ChordTable[i].Clone()));
            return list;
        }

        // Her cümle 16 vuruş: (başlangıç, süre, midi). Melodi 5. ölçüden girer (4 ölçülük pad girişi).
        private static readonly int[][] Phrases =
        {
            new[] { 0, 3, 69, 3, 1, 72, 4, 4, 74, 8, 3, 72, 11, 1, 70, 12, 4, 69 },
            new[] { 0, 3, 72, 3, 1, 74, 4, 3, 76, 7, 1, 74, 8, 4, 72, 12, 4, 70 },
            new[] { 0, 2, 76, 2, 2, 77, 4, 4, 76, 8, 3, 74, 11, 1, 72, 12, 4, 76 },
            new[] { 0, 4, 74, 4, 4, 72, 8, 4, 70, 12, 4, 69 },
        };

        public static IReadOnlyList<ScoreNote> BuildMelody()
        {
            var list = new List<ScoreNote>();
            for (var p = 0; p < Phrases.Length; p++)
            {
                var origin = (p + 1) * 16f; // 16, 32, 48, 64
                var d = Phrases[p];
                for (var i = 0; i + 2 < d.Length; i += 3)
                {
                    var vel = 0.8f + 0.06f * p;
                    list.Add(new ScoreNote(origin + d[i], d[i + 1] - 0.08f, d[i + 2], vel));
                }
            }
            return list;
        }

        /// <summary>Seyrek davul (düyek esintili): 2. ölçüden sonra, her ölçüde derin dum, bazı ölçülerde tek/ka.</summary>
        public static IReadOnlyList<DrumHit> BuildDrums()
        {
            var list = new List<DrumHit>();
            for (var bar = 1; bar < LoopBars; bar++)
            {
                var b = bar * BeatsPerBar;
                list.Add(new DrumHit(b, DrumKind.Dum, bar % 2 == 0 ? 1f : 0.8f));
                if (bar >= 2 && bar % 2 == 1)
                    list.Add(new DrumHit(b + 2.5f, DrumKind.Dum, 0.55f));
                if (bar >= 4 && bar % 4 == 3)
                    list.Add(new DrumHit(b + 3.5f, DrumKind.Tek, 0.5f));
                if (bar >= 8 && bar % 4 == 1)
                    list.Add(new DrumHit(b + 1.5f, DrumKind.Ka, 0.35f));
            }

            // Döngü öncesi küçük geçiş: son ölçüde üçlü vuruş.
            var last = (LoopBars - 1) * BeatsPerBar;
            list.Add(new DrumHit(last + 2f, DrumKind.Dum, 0.7f));
            list.Add(new DrumHit(last + 3f, DrumKind.Tek, 0.55f));
            list.Add(new DrumHit(last + 3.5f, DrumKind.Tek, 0.7f));
            list.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            return list;
        }

        public static StingScore BuildSting(StingKind kind)
        {
            var s = new StingScore { Kind = kind };
            switch (kind)
            {
                case StingKind.MatchStart: BuildMatchStart(s); break;
                case StingKind.Victory: BuildVictory(s); break;
                default: BuildDefeat(s); break;
            }
            return s;
        }

        private static void BuildMatchStart(StingScore s)
        {
            s.Seconds = 20f;
            s.Riser = true;
            s.Drone.Add(new ScoreNote(0f, 20f, 33, 1f));    // A1
            s.Drone.Add(new ScoreNote(5f, 15f, 34, 0.7f));  // Bb1 küçük ikili gerilimi
            s.Drone.Add(new ScoreNote(11f, 9f, 40, 0.6f));  // E2

            // Kalp atışı: aralık daralır (2 sn -> 0,6 sn).
            var t = 1f;
            var gap = 2f;
            while (t < 19f)
            {
                var v = 0.55f + 0.45f * (t / 19f);
                s.Drums.Add(new DrumHit(t, DrumKind.Dum, v));
                s.Drums.Add(new DrumHit(t + 0.28f, DrumKind.Dum, v * 0.6f));
                t += gap;
                gap = Math.Max(0.6f, gap * 0.88f);
            }
            s.Drums.Add(new DrumHit(19.4f, DrumKind.Dum, 1f));

            // Gerilim figürü (La - Si bemol - La - Sol#), 8. sn'den itibaren sekizlik.
            int[] fig = { 57, 58, 57, 56 };
            var n = 0;
            for (var beat = 8f; beat < 19f; beat += 0.5f, n++)
                s.Melody.Add(new ScoreNote(beat, 0.4f, fig[n % 4] + (beat > 15f ? 12 : 0), 0.35f + 0.5f * ((beat - 8f) / 11f)));
        }

        private static void BuildVictory(StingScore s)
        {
            s.Seconds = 9f;
            s.Chords.Add(new ChordSpan(0f, 8f, new[] { 45, 52, 57, 61, 64 })); // La majör
            s.Melody.Add(new ScoreNote(0f, 1f, 64, 0.9f));
            s.Melody.Add(new ScoreNote(1f, 1f, 69, 0.95f));
            s.Melody.Add(new ScoreNote(2f, 2f, 73, 1f));
            s.Melody.Add(new ScoreNote(4f, 4f, 76, 1f));
            s.Drums.Add(new DrumHit(0f, DrumKind.Dum, 1f));
            s.Drums.Add(new DrumHit(2f, DrumKind.Dum, 0.8f));
            s.Drums.Add(new DrumHit(3.5f, DrumKind.Tek, 0.6f));
            s.Drums.Add(new DrumHit(4f, DrumKind.Dum, 1f));
        }

        private static void BuildDefeat(StingScore s)
        {
            s.Seconds = 10f;
            s.Chords.Add(new ChordSpan(0f, 9f, new[] { 33, 45, 52, 57 }));
            s.Melody.Add(new ScoreNote(0f, 2f, 72, 0.8f));
            s.Melody.Add(new ScoreNote(2f, 2f, 70, 0.75f));
            s.Melody.Add(new ScoreNote(4f, 5f, 69, 0.7f));
            s.Drums.Add(new DrumHit(0f, DrumKind.Dum, 0.9f));
            s.Drums.Add(new DrumHit(4f, DrumKind.Dum, 0.7f));
        }
    }
}
