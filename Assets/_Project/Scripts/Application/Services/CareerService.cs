using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    public enum MedalTier { None = 0, Bronz = 1, Gumus = 2, Altin = 3 }

    /// <summary>Madalya/şerit tanımı: metrik + 3 kademe eşiği (bronz, gümüş, altın).</summary>
    public sealed class MedalDefinition
    {
        public string Id;
        public string Title;
        public string Description;
        public string Metric;
        /// <summary>true: metrik "en yüksek değer" (tek maç/tek atış rekoru); false: toplamsal.</summary>
        public bool IsMax;
        public int Bronze, Silver, Gold;

        public int Threshold(MedalTier tier) =>
            tier == MedalTier.Bronz ? Bronze : tier == MedalTier.Gumus ? Silver : tier == MedalTier.Altin ? Gold : 0;
    }

    /// <summary>Son maç özeti (geçmiş + sıralama grafiği).</summary>
    public readonly struct CareerMatchEntry
    {
        public readonly int Placement, TotalPlayers, Kills, Headshots, Damage, SurvivalSeconds;
        public readonly bool Won;

        public CareerMatchEntry(int placement, int total, int kills, int headshots, int damage, int survival, bool won)
        {
            Placement = placement; TotalPlayers = total; Kills = kills; Headshots = headshots;
            Damage = damage; SurvivalSeconds = survival; Won = won;
        }
    }

    public sealed class WeaponCareer
    {
        public int Shots, Hits, Kills, Headshots;
        /// <summary>Desimetre (int olarak saklanır).</summary>
        public int LongestKillDm;
        public float Accuracy => Shots > 0 ? Math.Min(1f, (float)Hits / Shots) : 0f;
        public float LongestKillMeters => LongestKillDm / 10f;
    }

    public sealed class MapCareer
    {
        public int Matches, Wins, Kills, BestPlacement;
    }

    /// <summary>
    /// Derin kariyer (saf C#): silah başına istatistik, harita başına istatistik, 30 madalya (3 kademe), son 20 maç geçmişi
    /// ve sezon kartından bağımsız kariyer seviyesi. Her şey ISettingsStore (int) üzerinden kalıcıdır. Silah/harita adları
    /// ISettingsStore sözlüğü sunmadığı için kurucuya verilen "bilinen" listeden geri yüklenir; yeni adlar çalışma anında eklenir.
    /// Rütbe ilerlemesi CareerStatsService'te kalır.
    /// </summary>
    public sealed class CareerService
    {
        public const int HistoryCapacity = 20;
        public const int MaxLevel = 100;
        private const string P = "career.";

        private readonly ISettingsStore _store;
        private readonly Dictionary<string, WeaponCareer> _weapons = new Dictionary<string, WeaponCareer>();
        private readonly Dictionary<string, MapCareer> _maps = new Dictionary<string, MapCareer>();
        private readonly Dictionary<string, int> _metrics = new Dictionary<string, int>();
        private readonly List<CareerMatchEntry> _history = new List<CareerMatchEntry>(HistoryCapacity);
        private readonly List<MedalDefinition> _medals;
        private int _xp;

        public event Action<MedalDefinition, MedalTier> MedalEarned;
        public event Action<int> LevelUp;
        public event Action Changed;

        public CareerService(ISettingsStore store, IEnumerable<string> knownWeapons = null, IEnumerable<string> knownMaps = null)
        {
            _store = store;
            _medals = DefaultMedals();
            if (knownWeapons != null)
                foreach (var w in knownWeapons)
                    if (!string.IsNullOrEmpty(w)) _weapons[w] = new WeaponCareer();
            if (knownMaps != null)
                foreach (var m in knownMaps)
                    if (!string.IsNullOrEmpty(m)) _maps[m] = new MapCareer();
            Load();
        }

        public IReadOnlyList<MedalDefinition> Medals => _medals;
        public IReadOnlyList<CareerMatchEntry> History => _history;
        public IReadOnlyDictionary<string, WeaponCareer> Weapons => _weapons;
        public IReadOnlyDictionary<string, MapCareer> Maps => _maps;
        public int Experience => _xp;
        public int Level => LevelForXp(_xp);

        // ---- Seviye (sezon kartından bağımsız) ----
        /// <summary>Seviye L'ye ulaşmak için toplam TP: 100*L*(L-1) (L1=0, L2=200, L3=600...).</summary>
        public static int XpForLevel(int level) => level <= 1 ? 0 : 100 * level * (level - 1);

        public static int LevelForXp(int xp)
        {
            var level = 1;
            while (level < MaxLevel && xp >= XpForLevel(level + 1))
                level++;
            return level;
        }

        /// <summary>Mevcut seviyede ilerleme 0..1 (son seviyede 1).</summary>
        public float LevelProgress()
        {
            var l = Level;
            if (l >= MaxLevel) return 1f;
            var a = XpForLevel(l);
            return (float)(_xp - a) / (XpForLevel(l + 1) - a);
        }

        /// <summary>Maç tecrübesi: katılım 50 + öldürme 20 + kafa 10 + sıralama bonusu + zafer 300 + hayatta kalma dk*2 (üst 60).</summary>
        public static int MatchXp(MatchResult r)
        {
            var xp = 50 + Math.Max(0, r.Kills) * 20 + Math.Max(0, r.Headshots) * 10;
            if (r.IsWinner) xp += 300;
            else if (r.Placement > 0 && r.Placement <= 3) xp += 120;
            else if (r.Placement > 0 && r.Placement <= 10) xp += 50;
            xp += Math.Min(60, (int)(Math.Max(0f, r.SurvivalSeconds) / 60f) * 2);
            return xp;
        }

        // ---- Kayıt ----
        /// <summary>Maç sonu: weapons = MatchStatsService.GetWeaponStats(id), mapName = MatchConfig.MapName.</summary>
        public void RecordMatch(MatchResult r, IReadOnlyDictionary<string, WeaponMatchStats> weapons, string mapName)
        {
            var before = Level;
            var snapshot = SnapshotTiers();

            _history.Add(new CareerMatchEntry(Math.Max(1, r.Placement), Math.Max(1, r.TotalPlayers), r.Kills, r.Headshots,
                (int)Math.Min(int.MaxValue, Math.Max(0f, r.DamageDealt)), (int)Math.Max(0f, r.SurvivalSeconds), r.IsWinner));
            if (_history.Count > HistoryCapacity)
                _history.RemoveAt(0);

            Add("matches", 1);
            Add("kills", r.Kills);
            Add("headshots", r.Headshots);
            Add("damage", (int)Math.Max(0f, r.DamageDealt));
            Add("survival_min", (int)(Math.Max(0f, r.SurvivalSeconds) / 60f));
            if (r.IsWinner) Add("wins", 1);
            if (r.Placement > 0 && r.Placement <= 3) Add("top3", 1);
            if (r.Placement > 0 && r.Placement <= 10) Add("top10", 1);
            if (r.Kills == 0 && r.Placement > 0 && r.Placement <= 10) Add("ghost_top10", 1);
            if (r.IsWinner && r.Kills == 0) Add("ghost_wins", 1);
            if (r.Accuracy >= 0.5f && r.Kills > 0) Add("accurate_matches", 1);
            if (r.SurvivalSeconds >= 900f) Add("long_survival", 1);
            if (r.IsWinner && r.Kills >= 5) Add("dominant_wins", 1);
            Max("match_kills_max", r.Kills);
            Max("match_headshots_max", r.Headshots);
            Max("match_damage_max", (int)Math.Max(0f, r.DamageDealt));

            if (weapons != null)
            {
                foreach (var kv in weapons)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                    var w = GetOrAddWeapon(kv.Key);
                    w.Shots += kv.Value.Shots;
                    w.Hits += Math.Min(kv.Value.Hits, kv.Value.Shots);
                    w.Kills += kv.Value.Kills;
                    w.Headshots += kv.Value.Headshots;
                    var dm = (int)Math.Round(Math.Max(0f, kv.Value.LongestKill) * 10f);
                    if (dm > w.LongestKillDm) w.LongestKillDm = dm;
                    Max("longest_kill_m", dm / 10);
                    SaveWeapon(kv.Key, w);
                }
            }

            if (!string.IsNullOrEmpty(mapName))
            {
                if (!_maps.TryGetValue(mapName, out var m)) _maps[mapName] = m = new MapCareer();
                m.Matches++;
                if (r.IsWinner) m.Wins++;
                m.Kills += r.Kills;
                if (r.Placement > 0 && (m.BestPlacement == 0 || r.Placement < m.BestPlacement)) m.BestPlacement = r.Placement;
                SaveMap(mapName, m);
            }

            _xp += MatchXp(r);
            FinishUpdate(before, snapshot);
            SaveHistory();
        }

        /// <summary>Dış sistemler için metrik artırır (ör. "heals", "revives", "assists", "grenade_kills", "vehicle_kills").</summary>
        public void AddProgress(string metric, int amount)
        {
            if (string.IsNullOrEmpty(metric) || amount <= 0) return;
            var before = Level;
            var snapshot = SnapshotTiers();
            Add(metric, amount);
            FinishUpdate(before, snapshot);
        }

        public int GetMetric(string metric) => metric != null && _metrics.TryGetValue(metric, out var v) ? v : 0;

        // ---- Madalyalar ----
        public MedalTier GetTier(MedalDefinition d)
        {
            var v = GetMetric(d.Metric);
            return v >= d.Gold ? MedalTier.Altin : v >= d.Silver ? MedalTier.Gumus : v >= d.Bronze ? MedalTier.Bronz : MedalTier.None;
        }

        /// <summary>Sonraki kademeye ilerleme 0..1 (altında 1).</summary>
        public float NextTierProgress(MedalDefinition d)
        {
            var tier = GetTier(d);
            if (tier == MedalTier.Altin) return 1f;
            var lo = d.Threshold(tier);
            var hi = d.Threshold(tier + 1);
            return hi <= lo ? 1f : Math.Max(0f, Math.Min(1f, (float)(GetMetric(d.Metric) - lo) / (hi - lo)));
        }

        public int EarnedMedalCount() { var n = 0; foreach (var d in _medals) if (GetTier(d) != MedalTier.None) n++; return n; }

        // ---- Geçmiş grafiği ----
        /// <summary>Sıralama grafiği için 0..1 değerler (1 = zafer, 0 = son). Eskiden yeniye.</summary>
        public float[] PlacementSeries()
        {
            var s = new float[_history.Count];
            for (var i = 0; i < s.Length; i++)
            {
                var e = _history[i];
                s[i] = e.TotalPlayers <= 1 ? 1f : 1f - (float)(e.Placement - 1) / (e.TotalPlayers - 1);
            }
            return s;
        }

        /// <summary>Metin çubuk grafiği (▁..█), UI yoksa bile gösterilebilir.</summary>
        public string PlacementSparkline()
        {
            const string bars = "▁▂▃▄▅▆▇█";
            var s = PlacementSeries();
            var chars = new char[s.Length];
            for (var i = 0; i < s.Length; i++)
                chars[i] = bars[Math.Max(0, Math.Min(bars.Length - 1, (int)Math.Round(s[i] * (bars.Length - 1))))];
            return new string(chars);
        }

        // ---- İç ----
        private WeaponCareer GetOrAddWeapon(string id)
        {
            if (!_weapons.TryGetValue(id, out var w)) _weapons[id] = w = new WeaponCareer();
            return w;
        }

        private void Add(string metric, int amount)
        {
            if (amount <= 0) return;
            var v = (long)GetMetric(metric) + amount;
            _metrics[metric] = (int)Math.Min(int.MaxValue, v);
        }

        private void Max(string metric, int value)
        {
            if (value > GetMetric(metric)) _metrics[metric] = value;
        }

        private MedalTier[] SnapshotTiers()
        {
            var t = new MedalTier[_medals.Count];
            for (var i = 0; i < t.Length; i++) t[i] = GetTier(_medals[i]);
            return t;
        }

        private void FinishUpdate(int levelBefore, MedalTier[] tiersBefore)
        {
            for (var i = 0; i < _medals.Count; i++)
            {
                var now = GetTier(_medals[i]);
                if (now > tiersBefore[i])
                {
                    _xp += 100 * (int)now; // madalya kademesi ödülü
                    MedalEarned?.Invoke(_medals[i], now);
                }
            }

            var lvl = Level;
            Save();
            if (lvl > levelBefore) LevelUp?.Invoke(lvl);
            Changed?.Invoke();
        }

        private static string Safe(string s) => s.Replace('.', '_');

        private void Save()
        {
            if (_store == null) return;
            _store.SetInt(P + "xp", _xp);
            foreach (var kv in _metrics) _store.SetInt(P + "m." + kv.Key, kv.Value);
            _store.Save();
        }

        private void SaveWeapon(string id, WeaponCareer w)
        {
            if (_store == null) return;
            var k = P + "w." + Safe(id) + ".";
            _store.SetInt(k + "shots", w.Shots); _store.SetInt(k + "hits", w.Hits);
            _store.SetInt(k + "kills", w.Kills); _store.SetInt(k + "hs", w.Headshots);
            _store.SetInt(k + "far", w.LongestKillDm);
        }

        private void SaveMap(string id, MapCareer m)
        {
            if (_store == null) return;
            var k = P + "map." + Safe(id) + ".";
            _store.SetInt(k + "matches", m.Matches); _store.SetInt(k + "wins", m.Wins);
            _store.SetInt(k + "kills", m.Kills); _store.SetInt(k + "best", m.BestPlacement);
        }

        private void SaveHistory()
        {
            if (_store == null) return;
            _store.SetInt(P + "h.n", _history.Count);
            for (var i = 0; i < _history.Count; i++)
            {
                var e = _history[i]; var k = P + "h." + i + ".";
                _store.SetInt(k + "p", e.Placement); _store.SetInt(k + "t", e.TotalPlayers);
                _store.SetInt(k + "k", e.Kills); _store.SetInt(k + "hs", e.Headshots);
                _store.SetInt(k + "d", e.Damage); _store.SetInt(k + "s", e.SurvivalSeconds);
                _store.SetInt(k + "w", e.Won ? 1 : 0);
            }
            _store.Save();
        }

        private void Load()
        {
            if (_store == null) return;
            _xp = Math.Max(0, _store.GetInt(P + "xp", 0));
            foreach (var d in _medals)
                if (!_metrics.ContainsKey(d.Metric)) _metrics[d.Metric] = Math.Max(0, _store.GetInt(P + "m." + d.Metric, 0));
            foreach (var m in new[] { "matches", "kills", "headshots", "damage", "survival_min", "wins", "top3", "top10" })
                if (!_metrics.ContainsKey(m)) _metrics[m] = Math.Max(0, _store.GetInt(P + "m." + m, 0));

            foreach (var kv in _weapons)
            {
                var k = P + "w." + Safe(kv.Key) + ".";
                var w = kv.Value;
                w.Shots = _store.GetInt(k + "shots", 0); w.Hits = _store.GetInt(k + "hits", 0);
                w.Kills = _store.GetInt(k + "kills", 0); w.Headshots = _store.GetInt(k + "hs", 0);
                w.LongestKillDm = _store.GetInt(k + "far", 0);
            }

            foreach (var kv in _maps)
            {
                var k = P + "map." + Safe(kv.Key) + ".";
                var m = kv.Value;
                m.Matches = _store.GetInt(k + "matches", 0); m.Wins = _store.GetInt(k + "wins", 0);
                m.Kills = _store.GetInt(k + "kills", 0); m.BestPlacement = _store.GetInt(k + "best", 0);
            }

            var n = Math.Max(0, Math.Min(HistoryCapacity, _store.GetInt(P + "h.n", 0)));
            for (var i = 0; i < n; i++)
            {
                var k = P + "h." + i + ".";
                _history.Add(new CareerMatchEntry(_store.GetInt(k + "p", 1), _store.GetInt(k + "t", 1), _store.GetInt(k + "k", 0),
                    _store.GetInt(k + "hs", 0), _store.GetInt(k + "d", 0), _store.GetInt(k + "s", 0), _store.GetInt(k + "w", 0) == 1));
            }
        }

        private static MedalDefinition M(string id, string title, string desc, string metric, int b, int s, int g, bool max = false) =>
            new MedalDefinition { Id = id, Title = title, Description = desc, Metric = metric, Bronze = b, Silver = s, Gold = g, IsMax = max };

        /// <summary>30 madalya. Dış metrikler (heals vb.) için AddProgress çağrılmalı.</summary>
        public static List<MedalDefinition> DefaultMedals() => new List<MedalDefinition>
        {
            M("keskin_nisanci", "Keskin Nişancı", "Kafadan isabet", "headshots", 25, 150, 600),
            M("hayalet", "Hayalet", "Hiç etkisiz bırakmadan ilk 10'a gir", "ghost_top10", 1, 5, 20),
            M("sihhiyeci", "Sıhhiyeci", "Yaralı arkadaşı iyileştir", "heals", 10, 60, 250),
            M("nobetci", "Nöbetçi", "Harekata katıl", "matches", 10, 75, 300),
            M("fatih", "Fatih", "Harekat kazan", "wins", 1, 10, 50),
            M("podyum", "Podyum", "İlk 3'e gir", "top3", 3, 25, 100),
            M("hayatta_kalan", "Hayatta Kalan", "İlk 10'a gir", "top10", 5, 40, 150),
            M("avci", "Avcı", "Etkisiz bırak", "kills", 25, 250, 1500),
            M("kirici", "Kırıcı", "Toplam hasar", "damage", 5000, 50000, 300000),
            M("uzun_menzil", "Uzun Menzil", "En uzun etkisiz bırakma (m)", "longest_kill_m", 100, 250, 450, true),
            M("seri_katil", "Seri Av", "Tek harekatta etkisiz bırakma", "match_kills_max", 5, 10, 18, true),
            M("kafa_avcisi", "Kafa Avcısı", "Tek harekatta kafadan isabet", "match_headshots_max", 3, 6, 12, true),
            M("agir_darbe", "Ağır Darbe", "Tek harekatta hasar", "match_damage_max", 500, 1500, 3500, true),
            M("maraton", "Maraton", "Toplam hayatta kalma (dk)", "survival_min", 60, 600, 3000),
            M("sabirli", "Sabırlı", "15 dk+ hayatta kal", "long_survival", 3, 20, 80),
            M("nisanci", "Nişancı", "%50+ isabetle bitir", "accurate_matches", 3, 20, 75),
            M("gizli_zafer", "Gizli Zafer", "Hiç etkisiz bırakmadan kazan", "ghost_wins", 1, 3, 10),
            M("hakimiyet", "Hakimiyet", "5+ leşle kazan", "dominant_wins", 1, 8, 30),
            M("kurtarici", "Kurtarıcı", "Düşen arkadaşı ayağa kaldır", "revives", 5, 30, 120),
            M("destek", "Destek", "Yardım (asist)", "assists", 10, 60, 300),
            M("bombaci", "Bombacı", "El bombasıyla etkisiz bırak", "grenade_kills", 3, 25, 100),
            M("tank_avcisi", "Zırh Avcısı", "Araçla/araca karşı etkisiz bırak", "vehicle_kills", 1, 10, 40),
            M("yagmaci", "Yağmacı", "Düşen ekipmanı topla", "loot_picked", 25, 200, 1000),
            M("kasif", "Kâşif", "Farklı bölgeleri keşfet", "areas_discovered", 5, 20, 60),
            M("pusucu", "Pusucu", "Pusudan etkisiz bırak", "ambush_kills", 3, 20, 80),
            M("tabancaci", "Tabancacı", "Tabancayla etkisiz bırak", "pistol_kills", 3, 25, 100),
            M("pompali", "Pompalı", "Pompalıyla etkisiz bırak", "shotgun_kills", 5, 40, 150),
            M("bicak", "Süngü", "Yakın dövüşle etkisiz bırak", "melee_kills", 1, 10, 40),
            M("havan", "Topçu", "Destek atışıyla etkisiz bırak", "support_kills", 1, 8, 30),
            M("paraşutçu", "Paraşütçü", "Hedef bölgeye isabetli iniş", "precise_landings", 3, 20, 80),
        };
    }
}
