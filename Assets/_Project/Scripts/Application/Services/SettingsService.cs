using System;
using System.Text;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// GameSettings'i ISettingsStore'a anahtar/değer olarak kaydeder/yükler (değerler güvenli aralıklara kırpılır).
    /// Mağaza yalnızca int/float desteklediğinden oyuncu adı karakter kodları olarak saklanır.
    /// Mağaza null ise ayarlar yalnızca bellekte tutulur.
    /// </summary>
    public sealed class SettingsService
    {
        public static class Keys
        {
            public const string MouseSensitivity = "settings.mouseSensitivity";
            public const string AdsSensitivityMultiplier = "settings.adsSensitivity";
            public const string FieldOfView = "settings.fov";
            public const string MasterVolume = "settings.masterVolume";
            public const string AmbientVolume = "settings.ambientVolume";
            public const string InvertY = "settings.invertY";
            public const string QualityLevel = "settings.quality";
            public const string Fullscreen = "settings.fullscreen";
            public const string ShowFps = "settings.showFps";
            public const string TeamCount = "settings.teamCount";
            public const string Difficulty = "settings.difficulty";
            public const string Insertion = "settings.insertion";
            public const string PlayerNameLength = "settings.playerName.length";
            public const string PlayerNameCharPrefix = "settings.playerName.c";
        }

        public const float MinMouseSensitivity = 0.01f;
        public const float MaxMouseSensitivity = 1f;
        public const float MinAdsMultiplier = 0.2f;
        public const float MaxAdsMultiplier = 1.5f;
        public const float MinFieldOfView = 60f;
        public const float MaxFieldOfView = 110f;
        public const int MinQualityLevel = 0;
        public const int MaxQualityLevel = 3;
        public const int MinTeamCount = 2;
        public const int MaxTeamCount = 6;
        public const int TeamSize = 10;
        public const int MaxPlayerNameLength = 16;
        public const string DefaultPlayerName = "Komutan";

        private readonly ISettingsStore _store;
        private GameSettings _current;

        public SettingsService(ISettingsStore store)
        {
            _store = store;
            _current = Sanitize(new GameSettings());
        }

        /// <summary>Geçerli (kırpılmış) ayarlar. Değiştirmek için kopyalayıp Apply() çağırın.</summary>
        public GameSettings Current => _current;

        public event Action<GameSettings> Changed;

        public void Load()
        {
            var defaults = new GameSettings();
            var loaded = defaults.Clone();
            if (_store != null)
            {
                try
                {
                    loaded.MouseSensitivity = GetFloat(Keys.MouseSensitivity, defaults.MouseSensitivity);
                    loaded.AdsSensitivityMultiplier = GetFloat(Keys.AdsSensitivityMultiplier, defaults.AdsSensitivityMultiplier);
                    loaded.FieldOfView = GetFloat(Keys.FieldOfView, defaults.FieldOfView);
                    loaded.MasterVolume = GetFloat(Keys.MasterVolume, defaults.MasterVolume);
                    loaded.AmbientVolume = GetFloat(Keys.AmbientVolume, defaults.AmbientVolume);
                    loaded.InvertY = GetBool(Keys.InvertY, defaults.InvertY);
                    loaded.QualityLevel = GetInt(Keys.QualityLevel, defaults.QualityLevel);
                    loaded.Fullscreen = GetBool(Keys.Fullscreen, defaults.Fullscreen);
                    loaded.ShowFps = GetBool(Keys.ShowFps, defaults.ShowFps);
                    loaded.TeamCount = GetInt(Keys.TeamCount, defaults.TeamCount);
                    loaded.Difficulty = (BotDifficulty)GetInt(Keys.Difficulty, (int)defaults.Difficulty);
                    loaded.Insertion = (InsertionMethod)GetInt(Keys.Insertion, (int)defaults.Insertion);
                    loaded.PlayerName = LoadName() ?? defaults.PlayerName;
                }
                catch (Exception)
                {
                    // Bozuk/erişilemeyen mağaza: varsayılanlarla devam.
                    loaded = defaults.Clone();
                }
            }

            _current = Sanitize(loaded);
            Changed?.Invoke(_current);
        }

        /// <summary>Ayarları kırpar, saklar (Save) ve Changed yayınlar. null yok sayılır.</summary>
        public void Apply(GameSettings settings)
        {
            if (settings == null)
                return;

            _current = Sanitize(settings);
            Persist(_current);
            Changed?.Invoke(_current);
        }

        /// <summary>
        /// Geçerli ayarların bir kopyasını değiştirip uygular: <c>settings.Modify(s =&gt; s.FieldOfView = 90f)</c>.
        /// mutate null ise bir şey yapılmaz.
        /// </summary>
        public void Modify(Action<GameSettings> mutate)
        {
            if (mutate == null)
                return;

            var copy = _current.Clone();
            mutate(copy);
            Apply(copy);
        }

        /// <summary>Geçerli ayarlardan maç yapılandırması alanlarını (tim sayısı, zorluk, intikal) doldurur.</summary>
        public MatchConfig CreateMatchConfig()
        {
            var config = new MatchConfig().WithTeams(_current.TeamCount, TeamSize);
            config.Difficulty = _current.Difficulty;
            config.PlayerInsertion = _current.Insertion;
            return config;
        }

        /// <summary>Varsayılan ayarlara döner ve kaydeder.</summary>
        public void ResetToDefaults() => Apply(new GameSettings());

        /// <summary>Kırpılmış bir kopya döndürür (girdi değiştirilmez).</summary>
        public static GameSettings Sanitize(GameSettings settings)
        {
            var s = settings != null ? settings.Clone() : new GameSettings();
            var defaults = new GameSettings();

            s.MouseSensitivity = ClampFloat(s.MouseSensitivity, MinMouseSensitivity, MaxMouseSensitivity, defaults.MouseSensitivity);
            s.AdsSensitivityMultiplier = ClampFloat(s.AdsSensitivityMultiplier, MinAdsMultiplier, MaxAdsMultiplier, defaults.AdsSensitivityMultiplier);
            s.FieldOfView = ClampFloat(s.FieldOfView, MinFieldOfView, MaxFieldOfView, defaults.FieldOfView);
            s.MasterVolume = ClampFloat(s.MasterVolume, 0f, 1f, defaults.MasterVolume);
            s.AmbientVolume = ClampFloat(s.AmbientVolume, 0f, 1f, defaults.AmbientVolume);
            s.QualityLevel = ClampInt(s.QualityLevel, MinQualityLevel, MaxQualityLevel);
            s.TeamCount = ClampInt(s.TeamCount, MinTeamCount, MaxTeamCount);
            s.Difficulty = (BotDifficulty)ClampInt((int)s.Difficulty, (int)BotDifficulty.Easy, (int)BotDifficulty.Hard);
            s.Insertion = (InsertionMethod)ClampInt((int)s.Insertion, (int)InsertionMethod.Helicopter, (int)InsertionMethod.ArmoredVehicle);
            s.PlayerName = SanitizeName(s.PlayerName);
            s.BotCount = s.TeamCount * TeamSize - 1;
            return s;
        }

        /// <summary>Oyuncu adını kırpar: boşluklar temizlenir, kontrol karakterleri atılır, en fazla 16 karakter.</summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return DefaultPlayerName;

            var builder = new StringBuilder(MaxPlayerNameLength);
            var trimmed = name.Trim();
            for (var i = 0; i < trimmed.Length && builder.Length < MaxPlayerNameLength; i++)
            {
                var c = trimmed[i];
                if (char.IsControl(c) || char.IsSurrogate(c))
                    continue;
                builder.Append(c);
            }

            var result = builder.ToString().Trim();
            return result.Length == 0 ? DefaultPlayerName : result;
        }

        private void Persist(GameSettings s)
        {
            if (_store == null)
                return;

            try
            {
                _store.SetFloat(Keys.MouseSensitivity, s.MouseSensitivity);
                _store.SetFloat(Keys.AdsSensitivityMultiplier, s.AdsSensitivityMultiplier);
                _store.SetFloat(Keys.FieldOfView, s.FieldOfView);
                _store.SetFloat(Keys.MasterVolume, s.MasterVolume);
                _store.SetFloat(Keys.AmbientVolume, s.AmbientVolume);
                _store.SetInt(Keys.InvertY, s.InvertY ? 1 : 0);
                _store.SetInt(Keys.QualityLevel, s.QualityLevel);
                _store.SetInt(Keys.Fullscreen, s.Fullscreen ? 1 : 0);
                _store.SetInt(Keys.ShowFps, s.ShowFps ? 1 : 0);
                _store.SetInt(Keys.TeamCount, s.TeamCount);
                _store.SetInt(Keys.Difficulty, (int)s.Difficulty);
                _store.SetInt(Keys.Insertion, (int)s.Insertion);
                SaveName(s.PlayerName);
                _store.Save();
            }
            catch (Exception)
            {
                // Kalıcılık hatası oyunu durdurmamalı; ayarlar bellekte geçerli kalır.
            }
        }

        private void SaveName(string name)
        {
            var length = Math.Min(name.Length, MaxPlayerNameLength);
            _store.SetInt(Keys.PlayerNameLength, length);
            for (var i = 0; i < length; i++)
                _store.SetInt(Keys.PlayerNameCharPrefix + i, name[i]);
        }

        private string LoadName()
        {
            if (!_store.HasKey(Keys.PlayerNameLength))
                return null;

            var length = _store.GetInt(Keys.PlayerNameLength, 0);
            if (length <= 0 || length > MaxPlayerNameLength)
                return null;

            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                var code = _store.GetInt(Keys.PlayerNameCharPrefix + i, ' ');
                chars[i] = code > 0 && code <= char.MaxValue ? (char)code : ' ';
            }

            return new string(chars);
        }

        private float GetFloat(string key, float fallback) => _store.HasKey(key) ? _store.GetFloat(key, fallback) : fallback;
        private int GetInt(string key, int fallback) => _store.HasKey(key) ? _store.GetInt(key, fallback) : fallback;
        private bool GetBool(string key, bool fallback) => _store.HasKey(key) ? _store.GetInt(key, fallback ? 1 : 0) != 0 : fallback;

        private static float ClampFloat(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return fallback;
            return value < min ? min : value > max ? max : value;
        }

        private static int ClampInt(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// Kariyer istatistikleri (maç, galibiyet, leş, en iyi sıra...). Tecrübe (XP) =
    /// leş*100 + kafadan vuruş*25 + max(0, TimSayısı - TimSırası)*150 + galibiyet 1000; rütbe XP'den RankCatalog ile türetilir.
    /// </summary>
    public sealed class CareerStatsService
    {
        public static class Keys
        {
            public const string Matches = "career.matches";
            public const string Wins = "career.wins";
            public const string Kills = "career.kills";
            public const string Headshots = "career.headshots";
            public const string BestPlacement = "career.bestPlacement";
            public const string TotalDamage = "career.totalDamage";
            public const string LongestSurvival = "career.longestSurvival";
            public const string Experience = "career.experience";
            public const string Rank = "career.rank";
        }

        public const int ExperiencePerKill = 100;
        public const int ExperiencePerHeadshot = 25;
        public const int ExperiencePerTeamOutlasted = 150;
        public const int ExperienceForWin = 1000;

        private readonly ISettingsStore _store;
        private CareerStats _current = new();

        public CareerStatsService(ISettingsStore store)
        {
            _store = store;
        }

        public CareerStats Current => _current;

        /// <summary>Son kaydedilen maçta kazanılan XP.</summary>
        public int LastExperienceGained { get; private set; }

        /// <summary>Son maçtan önceki rütbe.</summary>
        public MilitaryRank LastPreviousRank { get; private set; } = MilitaryRank.Er;

        /// <summary>Son maçta terfi edildi mi?</summary>
        public bool PromotedLastMatch => _current.Rank > LastPreviousRank;

        /// <summary>Kariyer değiştiğinde (Load/Record).</summary>
        public event Action<CareerStats> Changed;

        /// <summary>Terfi olduğunda (eski rütbe, yeni rütbe).</summary>
        public event Action<MilitaryRank, MilitaryRank> Promoted;

        public static int ComputeExperience(MatchResult result)
        {
            long xp = (long)Math.Max(0, result.Kills) * ExperiencePerKill
                      + (long)Math.Max(0, result.Headshots) * ExperiencePerHeadshot
                      + (long)Math.Max(0, result.TeamCount - result.TeamPlacement) * ExperiencePerTeamOutlasted
                      + (result.IsWinner ? ExperienceForWin : 0);
            return xp > int.MaxValue ? int.MaxValue : (int)xp;
        }

        public void Load()
        {
            var stats = new CareerStats();
            if (_store != null)
            {
                try
                {
                    stats.Matches = Math.Max(0, GetInt(Keys.Matches));
                    stats.Wins = Math.Max(0, GetInt(Keys.Wins));
                    stats.Kills = Math.Max(0, GetInt(Keys.Kills));
                    stats.Headshots = Math.Max(0, GetInt(Keys.Headshots));
                    stats.BestPlacement = Math.Max(0, GetInt(Keys.BestPlacement));
                    stats.TotalDamage = SanitizeFloat(GetFloat(Keys.TotalDamage));
                    stats.LongestSurvivalSeconds = SanitizeFloat(GetFloat(Keys.LongestSurvival));
                    stats.Experience = Math.Max(0, GetInt(Keys.Experience));
                }
                catch (Exception)
                {
                    stats = new CareerStats();
                }
            }

            stats.Rank = RankCatalog.RankForExperience(stats.Experience);
            _current = stats;
            LastExperienceGained = 0;
            LastPreviousRank = stats.Rank;
            Changed?.Invoke(_current);
        }

        public void Record(MatchResult result)
        {
            var stats = _current;
            var previousRank = stats.Rank;

            stats.Matches = SafeAdd(stats.Matches, 1);
            if (result.IsWinner)
                stats.Wins = SafeAdd(stats.Wins, 1);
            stats.Kills = SafeAdd(stats.Kills, Math.Max(0, result.Kills));
            stats.Headshots = SafeAdd(stats.Headshots, Math.Max(0, result.Headshots));

            var placement = result.TeamPlacement > 0 ? result.TeamPlacement : result.Placement;
            if (result.IsWinner)
                placement = 1;
            if (placement > 0 && (stats.BestPlacement <= 0 || placement < stats.BestPlacement))
                stats.BestPlacement = placement;

            stats.TotalDamage += SanitizeFloat(result.DamageDealt);
            var survival = SanitizeFloat(result.SurvivalSeconds);
            if (survival > stats.LongestSurvivalSeconds)
                stats.LongestSurvivalSeconds = survival;

            var gained = ComputeExperience(result);
            stats.Experience = SafeAdd(stats.Experience, gained);
            stats.Rank = RankCatalog.RankForExperience(stats.Experience);

            LastExperienceGained = gained;
            LastPreviousRank = previousRank;

            Persist(stats);
            Changed?.Invoke(stats);
            if (stats.Rank > previousRank)
                Promoted?.Invoke(previousRank, stats.Rank);
        }

        /// <summary>Kariyeri sıfırlar ve kaydeder.</summary>
        public void ResetCareer()
        {
            _current = new CareerStats();
            LastExperienceGained = 0;
            LastPreviousRank = MilitaryRank.Er;
            Persist(_current);
            Changed?.Invoke(_current);
        }

        private void Persist(CareerStats stats)
        {
            if (_store == null)
                return;

            try
            {
                _store.SetInt(Keys.Matches, stats.Matches);
                _store.SetInt(Keys.Wins, stats.Wins);
                _store.SetInt(Keys.Kills, stats.Kills);
                _store.SetInt(Keys.Headshots, stats.Headshots);
                _store.SetInt(Keys.BestPlacement, stats.BestPlacement);
                _store.SetFloat(Keys.TotalDamage, stats.TotalDamage);
                _store.SetFloat(Keys.LongestSurvival, stats.LongestSurvivalSeconds);
                _store.SetInt(Keys.Experience, stats.Experience);
                _store.SetInt(Keys.Rank, (int)stats.Rank);
                _store.Save();
            }
            catch (Exception)
            {
                // Kalıcılık hatası maç sonunu bozmamalı.
            }
        }

        private int GetInt(string key) => _store.HasKey(key) ? _store.GetInt(key, 0) : 0;
        private float GetFloat(string key) => _store.HasKey(key) ? _store.GetFloat(key, 0f) : 0f;

        private static float SanitizeFloat(float value) => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;

        private static int SafeAdd(int a, int b)
        {
            var sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : sum < 0 ? 0 : (int)sum;
        }
    }
}
