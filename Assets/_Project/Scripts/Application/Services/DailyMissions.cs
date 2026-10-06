using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>Günlük görev şablonu (Resources/Progression/daily_missions.json ile aynı alanlar).</summary>
    public sealed class DailyMissionTemplate
    {
        public string Id;
        public string Title;
        public string Metric;
        public int Target;
        public int Tp;
        public int Keys;

        public DailyMissionTemplate(string id, string title, string metric, int target, int tp, int keys)
        {
            Id = id; Title = title; Metric = metric; Target = target; Tp = tp; Keys = keys;
        }
    }

    /// <summary>Bugünün bir görevi (şablon + kalıcı ilerleme).</summary>
    public sealed class DailyMission
    {
        public int Slot;
        public DailyMissionTemplate Template;
        public int Progress;
        public bool Done;

        public string Title => Template.Title;
        public int Target => Template.Target;
        public int Tp => Template.Tp;
        public int Keys => Template.Keys;
        public float Ratio => Template.Target <= 0 ? 0f : Math.Min(1f, (float)Progress / Template.Target);
    }

    /// <summary>
    /// Günlük görevler (saf C#): 12 şablonluk havuzdan yerel tarihe göre (seed = YYYYMMDD) her gün aynı 3 görev seçilir;
    /// ilerleme EventBus olaylarından (<see cref="Attach"/>) ve <see cref="Record"/> ile gelir; tamamlanınca TP + kozmetik anahtarı
    /// ödülü verilir. Her şey <see cref="ISettingsStore"/> üzerinden kalıcıdır; gün değişince sıfırlanır.
    /// </summary>
    public sealed class DailyMissions : IDisposable
    {
        public const int PerDay = 3;
        public const string DayKey = "daily.day";
        public const string ProgressPrefix = "daily.p.";
        public const string DonePrefix = "daily.d.";
        public const string TotalTpKey = "daily.tp";
        public const string TotalKeysKey = "daily.keys";

        public const string MKills = "kills", MHeadshots = "headshots", MZones = "zones", MLoot = "loot",
            MDistance = "distance", MOrders = "orders", MSmoke = "smoke", MRevives = "revives",
            MGrenadeKills = "grenade_kills", MMatches = "matches", MDamage = "damage", MWins = "wins";

        /// <summary>Varsayılan 12 şablon.</summary>
        public static readonly IReadOnlyList<DailyMissionTemplate> DefaultPool = new List<DailyMissionTemplate>
        {
            new DailyMissionTemplate("d_kills", "{0} leş düşür", MKills, 3, 150, 1),
            new DailyMissionTemplate("d_headshots", "{0} kafadan vuruş yap", MHeadshots, 2, 180, 1),
            new DailyMissionTemplate("d_zones", "{0} bölge daralmasında hayatta kal", MZones, 3, 200, 1),
            new DailyMissionTemplate("d_loot", "{0} ganimet topla", MLoot, 10, 120, 1),
            new DailyMissionTemplate("d_distance", "{0} m koş", MDistance, 2000, 140, 1),
            new DailyMissionTemplate("d_orders", "{0} kez timine komut ver", MOrders, 5, 130, 1),
            new DailyMissionTemplate("d_smoke", "{0} sis bombası kullan", MSmoke, 2, 120, 1),
            new DailyMissionTemplate("d_revives", "{0} yaralı arkadaşını ayağa kaldır", MRevives, 1, 160, 1),
            new DailyMissionTemplate("d_grenade", "{0} el bombasıyla leş düşür", MGrenadeKills, 1, 170, 1),
            new DailyMissionTemplate("d_matches", "{0} maç oyna", MMatches, 2, 110, 1),
            new DailyMissionTemplate("d_damage", "{0} hasar ver", MDamage, 500, 150, 1),
            new DailyMissionTemplate("d_wins", "{0} maç kazan", MWins, 1, 300, 2),
        };

        private readonly ISettingsStore _store;
        private readonly IReadOnlyList<DailyMissionTemplate> _pool;
        private readonly Func<int> _dayProvider;
        private readonly List<DailyMission> _today = new List<DailyMission>();
        private int _day;

        private IEventBus _bus;
        private PlayerId _local;
        private int _team;
        private bool _alive = true;

        /// <summary>Bir görev tamamlandığında (toast için).</summary>
        public event Action<DailyMission> Completed;
        public event Action Changed;

        public DailyMissions(ISettingsStore store, Func<int> dayProvider = null, IReadOnlyList<DailyMissionTemplate> pool = null)
        {
            _store = store;
            _pool = pool != null && pool.Count > 0 ? pool : DefaultPool;
            _dayProvider = dayProvider ?? (() => DayNumber(DateTime.Now));
            EnsureToday();
        }

        public IReadOnlyList<DailyMission> Today { get { EnsureToday(); return _today; } }
        public int Day => _day;
        public int TotalTp => _store != null ? _store.GetInt(TotalTpKey, 0) : 0;
        public int TotalKeys => _store != null ? _store.GetInt(TotalKeysKey, 0) : 0;

        // ---- Saf kurallar ----
        public static int DayNumber(DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;

        /// <summary>Gün numarasından (YYYYMMDD) deterministik PerDay şablon indeksi (tekrarsız).</summary>
        public static int[] PickIndices(int day, int poolSize, int count = PerDay)
        {
            count = Math.Min(count, poolSize);
            var idx = new int[poolSize];
            for (var i = 0; i < poolSize; i++) idx[i] = i;
            var state = (uint)day * 2654435761u + 12345u;
            for (var i = 0; i < count; i++)
            {
                state = state * 1664525u + 1013904223u;
                var j = i + (int)((state >> 8) % (uint)(poolSize - i));
                var t = idx[i]; idx[i] = idx[j]; idx[j] = t;
            }
            var result = new int[count];
            Array.Copy(idx, result, count);
            return result;
        }

        public static string Describe(DailyMission m) => m == null ? "" : string.Format(m.Template.Title, m.Template.Target);

        /// <summary>Tamamlanmamışlar arasında hedefe en yakın görev (hepsi bitmişse null).</summary>
        public static DailyMission ClosestToComplete(IReadOnlyList<DailyMission> missions)
        {
            DailyMission best = null;
            if (missions == null) return null;
            for (var i = 0; i < missions.Count; i++)
            {
                var m = missions[i];
                if (m == null || m.Done) continue;
                if (best == null || m.Ratio > best.Ratio) best = m;
            }
            return best;
        }

        public DailyMission ClosestToComplete() => ClosestToComplete(Today);

        // ---- Durum ----
        public void EnsureToday()
        {
            var day = _dayProvider();
            if (day == _day && _today.Count > 0) return;
            _day = day;
            _today.Clear();
            var storedDay = _store != null ? _store.GetInt(DayKey, 0) : 0;
            var fresh = storedDay != day;
            var picks = PickIndices(day, _pool.Count);
            for (var i = 0; i < picks.Length; i++)
            {
                var m = new DailyMission { Slot = i, Template = _pool[picks[i]] };
                if (!fresh && _store != null)
                {
                    m.Progress = Math.Max(0, _store.GetInt(ProgressPrefix + i, 0));
                    m.Done = _store.GetInt(DonePrefix + i, 0) != 0;
                }
                _today.Add(m);
            }
            if (fresh && _store != null)
            {
                _store.SetInt(DayKey, day);
                for (var i = 0; i < picks.Length; i++)
                {
                    _store.SetInt(ProgressPrefix + i, 0);
                    _store.SetInt(DonePrefix + i, 0);
                }
                _store.Save();
            }
        }

        /// <summary>Metriğe ilerleme ekler (mesafe için metre, vb.).</summary>
        public void Record(string metric, int amount)
        {
            if (string.IsNullOrEmpty(metric) || amount <= 0) return;
            EnsureToday();
            var changed = false;
            for (var i = 0; i < _today.Count; i++)
            {
                var m = _today[i];
                if (m.Done || m.Template.Metric != metric) continue;
                var next = (long)m.Progress + amount;
                m.Progress = (int)Math.Min(next, m.Template.Target);
                changed = true;
                _store?.SetInt(ProgressPrefix + m.Slot, m.Progress);
                if (m.Progress >= m.Template.Target)
                {
                    m.Done = true;
                    _store?.SetInt(DonePrefix + m.Slot, 1);
                    if (_store != null)
                    {
                        _store.SetInt(TotalTpKey, TotalTp + m.Template.Tp);
                        _store.SetInt(TotalKeysKey, TotalKeys + m.Template.Keys);
                    }
                    try { Completed?.Invoke(m); } catch (Exception) { }
                }
            }
            if (!changed) return;
            _store?.Save();
            Changed?.Invoke();
        }

        /// <summary>Maç sonu: maç sayısı ve zafer.</summary>
        public void RecordMatch(MatchResult result)
        {
            Record(MMatches, 1);
            if (result.IsWinner) Record(MWins, 1);
        }

        // ---- EventBus ----
        public void Attach(IEventBus bus, PlayerId local, int localTeam)
        {
            Detach();
            if (bus == null) return;
            _bus = bus; _local = local; _team = localTeam; _alive = true;
            bus.Subscribe<PlayerDiedEvent>(OnDied);
            bus.Subscribe<HitConfirmedEvent>(OnHit);
            bus.Subscribe<LootPickedUpEvent>(OnLoot);
            bus.Subscribe<ZoneStageChangedEvent>(OnZone);
            bus.Subscribe<SquadOrderIssuedEvent>(OnOrder);
            bus.Subscribe<ItemUsedEvent>(OnItem);
            bus.Subscribe<RevivedEvent>(OnRevived);
        }

        public void Detach()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<PlayerDiedEvent>(OnDied);
            _bus.Unsubscribe<HitConfirmedEvent>(OnHit);
            _bus.Unsubscribe<LootPickedUpEvent>(OnLoot);
            _bus.Unsubscribe<ZoneStageChangedEvent>(OnZone);
            _bus.Unsubscribe<SquadOrderIssuedEvent>(OnOrder);
            _bus.Unsubscribe<ItemUsedEvent>(OnItem);
            _bus.Unsubscribe<RevivedEvent>(OnRevived);
            _bus = null;
        }

        public void Dispose() => Detach();

        private void OnDied(PlayerDiedEvent e)
        {
            if (e.VictimId.Equals(_local)) { _alive = false; return; }
            if (!e.KillerId.Equals(_local)) return;
            Record(MKills, 1);
            if (e.IsHeadshot) Record(MHeadshots, 1);
            if (e.WeaponId == DamageSourceIds.FragGrenade) Record(MGrenadeKills, 1);
        }

        private void OnHit(HitConfirmedEvent e)
        {
            if (e.AttackerId.Equals(_local) && !e.VictimId.Equals(_local))
                Record(MDamage, (int)Math.Max(0f, e.Damage));
        }

        private void OnLoot(LootPickedUpEvent e)
        {
            if (e.PlayerId.Equals(_local)) Record(MLoot, 1);
        }

        private void OnZone(ZoneStageChangedEvent e)
        {
            if (_alive && e.Stage == ZoneStage.Shrinking) Record(MZones, 1);
        }

        private void OnOrder(SquadOrderIssuedEvent e)
        {
            if (e.Team == _team) Record(MOrders, 1);
        }

        private void OnItem(ItemUsedEvent e)
        {
            if (e.UserId.Equals(_local) && e.Completed && e.ItemId == ItemIds.SmokeGrenade) Record(MSmoke, 1);
        }

        private void OnRevived(RevivedEvent e)
        {
            if (e.ReviverId.IsValid && e.ReviverId.Equals(_local) && !e.VictimId.Equals(_local)) Record(MRevives, 1);
        }
    }
}
