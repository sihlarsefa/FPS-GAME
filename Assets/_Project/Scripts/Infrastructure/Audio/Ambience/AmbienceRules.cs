using System;
using Project.Core.Domain;

namespace Project.Infrastructure.Audio.Ambience
{
    public enum AmbienceBiome
    {
        /// <summary>Çam ormanlı dağ vadisi (Kuzgun Vadisi).</summary>
        DagCam = 0,
        /// <summary>Karlı geçit (Ayaz Geçidi).</summary>
        Kar,
        /// <summary>Kıyı (Mavi Liman).</summary>
        Kiyi,
        /// <summary>Yayla (Kartal Yaylası).</summary>
        Yayla
    }

    public enum AmbienceLoop
    {
        WindGust = 0,
        ForestRustle,
        Insects,
        RainOutdoor,
        RainRoof,
        SnowWind,
        Waves,
        /// <summary>Güçlü rüzgâr katmanı (fırtına/yüksek rakım).</summary>
        WindStrong,
        /// <summary>Gündüz çekirge/yaz böceği.</summary>
        Grasshopper,
        /// <summary>Üs bölgesinde telsiz parazit fısıltısı.</summary>
        RadioHiss
    }

    public enum AmbienceOneShot
    {
        Bird = 0,
        Owl,
        DogBark,
        DogHowl,
        Rooster,
        AxeChop,
        SheepBell,
        Gull,
        DistantGun,
        DistantBlast
    }

    /// <summary>Ortam bağlamı: harita yaşamı, günün saati, hava, iç/dış, köy yakınlığı (saf veri).</summary>
    public struct AmbienceContext
    {
        public AmbienceBiome Biome;
        public TimeOfDay Time;
        public WeatherKind Weather;
        public bool Indoor;
        public bool NearVillage;
        /// <summary>Üs/karakol bölgesinde mi (telsiz parazit fısıltısı).</summary>
        public bool NearBase;
        /// <summary>Canlı yağış şiddeti 0..1 (hava çizelgesi); 0 = yok, yalnız <see cref="Weather"/> geçerli.</summary>
        public float RainLevel;
        /// <summary>Canlı rüzgâr şiddeti 0..1 (WindSystem); 0 = bilinmiyor, biyom varsayılanı kullanılır.</summary>
        public float WindLevel;
    }

    /// <summary>Bağlamdan çıkan yatak planı: döngü seviyeleri (0..1) ve tek seferlik olay oranları (olay/dakika).</summary>
    public sealed class AmbiencePlan
    {
        public readonly float[] LoopLevel = new float[Enum.GetValues(typeof(AmbienceLoop)).Length];
        public readonly float[] OneShotPerMinute = new float[Enum.GetValues(typeof(AmbienceOneShot)).Length];
        /// <summary>Tek seferlik olay ses çarpanı (iç mekânda kısık).</summary>
        public float OneShotVolume = 1f;

        public float Loop(AmbienceLoop l) => LoopLevel[(int)l];
        public float Rate(AmbienceOneShot o) => OneShotPerMinute[(int)o];
    }

    /// <summary>Biyom + günün saati + hava → ortam planı. Saf mantık.</summary>
    public static class AmbienceRules
    {
        public static AmbienceBiome BiomeForMap(string mapId)
        {
            switch (mapId)
            {
                case MapCatalog.AyazGecidi: return AmbienceBiome.Kar;
                case MapCatalog.MaviLiman: return AmbienceBiome.Kiyi;
                case MapCatalog.KartalYaylasi: return AmbienceBiome.Yayla;
                default: return AmbienceBiome.DagCam;
            }
        }

        /// <summary>Resources/Audio/Ambience/&lt;klasör&gt;/ altında aranacak klasör adı.</summary>
        public static string FolderFor(AmbienceBiome b)
        {
            switch (b)
            {
                case AmbienceBiome.Kar: return "kar";
                case AmbienceBiome.Kiyi: return "kiyi";
                case AmbienceBiome.Yayla: return "yayla";
                default: return "dagcam";
            }
        }

        public static string NameFor(AmbienceLoop l) => "loop_" + l.ToString().ToLowerInvariant();

        public static string NameFor(AmbienceOneShot o) => "shot_" + o.ToString().ToLowerInvariant();

        public static AmbiencePlan Build(AmbienceContext c)
        {
            var p = new AmbiencePlan();
            var night = c.Time == TimeOfDay.Gece;
            var dawn = c.Time == TimeOfDay.Safak;
            var dusk = c.Time == TimeOfDay.Aksam;
            var day = c.Time == TimeOfDay.Gunduz;
            var rain = c.Weather == WeatherKind.Yagmur || c.RainLevel > 0.15f;
            // Yağmur kademesi: canlı şiddet varsa ölçekler (hafif çiseleme kısık, sağanak tam); yoksa tam.
            var rainGain = c.RainLevel > 0f ? Clamp01(0.35f + 0.65f * c.RainLevel) : 1f;
            var snow = c.Weather == WeatherKind.Kar || c.Biome == AmbienceBiome.Kar;
            var clear = c.Weather == WeatherKind.Acik && c.Biome != AmbienceBiome.Kar;
            var shelter = c.Indoor ? 0.25f : 1f;

            // Rüzgâr esintileri: dağda güçlü, kıyıda orta, kapalı alanda boğuk.
            float wind;
            switch (c.Biome)
            {
                case AmbienceBiome.Kar: wind = 0.8f; break;
                case AmbienceBiome.Yayla: wind = 0.7f; break;
                case AmbienceBiome.Kiyi: wind = 0.45f; break;
                default: wind = 0.5f; break;
            }

            if (rain) wind *= 1.15f;
            if (c.WindLevel > 0f) wind = Clamp01(wind * (0.6f + 0.8f * Clamp01(c.WindLevel)));
            // Şiddet katmanları: hafif esinti her zaman, güçlü katman yalnız rüzgâr yüksekken devreye girer.
            var strong = Clamp01((wind - 0.55f) / 0.35f);
            p.LoopLevel[(int)AmbienceLoop.WindGust] = Clamp01(wind * shelter * (1f - 0.5f * strong));
            p.LoopLevel[(int)AmbienceLoop.WindStrong] = Clamp01(strong * shelter);

            // Gündüz çekirge/yaz böceği (açık hava, kar yok).
            if (day && clear)
                p.LoopLevel[(int)AmbienceLoop.Grasshopper] = (c.Biome == AmbienceBiome.Yayla ? 0.45f : c.Biome == AmbienceBiome.DagCam ? 0.35f : 0.25f) * (c.Indoor ? 0.15f : 1f);

            // Üs bölgesinde çok kısık telsiz parazit fısıltısı (geceleri biraz daha duyulur).
            if (c.NearBase)
                p.LoopLevel[(int)AmbienceLoop.RadioHiss] = (night ? 0.14f : 0.09f) * (c.Indoor ? 1.4f : 1f);

            // Çam hışırtısı (yalnız ormanlık biyom).
            if (c.Biome == AmbienceBiome.DagCam)
                p.LoopLevel[(int)AmbienceLoop.ForestRustle] = (night ? 0.25f : 0.35f) * shelter;

            // Böcekler: açık havalı gece (cırcır), akşam/şafakta hafif, kar ve yağmurda yok.
            if (clear && !snow)
                p.LoopLevel[(int)AmbienceLoop.Insects] = (night ? 0.45f : dawn || dusk ? 0.2f : 0.1f) * (c.Indoor ? 0.2f : 1f);

            // Yağmur: dışarıda yüzey yağmuru, içeride çatı yağmuru.
            if (rain)
            {
                p.LoopLevel[(int)AmbienceLoop.RainOutdoor] = (c.Indoor ? 0.08f : 0.7f) * rainGain;
                p.LoopLevel[(int)AmbienceLoop.RainRoof] = c.Indoor ? 0.65f * rainGain : 0f;
            }

            if (snow)
                p.LoopLevel[(int)AmbienceLoop.SnowWind] = 0.5f * shelter;

            if (c.Biome == AmbienceBiome.Kiyi)
                p.LoopLevel[(int)AmbienceLoop.Waves] = 0.6f * (c.Indoor ? 0.3f : 1f);

            // Tek seferlik olaylar (olay/dakika).
            var birdBase = c.Biome == AmbienceBiome.DagCam ? 7f : c.Biome == AmbienceBiome.Yayla ? 4f : c.Biome == AmbienceBiome.Kiyi ? 2f : 0f;
            var birds = day ? birdBase : dawn ? birdBase * 1.6f : dusk ? birdBase * 0.55f : 0f;
            if (rain) birds *= 0.2f;
            if (snow) birds = 0f;
            p.OneShotPerMinute[(int)AmbienceOneShot.Bird] = birds;

            if (night && !rain && !snow)
                p.OneShotPerMinute[(int)AmbienceOneShot.Owl] = c.Biome == AmbienceBiome.DagCam ? 2.5f : c.Biome == AmbienceBiome.Yayla ? 1.5f : 0.5f;

            var dogs = c.NearVillage ? (night ? 3.5f : day ? 1.6f : 2.2f) : (night ? 0.5f : 0.2f);
            if (rain) dogs *= 0.5f;
            p.OneShotPerMinute[(int)AmbienceOneShot.DogBark] = dogs;
            p.OneShotPerMinute[(int)AmbienceOneShot.DogHowl] = night && !snow ? (c.NearVillage ? 0.7f : 0.2f) : 0f;

            if (c.NearVillage && !rain)
            {
                p.OneShotPerMinute[(int)AmbienceOneShot.Rooster] = dawn ? 4f : day ? 0.8f : 0f;
                p.OneShotPerMinute[(int)AmbienceOneShot.AxeChop] = day ? 1.5f : 0f;
            }

            if (c.Biome == AmbienceBiome.Yayla && day && !rain)
                p.OneShotPerMinute[(int)AmbienceOneShot.SheepBell] = 2f;

            if (c.Biome == AmbienceBiome.Kiyi && !night && !rain)
                p.OneShotPerMinute[(int)AmbienceOneShot.Gull] = day ? 4f : 1.5f;

            // Uzak çatışma sentetik yedek hızı (gerçek olay yoksa); ayrıntı DistantBattleScheduler'da.
            p.OneShotPerMinute[(int)AmbienceOneShot.DistantGun] = 0f;
            p.OneShotPerMinute[(int)AmbienceOneShot.DistantBlast] = 0f;

            p.OneShotVolume = c.Indoor ? 0.3f : 1f;
            return p;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>Küçük deterministik xorshift (testlerde sabit sonuç).</summary>
    public struct AmbienceRng
    {
        private uint _s;

        public AmbienceRng(uint seed) => _s = seed == 0 ? 0x9E3779B9u : seed;

        public uint Next()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return _s;
        }

        public float Unit() => (Next() >> 8) * (1f / 16777216f);
        public bool Chance(float p) => Unit() < p;
        public float Range(float a, float b) => a + (b - a) * Unit();
        public int RangeInt(int minInclusive, int maxExclusive) => maxExclusive <= minInclusive ? minInclusive : minInclusive + (int)(Next() % (uint)(maxExclusive - minInclusive));
    }

    /// <summary>Tek seferlik ortam olayı (konum polar: yön + mesafe).</summary>
    public struct AmbienceShot
    {
        public AmbienceOneShot Kind;
        public int Variant;
        public float AngleDeg;
        public float Distance;
        public float Volume;
        public float Pitch;
    }

    /// <summary>Plan oranlarına göre rastgele tek seferlik ortam olayları zamanlar (Poisson, tür başına en az aralık).</summary>
    public sealed class AmbienceOneShotScheduler
    {
        public const int Variants = 3;
        private const float MinGapSeconds = 5f;
        private const float GlobalGapSeconds = 1.2f;

        private readonly float[] _lastTime = new float[Enum.GetValues(typeof(AmbienceOneShot)).Length];
        private AmbienceRng _rng;
        private float _lastAny = -100f;

        public AmbienceOneShotScheduler(uint seed = 12345u)
        {
            _rng = new AmbienceRng(seed);
            for (var i = 0; i < _lastTime.Length; i++)
                _lastTime[i] = -100f;
        }

        public bool TryTick(AmbiencePlan plan, float now, float dt, out AmbienceShot shot)
        {
            shot = default;
            if (plan == null || dt <= 0f || now - _lastAny < GlobalGapSeconds)
                return false;

            for (var i = 0; i < plan.OneShotPerMinute.Length; i++)
            {
                var rate = plan.OneShotPerMinute[i];
                if (rate <= 0f || now - _lastTime[i] < MinGapSeconds)
                    continue;
                if (_rng.Unit() >= rate / 60f * dt)
                    continue;

                var kind = (AmbienceOneShot)i;
                _lastTime[i] = now;
                _lastAny = now;
                shot = new AmbienceShot
                {
                    Kind = kind,
                    Variant = _rng.RangeInt(0, Variants),
                    AngleDeg = _rng.Range(0f, 360f),
                    Distance = DistanceFor(kind, ref _rng),
                    Volume = plan.OneShotVolume * _rng.Range(0.6f, 1f),
                    Pitch = _rng.Range(0.94f, 1.06f)
                };
                return true;
            }

            return false;
        }

        public static float DistanceFor(AmbienceOneShot k, ref AmbienceRng rng)
        {
            switch (k)
            {
                case AmbienceOneShot.Bird: return rng.Range(8f, 45f);
                case AmbienceOneShot.Owl: return rng.Range(25f, 70f);
                case AmbienceOneShot.DogHowl: return rng.Range(120f, 260f);
                case AmbienceOneShot.DogBark: return rng.Range(50f, 200f);
                case AmbienceOneShot.Rooster: return rng.Range(60f, 180f);
                case AmbienceOneShot.AxeChop: return rng.Range(80f, 200f);
                case AmbienceOneShot.SheepBell: return rng.Range(30f, 110f);
                case AmbienceOneShot.Gull: return rng.Range(20f, 90f);
                default: return rng.Range(100f, 300f);
            }
        }
    }
}
