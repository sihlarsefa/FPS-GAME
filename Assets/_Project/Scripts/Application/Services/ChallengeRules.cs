using System;
using System.Collections.Generic;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>Poligon meydan okuma türleri.</summary>
    public enum ChallengeKind
    {
        QuickFire = 0,
        Sniper = 1,
        KillHouse = 2,
        Grenade = 3
    }

    /// <summary>Çıkıp kalkan (pop-up) hedefin zamanlaması ve yeri.</summary>
    public readonly struct PopUpSpec
    {
        public readonly float Distance;
        public readonly float Lateral;
        public readonly float SpawnTime;
        public readonly float LifeSeconds;

        public PopUpSpec(float distance, float lateral, float spawnTime, float lifeSeconds)
        {
            Distance = distance;
            Lateral = lateral;
            SpawnTime = spawnTime;
            LifeSeconds = lifeSeconds;
        }
    }

    /// <summary>Poligon meydan okumalarının saf kuralları: süreler, puanlama, derece ve yerel rekor kaydı.</summary>
    public static class ChallengeRules
    {
        public const float QuickFireDuration = 60f;
        public const float KillHouseParSeconds = 60f;
        public const float KillHouseTimeLimit = 120f;
        public const float GrenadeTimeLimit = 90f;
        public const int GrenadeThrows = 6;
        public const int KillHouseFriendlyPenalty = 150;
        public const int KillHouseKillPoints = 100;

        public static readonly float[] SniperDistances = { 200f, 300f, 500f };
        public const int SniperTargetsPerDistance = 2;
        public const float SniperTimeLimit = 150f;

        /// <summary>Bomba halkaları (yarıçap m, puan): içten dışa.</summary>
        public static readonly float[] GrenadeRingRadii = { 3f, 6f, 10f, 15f };
        public static readonly int[] GrenadeRingPoints = { 100, 60, 30, 10 };

        public static string Name(ChallengeKind kind)
        {
            switch (kind)
            {
                case ChallengeKind.QuickFire: return "Hızlı Atış";
                case ChallengeKind.Sniper: return "Keskin Nişancı";
                case ChallengeKind.KillHouse: return "Kill House";
                case ChallengeKind.Grenade: return "Bomba Atma";
                default: return "Meydan Okuma";
            }
        }

        public static string Description(ChallengeKind kind)
        {
            switch (kind)
            {
                case ChallengeKind.QuickFire:
                    return "60 sn içinde 25-100 m arası çıkıp kalkan hedefleri vur. Kafa vuruşu ve uzaklık puanı artırır.";
                case ChallengeKind.Sniper:
                    return "JNG-90 ile 200 / 300 / 500 m hedefleri vur. Rüzgâr yok; mesafeye göre mermi düşüşünü hesapla.";
                case ChallengeKind.KillHouse:
                    return "İki binayı hızla temizle: düşman siluetleri vur, dost siluetlere ateş etme (ceza).";
                case ChallengeKind.Grenade:
                    return "Elbombalarını halkaların içine at; merkeze ne kadar yakınsa o kadar puan.";
                default:
                    return string.Empty;
            }
        }

        public static string PadId(ChallengeKind kind)
        {
            switch (kind)
            {
                case ChallengeKind.QuickFire: return "challenge_quickfire";
                case ChallengeKind.Sniper: return "challenge_sniper";
                case ChallengeKind.KillHouse: return "challenge_killhouse";
                case ChallengeKind.Grenade: return "challenge_grenade";
                default: return "challenge_unknown";
            }
        }

        public static string BestKey(ChallengeKind kind) => "challenge.best." + (int)kind;

        public static string RunsKey(ChallengeKind kind) => "challenge.runs." + (int)kind;

        // ------------------------------------------------------------------ Puanlama

        /// <summary>Hızlı Atış: mesafe başına ek puan, kafa vuruşu iki katı.</summary>
        public static int QuickFireHitScore(float distance, bool headshot)
        {
            var d = Math.Max(0f, distance);
            var baseScore = 10 + (int)Math.Round(d / 5f);
            return headshot ? baseScore * 2 : baseScore;
        }

        /// <summary>Keskin Nişancı: uzak mesafe çok daha değerli, kafa vuruşu +50%.</summary>
        public static int SniperHitScore(float distance, bool headshot)
        {
            var d = Math.Max(0f, distance);
            var baseScore = 50 + (int)Math.Round(d / 2f);
            return headshot ? baseScore + baseScore / 2 : baseScore;
        }

        /// <summary>Kill House: vuruş puanı + süre bonusu - dost ateşi cezası; tamamlama bonusu 200. Negatif olmaz.</summary>
        public static int KillHouseScore(int enemyKills, int friendlyHits, float elapsed, bool cleared)
        {
            var score = Math.Max(0, enemyKills) * KillHouseKillPoints;
            if (cleared)
            {
                score += 200;
                score += (int)Math.Round(Math.Max(0f, KillHouseParSeconds - elapsed) * 5f);
            }

            score -= Math.Max(0, friendlyHits) * KillHouseFriendlyPenalty;
            return Math.Max(0, score);
        }

        /// <summary>Bomba Atma: merkeze uzaklığa (m) göre halka puanı; tüm halkaların dışı 0.</summary>
        public static int GrenadeScore(float distanceToCenter)
        {
            for (var i = 0; i < GrenadeRingRadii.Length; i++)
                if (distanceToCenter <= GrenadeRingRadii[i])
                    return GrenadeRingPoints[i];
            return 0;
        }

        // ------------------------------------------------------------------ Derece

        /// <summary>Türe göre S/A/B/C/D eşikleri (azalan).</summary>
        public static int[] RankThresholds(ChallengeKind kind)
        {
            switch (kind)
            {
                case ChallengeKind.QuickFire: return new[] { 700, 500, 300, 150 };
                case ChallengeKind.Sniper: return new[] { 600, 450, 300, 150 };
                case ChallengeKind.KillHouse: return new[] { 1000, 800, 600, 300 };
                case ChallengeKind.Grenade: return new[] { 500, 360, 240, 100 };
                default: return new[] { 100, 75, 50, 25 };
            }
        }

        public static string Rank(ChallengeKind kind, int score)
        {
            var t = RankThresholds(kind);
            if (score >= t[0]) return "S";
            if (score >= t[1]) return "A";
            if (score >= t[2]) return "B";
            if (score >= t[3]) return "C";
            return "D";
        }

        // ------------------------------------------------------------------ Hedef çizelgeleri

        /// <summary>
        /// Hızlı Atış için deterministik hedef çizelgesi: 25-100 m, yanlama -12..12 m, her ~1,6 sn'de yeni hedef,
        /// ömür mesafeyle uzar (yakın 1,8 sn, uzak 3,2 sn). duration süresinden sonra hedef üretilmez.
        /// </summary>
        public static List<PopUpSpec> BuildQuickFireSchedule(int seed, float duration = QuickFireDuration)
        {
            var rng = new Random(seed);
            var list = new List<PopUpSpec>();
            var t = 1.5f;
            while (t < duration - 1f)
            {
                var distance = 25f + (float)rng.NextDouble() * 75f;
                var lateral = ((float)rng.NextDouble() * 2f - 1f) * 12f;
                var life = 1.8f + (distance - 25f) / 75f * 1.4f;
                list.Add(new PopUpSpec(distance, lateral, t, life));
                t += 1.2f + (float)rng.NextDouble() * 0.8f;
            }

            return list;
        }

        /// <summary>Keskin Nişancı hedef yerleşimi: her mesafede SniperTargetsPerDistance hedef (mesafe, yanlama).</summary>
        public static List<PopUpSpec> BuildSniperLayout(int seed)
        {
            var rng = new Random(seed);
            var list = new List<PopUpSpec>();
            for (var i = 0; i < SniperDistances.Length; i++)
            {
                for (var k = 0; k < SniperTargetsPerDistance; k++)
                {
                    var lateral = ((float)rng.NextDouble() * 2f - 1f) * (10f + i * 8f);
                    list.Add(new PopUpSpec(SniperDistances[i], lateral, 0f, SniperTimeLimit));
                }
            }

            return list;
        }

        // ------------------------------------------------------------------ Rekor kaydı

        public static int GetBest(ISettingsStore store, ChallengeKind kind)
            => store == null ? 0 : Math.Max(0, store.GetInt(BestKey(kind), 0));

        public static int GetRuns(ISettingsStore store, ChallengeKind kind)
            => store == null ? 0 : Math.Max(0, store.GetInt(RunsKey(kind), 0));

        /// <summary>Sonucu kaydeder; yeni rekorsa true. Deneme sayısı her çağrıda artar.</summary>
        public static bool RecordResult(ISettingsStore store, ChallengeKind kind, int score)
        {
            if (store == null)
                return false;

            var best = GetBest(store, kind);
            var isBest = score > best;
            if (isBest)
                store.SetInt(BestKey(kind), score);
            store.SetInt(RunsKey(kind), GetRuns(store, kind) + 1);
            store.Save();
            return isBest;
        }
    }
}
