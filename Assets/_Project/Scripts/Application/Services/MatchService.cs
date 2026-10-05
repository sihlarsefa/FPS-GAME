using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Maç durum makinesi: None → Lobby → PreMatch (geri sayım) → Insertion → InMatch → Ending. Tim tabanlı: son ayakta kalan tim kazanır.
    ///  - Begin(): Lobby'den hemen PreMatch'e geçer.
    ///  - Tick: PreMatch süresi dolunca Insertion. Insertion → InMatch dışarıdan NotifyDropComplete() ile.
    ///  - PlayerDiedEvent'e abone olur: bireysel sıralama = ölüm anındaki hayatta sayısı; tim sıralaması = tim elendiğinde hayatta kalan tim sayısı+1;
    ///    hayatta tek tim (veya hiç) kalınca MatchEndedEvent(winnerId, winnerTeam) yayınlar ve Ending'e geçer. Her geçişte MatchPhaseChangedEvent yayınlar.
    /// Ek kurallar:
    ///  - Yeni oluşturulan maç Lobby fazındadır (kayıtlar bu fazda yapılır).
    ///  - Bilinmeyen ya da zaten ölü kimliklerin ölümleri yok sayılır; maç bittikten sonra sonuçlar donar (ölümler yok sayılır).
    ///  - Tek timli maç (ör. eğitim) ancak tim tamamen ölünce biter (kazanan yok).
    ///  - Negatif tim indeksiyle kaydolan savaşana kendi başına yeni bir tim indeksi atanır.
    /// Görünen adlar çağıran tarafından rütbeli biçimde verilir (RankCatalog.FormatName), burada aynen saklanır.
    /// </summary>
    public sealed class MatchService : IMatchService, IGameTickService, ICombatantDirectory, ITeamRelations, IDisposable
    {
        private static readonly string[] TeamNames =
        {
            "Kartal Timi",
            "Bozkurt Timi",
            "Şimşek Timi",
            "Yıldırım Timi",
            "Kılıç Timi",
            "Kaplan Timi",
            "Pars Timi",
            "Atmaca Timi"
        };

        private static readonly IReadOnlyList<PlayerId> EmptyMembers = Array.Empty<PlayerId>();

        private sealed class CombatantRecord
        {
            public PlayerId Id;
            public string DisplayName;
            public bool IsLocal;
            public int Team;
            public TeamRole Role;
            public bool IsAlive;
            public int Placement;
            public float DeathTime;
        }

        private sealed class TeamRecord
        {
            public readonly int Index;
            public readonly List<PlayerId> Members = new();
            public readonly ReadOnlyCollection<PlayerId> ReadOnlyMembers;
            public int AliveCount;
            public int Placement;

            public TeamRecord(int index)
            {
                Index = index;
                ReadOnlyMembers = Members.AsReadOnly();
            }
        }

        private readonly IEventBus _eventBus;
        private readonly Action<PlayerDiedEvent> _onPlayerDied;
        private readonly Dictionary<int, CombatantRecord> _records = new();
        private readonly List<PlayerId> _registrationOrder = new();
        private readonly List<PlayerId> _alive = new();
        private readonly List<TeamRecord> _teams = new();   // indeks = tim numarası (boşluklar null)

        private MatchPhase _phase = MatchPhase.Lobby;
        private float _phaseElapsed;
        private float _matchElapsed;
        private bool _matchClockStarted;
        private bool _ended;
        private bool _disposed;
        private int _registeredTeamCount;
        private int _aliveTeamCount;
        private int _winnerTeam = -1;
        private PlayerId _winnerId = PlayerId.Invalid;
        private PlayerId _localId = PlayerId.Invalid;

        public MatchService(MatchConfig config, IEventBus eventBus)
        {
            Config = config ?? new MatchConfig();
            _eventBus = eventBus;
            _onPlayerDied = OnPlayerDied;
            _eventBus?.Subscribe(_onPlayerDied);
        }

        /// <summary>Bir tim tamamen elendiğinde (tim, tim sıralaması). Olay yolundan ayrı, sunum kolaylığı için.</summary>
        public event Action<int, int> TeamEliminated;

        public MatchPhase CurrentPhase => _phase;
        public MatchConfig Config { get; }
        public int AlivePlayerCount => _alive.Count;
        public int TotalPlayers => _records.Count;
        public int AliveTeamCount => _aliveTeamCount;

        /// <summary>Kayıtlı (en az bir üyesi olan) tim sayısı; hiç kayıt yoksa yapılandırmadaki tim sayısı.</summary>
        public int TeamCount => _registeredTeamCount > 0 ? _registeredTeamCount : Math.Max(0, Config.TeamCount);

        public int WinnerTeam => _winnerTeam;
        public int LocalTeam => TryGetRecord(_localId, out var record) ? record.Team : -1;
        public PlayerId WinnerId => _winnerId;
        public PlayerId LocalPlayerId => _localId;
        public float PhaseElapsedSeconds => _phaseElapsed;

        /// <summary>Maç sona erdi mi (MatchEndedEvent yayınlandı ya da Ending'e geçildi).</summary>
        public bool HasEnded => _ended;

        /// <summary>PreMatch geri sayımı gibi zamanlı fazlarda kalan süre; değilse 0.</summary>
        public float PhaseRemainingSeconds
        {
            get
            {
                if (_phase != MatchPhase.PreMatch)
                    return 0f;

                var remaining = Config.PreMatchDurationSeconds - _phaseElapsed;
                return remaining > 0f ? remaining : 0f;
            }
        }

        /// <summary>Insertion başlangıcından itibaren geçen süre.</summary>
        public float MatchElapsedSeconds => _matchElapsed;

        /// <summary>Hayattaki savaşanlar (kayıt sırasıyla; canlı görünüm — yinelerken ölüm işlemeyin).</summary>
        public IReadOnlyCollection<PlayerId> AliveIds => _alive;

        /// <summary>Kayıtlı tüm savaşanlar (kayıt sırasıyla).</summary>
        public IReadOnlyList<PlayerId> AllIds => _registrationOrder;

        public void Begin()
        {
            if (_phase != MatchPhase.Lobby && _phase != MatchPhase.None)
                return;

            TransitionTo(MatchPhase.PreMatch);
        }

        public void NotifyDropComplete()
        {
            if (_phase != MatchPhase.Insertion)
                return;

            TransitionTo(MatchPhase.InMatch);
        }

        public void RegisterCombatant(PlayerId id, string displayName, bool isLocalPlayer, int team, TeamRole role)
        {
            if (!id.IsValid)
                return;

            if (_records.TryGetValue(id.Value, out var existing))
            {
                UpdateExisting(existing, displayName, isLocalPlayer, team, role);
                return;
            }

            if (team < 0)
                team = NextFreeTeamIndex();

            var record = new CombatantRecord
            {
                Id = id,
                DisplayName = displayName,
                IsLocal = isLocalPlayer,
                Team = team,
                Role = role,
                IsAlive = true,
                Placement = 0,
                DeathTime = -1f
            };

            _records.Add(id.Value, record);
            _registrationOrder.Add(id);
            _alive.Add(id);
            AddToTeam(record);

            if (isLocalPlayer)
                SetLocal(record);
        }

        public bool IsTeamAlive(int team)
        {
            var record = GetTeamRecord(team);
            return record != null && record.AliveCount > 0;
        }

        public int GetAliveCountInTeam(int team)
        {
            var record = GetTeamRecord(team);
            return record?.AliveCount ?? 0;
        }

        public int GetTeamPlacement(int team)
        {
            var record = GetTeamRecord(team);
            return record?.Placement ?? 0;
        }

        public int GetTeam(PlayerId id) => TryGetRecord(id, out var record) ? record.Team : -1;

        public bool AreAllies(PlayerId a, PlayerId b)
        {
            if (!TryGetRecord(a, out var ra) || !TryGetRecord(b, out var rb))
                return false;

            return ra.Team >= 0 && ra.Team == rb.Team;
        }

        public string GetTeamName(int team)
        {
            if (team < 0)
                return "Bilinmeyen Tim";

            return team < TeamNames.Length ? TeamNames[team] : (team + 1) + ". Tim";
        }

        public TeamRole GetRole(PlayerId id) => TryGetRecord(id, out var record) ? record.Role : TeamRole.Rifleman;

        /// <summary>Timin tüm üyeleri (ölüler dahil, kayıt sırasıyla). Bilinmeyen tim için boş liste.</summary>
        public IReadOnlyList<PlayerId> GetTeamMembers(int team)
        {
            var record = GetTeamRecord(team);
            return record != null ? record.ReadOnlyMembers : EmptyMembers;
        }

        /// <summary>Timin hayattaki üyelerini verilen listeye ekler (çağıran temizler; GC üretmez).</summary>
        public void GetAliveTeamMembers(int team, List<PlayerId> output)
        {
            if (output == null)
                return;

            var record = GetTeamRecord(team);
            if (record == null)
                return;

            for (var i = 0; i < record.Members.Count; i++)
            {
                var member = record.Members[i];
                if (IsAlive(member))
                    output.Add(member);
            }
        }

        /// <summary>Hayattaki timlerin indekslerini artan sırayla verilen listeye ekler (çağıran temizler; GC üretmez).</summary>
        public void GetAliveTeams(List<int> output)
        {
            if (output == null)
                return;

            for (var i = 0; i < _teams.Count; i++)
            {
                var team = _teams[i];
                if (team != null && team.AliveCount > 0)
                    output.Add(team.Index);
            }
        }

        /// <summary>Verilen tim yerel oyuncunun timi mi (yerel oyuncu yoksa false).</summary>
        public bool IsLocalTeam(int team) => team >= 0 && team == LocalTeam;

        /// <summary>Ölüm zamanı (MatchElapsedSeconds cinsinden); hayattaysa ya da bilinmiyorsa -1.</summary>
        public float GetDeathTime(PlayerId id) => TryGetRecord(id, out var record) && !record.IsAlive ? record.DeathTime : -1f;

        public bool IsRegistered(PlayerId id) => id.IsValid && _records.ContainsKey(id.Value);

        public void TransitionTo(MatchPhase phase)
        {
            if (phase == _phase)
                return;

            var previous = _phase;
            _phase = phase;
            _phaseElapsed = 0f;

            if ((phase == MatchPhase.Insertion || phase == MatchPhase.InMatch) && !_matchClockStarted)
            {
                _matchClockStarted = true;
                _matchElapsed = 0f;
            }

            if (phase == MatchPhase.Ending)
                _ended = true;

            _eventBus?.Publish(new MatchPhaseChangedEvent(previous, phase));
        }

        public void RegisterPlayerDeath(PlayerId playerId)
        {
            if (_ended || !TryGetRecord(playerId, out var record) || !record.IsAlive)
                return;

            // Bireysel sıralama: ölüm anında (kendisi dahil) hayatta olan sayısı.
            record.Placement = _alive.Count;
            record.IsAlive = false;
            record.DeathTime = _matchElapsed;
            _alive.Remove(playerId);

            var team = GetTeamRecord(record.Team);
            var eliminated = false;
            if (team != null && team.AliveCount > 0)
            {
                team.AliveCount--;
                if (team.AliveCount == 0)
                {
                    _aliveTeamCount--;
                    team.Placement = _aliveTeamCount + 1;
                    eliminated = true;
                }
            }

            if (!eliminated)
            {
                CheckForMatchEnd();
                return;
            }

            // Abone hata verse bile maç sonu kontrolü mutlaka çalışır (aksi halde maç hiç bitmeyebilir).
            try
            {
                TeamEliminated?.Invoke(team.Index, team.Placement);
            }
            finally
            {
                CheckForMatchEnd();
            }
        }

        public bool IsAlive(PlayerId id) => TryGetRecord(id, out var record) && record.IsAlive;

        public int GetPlacement(PlayerId id) => TryGetRecord(id, out var record) && !record.IsAlive ? record.Placement : 0;

        public string GetDisplayName(PlayerId id)
        {
            if (TryGetRecord(id, out var record) && !string.IsNullOrEmpty(record.DisplayName))
                return record.DisplayName;

            return id.IsValid ? "Asker " + id.Value : "Bilinmeyen";
        }

        public bool IsLocalPlayer(PlayerId id) => TryGetRecord(id, out var record) && record.IsLocal;

        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return;

            _phaseElapsed += deltaTime;

            if (_matchClockStarted && !_ended && (_phase == MatchPhase.Insertion || _phase == MatchPhase.InMatch))
                _matchElapsed += deltaTime;

            if (_phase == MatchPhase.PreMatch)
            {
                var duration = Config.PreMatchDurationSeconds > 0f ? Config.PreMatchDurationSeconds : 0f;
                if (_phaseElapsed >= duration)
                {
                    // Büyük adımda taşan süre Insertion'a aktarılır (deterministik zamanlama).
                    var overflow = _phaseElapsed - duration;
                    var clockWasStarted = _matchClockStarted;
                    TransitionTo(MatchPhase.Insertion);
                    if (_phase == MatchPhase.Insertion)
                    {
                        _phaseElapsed = overflow;
                        if (!clockWasStarted)
                            _matchElapsed = overflow;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _eventBus?.Unsubscribe(_onPlayerDied);
            TeamEliminated = null;
        }

        private void OnPlayerDied(PlayerDiedEvent e) => RegisterPlayerDeath(e.VictimId);

        private void CheckForMatchEnd()
        {
            if (_ended)
                return;

            var shouldEnd = _registeredTeamCount >= 2 ? _aliveTeamCount <= 1 : _registeredTeamCount == 1 && _aliveTeamCount == 0;
            if (!shouldEnd)
                return;

            _ended = true;
            _winnerTeam = FindAliveTeam();
            _winnerId = _winnerTeam >= 0 ? PickWinnerRepresentative(_winnerTeam) : PlayerId.Invalid;

            if (_winnerTeam >= 0)
            {
                var winner = GetTeamRecord(_winnerTeam);
                if (winner != null)
                    winner.Placement = 1;
            }

            _eventBus?.Publish(new MatchEndedEvent(_winnerId, _winnerTeam));
            TransitionTo(MatchPhase.Ending);
        }

        private int FindAliveTeam()
        {
            for (var i = 0; i < _teams.Count; i++)
            {
                var team = _teams[i];
                if (team != null && team.AliveCount > 0)
                    return team.Index;
            }

            return -1;
        }

        /// <summary>Kazanan timi temsil eden üye: yerel oyuncu, yoksa tim komutanı, yoksa ilk hayattaki üye.</summary>
        private PlayerId PickWinnerRepresentative(int teamIndex)
        {
            var team = GetTeamRecord(teamIndex);
            if (team == null)
                return PlayerId.Invalid;

            var firstAlive = PlayerId.Invalid;
            var leader = PlayerId.Invalid;
            for (var i = 0; i < team.Members.Count; i++)
            {
                if (!TryGetRecord(team.Members[i], out var record) || !record.IsAlive)
                    continue;

                if (record.IsLocal)
                    return record.Id;

                if (!firstAlive.IsValid)
                    firstAlive = record.Id;

                if (!leader.IsValid && record.Role == TeamRole.Leader)
                    leader = record.Id;
            }

            return leader.IsValid ? leader : firstAlive;
        }

        private void UpdateExisting(CombatantRecord record, string displayName, bool isLocalPlayer, int team, TeamRole role)
        {
            if (!string.IsNullOrEmpty(displayName))
                record.DisplayName = displayName;

            record.Role = role;

            if (team >= 0 && team != record.Team)
            {
                RemoveFromTeam(record);
                record.Team = team;
                AddToTeam(record);
            }

            if (isLocalPlayer)
                SetLocal(record);
            else if (record.IsLocal)
            {
                record.IsLocal = false;
                if (_localId == record.Id)
                    _localId = PlayerId.Invalid;
            }
        }

        private void SetLocal(CombatantRecord record)
        {
            if (_localId.IsValid && _localId != record.Id && TryGetRecord(_localId, out var previous))
                previous.IsLocal = false;

            record.IsLocal = true;
            _localId = record.Id;
        }

        private void AddToTeam(CombatantRecord record)
        {
            var team = GetOrCreateTeam(record.Team);
            if (team.Members.Count == 0)
                _registeredTeamCount++;

            team.Members.Add(record.Id);

            if (!record.IsAlive)
                return;

            if (team.AliveCount == 0)
            {
                _aliveTeamCount++;
                team.Placement = 0;
            }

            team.AliveCount++;
        }

        private void RemoveFromTeam(CombatantRecord record)
        {
            var team = GetTeamRecord(record.Team);
            if (team == null || !team.Members.Remove(record.Id))
                return;

            if (team.Members.Count == 0)
                _registeredTeamCount--;

            if (!record.IsAlive || team.AliveCount <= 0)
                return;

            team.AliveCount--;
            if (team.AliveCount == 0)
                _aliveTeamCount--;
        }

        private TeamRecord GetOrCreateTeam(int index)
        {
            while (_teams.Count <= index)
                _teams.Add(null);

            return _teams[index] ??= new TeamRecord(index);
        }

        private TeamRecord GetTeamRecord(int index) => index >= 0 && index < _teams.Count ? _teams[index] : null;

        private int NextFreeTeamIndex()
        {
            var index = Math.Max(Config.TeamCount, 0);
            while (index < _teams.Count && _teams[index] != null && _teams[index].Members.Count > 0)
                index++;

            return index;
        }

        private bool TryGetRecord(PlayerId id, out CombatantRecord record)
        {
            if (!id.IsValid)
            {
                record = null;
                return false;
            }

            return _records.TryGetValue(id.Value, out record);
        }
    }
}
