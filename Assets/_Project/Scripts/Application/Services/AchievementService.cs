using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>Başarım tanımı (Design/Progression/achievements.json ile aynı alanlar).</summary>
    [Serializable]
    public sealed class AchievementDefinition
    {
        public string id;
        public string title;
        public string description;
        public string metric;
        public int target;
        public int xpReward;
        public string category;
    }

    /// <summary>
    /// Başarım değerlendirmesi (saf C#). Metrikler "metric anahtarı -> ilerleme" olarak tutulur; ilerleme >= hedef olunca
    /// başarım açılır. Açılan kimlikler ve ilerleme <see cref="ISettingsStore"/> üzerinden kalıcıdır.
    /// </summary>
    public sealed class AchievementService
    {
        public const string ProgressKeyPrefix = "ach.p.";
        public const string UnlockedKeyPrefix = "ach.u.";

        private readonly ISettingsStore _store;
        private readonly List<AchievementDefinition> _definitions = new List<AchievementDefinition>();
        private readonly Dictionary<string, int> _progress = new Dictionary<string, int>();
        private readonly HashSet<string> _unlocked = new HashSet<string>();

        /// <summary>Bir başarım açıldığında tetiklenir.</summary>
        public event Action<AchievementDefinition> AchievementUnlocked;

        /// <summary>İlerleme veya açılma değiştiğinde tetiklenir (UI yenilemesi için).</summary>
        public event Action Changed;

        public AchievementService(ISettingsStore store, IEnumerable<AchievementDefinition> definitions)
        {
            _store = store;
            if (definitions == null)
                return;

            var seen = new HashSet<string>();
            foreach (var d in definitions)
            {
                if (d == null || string.IsNullOrEmpty(d.id) || string.IsNullOrEmpty(d.metric) || d.target <= 0)
                    continue;
                if (seen.Add(d.id))
                    _definitions.Add(d);
            }
        }

        public IReadOnlyList<AchievementDefinition> Definitions => _definitions;
        public int UnlockedCount => _unlocked.Count;

        public bool IsUnlocked(string id) => id != null && _unlocked.Contains(id);

        public int GetProgress(string metric) => metric != null && _progress.TryGetValue(metric, out var v) ? v : 0;

        /// <summary>Başarımın görünen ilerlemesi (hedefe kırpılmış).</summary>
        public int GetProgress(AchievementDefinition def)
        {
            if (def == null)
                return 0;
            if (IsUnlocked(def.id))
                return def.target;
            return Math.Min(GetProgress(def.metric), def.target);
        }

        public void Load()
        {
            _progress.Clear();
            _unlocked.Clear();
            if (_store == null)
                return;

            foreach (var d in _definitions)
            {
                var key = ProgressKeyPrefix + d.metric;
                if (!_progress.ContainsKey(d.metric) && _store.HasKey(key))
                    _progress[d.metric] = Math.Max(0, _store.GetInt(key, 0));
                if (_store.GetInt(UnlockedKeyPrefix + d.id, 0) != 0)
                    _unlocked.Add(d.id);
            }
        }

        /// <summary>Metriğe ekleme yapar (olay sayaçları).</summary>
        public void AddProgress(string metric, int amount)
        {
            if (string.IsNullOrEmpty(metric) || amount <= 0)
                return;
            var next = (long)GetProgress(metric) + amount;
            Apply(metric, next > int.MaxValue ? int.MaxValue : (int)next);
        }

        /// <summary>Metriği yalnızca artıyorsa yükseltir (en iyi değer: rütbe, hayatta kalma süresi).</summary>
        public void SetMax(string metric, int value)
        {
            if (string.IsNullOrEmpty(metric) || value <= GetProgress(metric))
                return;
            Apply(metric, value);
        }

        /// <summary>Bir maç sonucundan metrikleri günceller ve değerlendirir. rank: MilitaryRank tamsayı değeri (-1: yok).</summary>
        public void RecordMatch(MatchResult result, int rank)
        {
            Touch("matches", 1);
            if (result.IsWinner)
                Touch("wins", 1);
            Touch("kills", result.Kills);
            Touch("headshots", result.Headshots);
            Touch("damage", (int)Math.Max(0f, result.DamageDealt));
            if (result.IsWinner || (result.TeamPlacement > 0 && result.TeamPlacement <= 3))
                Touch("top3", 1);
            SetMaxQuiet("survival", (int)Math.Max(0f, result.SurvivalSeconds));
            if (rank >= 0)
                SetMaxQuiet("rank", rank);
            Evaluate();
            Persist();
        }

        private void Touch(string metric, int amount)
        {
            if (amount <= 0)
                return;
            var next = (long)GetProgress(metric) + amount;
            _progress[metric] = next > int.MaxValue ? int.MaxValue : (int)next;
        }

        private void SetMaxQuiet(string metric, int value)
        {
            if (value > GetProgress(metric))
                _progress[metric] = value;
        }

        private void Apply(string metric, int value)
        {
            _progress[metric] = value;
            Evaluate();
            Persist();
        }

        private void Evaluate()
        {
            List<AchievementDefinition> fresh = null;
            foreach (var d in _definitions)
            {
                if (_unlocked.Contains(d.id) || GetProgress(d.metric) < d.target)
                    continue;
                _unlocked.Add(d.id);
                (fresh ??= new List<AchievementDefinition>()).Add(d);
                if (_store != null)
                    _store.SetInt(UnlockedKeyPrefix + d.id, 1);
            }

            if (fresh == null)
                return;

            foreach (var d in fresh)
            {
                try { AchievementUnlocked?.Invoke(d); }
                catch (Exception) { /* abone hatası değerlendirmeyi bozmasın */ }
            }
        }

        private void Persist()
        {
            if (_store != null)
            {
                var written = new HashSet<string>();
                foreach (var d in _definitions)
                {
                    if (written.Add(d.metric))
                        _store.SetInt(ProgressKeyPrefix + d.metric, GetProgress(d.metric));
                }
                _store.Save();
            }

            try { Changed?.Invoke(); }
            catch (Exception) { }
        }
    }
}
