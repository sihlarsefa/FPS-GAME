using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Presentation.Training
{
    /// <summary>Poligon istasyonları (Poligon Pro): saf mantık <see cref="RangeScoring"/>'dedir.</summary>
    public enum RangeStation
    {
        Reaction = 0,
        Rails = 1,
        ShootHouse = 2,
        GrenadePit = 3,
        VehiclePad = 4
    }

    public enum HitZone
    {
        Head = 0,
        Torso = 1,
        Limb = 2
    }

    /// <summary>Menzil subayı repliği türleri.</summary>
    public enum RangeBark
    {
        Welcome,
        StationStart,
        GoodShot,
        Headshot,
        Miss,
        HostageHit,
        CourseClear,
        NewRecord,
        SlowTime,
        CeaseFire,
        GrenadeOut,
        VehicleStart
    }

    /// <summary>Çıkıp kalkan hedef: şerit (0 tabanlı), mesafe, çıkış zamanı ve ömür.</summary>
    public readonly struct RangePopUp
    {
        public readonly int Lane;
        public readonly float Distance;
        public readonly float SpawnTime;
        public readonly float Life;

        public RangePopUp(int lane, float distance, float spawnTime, float life)
        {
            Lane = lane;
            Distance = distance;
            SpawnTime = spawnTime;
            Life = life;
        }
    }

    /// <summary>Atış evi odası (yerel koordinat: koridor +Z'ye uzanır, merkez x=0).</summary>
    public readonly struct HouseRoom
    {
        public readonly float CenterX;
        public readonly float CenterZ;
        public readonly float Width;
        public readonly float Depth;

        public HouseRoom(float centerX, float centerZ, float width, float depth)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            Width = width;
            Depth = depth;
        }
    }

    /// <summary>Atış evindeki siluet: tehdit ya da rehine (vurulmaz).</summary>
    public readonly struct HouseTarget
    {
        public readonly float X;
        public readonly float Z;
        public readonly bool Hostage;
        public readonly int Room;

        public HouseTarget(float x, float z, bool hostage, int room)
        {
            X = x;
            Z = z;
            Hostage = hostage;
            Room = room;
        }
    }

    public sealed class HouseLayout
    {
        public const float CorridorHalfWidth = 1.5f;
        public const float RoomWidth = 6f;
        public const float RowDepth = 7f;

        public readonly List<HouseRoom> Rooms = new List<HouseRoom>();
        public readonly List<HouseTarget> Targets = new List<HouseTarget>();
        public int Rows;
        public float Length;
        public int ThreatCount;
        public int HostageCount;
    }

    /// <summary>
    /// Poligon Pro saf kuralları: şerit numaralama, bölge puanı, tepki süresi çarpanı, atış evi yerleşimi/par/yıldız,
    /// bomba çukuru puanı, rekor kaydı (ISettingsStore) ve menzil subayı replikleri. Unity'ye bağımlı değildir.
    /// </summary>
    public static class RangeScoring
    {
        public const int LaneCount = 5;
        public const float LaneSpacing = 10f;
        public static readonly float[] LaneMarkerDistances = { 100f, 200f, 300f, 400f, 500f, 600f };

        public const int HeadPoints = 100;
        public const int TorsoPoints = 60;
        public const int LimbPoints = 30;

        public const float FastReaction = 0.4f;
        public const float SlowReaction = 2f;

        public const int ReactionTargets = 20;
        public const float RailsDuration = 45f;
        public const float HostagePenalty = 200f;
        public const int GrenadeThrows = 6;
        public const float DriveTimeLimit = 90f;

        // ------------------------------------------------------------------ Şeritler

        /// <summary>Şerit (0 tabanlı) yanlama konumu: orta şerit 0, her şerit 10 m.</summary>
        public static float LaneLateral(int lane)
            => (Clamp(lane, 0, LaneCount - 1) - (LaneCount - 1) * 0.5f) * LaneSpacing;

        public static string LaneLabel(int lane) => "ŞERİT " + (Clamp(lane, 0, LaneCount - 1) + 1);

        public static string DistanceLabel(float meters) => ((int)Math.Round(meters)) + " m";

        // ------------------------------------------------------------------ Bölge / tepki

        public static HitZone ZoneOf(BodyPart part, bool headshot)
        {
            if (headshot || part == BodyPart.Head)
                return HitZone.Head;
            return part == BodyPart.Torso ? HitZone.Torso : HitZone.Limb;
        }

        public static int ZonePoints(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return HeadPoints;
                case HitZone.Torso: return TorsoPoints;
                default: return LimbPoints;
            }
        }

        public static string ZoneName(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return "KAFA";
                case HitZone.Torso: return "GÖVDE";
                default: return "UZUV";
            }
        }

        /// <summary>Tepki çarpanı: ≤0,4 sn → 1,5; ≥2 sn → 0,5; arası doğrusal.</summary>
        public static float ReactionFactor(float seconds)
        {
            var t = Math.Max(0f, seconds);
            if (t <= FastReaction) return 1.5f;
            if (t >= SlowReaction) return 0.5f;
            return 1.5f - (t - FastReaction) / (SlowReaction - FastReaction);
        }

        public static int PopUpScore(HitZone zone, float reactionSeconds)
            => (int)Math.Round(ZonePoints(zone) * ReactionFactor(reactionSeconds));

        /// <summary>Hareketli hedef: hız ve bölge puanı (hızlı hedef daha değerli).</summary>
        public static int RailScore(HitZone zone, float speed)
            => (int)Math.Round(ZonePoints(zone) * (1f + Math.Min(Math.Max(0f, speed), 8f) / 8f));

        /// <summary>
        /// Deterministik tepki çizelgesi: şerit rastgele, mesafe 25/50/75/100/150/200 m, ömür mesafeyle uzar,
        /// çıkışlar 1,2-2,2 sn arayla.
        /// </summary>
        public static List<RangePopUp> BuildPopUpSchedule(int seed, int count = ReactionTargets)
        {
            var rng = new Random(seed);
            var distances = new[] { 25f, 50f, 75f, 100f, 150f, 200f };
            var list = new List<RangePopUp>(Math.Max(0, count));
            var t = 2f;
            for (var i = 0; i < count; i++)
            {
                var lane = rng.Next(LaneCount);
                var distance = distances[rng.Next(distances.Length)];
                var life = 1.6f + distance / 200f * 1.4f;
                list.Add(new RangePopUp(lane, distance, t, life));
                t += 1.2f + (float)rng.NextDouble();
            }

            return list;
        }

        public static string ReactionRank(int score, int targets)
        {
            var max = Math.Max(1, targets) * HeadPoints * 1.5f;
            var ratio = score / max;
            if (ratio >= 0.55f) return "S";
            if (ratio >= 0.4f) return "A";
            if (ratio >= 0.28f) return "B";
            if (ratio >= 0.15f) return "C";
            return "D";
        }

        // ------------------------------------------------------------------ Atış evi

        /// <summary>Par süresi: oda başına 10 sn + 12 sn giriş payı.</summary>
        public static float ParSeconds(int rooms) => 12f + Math.Max(1, rooms) * 10f;

        public static float HouseTimeLimit(int rooms) => ParSeconds(rooms) * 2.5f;

        /// <summary>
        /// Prosedürel atış evi: ortada koridor, iki yanda sıralı odalar (satır başına iki oda). Her odada en az bir tehdit,
        /// odaların yaklaşık yarısında rehine (vurulmaz). Deterministik.
        /// </summary>
        public static HouseLayout BuildShootHouse(int seed, int rooms)
        {
            rooms = Math.Max(2, Math.Min(8, rooms));
            var rng = new Random(seed);
            var layout = new HouseLayout { Rows = (rooms + 1) / 2 };
            layout.Length = layout.Rows * HouseLayout.RowDepth;

            var roomX = HouseLayout.CorridorHalfWidth + HouseLayout.RoomWidth * 0.5f;
            for (var i = 0; i < rooms; i++)
            {
                var row = i / 2;
                var side = i % 2 == 0 ? -1f : 1f;
                var cz = HouseLayout.RowDepth * (row + 0.5f);
                layout.Rooms.Add(new HouseRoom(side * roomX, cz, HouseLayout.RoomWidth, HouseLayout.RowDepth));

                var hx = HouseLayout.RoomWidth * 0.5f - 1f;
                var hz = HouseLayout.RowDepth * 0.5f - 1f;
                layout.Targets.Add(new HouseTarget(side * roomX + Jitter(rng, hx), cz + Jitter(rng, hz), false, i));
                layout.ThreatCount++;

                if (rng.NextDouble() < 0.55)
                {
                    layout.Targets.Add(new HouseTarget(side * roomX - Math.Sign(side) * Math.Abs(Jitter(rng, hx)) * 0.5f, cz - Jitter(rng, hz) * 0.5f, true, i));
                    layout.HostageCount++;
                }
            }

            // Koridor sonunda son tehdit.
            layout.Targets.Add(new HouseTarget(0f, layout.Length - 1.2f, false, -1));
            layout.ThreatCount++;
            return layout;
        }

        private static float Jitter(Random rng, float range) => ((float)rng.NextDouble() * 2f - 1f) * range;

        /// <summary>Atış evi puanı: tehdit 100, temizleme +250, par altı süre saniyesi 5, rehine -200. Negatif olmaz.</summary>
        public static int HouseScore(int threatsDown, int hostageHits, float elapsed, float par, bool cleared)
        {
            var score = Math.Max(0, threatsDown) * 100;
            if (cleared)
            {
                score += 250;
                score += (int)Math.Round(Math.Max(0f, par - elapsed) * 5f);
            }

            score -= Math.Max(0, hostageHits) * (int)HostagePenalty;
            return Math.Max(0, score);
        }

        /// <summary>
        /// Yıldız: 3 = hepsi temiz + rehine yok + par içinde; 2 = temiz, en çok 1 rehine, par*1,5 içinde; 1 = temizlendi; 0 = yarım.
        /// </summary>
        public static int HouseStars(bool cleared, int hostageHits, float elapsed, float par)
        {
            if (!cleared) return 0;
            if (hostageHits == 0 && elapsed <= par) return 3;
            if (hostageHits <= 1 && elapsed <= par * 1.5f) return 2;
            return 1;
        }

        public static string StarText(int stars)
        {
            stars = Clamp(stars, 0, 3);
            return new string('*', stars) + new string('-', 3 - stars);
        }

        // ------------------------------------------------------------------ Bomba çukuru

        /// <summary>Bomba çukuru kovaları: (mesafe m, puan); yarıçap 4 m.</summary>
        public static readonly float[] PitDistances = { 20f, 30f, 40f };
        public static readonly int[] PitPoints = { 40, 70, 100 };
        public const float PitBinRadius = 4f;

        /// <summary>Patlama noktasının (başlangıca göre ileri m, yanlama m) en yakın kovaya göre puanı; kova dışı 0.</summary>
        public static int PitScore(float forward, float lateral)
        {
            var best = 0;
            for (var i = 0; i < PitDistances.Length; i++)
            {
                var df = forward - PitDistances[i];
                var d = (float)Math.Sqrt(df * df + lateral * lateral);
                if (d > PitBinRadius)
                    continue;
                var closeness = 1f - 0.4f * d / PitBinRadius;
                best = Math.Max(best, (int)Math.Round(PitPoints[i] * closeness));
            }

            return best;
        }

        // ------------------------------------------------------------------ Araç pisti

        /// <summary>Slalom kontrol noktası i (yerel: ileri m, yanlama m).</summary>
        public static void DriveCheckpoint(int index, out float forward, out float lateral)
        {
            var i = Clamp(index, 0, 3);
            forward = 35f + i * 35f;
            lateral = i % 2 == 0 ? -14f : 14f;
        }

        public const int DriveCheckpointCount = 4;
        public const float DriveCheckpointRadius = 7f;

        // ------------------------------------------------------------------ Rekor

        public static string BestScoreKey(RangeStation s) => "range.score." + (int)s;
        public static string BestTimeKey(RangeStation s) => "range.timems." + (int)s;
        public static string StarsKey(RangeStation s) => "range.stars." + (int)s;
        public static string RunsKey(RangeStation s) => "range.runs." + (int)s;

        public static int GetBestScore(ISettingsStore store, RangeStation s)
            => store == null ? 0 : Math.Max(0, store.GetInt(BestScoreKey(s), 0));

        /// <summary>En iyi süre (ms); kayıt yoksa 0.</summary>
        public static int GetBestTimeMs(ISettingsStore store, RangeStation s)
            => store == null ? 0 : Math.Max(0, store.GetInt(BestTimeKey(s), 0));

        public static int GetBestStars(ISettingsStore store, RangeStation s)
            => store == null ? 0 : Clamp(store.GetInt(StarsKey(s), 0), 0, 3);

        public static int GetRuns(ISettingsStore store, RangeStation s)
            => store == null ? 0 : Math.Max(0, store.GetInt(RunsKey(s), 0));

        /// <summary>
        /// Koşu sonucunu kaydeder. Skor yüksek iyi, süre düşük iyi (timeMs ≤ 0 → süre kaydı yok), yıldız yüksek iyi.
        /// Dönen bayraklar: yeni rekor skoru / yeni rekor süre.
        /// </summary>
        public static void RecordRun(ISettingsStore store, RangeStation s, int score, int timeMs, int stars,
            out bool newBestScore, out bool newBestTime)
        {
            newBestScore = false;
            newBestTime = false;
            if (store == null)
                return;

            if (score > GetBestScore(store, s))
            {
                store.SetInt(BestScoreKey(s), score);
                newBestScore = true;
            }

            var bestTime = GetBestTimeMs(store, s);
            if (timeMs > 0 && (bestTime <= 0 || timeMs < bestTime))
            {
                store.SetInt(BestTimeKey(s), timeMs);
                newBestTime = true;
            }

            if (stars > GetBestStars(store, s))
                store.SetInt(StarsKey(s), Clamp(stars, 0, 3));

            store.SetInt(RunsKey(s), GetRuns(store, s) + 1);
            store.Save();
        }

        public static string FormatTime(int ms)
        {
            if (ms <= 0) return "--";
            var total = ms / 1000f;
            var minutes = (int)(total / 60f);
            var seconds = total - minutes * 60f;
            return minutes.ToString() + ":" + seconds.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string StationName(RangeStation s)
        {
            switch (s)
            {
                case RangeStation.Reaction: return "Tepki Atışı";
                case RangeStation.Rails: return "Ray Hedefleri";
                case RangeStation.ShootHouse: return "Atış Evi";
                case RangeStation.GrenadePit: return "Bomba Çukuru";
                case RangeStation.VehiclePad: return "Araç Pisti";
                default: return "İstasyon";
            }
        }

        public static string StationDescription(RangeStation s)
        {
            switch (s)
            {
                case RangeStation.Reaction:
                    return "Şeritlerde çıkan hedefleri vur. Kafa 100, gövde 60, uzuv 30; ne kadar çabuk vurursan çarpan o kadar yüksek.";
                case RangeStation.Rails:
                    return "Raylarda giden hedefleri 45 sn boyunca vur. Hızlı hedef daha değerli.";
                case RangeStation.ShootHouse:
                    return "Odaları temizle, tehditleri vur, REHİNELERE ateş etme. Par süresi içinde bitir, 3 yıldızı topla.";
                case RangeStation.GrenadePit:
                    return "6 bombayı 20 / 30 / 40 m kovalara at; merkeze yakınlık puan getirir.";
                case RangeStation.VehiclePad:
                    return "Kirpi ile 4 kontrol noktasından geç, en iyi süreni kır.";
                default:
                    return string.Empty;
            }
        }

        // ------------------------------------------------------------------ Menzil subayı

        private static readonly string[][] Barks =
        {
            /* Welcome */ new[] { "Poligona hoş geldin evlat. Silahın emniyetinde, parmağın tetik dışında.", "Menzil açık. Şeritler numaralı, mesafe levhaları 100'den 600 metreye kadar." },
            /* StationStart */ new[] { "Hazır ol... ATEŞ SERBEST!", "Süre başladı, oyalanma!", "Başla! Nişan al, nefes ver, ateş et." },
            /* GoodShot */ new[] { "İyi atış!", "Tam isabet, böyle devam.", "Güzel, hedef yerde." },
            /* Headshot */ new[] { "Kafadan! Keskin nişancı gibi.", "Bu bir madalya vuruşu!" },
            /* Miss */ new[] { "Iskaladın, nişanını düzelt.", "Hedef kaçtı, daha çabuk ol." },
            /* HostageHit */ new[] { "REHİNE! Ateşi kes!", "Sivile ateş ettin, bu ağır bir hata!", "Dostu vurdun, puan kaybettin." },
            /* CourseClear */ new[] { "Bina temiz. Temiz iş çıkardın.", "Tüm odalar güvende, aferin." },
            /* NewRecord */ new[] { "Yeni rekor! Tahtaya yazıyoruz.", "Rekor kırıldı, tebrikler komutanım." },
            /* SlowTime */ new[] { "Par süresini aştın, daha hızlı hareket etmelisin.", "Çok yavaş, savaşta bu seni öldürür." },
            /* CeaseFire */ new[] { "ATEŞİ KES! Silahları indirin.", "Ateş kesildi, silahını boşalt." },
            /* GrenadeOut */ new[] { "Bomba geliyor, siper al!", "Pimi çek, at, yere yat!" },
            /* VehicleStart */ new[] { "Direksiyon sende, kontrol noktalarını sırayla geç.", "Gaza bas ama devrilme!" }
        };

        public static int BarkVariants(RangeBark bark) => Barks[(int)bark].Length;

        /// <summary>Deterministik replik seçimi (seed % varyant sayısı).</summary>
        public static string BarkText(RangeBark bark, int seed)
        {
            var lines = Barks[(int)bark];
            var i = (seed % lines.Length + lines.Length) % lines.Length;
            return lines[i];
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
