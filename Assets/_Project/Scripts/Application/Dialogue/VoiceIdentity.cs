using System;
using System.Collections.Generic;

namespace Project.Application.Dialogue
{
    /// <summary>
    /// Asker ses kimliği. Pitch: oynatma/yeniden örnekleme oranı; Formant: ses rengi (gövde/boğaz) kayması;
    /// Speed: konuşma hızı (boşluklar + hafif örnekleme); Intensity: güç/zorlanma (sürüş, parlaklık).
    /// </summary>
    public readonly struct VoiceIdentity
    {
        public readonly int VoiceId;
        public readonly string Name;
        public readonly float Pitch;
        public readonly float Formant;
        public readonly float Speed;
        public readonly float Intensity;

        public VoiceIdentity(int voiceId, string name, float pitch, float formant, float speed, float intensity)
        {
            VoiceId = voiceId;
            Name = name;
            Pitch = pitch;
            Formant = formant;
            Speed = speed;
            Intensity = intensity;
        }

        /// <summary>Stres hâline göre: panikte perde yükselir, hız ve şiddet artar; sakinde yavaş ve yumuşak.</summary>
        public VoiceIdentity ForStress(DialogueStress stress)
        {
            switch (stress)
            {
                case DialogueStress.Panic:
                    return new VoiceIdentity(VoiceId, Name, Pitch * (1f + 0.07f * Intensity), Formant * 1.02f, Speed * 1.14f, Math.Min(1.5f, Intensity * 1.45f));
                case DialogueStress.Combat:
                    return new VoiceIdentity(VoiceId, Name, Pitch * (1f + 0.025f * Intensity), Formant, Speed * 1.05f, Math.Min(1.5f, Intensity * 1.15f));
                default:
                    return new VoiceIdentity(VoiceId, Name, Pitch, Formant, Speed * 0.96f, Intensity * 0.8f);
            }
        }
    }

    /// <summary>8 farklı asker sesi + askere kalıcı atama (aynı asker her zaman aynı ses).</summary>
    public static class VoiceIdentities
    {
        public const int Count = 8;

        private static readonly VoiceIdentity[] Table =
        {
            new VoiceIdentity(0, "Derin komutan", 0.86f, 0.90f, 0.95f, 0.90f),
            new VoiceIdentity(1, "Genç tiz", 1.06f, 1.08f, 1.05f, 0.80f),
            new VoiceIdentity(2, "Ağır sakin", 0.92f, 0.96f, 0.90f, 0.70f),
            new VoiceIdentity(3, "Hızlı gergin", 1.00f, 1.00f, 1.12f, 1.00f),
            new VoiceIdentity(4, "Boğuk", 0.89f, 0.93f, 1.00f, 0.85f),
            new VoiceIdentity(5, "Parlak", 1.03f, 1.12f, 1.00f, 0.90f),
            new VoiceIdentity(6, "Yorgun", 0.95f, 0.98f, 0.88f, 0.65f),
            new VoiceIdentity(7, "Sert çavuş", 0.90f, 0.94f, 1.05f, 1.10f)
        };

        public static VoiceIdentity Get(int voiceId)
        {
            var i = ((voiceId % Count) + Count) % Count;
            return Table[i];
        }

        /// <summary>Kararlı (platformdan bağımsız) FNV-1a özeti.</summary>
        public static uint Hash(int value)
        {
            unchecked
            {
                var h = 2166136261u;
                for (var b = 0; b < 4; b++)
                {
                    h ^= (uint)((value >> (b * 8)) & 0xFF);
                    h *= 16777619u;
                }

                return h;
            }
        }

        public static int PreferredVoice(int soldierId) => (int)(Hash(soldierId) % Count);
    }

    /// <summary>
    /// Takım içinde benzersiz ses ataması. Asker kimliği ile tercih edilen ses çakışırsa sıradaki boş ses
    /// verilir; atama oturum boyunca korunur (tutarlılık).
    /// </summary>
    public sealed class VoiceAssigner
    {
        private readonly Dictionary<int, int> _assigned = new Dictionary<int, int>(16);
        private readonly bool[] _taken = new bool[VoiceIdentities.Count];

        public int Assign(int soldierId)
        {
            if (_assigned.TryGetValue(soldierId, out var voice))
                return voice;

            var pref = VoiceIdentities.PreferredVoice(soldierId);
            voice = pref;
            var allTaken = true;
            for (var i = 0; i < VoiceIdentities.Count; i++)
                if (!_taken[i])
                    allTaken = false;

            if (!allTaken)
            {
                for (var i = 0; i < VoiceIdentities.Count; i++)
                {
                    var c = (pref + i) % VoiceIdentities.Count;
                    if (!_taken[c])
                    {
                        voice = c;
                        break;
                    }
                }
            }

            _taken[voice] = true;
            _assigned[soldierId] = voice;
            return voice;
        }

        public VoiceIdentity IdentityFor(int soldierId) => VoiceIdentities.Get(Assign(soldierId));

        public void Clear()
        {
            _assigned.Clear();
            for (var i = 0; i < _taken.Length; i++)
                _taken[i] = false;
        }
    }
}
