using System;

namespace Project.Application.Dialogue
{
    public readonly struct RouteDecision
    {
        public readonly DialogueChannel Channel;
        /// <summary>Bağırma için 3D azami duyulma mesafesi (m).</summary>
        public readonly float MaxDistance;
        /// <summary>0..1 hacim (bağırma: stres ve mesafeye göre).</summary>
        public readonly float Volume;
        /// <summary>Telsiz için sinyal kalitesi 0..1 (uzakta düşer).</summary>
        public readonly float SignalQuality;

        public RouteDecision(DialogueChannel channel, float maxDistance, float volume, float signalQuality)
        {
            Channel = channel;
            MaxDistance = maxDistance;
            Volume = volume;
            SignalQuality = signalQuality;
        }
    }

    /// <summary>
    /// Yakındaki takım arkadaşı bağırır (3D, filtresiz, yüksek), uzaktakiler telsizle konuşur (filtreli).
    /// Bağırma menzili stresle büyür (sakin 14 m, çatışma 32 m, panik 48 m). Telsiz menzili 900 m; sınırda sinyal zayıflar.
    /// </summary>
    public static class ProximityRouter
    {
        public const float RadioRange = 900f;
        public const float HysteresisMeters = 3f;

        public static float ShoutRange(DialogueStress stress, bool critical)
        {
            var range = stress == DialogueStress.Panic ? 48f : stress == DialogueStress.Combat ? 32f : 14f;
            return critical ? range * 1.25f : range;
        }

        public static float SignalQuality(float distance)
        {
            if (distance <= 150f)
                return 1f;
            if (distance >= RadioRange)
                return 0.15f;
            return 1f - 0.85f * ((distance - 150f) / (RadioRange - 150f));
        }

        /// <param name="lastChannel">Histerezis için bu askerin son kanalı (None = yok).</param>
        public static RouteDecision Route(float distance, DialogueStress stress, bool critical, bool allowShout, bool allowRadio,
            DialogueChannel lastChannel = DialogueChannel.None)
        {
            if (float.IsNaN(distance) || distance < 0f)
                distance = 0f;

            var range = ShoutRange(stress, critical);
            if (lastChannel == DialogueChannel.Shout)
                range += HysteresisMeters;

            if (allowShout && distance <= range)
            {
                var vol = stress == DialogueStress.Panic ? 1f : stress == DialogueStress.Combat ? 0.9f : 0.65f;
                return new RouteDecision(DialogueChannel.Shout, range * 1.6f, vol, 1f);
            }

            if (allowRadio && distance <= RadioRange)
                return new RouteDecision(DialogueChannel.Radio, 0f, 0.9f, SignalQuality(distance));

            return new RouteDecision(DialogueChannel.None, 0f, 0f, 0f);
        }
    }
}
