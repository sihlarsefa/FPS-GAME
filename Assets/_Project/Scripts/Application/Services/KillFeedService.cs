using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>PlayerDiedEvent'lerden son N öldürme kaydını tutar (silah adı WeaponCatalog.GetDisplayName).</summary>
    /// <remarks>
    /// Kayıtlar eskiden yeniye sıralıdır (en yeni sonda); kapasite aşılınca en eski düşer.
    /// Dost/düşman bayrakları yerel oyuncunun timine göredir (dizin ITeamRelations ise). Çevresel ölümlerde
    /// (bölge, düşme — geçerli öldüren yok) KillerName boştur ve WeaponName kaynağın adıdır.
    /// </remarks>
    public sealed class KillFeedService : IDisposable
    {
        public const int DefaultCapacity = 6;

        private readonly IEventBus _eventBus;
        private readonly ICombatantDirectory _directory;
        private readonly ITeamRelations _teams;
        private readonly List<KillFeedEntry> _entries;
        private readonly Action<PlayerDiedEvent> _onPlayerDied;
        private int _localTeamOverride = -1;
        private int _learnedLocalTeam = -1;
        private bool _disposed;

        public KillFeedService(IEventBus eventBus, ICombatantDirectory directory, int capacity = 6)
        {
            _eventBus = eventBus;
            _directory = directory;
            _teams = directory as ITeamRelations;
            Capacity = capacity > 0 ? capacity : DefaultCapacity;
            _entries = new List<KillFeedEntry>(Capacity + 1);
            _onPlayerDied = OnPlayerDied;
            _eventBus?.Subscribe(_onPlayerDied);
        }

        public int Capacity { get; }

        /// <summary>Son öldürmeler, eskiden yeniye.</summary>
        public IReadOnlyList<KillFeedEntry> Entries => _entries;

        public event Action<KillFeedEntry> EntryAdded;

        /// <summary>
        /// Yerel oyuncunun timini açıkça belirler (dizin MatchService değilse ya da yerel oyuncu henüz kayıtlı değilse).
        /// -1 = otomatik (MatchService.LocalTeam, yoksa yerel oyuncunun göründüğü son kayıttan öğrenilen tim).
        /// </summary>
        public int LocalTeamOverride
        {
            get => _localTeamOverride;
            set => _localTeamOverride = value < 0 ? -1 : value;
        }

        /// <summary>Dışarıdan kayıt ekler (ör. ağdan gelen kayıt ya da sunum testleri).</summary>
        public void Add(KillFeedEntry entry)
        {
            if (_disposed)
                return;

            _entries.Add(entry);
            while (_entries.Count > Capacity)
                _entries.RemoveAt(0);

            EntryAdded?.Invoke(entry);
        }

        public void Clear() => _entries.Clear();

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _eventBus?.Unsubscribe(_onPlayerDied);
            EntryAdded = null;
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            if (_disposed || !e.VictimId.IsValid)
                return;

            var killer = e.KillerId;
            var victim = e.VictimId;
            var environmental = !killer.IsValid;

            var killerIsLocal = !environmental && SafeIsLocal(killer);
            var victimIsLocal = SafeIsLocal(victim);
            var localTeam = ResolveLocalTeam(killer, killerIsLocal, victim, victimIsLocal);

            var killerName = environmental ? string.Empty : SafeName(killer);
            var victimName = SafeName(victim);
            var weaponName = DamageSourceText.GetDisplayName(e.WeaponId, environmental);

            var killerIsAlly = !environmental && (killerIsLocal || IsOnTeam(killer, localTeam));
            var victimIsAlly = victimIsLocal || IsOnTeam(victim, localTeam);

            Add(new KillFeedEntry(killerName, victimName, weaponName, e.IsHeadshot, killerIsLocal, victimIsLocal,
                killerIsAlly, victimIsAlly));
        }

        private int ResolveLocalTeam(PlayerId killer, bool killerIsLocal, PlayerId victim, bool victimIsLocal)
        {
            if (_localTeamOverride >= 0)
                return _localTeamOverride;

            if (_directory is MatchService match && match.LocalTeam >= 0)
                return match.LocalTeam;

            if (_teams == null)
                return -1;

            // Yerel oyuncu bir kayıtta göründüğünde timi öğrenilir ve sonraki kayıtlarda da kullanılır.
            var team = killerIsLocal ? SafeTeam(killer) : victimIsLocal ? SafeTeam(victim) : -1;
            if (team >= 0)
                _learnedLocalTeam = team;

            return _learnedLocalTeam;
        }

        private bool IsOnTeam(PlayerId id, int team) => team >= 0 && id.IsValid && SafeTeam(id) == team;

        private int SafeTeam(PlayerId id)
        {
            if (_teams == null)
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

        private bool SafeIsLocal(PlayerId id)
        {
            if (_directory == null || !id.IsValid)
                return false;

            try
            {
                return _directory.IsLocalPlayer(id);
            }
            catch (Exception)
            {
                return false;
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

            return string.IsNullOrEmpty(name) ? (id.IsValid ? "Asker " + id.Value : "Bilinmeyen") : name;
        }
    }

    /// <summary>Hasar kaynağı (silah/çevre) kimliğinden Türkçe görünen ad; katalog hata verirse kimliğin kendisi.</summary>
    internal static class DamageSourceText
    {
        public const string Environment = "Çevre";
        public const string UnknownWeapon = "Silah";

        public static string GetDisplayName(string sourceId, bool environmental)
        {
            if (string.IsNullOrEmpty(sourceId))
                return environmental ? Environment : UnknownWeapon;

            try
            {
                var name = WeaponCatalog.GetDisplayName(sourceId);
                return string.IsNullOrEmpty(name) ? sourceId : name;
            }
            catch (Exception)
            {
                return sourceId;
            }
        }
    }
}
