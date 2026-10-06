using System;
using Project.Application.Services;

namespace Project.Infrastructure.Weapons.Reload
{
    public enum MagEstimateKind { Vague, Approx, Exact }

    public readonly struct MagEstimate
    {
        public readonly MagEstimateKind Kind;
        /// <summary>Gösterilecek tahmini sayı (Vague'da -1).</summary>
        public readonly int Shown;
        public readonly int Capacity;
        public readonly bool Chambered;
        public readonly string Text;

        public MagEstimate(MagEstimateKind kind, int shown, int capacity, bool chambered, string text)
        {
            Kind = kind; Shown = shown; Capacity = capacity; Chambered = chambered; Text = text;
        }
    }

    /// <summary>Tarkov tarzı şarjör kontrolü (saf): beceri seviyesine göre belirsiz / yaklaşık / kesin sayı, kontrol süresi.</summary>
    public static class MagazineCheck
    {
        public const int ApproxSkillLevel = 10;
        public const int ExactSkillLevel = 20;
        public const int InstantSkillLevel = 51;
        public const float BaseSeconds = 1.9f;
        /// <summary>Seviye başına hızlanma (Tarkov Mag Drills: %0.8/seviye, en çok %40).</summary>
        public const float SpeedPerLevel = 0.008f;
        public const float MaxSpeedBonus = 0.40f;
        /// <summary>Sayının ekranda görünür olduğu an (kontrol süresinin oranı): şarjör çekilip bakıldığında.</summary>
        public const float RevealFraction = 0.5f;

        public static float DurationSeconds(int skillLevel)
        {
            if (skillLevel >= InstantSkillLevel) return 0.35f;
            var bonus = Math.Min(MaxSpeedBonus, Math.Max(0, skillLevel) * SpeedPerLevel);
            return BaseSeconds * (1f - bonus);
        }

        public static MagEstimate Estimate(int rounds, int capacity, bool chambered, int skillLevel)
        {
            capacity = Math.Max(1, capacity);
            rounds = Math.Max(0, Math.Min(capacity, rounds));
            if (skillLevel >= ExactSkillLevel)
                return new MagEstimate(MagEstimateKind.Exact, rounds, capacity, chambered, rounds + "/" + capacity + (chambered ? " +1" : ""));

            if (skillLevel >= ApproxSkillLevel)
            {
                // Beşe yuvarlanmış yaklaşık değer (kapasite 10'dan küçükse en yakın 1-2'ye).
                var step = capacity >= 20 ? 5 : (capacity >= 10 ? 2 : 1);
                var shown = Math.Min(capacity, ((rounds + step / 2) / step) * step);
                return new MagEstimate(MagEstimateKind.Approx, shown, capacity, chambered, "~" + shown + "/" + capacity);
            }

            return new MagEstimate(MagEstimateKind.Vague, -1, capacity, chambered, VagueText(rounds, capacity));
        }

        public static string VagueText(int rounds, int capacity)
        {
            if (rounds <= 0) return "Boş";
            var f = rounds / (float)Math.Max(1, capacity);
            if (f <= 0.25f) return "Neredeyse boş";
            if (f <= 0.6f) return "Yarıya yakın";
            if (f < 0.95f) return "Neredeyse dolu";
            return "Dolu";
        }
    }

    /// <summary>Şarjör kontrolü animasyon kanalları (0..1): eğ, şarjörü kısmen çek, bak, geri it.</summary>
    public sealed class MagCheckTimeline
    {
        public readonly PoseTrack Tilt = new PoseTrack();
        /// <summary>Şarjör çekilme miktarı (0 yuvada, 1 çıkış sınırı).</summary>
        public readonly PoseTrack MagPull = new PoseTrack();
        public readonly PoseTrack HandReach = new PoseTrack();
        public readonly PoseTrack Jolt = new PoseTrack();
        /// <summary>Başa yaklaştırma / yan çevirme: şarjörün arkası (mermi penceresi) kameraya döner.</summary>
        public readonly PoseTrack Roll = new PoseTrack();

        private static MagCheckTimeline _shared;

        public static MagCheckTimeline Shared => _shared ?? (_shared = Build());

        private static MagCheckTimeline Build()
        {
            var m = new MagCheckTimeline();
            m.Tilt.Key(0f, 0f).Key(0.04f, -0.1f).Key(0.20f, 1f, PoseEase.Overshoot).Key(0.80f, 1f).Key(1f, 0f);
            m.HandReach.Key(0f, 0f).Key(0.12f, 1f, PoseEase.EaseOut).Key(0.82f, 1f).Key(0.95f, 0f);
            m.MagPull.Key(0f, 0f).Key(0.20f, 0f).Key(0.34f, 1f, PoseEase.EaseOut).Key(0.62f, 1f).Key(0.76f, 0f, PoseEase.Overshoot);
            m.Roll.Key(0f, 0f).Key(0.30f, 0f).Key(0.42f, 1f, PoseEase.Smooth).Key(0.60f, 1f).Key(0.72f, 0f);
            m.Jolt.Key(0f, 0f).Key(0.74f, 0f).Key(0.77f, 1f, PoseEase.EaseOut).Key(0.86f, 0f);
            return m;
        }
    }
}
