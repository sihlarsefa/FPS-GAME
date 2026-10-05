using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Komuta zinciri: her timin üyeleri rütbe (sonra kayıt sırası) ile sıralanır; en kıdemli hayatta olan komutandır.
    /// PlayerDiedEvent'te komutan ölürse sıradakine geçer ve CommandTransferredEvent yayınlar.
    /// Register çağrıları olay yayınlamaz (maç kurulumu sırasında sessizce zincir oluşur).
    /// </summary>
    public sealed class ChainOfCommandService : IDisposable
    {
        private sealed class Member
        {
            public PlayerId Id;
            public int Team;
            public MilitaryRank Rank;
            public long Order;
            public bool Alive;
        }

        private sealed class TeamChain
        {
            public readonly List<Member> Members = new(10);
            public readonly List<PlayerId> AliveChain = new(10);
            public bool Dirty = true;
        }

        private static readonly IReadOnlyList<PlayerId> EmptyChain = Array.Empty<PlayerId>();

        private readonly IEventBus _eventBus;
        private readonly Dictionary<PlayerId, Member> _members = new();
        private readonly Dictionary<int, TeamChain> _teams = new();
        private long _nextOrder;
        private bool _disposed;

        public ChainOfCommandService(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus?.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        /// <summary>Komutan değiştiğinde (ölüm sonrası devir) — olay veri yoluna ek olarak.</summary>
        public event Action<CommandTransferredEvent> CommandTransferred;

        public int RegisteredCount => _members.Count;

        /// <summary>
        /// Askeri timine rütbesiyle kaydeder (hayatta kabul edilir). Aynı id tekrar kaydedilirse timi/rütbesi güncellenir
        /// ve zincirin sonuna (aynı rütbedekiler arasında) eklenir.
        /// </summary>
        public void Register(PlayerId id, int team, MilitaryRank rank)
        {
            if (!id.IsValid)
                return;

            if (_members.TryGetValue(id, out var existing))
                RemoveFromTeam(existing);
            else
            {
                existing = new Member { Id = id };
                _members[id] = existing;
            }

            existing.Team = team;
            existing.Rank = rank;
            existing.Order = _nextOrder++;
            existing.Alive = true;

            var chain = GetOrCreateTeam(team);
            InsertSorted(chain.Members, existing);
            chain.Dirty = true;
        }

        /// <summary>Askeri zincirden tamamen çıkarır (olay yayınlamaz).</summary>
        public void Unregister(PlayerId id)
        {
            if (!_members.TryGetValue(id, out var member))
                return;

            RemoveFromTeam(member);
            _members.Remove(id);
        }

        public bool IsRegistered(PlayerId id) => _members.ContainsKey(id);

        public bool IsAlive(PlayerId id) => _members.TryGetValue(id, out var member) && member.Alive;

        public MilitaryRank GetRank(PlayerId id) => _members.TryGetValue(id, out var member) ? member.Rank : MilitaryRank.Er;

        /// <summary>Askerin timi; kayıtlı değilse -1.</summary>
        public int GetTeam(PlayerId id) => _members.TryGetValue(id, out var member) ? member.Team : -1;

        /// <summary>Timin en kıdemli hayatta olan üyesi; kimse yoksa PlayerId.Invalid.</summary>
        public PlayerId GetCommander(int team)
        {
            var chain = GetChain(team);
            return chain.Count > 0 ? chain[0] : PlayerId.Invalid;
        }

        /// <summary>Komutan yardımcısı (zincirde 2. sıradaki hayatta olan); yoksa PlayerId.Invalid.</summary>
        public PlayerId GetDeputy(int team)
        {
            var chain = GetChain(team);
            return chain.Count > 1 ? chain[1] : PlayerId.Invalid;
        }

        /// <summary>Timin hayatta olan kayıtlı üye sayısı.</summary>
        public int GetAliveCount(int team) => GetChain(team).Count;

        /// <summary>Timin kayıtlı (ölü dahil) üye sayısı.</summary>
        public int GetMemberCount(int team) => _teams.TryGetValue(team, out var chain) ? chain.Members.Count : 0;

        public bool IsCommander(PlayerId id)
        {
            if (!_members.TryGetValue(id, out var member) || !member.Alive)
                return false;

            return GetCommander(member.Team) == id;
        }

        /// <summary>Komuta sırasındaki yeri (0 = komutan); ölü veya kayıtsızsa -1.</summary>
        public int GetChainIndex(PlayerId id)
        {
            if (!_members.TryGetValue(id, out var member) || !member.Alive)
                return -1;

            var chain = GetChain(member.Team);
            for (var i = 0; i < chain.Count; i++)
            {
                if (chain[i] == id)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Komuta sırası (0 = komutan), ölüler hariç. Dönen liste önbelleklenir (GC yok) ve zincir değişene kadar geçerlidir;
        /// saklamak isteyen kopyalamalıdır.
        /// </summary>
        public IReadOnlyList<PlayerId> GetChain(int team)
        {
            if (!_teams.TryGetValue(team, out var chain))
                return EmptyChain;

            if (chain.Dirty)
                Rebuild(chain);

            return chain.AliveChain;
        }

        /// <summary>Askeri ölü olarak işaretler; komutansa komutayı devreder (PlayerDiedEvent ile aynı mantık).</summary>
        public void MarkDead(PlayerId id)
        {
            if (!_members.TryGetValue(id, out var member) || !member.Alive)
                return;

            var team = member.Team;
            var previousCommander = GetCommander(team);
            member.Alive = false;
            if (_teams.TryGetValue(team, out var chain))
                chain.Dirty = true;

            if (previousCommander != id)
                return;

            var newCommander = GetCommander(team);
            if (!newCommander.IsValid)
                return;

            var transferred = new CommandTransferredEvent(team, id, newCommander);
            _eventBus?.Publish(transferred);
            CommandTransferred?.Invoke(transferred);
        }

        /// <summary>Askeri tekrar hayatta işaretler (eğitim modu / yeniden doğma). Olay yayınlamaz.</summary>
        public void Revive(PlayerId id)
        {
            if (!_members.TryGetValue(id, out var member) || member.Alive)
                return;

            member.Alive = true;
            if (_teams.TryGetValue(member.Team, out var chain))
                chain.Dirty = true;
        }

        public void Clear()
        {
            _members.Clear();
            _teams.Clear();
            _nextOrder = 0;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _eventBus?.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            CommandTransferred = null;
        }

        private void OnPlayerDied(PlayerDiedEvent died) => MarkDead(died.VictimId);

        private TeamChain GetOrCreateTeam(int team)
        {
            if (!_teams.TryGetValue(team, out var chain))
            {
                chain = new TeamChain();
                _teams[team] = chain;
            }

            return chain;
        }

        private void RemoveFromTeam(Member member)
        {
            if (!_teams.TryGetValue(member.Team, out var chain))
                return;

            chain.Members.Remove(member);
            chain.Dirty = true;
        }

        private static void InsertSorted(List<Member> members, Member member)
        {
            var index = members.Count;
            for (var i = 0; i < members.Count; i++)
            {
                if (IsSenior(member, members[i]))
                {
                    index = i;
                    break;
                }
            }

            members.Insert(index, member);
        }

        /// <summary>a, b'den önce mi gelir: rütbe büyükse ya da rütbe eşit ve daha önce kaydedildiyse.</summary>
        private static bool IsSenior(Member a, Member b)
        {
            if (a.Rank != b.Rank)
                return a.Rank > b.Rank;

            return a.Order < b.Order;
        }

        private static void Rebuild(TeamChain chain)
        {
            chain.AliveChain.Clear();
            for (var i = 0; i < chain.Members.Count; i++)
            {
                var member = chain.Members[i];
                if (member.Alive)
                    chain.AliveChain.Add(member.Id);
            }

            chain.Dirty = false;
        }
    }
}
