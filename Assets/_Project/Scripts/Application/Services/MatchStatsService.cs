using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    public sealed class WeaponMatchStats
    {
        public int Shots;
        public int Hits;
        public int Kills;
        public int Headshots;
        public float Damage;
        /// <summary>En uzun leş mesafesi (metre).</summary>
        public float LongestKill;
    }

    /// <summary>
    /// Savaşan başına istatistik: WeaponFiredEvent (atış), HitConfirmedEvent (isabet/hasar/kafa), PlayerDiedEvent (leş,
    /// hayatta kalma süresi = match.MatchElapsedSeconds, öldüren). BuildResult maç sonu ekranı için sonuç üretir.
    /// Kurallar:
    ///  - Dosta (aynı tim) verilen hasar ve dost/kendini öldürme leş sayılmaz.
    ///  - İsabet sayısı atış sayısını aşamaz (saçma tanesi/patlama isabetleri doğruluğu %100'ün üstüne çıkarmaz).
    ///  - Tim leşleri, öldürenin timine yazılır (ITeamRelations: dizin, yoksa maç servisi).
    ///  - Olay sırasından bağımsızdır: MatchService ölümü bu servisten önce ya da sonra işlese de sıralama doğru kalır.
    /// </summary>
    public sealed class MatchStatsService : IDisposable
    {
        private sealed class DeathRecord
        {
            public float SurvivalSeconds;
            public int Placement;
            public string KillerName;
            public PlayerId KillerId;
        }

        private readonly IEventBus _eventBus;
        private readonly IMatchService _match;
        private readonly ICombatantDirectory _directory;
        private readonly ITeamRelations _teams;
        private readonly Dictionary<int, CombatantStats> _stats = new();
        private readonly Dictionary<int, DeathRecord> _deaths = new();
        private readonly Dictionary<int, int> _teamKills = new();
        private readonly Dictionary<int, Dictionary<string, WeaponMatchStats>> _weapons = new();
        private readonly Dictionary<int, LastShot> _lastShot = new();

        private struct LastShot
        {
            public string WeaponId;
            public float Distance;
        }
        private readonly Action<WeaponFiredEvent> _onWeaponFired;
        private readonly Action<HitConfirmedEvent> _onHitConfirmed;
        private readonly Action<PlayerDiedEvent> _onPlayerDied;
        private bool _disposed;

        public MatchStatsService(IEventBus eventBus, IMatchService match, ICombatantDirectory directory)
        {
            _eventBus = eventBus;
            _match = match;
            _directory = directory ?? match as ICombatantDirectory;
            _teams = directory as ITeamRelations ?? match as ITeamRelations;

            _onWeaponFired = OnWeaponFired;
            _onHitConfirmed = OnHitConfirmed;
            _onPlayerDied = OnPlayerDied;

            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onWeaponFired);
                _eventBus.Subscribe(_onHitConfirmed);
                _eventBus.Subscribe(_onPlayerDied);
            }
        }

        /// <summary>Savaşanın istatistikleri (yoksa oluşturulur; asla null değildir).</summary>
        public CombatantStats Get(PlayerId id)
        {
            if (!id.IsValid)
                return new CombatantStats();

            if (!_stats.TryGetValue(id.Value, out var stats))
            {
                stats = new CombatantStats();
                _stats.Add(id.Value, stats);
            }

            return stats;
        }

        /// <summary>Kayıt oluşturmadan okur.</summary>
        public bool TryGet(PlayerId id, out CombatantStats stats)
        {
            stats = null;
            return id.IsValid && _stats.TryGetValue(id.Value, out stats);
        }

        /// <summary>Savaşanın silah başına maç istatistikleri (kayıt oluşturmaz; yoksa boş sözlük).</summary>
        public IReadOnlyDictionary<string, WeaponMatchStats> GetWeaponStats(PlayerId id)
        {
            if (id.IsValid && _weapons.TryGetValue(id.Value, out var map))
                return map;
            return new Dictionary<string, WeaponMatchStats>(0);
        }

        private WeaponMatchStats WeaponOf(PlayerId id, string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
                weaponId = "bilinmeyen";
            if (!_weapons.TryGetValue(id.Value, out var map))
            {
                map = new Dictionary<string, WeaponMatchStats>();
                _weapons.Add(id.Value, map);
            }

            if (!map.TryGetValue(weaponId, out var w))
            {
                w = new WeaponMatchStats();
                map.Add(weaponId, w);
            }

            return w;
        }

        /// <summary>Timin toplam leşi (dost öldürmeler hariç).</summary>
        public int GetTeamKills(int team) => team >= 0 && _teamKills.TryGetValue(team, out var kills) ? kills : 0;

        /// <summary>Öldürenin görünen adı (çevresel ölümde kaynak adı); hayattaysa null.</summary>
        public string GetKillerName(PlayerId id) => id.IsValid && _deaths.TryGetValue(id.Value, out var death) ? death.KillerName : null;

        public bool HasDied(PlayerId id) => id.IsValid && _deaths.ContainsKey(id.Value);

        public MatchResult BuildResult(PlayerId id)
        {
            var stats = id.IsValid && _stats.TryGetValue(id.Value, out var existing) ? existing : new CombatantStats();
            DeathRecord death = null;
            var dead = id.IsValid && _deaths.TryGetValue(id.Value, out death);
            var alive = !dead && (_match == null || _match.IsAlive(id));

            var team = SafeTeam(id);
            var winnerTeam = _match?.WinnerTeam ?? -1;
            var matchOver = _match != null && (_match.CurrentPhase == MatchPhase.Ending || winnerTeam >= 0);

            bool isWinner;
            if (team >= 0 && winnerTeam >= 0)
                isWinner = team == winnerTeam;
            else
                isWinner = _match != null && _match.WinnerId.IsValid && _match.WinnerId == id;

            // Bireysel sıralama.
            int placement;
            if (dead)
            {
                var fromMatch = _match?.GetPlacement(id) ?? 0;
                placement = fromMatch > 0 ? fromMatch : death.Placement;
            }
            else if (isWinner || (matchOver && alive))
                placement = 1;
            else
                placement = Math.Max(1, _match?.AlivePlayerCount ?? 1);

            if (placement <= 0)
                placement = 1;

            var totalPlayers = _match != null && _match.TotalPlayers > 0 ? _match.TotalPlayers : Math.Max(_stats.Count, placement);

            float survival;
            if (dead)
                survival = death.SurvivalSeconds;
            else
                survival = _match?.MatchElapsedSeconds ?? stats.SurvivalSeconds;

            var accuracy = stats.ShotsFired > 0 ? Math.Min(1f, (float)stats.ShotsHit / stats.ShotsFired) : 0f;
            var killerName = dead ? death.KillerName : null;

            // Tim sıralaması.
            var teamPlacement = 0;
            var teamCount = _match?.TeamCount ?? 0;
            if (team >= 0)
            {
                var fromMatch = _match?.GetTeamPlacement(team) ?? 0;
                if (fromMatch > 0)
                    teamPlacement = fromMatch;
                else if (isWinner)
                    teamPlacement = 1;
                else if (_match != null)
                    teamPlacement = Math.Max(1, _match.AliveTeamCount);
            }
            else if (isWinner)
            {
                teamPlacement = 1;
            }
            else
            {
                teamPlacement = placement;
            }

            if (teamCount < teamPlacement)
                teamCount = teamPlacement;

            string teamName = null;
            if (team >= 0 && _teams != null)
            {
                try
                {
                    teamName = _teams.GetTeamName(team);
                }
                catch (Exception)
                {
                    teamName = null;
                }
            }

            stats.Placement = placement;

            return new MatchResult(isWinner, placement, totalPlayers, stats.Kills, stats.Headshots, stats.DamageDealt,
                survival, accuracy, killerName, teamPlacement, teamCount, teamName, GetTeamKills(team));
        }

        /// <summary>Verilen tüm savaşanların sonuçlarını tek özette toplar (sunucu → backend sonucu için).</summary>
        public MatchSummary BuildSummary(IEnumerable<PlayerId> ids, bool timedOut)
        {
            var list = new List<CombatantResult>();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    if (!id.IsValid)
                        continue;

                    var r = BuildResult(id);
                    var team = SafeTeam(id);
                    list.Add(new CombatantResult(id, SafeName(id), team, r.TeamName, r.TeamPlacement, r.Placement,
                        r.Kills, r.Headshots, r.DamageDealt, r.SurvivalSeconds, r.IsWinner));
                }
            }

            return new MatchSummary(list, _match?.WinnerTeam ?? -1, _match?.MatchElapsedSeconds ?? 0f, timedOut);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe(_onWeaponFired);
            _eventBus.Unsubscribe(_onHitConfirmed);
            _eventBus.Unsubscribe(_onPlayerDied);
        }

        private void OnWeaponFired(WeaponFiredEvent e)
        {
            if (!e.ShooterId.IsValid)
                return;

            Get(e.ShooterId).ShotsFired++;

            var weapon = WeaponOf(e.ShooterId, e.WeaponId);
            weapon.Shots++;

            var distance = 0f;
            if (e.HasOrigin && e.Hit.HasHit)
            {
                var dx = e.Hit.HitX - e.Origin.X;
                var dy = e.Hit.HitY - e.Origin.Y;
                var dz = e.Hit.HitZ - e.Origin.Z;
                var d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (!float.IsNaN(d) && !float.IsInfinity(d))
                    distance = d;
            }

            _lastShot[e.ShooterId.Value] = new LastShot { WeaponId = e.WeaponId, Distance = distance };
        }

        private void OnHitConfirmed(HitConfirmedEvent e)
        {
            var attacker = e.AttackerId;
            if (!attacker.IsValid || attacker == e.VictimId || AreAllies(attacker, e.VictimId))
                return;

            var stats = Get(attacker);
            if (e.Damage > 0f && !float.IsNaN(e.Damage) && !float.IsInfinity(e.Damage))
                stats.DamageDealt += e.Damage;

            var hasShot = _lastShot.TryGetValue(attacker.Value, out var shot);
            var weapon = WeaponOf(attacker, hasShot ? shot.WeaponId : null);
            if (e.Damage > 0f && !float.IsNaN(e.Damage) && !float.IsInfinity(e.Damage))
                weapon.Damage += e.Damage;

            if (stats.ShotsHit < stats.ShotsFired)
            {
                stats.ShotsHit++;
                if (weapon.Hits < weapon.Shots)
                    weapon.Hits++;
            }

            if (e.IsHeadshot)
            {
                stats.Headshots++;
                weapon.Headshots++;
            }
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            var victim = e.VictimId;
            if (!victim.IsValid || _deaths.ContainsKey(victim.Value))
                return;

            // Maç bittiyse (kazanan belirlendikten sonra) gelen ölümler sonucu değiştirmez.
            if (_match != null && _match.CurrentPhase == MatchPhase.Ending && _match.IsAlive(victim))
                return;

            var killer = e.KillerId;
            var validKiller = killer.IsValid && killer != victim;

            var death = new DeathRecord
            {
                SurvivalSeconds = _match?.MatchElapsedSeconds ?? 0f,
                Placement = ResolvePlacementAtDeath(victim),
                KillerId = killer,
                KillerName = validKiller ? SafeName(killer) : DamageSourceText.GetDisplayName(e.WeaponId, true)
            };
            _deaths.Add(victim.Value, death);

            var victimStats = Get(victim);
            victimStats.SurvivalSeconds = death.SurvivalSeconds;
            victimStats.Placement = death.Placement;

            if (!validKiller || AreAllies(killer, victim))
                return;

            Get(killer).Kills++;

            var killWeaponId = !string.IsNullOrEmpty(e.WeaponId) ? e.WeaponId
                : (_lastShot.TryGetValue(killer.Value, out var ks) ? ks.WeaponId : null);
            var kw = WeaponOf(killer, killWeaponId);
            kw.Kills++;
            if (_lastShot.TryGetValue(killer.Value, out var lastShot) && lastShot.Distance > kw.LongestKill)
                kw.LongestKill = lastShot.Distance;

            var killerTeam = SafeTeam(killer);
            if (killerTeam >= 0)
                _teamKills[killerTeam] = GetTeamKills(killerTeam) + 1;
        }

        /// <summary>
        /// MatchService bu ölümü zaten işlediyse onun sıralamasını, işlemediyse (abone sırası) hayattaki sayıyı kullanır —
        /// ölüm anındaki hayatta sayısı kurbanı da içerdiğinden ikisi aynı sonucu verir.
        /// </summary>
        private int ResolvePlacementAtDeath(PlayerId victim)
        {
            if (_match == null)
                return 0;

            if (_match.IsAlive(victim))
                return _match.AlivePlayerCount;

            var placement = _match.GetPlacement(victim);
            return placement > 0 ? placement : _match.AlivePlayerCount + 1;
        }

        private bool AreAllies(PlayerId a, PlayerId b)
        {
            if (_teams == null || !a.IsValid || !b.IsValid)
                return false;

            try
            {
                return _teams.AreAllies(a, b);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private int SafeTeam(PlayerId id)
        {
            if (_teams == null || !id.IsValid)
                return -1;

            try
            {
                return _teams.GetTeam(id);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private string SafeName(PlayerId id)
        {
            string name = null;
            if (_directory != null)
            {
                try
                {
                    name = _directory.GetDisplayName(id);
                }
                catch (Exception)
                {
                    name = null;
                }
            }

            return string.IsNullOrEmpty(name) ? "Asker " + id.Value : name;
        }
    }
}
